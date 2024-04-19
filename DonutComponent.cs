using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aki.PrePatch;
using Aki.Reflection.Utils;
using BepInEx.Logging;
using Comfort.Common;
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

        internal float PMCdespawnCooldown = 0f;
        internal float PMCdespawnCooldownDuration = DonutsPlugin.despawnInterval.Value;

        internal float SCAVdespawnCooldown = 0f;
        internal float SCAVdespawnCooldownDuration = DonutsPlugin.despawnInterval.Value;

        internal static List<HotspotTimer> hotspotTimers;
        internal static Dictionary<string, MethodInfo> methodCache;
        internal static MethodInfo displayMessageNotificationMethod;

        internal static WildSpawnType sptUsec;
        internal static WildSpawnType sptBear;

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

            // Remove despawned bots from bot EnemyInfos list.
            botSpawnerClass.OnBotRemoved += removedBot =>
            {
                // Clear the enemy list, and memory about the main player
                removedBot.Memory.DeleteInfoAboutEnemy(gameWorld.MainPlayer);
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

        private readonly Stopwatch spawnCheckTimer = new Stopwatch();
        private const int SpawnCheckInterval = 1000;

        private void Start()
        {
            // setup the rest of donuts for the selected folder
            Initialization.InitializeStaticVariables();
            maplocation = gameWorld.MainPlayer.Location.ToLower();
            Logger.LogDebug("Setup maplocation: " + maplocation);
            Initialization.LoadFightLocations();
            if (DonutsPlugin.PluginEnabled.Value && fileLoaded)
            {
                Initialization.InitializeHotspotTimers();
            }

            Logger.LogDebug("Setup PMC Bot limit: " + PMCBotLimit);
            Logger.LogDebug("Setup SCAV Bot limit: " + SCAVBotLimit);

            spawnCheckTimer.Start();
        }

        private void Update()
        {
            if (!DonutsPlugin.PluginEnabled.Value || !fileLoaded)
                return;

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

            foreach (var hotspotTimer in hotspotTimers)
            {
                if (hotspotTimer.ShouldSpawn())
                {
                    Vector3 coordinate = new Vector3(hotspotTimer.Hotspot.Position.x, hotspotTimer.Hotspot.Position.y, hotspotTimer.Hotspot.Position.z);

                    if (CanSpawn(hotspotTimer, coordinate))
                    {
                        TriggerSpawn(hotspotTimer, coordinate);
                        yield return null;
                    }
                }
            }
        }

        private bool CanSpawn(HotspotTimer hotspotTimer, Vector3 coordinate)
        {
            // Check if the timer trigger is greater than the threshold and conditions are met
            if (BotSpawn.IsWithinBotActivationDistance(hotspotTimer.Hotspot, coordinate) && maplocation == hotspot.MapName)
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
            var bots = gameWorld.RegisteredPlayers;
            float maxDistance = -1f;
            Player furthestBot = null;
            var tempBotCount = 0;

            if (bottype == "pmc")
            {
                if (Time.time - PMCdespawnCooldown < PMCdespawnCooldownDuration)
                {
                    return; // Exit the method without despawning
                }

                //don't know distances so have to loop through all bots
                foreach (Player bot in bots)
                {
                    if (bot.AIData.BotOwner == null)
                    {
                        continue;
                    }
                    // Ignore bots on the invalid despawn list, and the player
                    if (bot.IsYourPlayer || !validDespawnListPMC.Contains(bot.Profile.Info.Settings.Role) || bot.AIData.BotOwner.BotState != EBotState.Active)
                    {
                        continue;
                    }


                    // Don't include bots that have spawned within the last 10 seconds
                    if (Time.time - 10 < bot.AIData.BotOwner.ActivateTime)
                    {
                        continue;
                    }

                    float distance = (bot.Position - gameWorld.MainPlayer.Position).sqrMagnitude;
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        furthestBot = bot;
                    }

                    //add bots that match criteria but distance doesn't matter
                    tempBotCount++;
                }


            }
            else if (bottype == "scav")
            {
                if (Time.time - SCAVdespawnCooldown < SCAVdespawnCooldownDuration)
                {
                    return;
                }

                //don't know distances so have to loop through all bots
                foreach (Player bot in bots)
                {
                    if (bot.AIData.BotOwner == null)
                    {
                        continue;
                    }
                    // Ignore bots on the invalid despawn list, and the player
                    if (bot.IsYourPlayer || !validDespawnListScav.Contains(bot.Profile.Info.Settings.Role) || bot.AIData.BotOwner.BotState != EBotState.Active)
                    {
                        continue;
                    }

                    // Don't include bots that have spawned within the last 10 seconds
                    if (Time.time - 10 < bot.AIData.BotOwner.ActivateTime)
                    {
                        continue;
                    }

                    float distance = (bot.Position - gameWorld.MainPlayer.Position).sqrMagnitude;
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        furthestBot = bot;
                    }

                    //add bots that match criteria but distance doesn't matter
                    tempBotCount++;
                }
            }

            if (furthestBot != null)
            {
                if (furthestBot.AIData.BotOwner == null)
                {
                    return;
                }
                if (bottype == "pmc" && tempBotCount <= PMCBotLimit)
                {
                    return;
                }
                else if (bottype == "scav" && tempBotCount <= SCAVBotLimit)
                {
                    return;
                }

                // Despawn the bot
#if DEBUG
                Logger.LogDebug($"Despawning bot: {furthestBot.Profile.Info.Nickname} ({furthestBot.name})");
#endif
                BotOwner botOwner = furthestBot.AIData.BotOwner;

                var botgame = Singleton<IBotGame>.Instance;
                Singleton<Effects>.Instance.EffectsCommutator.StopBleedingForPlayer(botOwner.GetPlayer);
                botOwner.Deactivate();
                botOwner.Dispose();
                botgame.BotsController.BotDied(botOwner);
                botgame.BotsController.DestroyInfo(botOwner.GetPlayer);
                DestroyImmediate(botOwner.gameObject);
                Destroy(botOwner);

                if (bottype == "pmc")
                {
                    PMCdespawnCooldown = Time.time;
                }
                else if (bottype == "scav")
                {
                    SCAVdespawnCooldown = Time.time;
                }
            }

        }




        private void OnGUI()
        {
            gizmos.ToggleGizmoDisplay(DonutsPlugin.DebugGizmos.Value);
        }

    }

}

