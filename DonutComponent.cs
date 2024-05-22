using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Aki.PrePatch;
using Aki.Reflection.Utils;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using HarmonyLib;
using Systems.Effects;
using UnityEngine;
using static Donuts.DefaultPluginVars;

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
        internal float PMCdespawnCooldownDuration = despawnInterval.Value;

        internal float SCAVdespawnCooldown = 0f;
        internal float SCAVdespawnCooldownDuration = despawnInterval.Value;

        internal static List<HotspotTimer> hotspotTimers;
        internal static Dictionary<string, MethodInfo> methodCache;
        internal static MethodInfo displayMessageNotificationMethod;

        internal static WildSpawnType sptUsec;
        internal static WildSpawnType sptBear;

        internal static bool isInBattle;
        internal static float timeSinceLastHit = 0;
        internal static Player mainplayer;
        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public DonutComponent()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(DonutComponent));
        }

        internal static void Enable()
        {
            if (Singleton<IBotGame>.Instantiated)
            {
                gameWorld = Singleton<GameWorld>.Instance;
                gameWorld.GetOrAddComponent<DonutComponent>();

                Logger.LogDebug("Donuts Enabled");
            }
        }

        private void Awake()
        {
            botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            botCreator = AccessTools.Field(botSpawnerClass.GetType(), "_botCreator").GetValue(botSpawnerClass) as IBotCreator;
            methodCache = new Dictionary<string, MethodInfo>();
            gizmos = new Gizmos(this);

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
                    if (!player.IsAI)
                    {
                        playerList.Add(player);
                    }
                }
            }

            botSpawnerClass.OnBotRemoved += removedBot =>
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
        }

        private Stopwatch spawnCheckTimer = new Stopwatch();
        private const int SpawnCheckInterval = 1000;

        private void Start()
        {
            Initialization.InitializeStaticVariables();
            maplocation = gameWorld.MainPlayer.Location.ToLower();
            mainplayer = gameWorld.MainPlayer;
            isInBattle = false;
            Logger.LogDebug("Setup maplocation: " + maplocation);
            Initialization.LoadFightLocations();
            if (PluginEnabled.Value && fileLoaded)
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
                    isInBattle = true;
                    timeSinceLastHit = 0;
                    break;
                default:
                    break;
            }
        }

        private void Update()
        {
            if (!PluginEnabled.Value || !fileLoaded)
                return;

            timeSinceLastHit += Time.deltaTime;

            foreach (var hotspotTimer in hotspotTimers)
            {
                hotspotTimer.UpdateTimer();
            }

            if (spawnCheckTimer.ElapsedMilliseconds >= SpawnCheckInterval)
            {
                spawnCheckTimer.Restart();
                StartSpawnProcess().Forget();
            }

            Gizmos.DisplayMarkerInformation();
        }

        private async UniTask StartSpawnProcess()
        {
            if (DespawnEnabledPMC.Value)
            {
                await DespawnFurthestBot("pmc");
            }

            if (DespawnEnabledSCAV.Value)
            {
                await DespawnFurthestBot("scav");
            }

            if (groupedHotspotTimers.Count > 0)
            {
                foreach (var groupHotspotTimers in groupedHotspotTimers.Values)
                {
                    if (groupHotspotTimers.Count == 0)
                    {
                        continue;
                    }

                    bool spawnTriggered = false;
                    var randomIndex = UnityEngine.Random.Range(0, groupHotspotTimers.Count);

                    for (int i = 0; i < groupHotspotTimers.Count; i++)
                    {
                        var index = (randomIndex + i) % groupHotspotTimers.Count;
                        var hotspotTimer = groupHotspotTimers[index];

                        if (hotspotTimer.ShouldSpawn())
                        {
                            Vector3 coordinate = new Vector3(hotspotTimer.Hotspot.Position.x, hotspotTimer.Hotspot.Position.y, hotspotTimer.Hotspot.Position.z);

                            if (isInBattle)
                            {
                                if (timeSinceLastHit < battleStateCoolDown.Value)
                                {
                                    break;
                                }
                                else
                                {
                                    isInBattle = false;
                                }
                            }

                            if (CanSpawn(hotspotTimer, coordinate))
                            {
                                await TriggerSpawn(hotspotTimer, coordinate);
                                spawnTriggered = true;
                                break;
                            }
                        }
                    }

                    if (spawnTriggered)
                    {
                        ResetGroupTimers(groupHotspotTimers[0].Hotspot.GroupNum);
                    }
                }
            }
        }

        private bool CanSpawn(HotspotTimer hotspotTimer, Vector3 coordinate)
        {
            if (BotSpawn.IsWithinBotActivationDistance(hotspotTimer.Hotspot, coordinate) && maplocation == hotspotTimer.Hotspot.MapName)
            {
                if ((hotspotTimer.Hotspot.WildSpawnType == "pmc" && hotspotBoostPMC.Value) ||
                    (hotspotTimer.Hotspot.WildSpawnType == "scav" && hotspotBoostSCAV.Value))
                {
                    hotspotTimer.Hotspot.SpawnChance = 100;
                }

                return UnityEngine.Random.Range(0, 100) < hotspotTimer.Hotspot.SpawnChance;
            }
            return false;
        }

        private async UniTask TriggerSpawn(HotspotTimer hotspotTimer, Vector3 coordinate)
        {
            if (forceAllBotType.Value != "Disabled")
            {
                hotspotTimer.Hotspot.WildSpawnType = forceAllBotType.Value.ToLower();
            }

            var tasks = new List<UniTask<bool>>();

            if (HardCapEnabled.Value)
            {
                tasks.Add(CheckHardCap(hotspotTimer.Hotspot.WildSpawnType));
            }

            tasks.Add(CheckRaidTime(hotspotTimer.Hotspot.WildSpawnType));

            bool[] results = await UniTask.WhenAll(tasks);

            if (results.Any(result => !result))
            {
                return;
            }

            await BotSpawn.SpawnBots(hotspotTimer, coordinate);
            hotspotTimer.timesSpawned++;

            if (hotspotTimer.timesSpawned >= hotspotTimer.Hotspot.MaxSpawnsBeforeCoolDown)
            {
                hotspotTimer.inCooldown = true;
            }

            ResetGroupTimers(hotspotTimer.Hotspot.GroupNum);
        }

        private async UniTask<bool> CheckHardCap(string wildSpawnType)
        {
            int activePMCs = BotCountManager.GetAlivePlayers("pmc");
            int activeSCAVs = BotCountManager.GetAlivePlayers("scav");

            if (wildSpawnType == "pmc" && activePMCs >= PMCBotLimit && !hotspotIgnoreHardCapPMC.Value)
            {
                Logger.LogDebug($"PMC spawn not allowed due to PMC bot limit - skipping this spawn. Active PMCs: {activePMCs}, PMC Bot Limit: {PMCBotLimit}");
                return false;
            }

            if (wildSpawnType == "scav" && activeSCAVs >= SCAVBotLimit && !hotspotIgnoreHardCapSCAV.Value)
            {
                Logger.LogDebug($"SCAV spawn not allowed due to SCAV bot limit - skipping this spawn. Active SCAVs: {activeSCAVs}, SCAV Bot Limit: {SCAVBotLimit}");
                return false;
            }

            return true;
        }

        private async UniTask<bool> CheckRaidTime(string wildSpawnType)
        {
            if (wildSpawnType == "pmc" && hardStopOptionPMC.Value && !IsRaidTimeRemaining("pmc"))
            {
#if DEBUG
                Logger.LogDebug("PMC spawn not allowed due to raid time conditions - skipping this spawn");
#endif
                return false;
            }

            if (wildSpawnType == "scav" && hardStopOptionSCAV.Value && !IsRaidTimeRemaining("scav"))
            {
#if DEBUG
                Logger.LogDebug("SCAV spawn not allowed due to raid time conditions - skipping this spawn");
#endif
                return false;
            }

            return true;
        }
        private bool IsRaidTimeRemaining(string spawnType)
        {
            int hardStopTime = 0;
            int hardStopPercent = 0;

            if (spawnType == "pmc")
            {
                hardStopTime = hardStopTimePMC.Value;
                hardStopPercent = hardStopPercentPMC.Value;
            }
            else if (spawnType == "scav")
            {
                hardStopTime = hardStopTimeSCAV.Value;
                hardStopPercent = hardStopPercentSCAV.Value;
            }

            int raidTimeLeftTime = (int)Aki.SinglePlayer.Utils.InRaid.RaidTimeUtil.GetRemainingRaidSeconds(); // Time left
            int raidTimeLeftPercent = (int)(Aki.SinglePlayer.Utils.InRaid.RaidTimeUtil.GetRaidTimeRemainingFraction() * 100f); // Percent left

            return useTimeBasedHardStop.Value ? raidTimeLeftTime >= hardStopTime : raidTimeLeftPercent >= hardStopPercent;
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
        private async UniTask DespawnFurthestBot(string bottype)
        {
            if (bottype != "pmc" && bottype != "scav")
                return;

            float despawnCooldown = bottype == "pmc" ? PMCdespawnCooldown : SCAVdespawnCooldown;
            float despawnCooldownDuration = bottype == "pmc" ? PMCdespawnCooldownDuration : SCAVdespawnCooldownDuration;
            if (Time.time - despawnCooldown < despawnCooldownDuration)
            {
                return;
            }

            if (!ShouldConsiderDespawning(bottype))
            {
                return;
            }

            await UniTask.SwitchToMainThread();

            UpdateDistancesAndFindFurthestBot(out Player furthestBot);

            if (furthestBot != null)
            {
                await UniTask.SwitchToThreadPool();
                DespawnBot(furthestBot, bottype);
            }
        }

        private bool ShouldConsiderDespawning(string botType)
        {
            int botLimit = botType == "pmc" ? PMCBotLimit : SCAVBotLimit;
            int activeBotCount = BotCountManager.GetAlivePlayers(botType);

            return activeBotCount > botLimit; // Only consider despawning if the number of active bots of the type exceeds the limit
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

        private void UpdateDistancesAndFindFurthestBot(out Player furthestBot)
        {
            float maxDistance = float.MinValue;
            furthestBot = null;

            foreach (var bot in gameWorld.AllAlivePlayersList)
            {
                // Get distance of bot to player using squared distance
                float distance = (mainplayer.Transform.position - bot.Transform.position).sqrMagnitude;

                // Check if this is the furthest distance
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    furthestBot = bot;
                }
            }
        }

        private void OnGUI()
        {
            gizmos.ToggleGizmoDisplay(DebugGizmos.Value);
        }

        private void OnDestroy()
        {
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

            StopAllCoroutines();

            isInBattle = false;
            groupedFightLocations = null;
            groupedHotspotTimers = null;
            hotspotTimers = null;
            methodCache = null;

            Logger.LogWarning("Donuts Component cleaned up and disabled.");
        }
    }
}
