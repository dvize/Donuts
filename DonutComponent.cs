using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Aki.PrePatch;
using Aki.Reflection.Utils;
using BepInEx.Logging;
using Comfort.Common;
using Donuts.Models;
using EFT;
using HarmonyLib;
using Systems.Effects;
using UnityEngine;

#pragma warning disable IDE0007, IDE0044
namespace Donuts
{
    public class DonutComponent : MonoBehaviour
    {

        internal static FightLocations fightLocations;
        internal static FightLocations sessionLocations;

        internal static List<List<Entry>> groupedFightLocations;
        internal static Dictionary<int, List<HotspotTimer>> groupedHotspotTimers;

        internal List<WildSpawnType> validDespawnListPMC = new List<WildSpawnType>()
        {
            (WildSpawnType)AkiBotsPrePatcher.sptUsecValue,
            (WildSpawnType)AkiBotsPrePatcher.sptBearValue
        };

        internal List<WildSpawnType> validDespawnListScav = new List<WildSpawnType>()
        {
            WildSpawnType.assault,
            WildSpawnType.cursedAssault
        };

        internal static bool fileLoaded = false;
        internal static Gizmos gizmos;
        internal static string maplocation;
        internal static int PMCBotLimit = 0;
        internal static int SCAVBotLimit = 0;
        internal static int currentInitialPMCs = 0;
        internal static int currentInitialSCAVs = 0;

        internal static GameWorld gameWorld;
        internal static BotSpawner botSpawnerClass;
        internal static IBotCreator botCreator;
        internal static List<Player> playerList = new List<Player>();

        internal float PMCdespawnCooldown = 0f;
        internal float PMCdespawnCooldownDuration = DonutsPlugin.despawnInterval.Value;

        internal float SCAVdespawnCooldown = 0f;
        internal float SCAVdespawnCooldownDuration = DonutsPlugin.despawnInterval.Value;

        internal static List<HotspotTimer> hotspotTimers;
        internal static Dictionary<string, MethodInfo> methodCache;
        internal static MethodInfo displayMessageNotificationMethod;

        internal static WildSpawnType sptUsec;
        internal static WildSpawnType sptBear;

        internal Coroutine battleCooldownCoroutine = null;
        internal static bool isInBattle;
        internal static Player mainplayer;
        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public DonutComponent()
        {
            if (Logger == null)
            {
                Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(DonutComponent));
            }

        }
        public static void Enable()
        {
            if (Singleton<IBotGame>.Instantiated)
            {
                gameWorld = Singleton<GameWorld>.Instance;
                gameWorld.GetOrAddComponent<DonutComponent>();

                Logger.LogDebug("Donuts Enabled");

            }
        }

        public void Awake()
        {
            botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            botCreator = AccessTools.Field(botSpawnerClass.GetType(), "_botCreator").GetValue(botSpawnerClass) as IBotCreator;
            methodCache = new Dictionary<string, MethodInfo>();
            gizmos = new Gizmos(this);

            // Retrieve displayMessageNotification MethodInfo
            var displayMessageNotification = PatchConstants.EftTypes.Single(x => x.GetMethod("DisplayMessageNotification") != null).GetMethod("DisplayMessageNotification");
            if (displayMessageNotification != null)
            {
                displayMessageNotificationMethod = displayMessageNotification;
                methodCache["DisplayMessageNotification"] = displayMessageNotification;
            }

            var methodInfo = typeof(BotSpawner).GetMethod("method_9", BindingFlags.Instance | BindingFlags.NonPublic);
            if (methodInfo != null)
            {
                methodCache[methodInfo.Name] = methodInfo;
            }

            methodInfo = AccessTools.Method(typeof(BotSpawner), "method_10");
            if (methodInfo != null)
            {
                methodCache[methodInfo.Name] = methodInfo;
            }

            if (gameWorld.RegisteredPlayers.Count > 0)
            {
                foreach (var player in gameWorld.AllPlayersEverExisted)
                {
                    // Only add humans
                    if (!player.IsAI)
                    {
                        playerList.Add(player);
                    }
                }
            }

            // Remove despawned bots from bot EnemyInfos list.
            botSpawnerClass.OnBotRemoved += removedBot =>
            {
                // Clear the enemy list, and memory about the main player
                foreach (var player in playerList)
                {
                    // Remove each player from enemy
                    removedBot.Memory.DeleteInfoAboutEnemy(player);
                }
                removedBot.EnemiesController.EnemyInfos.Clear();

                // Loop through the rest of the bots on the map, andd clear this bot from its memory/group info

                foreach (var player in gameWorld.AllAlivePlayersList)
                {
                    if (!player.IsAI)
                    {
                        continue;
                    }

                    // Clear the bot from all other bots enemy info
                    var botOwner = player.AIData.BotOwner;
                    botOwner.Memory.DeleteInfoAboutEnemy(removedBot);
                    botOwner.BotsGroup.RemoveInfo(removedBot);
                    botOwner.BotsGroup.RemoveEnemy(removedBot, EBotEnemyCause.death);
                    botOwner.BotsGroup.RemoveAlly(removedBot);
                }
            };
        }

        private Stopwatch spawnCheckTimer = new Stopwatch();
        private const int SpawnCheckInterval = 1000;

        private void Start()
        {
            // setup the rest of donuts for the selected folder
            Initialization.InitializeStaticVariables();
            maplocation = gameWorld.MainPlayer.Location.ToLower();
            mainplayer = gameWorld.MainPlayer;
            isInBattle = false;
            Logger.LogDebug("Setup maplocation: " + maplocation);
            Initialization.LoadFightLocations();
            if (DonutsPlugin.PluginEnabled.Value && fileLoaded)
            {
                Initialization.InitializeHotspotTimers();
            }

            Logger.LogDebug("Setup PMC Bot limit: " + PMCBotLimit);
            Logger.LogDebug("Setup SCAV Bot limit: " + SCAVBotLimit);

            spawnCheckTimer.Start();

            mainplayer.BeingHitAction += BeingHitBattleCoolDown;
        }

        private void BeingHitBattleCoolDown(DamageInfo info, EBodyPart part, float arg3)
        {
            switch (info.DamageType)
            {
                case EDamageType.Btr:
                case EDamageType.Melee:
                case EDamageType.Bullet:
                case EDamageType.Explosion:
                case EDamageType.GrenadeFragment:
                case EDamageType.Sniper:
                    if (battleCooldownCoroutine != null)
                    {
                        StopCoroutine(battleCooldownCoroutine); // Stop the existing coroutine if it's running
                    }
                    battleCooldownCoroutine = StartCoroutine(BattleStateCooldown());
                    break;
                default:
                    break;
            }
        }

        private IEnumerator BattleStateCooldown()
        {
            isInBattle = true;
#if DEBUG
            Logger.LogWarning("Starting/Restarting BattleState Cooldowns for actual spawns since the player was hit. Delay(s):" + DonutsPlugin.battleStateCoolDown.Value);
#endif
            yield return new WaitForSeconds(DonutsPlugin.battleStateCoolDown.Value); // Wait for 15 seconds if no more hits
            isInBattle = false;
        }

        private void Update()
        {
            if (!DonutsPlugin.PluginEnabled.Value || !fileLoaded)
                return;

            foreach (var hotspotTimer in hotspotTimers)
            {
                hotspotTimer.UpdateTimer();
            }

            if (spawnCheckTimer.ElapsedMilliseconds >= SpawnCheckInterval)
            {
                spawnCheckTimer.Restart();
                StartCoroutine(StartSpawnProcess());
            }
        }

        private IEnumerator StartSpawnProcess()
        {
            Gizmos.DisplayMarkerInformation();

            if (DonutsPlugin.DespawnEnabledPMC.Value)
            {
                DespawnFurthestBot("pmc");
            }

            if (DonutsPlugin.DespawnEnabledSCAV.Value)
            {
                DespawnFurthestBot("scav");
            }

            if (groupedHotspotTimers.Count > 0)
            {
                foreach (var groupHotspotTimers in groupedHotspotTimers.Values)
                {
                    if (!(groupHotspotTimers.Count > 0))
                    {
                        continue;
                    }

                    var randomIndex = UnityEngine.Random.Range(0, groupHotspotTimers.Count);
                    var hotspotTimer = groupHotspotTimers[randomIndex];

                    if (hotspotTimer.ShouldSpawn())
                    {
                        Vector3 coordinate = new Vector3(hotspotTimer.Hotspot.Position.x, hotspotTimer.Hotspot.Position.y, hotspotTimer.Hotspot.Position.z);

                        // Wait here if in battle
                        while (isInBattle)
                        {
                            yield return new WaitForSeconds(1); // Check every second if still in battle
                        }

                        if (CanSpawn(hotspotTimer, coordinate))
                        {
                            TriggerSpawn(hotspotTimer, coordinate);
                            yield return null;
                        }
                    }
                }
            }
        }

        private bool CanSpawn(HotspotTimer hotspotTimer, Vector3 coordinate)
        {
            // Check if the timer trigger is greater than the threshold and conditions are met
            if (BotSpawn.IsWithinBotActivationDistance(hotspotTimer.Hotspot, coordinate) && maplocation == hotspotTimer.Hotspot.MapName)
            {
                if ((hotspotTimer.Hotspot.WildSpawnType == "pmc" && DonutsPlugin.hotspotBoostPMC.Value) ||
                    (hotspotTimer.Hotspot.WildSpawnType == "scav" && DonutsPlugin.hotspotBoostSCAV.Value))
                {
                    hotspotTimer.Hotspot.SpawnChance = 100;  // Boosting spawn chance
                }

                return UnityEngine.Random.Range(0, 100) < hotspotTimer.Hotspot.SpawnChance;
            }
            return false;
        }

        private void TriggerSpawn(HotspotTimer hotspotTimer, Vector3 coordinate)
        {
            BotSpawn.SpawnBots(hotspotTimer, coordinate);
            hotspotTimer.timesSpawned++;

            if (hotspotTimer.timesSpawned >= hotspotTimer.Hotspot.MaxSpawnsBeforeCoolDown)
            {
                hotspotTimer.inCooldown = true;
            }

            ResetGroupTimers(hotspotTimer.Hotspot.GroupNum);
        }

        private void ResetGroupTimers(int groupNum)
        {
            foreach (var timer in groupedHotspotTimers[groupNum])
            {
                timer.ResetTimer();
                if (timer.Hotspot.IgnoreTimerFirstSpawn)
                    timer.Hotspot.IgnoreTimerFirstSpawn = false;
            }
        }
        private void DespawnFurthestBot(string bottype)
        {
            if (bottype != "pmc" && bottype != "scav")
                return;  // Return immediately if bot type is not recognized

            float despawnCooldown = bottype == "pmc" ? PMCdespawnCooldown : SCAVdespawnCooldown;
            float despawnCooldownDuration = bottype == "pmc" ? PMCdespawnCooldownDuration : SCAVdespawnCooldownDuration;
            if (Time.time - despawnCooldown < despawnCooldownDuration)
            {
                return; // Cooldown not completed
            }

            var bots = gameWorld.AllAlivePlayersList;
            if (!ShouldConsiderDespawning(bots, bottype))
            {
                return;
            }

            Dictionary<Player, float> botFurthestDistanceToAnyPlayerDict = new Dictionary<Player, float>();
            UpdateFurthestDistances(botFurthestDistanceToAnyPlayerDict);

            Player furthestBot = null;
            float maxDistance = float.MinValue;

            foreach (var botKvp in botFurthestDistanceToAnyPlayerDict)
            {
                if (botKvp.Value > maxDistance)
                {
                    maxDistance = botKvp.Value;
                    furthestBot = botKvp.Key;
                }
            }

            if (furthestBot != null)
            {
                DespawnBot(furthestBot, bottype);
            }
        }
        private bool ShouldConsiderDespawning(IEnumerable<Player> bots, string bottype)
        {
            int botLimit = bottype == "pmc" ? PMCBotLimit : SCAVBotLimit;
            int activeBotCount = gameWorld.AllAlivePlayersList.Count - 1;  //minus 1 for you the player

            return activeBotCount > botLimit; // Only consider despawning if the number of active bots exceeds the limit
        }
        private void DespawnBot(Player furthestBot, string bottype)
        {
            BotOwner botOwner = furthestBot.AIData.BotOwner;
            if (botOwner == null)
                return;

#if DEBUG
            Logger.LogDebug($"Despawning bot: {furthestBot.Profile.Info.Nickname} ({furthestBot.name})");
#endif

            gameWorld.RegisteredPlayers.Remove(botOwner);
            gameWorld.AllAlivePlayersList.Remove(botOwner.GetPlayer);

            var botgame = Singleton<IBotGame>.Instance;
            Singleton<Effects>.Instance.EffectsCommutator.StopBleedingForPlayer(botOwner.GetPlayer);
            botOwner.Deactivate();
            botOwner.Dispose();
            botgame.BotsController.BotDied(botOwner);
            botgame.BotsController.DestroyInfo(botOwner.GetPlayer);
            DestroyImmediate(botOwner.gameObject);
            Destroy(botOwner);

            // Update the cooldown
            if (bottype == "pmc")
            {
                PMCdespawnCooldown = Time.time;
            }
            else if (bottype == "scav")
            {
                SCAVdespawnCooldown = Time.time;
            }
        }
        private void UpdateFurthestDistances(Dictionary<Player, float> botFurthestFromPlayer)
        {
            foreach (var bot in gameWorld.AllAlivePlayersList) 
            {
                float furthestDistance = float.MinValue;
                foreach (var player in playerList)
                {
                    if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
                        continue;

                    float currentDistance = (bot.Position - player.Position).sqrMagnitude;
                    if (currentDistance > furthestDistance)
                    {
                        furthestDistance = currentDistance;
                    }
                }
                botFurthestFromPlayer[bot] = furthestDistance;
            }
        }

        private void OnGUI()
        {
            gizmos.ToggleGizmoDisplay(DonutsPlugin.DebugGizmos.Value);
        }

        private void OnDestroy()
        {
            // Unregister on-bot-removed event
            botSpawnerClass.OnBotRemoved -= removedBot =>
            {
                foreach (var player in playerList)
                {
                    removedBot.Memory.DeleteInfoAboutEnemy(player);
                }
                removedBot.EnemiesController.EnemyInfos.Clear();

                foreach (var player in gameWorld.AllAlivePlayersList)
                {
                    if (!player.IsAI)
                    {
                        continue;
                    }

                    var botOwner = player.AIData.BotOwner;
                    botOwner.Memory.DeleteInfoAboutEnemy(removedBot);
                    botOwner.BotsGroup.RemoveInfo(removedBot);
                    botOwner.BotsGroup.RemoveEnemy(removedBot, EBotEnemyCause.death);
                    botOwner.BotsGroup.RemoveAlly(removedBot);
                }
            };

            mainplayer.BeingHitAction -= BeingHitBattleCoolDown;

            // Stop and clear all coroutines
            StopAllCoroutines();
            if (battleCooldownCoroutine != null)
            {
                StopCoroutine(battleCooldownCoroutine);
                battleCooldownCoroutine = null;
            }

            if (spawnCheckTimer != null)
            {
                spawnCheckTimer.Stop();
                spawnCheckTimer.Reset();
                spawnCheckTimer = null;
            }

            // Reset static and instance variables
            isInBattle = false;  // Resetting static variable
            groupedFightLocations = null;
            groupedHotspotTimers = null;
            hotspotTimers = null;
            methodCache = null;

            // Log cleanup action if debugging
#if DEBUG
            Logger.LogWarning("Donuts Component cleaned up and disabled.");
#endif
        }
    }
}

