using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Aki.PrePatch;
using Aki.Reflection.Utils;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using HarmonyLib;
using Newtonsoft.Json;
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

        internal Dictionary<string, string> mapLocationDict = new Dictionary<string, string>
            {
                {"customs", "bigmap"},
                {"factory", "factory4_day"},
                {"factory_night", "factory4_night"},
                {"streets", "tarkovstreets"},
                {"reserve", "rezervbase"},
                {"interchange", "interchange"},
                {"woods", "woods"},
                {"groundzero", "sandbox"},
                {"laboratory", "laboratory"},
                {"lighthouse", "lighthouse"},
                {"shoreline", "shoreline"}
            };

        internal static bool hasSpawnedStartingBots;
        internal static bool fileLoaded = false;
        internal static Gizmos gizmos;
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
            mainplayer = gameWorld.MainPlayer;
            isInBattle = false;
            Logger.LogDebug("Setup maplocation: " + DonutsBotPrep.maplocation);
            Initialization.LoadFightLocations();

            var botWaveConfig = GetBotWavesConfig(DonutsBotPrep.selectionName, DonutsBotPrep.maplocation);

            // reset starting bots boolean each raid
            hasSpawnedStartingBots = false;

            Logger.LogDebug("Setup PMC Bot limit: " + Initialization.PMCBotLimit);
            Logger.LogDebug("Setup SCAV Bot limit: " + Initialization.SCAVBotLimit);

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

            var botWaves = botWaveConfig.Maps[DonutsBotPrep.maplocation];

            foreach (var pmcWave in botWaves.PMC)
            {
                pmcWave.UpdateTimer(Time.deltaTime, DefaultPluginVars.coolDownTimer.Value);
            }

            foreach (var scavWave in botWaves.SCAV)
            {
                scavWave.UpdateTimer(Time.deltaTime, DefaultPluginVars.coolDownTimer.Value);
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
            if (!hasSpawnedStartingBots)
            {
                if (DonutsBotPrep.botSpawnInfos != null && DonutsBotPrep.botSpawnInfos.Any())
                {
                    await BotSpawn.SpawnBotsFromInfo(DonutsBotPrep.botSpawnInfos);
                    hasSpawnedStartingBots = true;
                }
                else
                {
                    Logger.LogError("botSpawnInfos is not defined or empty. Cannot call SpawnBotsFromInfo.");
                }
            }

            if (DespawnEnabledPMC.Value)
            {
                await DespawnFurthestBot("pmc");
            }

            if (DespawnEnabledSCAV.Value)
            {
                await DespawnFurthestBot("scav");
            }

            // Spawn PMC and SCAV bot waves
            await SpawnBotWaves(botWaveConfig.Maps[DonutsBotPrep.maplocation].PMC, "pmc");
            await SpawnBotWaves(botWaveConfig.Maps[DonutsBotPrep.maplocation].SCAV, "scav");
        }

        private async UniTask SpawnBotWaves(List<BotWave> botWaves, string wildSpawnType)
        {
            bool spawnTriggered = false;
            var randomIndex = UnityEngine.Random.Range(0, botWaves.Count());

            for (int i = 0; i < botWaves.Count(); i++)
            {
                var index = (randomIndex + i) % botWaves.Count();
                var botWave = botWaves.ElementAt(index);

                if (botWave.ShouldSpawn())
                {
                    if (isInBattle && timeSinceLastHit < battleStateCoolDown.Value)
                    {
                        break;
                    }

                    // Get coordinates
                    var spawnPoints = DonutComponent.GetSpawnPointsForZones(DonutsBotPrep.allMapsZoneConfig, DonutsBotPrep.maplocation, botWave.Zones);

                    if (spawnPoints.Any())
                    {
                        var coordinate = spawnPoints[0];

                        if (CanSpawn(botWave, coordinate, wildSpawnType))
                        {
                            await TriggerSpawn(botWave, coordinate, wildSpawnType);
                            spawnTriggered = true;
                            break;
                        }
                    }
                }
            }

            if (spawnTriggered)
            {
                ResetGroupTimers(botWaves.First().GroupNum, wildSpawnType);
            }
        }

        private bool CanSpawn(BotWave botWave, Vector3 coordinate, string wildSpawnType)
        {
            if (BotSpawn.IsWithinBotActivationDistance(botWave, coordinate) && DonutsBotPrep.maplocation == DonutsBotPrep.maplocation)
            {
                if ((wildSpawnType == "pmc" && hotspotBoostPMC.Value) ||
                    (wildSpawnType == "scav" && hotspotBoostSCAV.Value))
                {
                    botWave.SpawnChance = 100;
                }

                return UnityEngine.Random.Range(0, 100) < botWave.SpawnChance;
            }
            return false;
        }

        private async UniTask TriggerSpawn(BotWave botWave, Vector3 coordinate, string wildSpawnType)
        {
            if (forceAllBotType.Value != "Disabled")
            {
                wildSpawnType = forceAllBotType.Value.ToLower();
            }

            var tasks = new List<UniTask<bool>>();

            if (HardCapEnabled.Value)
            {
                tasks.Add(CheckHardCap(wildSpawnType));
            }

            tasks.Add(CheckRaidTime(wildSpawnType));

            bool[] results = await UniTask.WhenAll(tasks);

            if (results.Any(result => !result))
            {
                return;
            }

            await BotSpawn.SpawnBots(botWave, coordinate, wildSpawnType);
            botWave.TimesSpawned++;

            if (botWave.TimesSpawned >= botWave.MaxTriggersBeforeCooldown)
            {
                botWave.InCooldown = true;
            }

            ResetGroupTimers(botWave.GroupNum, wildSpawnType);
        }

        public static BotWavesConfig GetBotWavesConfig(string selectionName, string maplocation)
        {
            var mapKey = mapLocationDict.FirstOrDefault(x => x.Value == maplocation).Key;

            if (mapKey == null)
            {
                Logger.LogError($"Map location {maplocation} not found in dictionary.");
                return null;
            }

            string dllPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(dllPath);
            string jsonFilePath = Path.Combine(directoryPath, "patterns", selectionName, $"{mapKey}_waves.json");

            if (File.Exists(jsonFilePath))
            {
                var jsonString = File.ReadAllText(jsonFilePath);
                var botWavesData = JsonConvert.DeserializeObject<BotWavesConfig>(jsonString);
                if (botWavesData != null)
                {
                    Logger.LogDebug($"Successfully loaded {mapKey}_waves.json for preset: {selectionName}");
                    return botWavesData;
                }
                else
                {
                    Logger.LogError($"Failed to deserialize {mapKey}_waves.json for preset: {selectionName}");
                    return null;
                }
            }
            else
            {
                Logger.LogError($"{mapKey}_waves.json file not found at path: {jsonFilePath}");
                return null;
            }
        }

        public static StartingBotConfig GetStartingBotConfig(string selectionName, string maplocation)
        {
            var mapKey = mapLocationDict.FirstOrDefault(x => x.Value == maplocation).Key;

            if (mapKey == null)
            {
                Logger.LogError($"Map location {maplocation} not found in dictionary.");
                return null;
            }

            string dllPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(dllPath);
            string jsonFilePath = Path.Combine(directoryPath, "patterns", selectionName, $"{mapKey}_start.json");

            if (File.Exists(jsonFilePath))
            {
                var jsonString = File.ReadAllText(jsonFilePath);
                var startingBotsData = JsonConvert.DeserializeObject<StartingBotConfig>(jsonString);
                if (startingBotsData != null)
                {
                    Logger.LogDebug($"Successfully loaded {mapKey}_start.json for preset: {selectionName}");
                    return startingBotsData;
                }
                else
                {
                    Logger.LogError($"Failed to deserialize {mapKey}_start.json for preset: {selectionName}");
                    return null;
                }
            }
            else
            {
                Logger.LogError($"{mapKey}_start.json file not found at path: {jsonFilePath}");
                return null;
            }
        }

        public static List<Vector3> GetSpawnPointsForZones(AllMapsZoneConfig allMapsZoneConfig, string maplocation, List<string> zones)
        {
            var mapConfig = allMapsZoneConfig.Maps[maplocation];
            var spawnPoints = new List<Vector3>();

            if (zones.Contains("all"))
            {
                foreach (var zone in mapConfig.Zones.Values)
                {
                    spawnPoints.AddRange(zone.Select(coord => new Vector3(coord.x, coord.y, coord.z)));
                }
            }
            else
            {
                foreach (var zoneName in zones)
                {
                    if (mapConfig.Zones.TryGetValue(zoneName, out var zonePoints))
                    {
                        spawnPoints.AddRange(zonePoints.Select(coord => new Vector3(coord.x, coord.y, coord.z)));
                    }
                }
            }

            return spawnPoints.OrderBy(_ => UnityEngine.Random.value).ToList();
        }

        private async UniTask<bool> CheckHardCap(string wildSpawnType)
        {
            int activePMCs = await BotCountManager.GetAlivePlayers("pmc");
            int activeSCAVs = await BotCountManager.GetAlivePlayers("scav");

            if (wildSpawnType == "pmc" && activePMCs >= Initialization.PMCBotLimit && !hotspotIgnoreHardCapPMC.Value)
            {
                Logger.LogDebug($"PMC spawn not allowed due to PMC bot limit - skipping this spawn. Active PMCs: {activePMCs}, PMC Bot Limit: {Initialization.PMCBotLimit}");
                return false;
            }

            if (wildSpawnType == "scav" && activeSCAVs >= Initialization.SCAVBotLimit && !hotspotIgnoreHardCapSCAV.Value)
            {
                Logger.LogDebug($"SCAV spawn not allowed due to SCAV bot limit - skipping this spawn. Active SCAVs: {activeSCAVs}, SCAV Bot Limit: {Initialization.SCAVBotLimit}");
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
            int hardStopTime;
            int hardStopPercent;

            if (spawnType == "pmc")
            {
                hardStopTime = hardStopTimePMC.Value;
                hardStopPercent = hardStopPercentPMC.Value;
            }
            else
            {
                hardStopTime = hardStopTimeSCAV.Value;
                hardStopPercent = hardStopPercentSCAV.Value;
            }

            int raidTimeLeftTime = (int)Aki.SinglePlayer.Utils.InRaid.RaidTimeUtil.GetRemainingRaidSeconds(); // Time left
            int raidTimeLeftPercent = (int)(Aki.SinglePlayer.Utils.InRaid.RaidTimeUtil.GetRaidTimeRemainingFraction() * 100f); // Percent left

            //why is this method failing?

            Logger.LogWarning("RaidTimeLeftTime: " + raidTimeLeftTime + " RaidTimeLeftPercent: " + raidTimeLeftPercent + " HardStopTime: " + hardStopTime + " HardStopPercent: " + hardStopPercent);
            return useTimeBasedHardStop.Value ? raidTimeLeftTime >= hardStopTime : raidTimeLeftPercent >= hardStopPercent;
        }

        private void ResetGroupTimers(int groupNum, string wildSpawnType)
        {
            IEnumerable<BotWave> waves;

            if (wildSpawnType.Equals("pmc"))
            {
                waves = botWaveConfig.Maps[DonutsBotPrep.maplocation].PMC;
            }
            else if (wildSpawnType.Equals("scav"))
            {
                waves = botWaveConfig.Maps[DonutsBotPrep.maplocation].SCAV;
            }
            else
            {
                return;
            }

            foreach (var wave in waves)
            {
                if (wave.GroupNum == groupNum)
                {
                    wave.TriggerTimer = 0;
                    if (wave.IgnoreTimerFirstSpawn)
                    {
                        wave.IgnoreTimerFirstSpawn = false;
                    }
                }
            }
        }

        private UniTask<Player> UpdateDistancesAndFindFurthestBot()
        {
            return UniTask.Create(async () =>
            {
                float maxDistance = float.MinValue;
                Player furthestBot = null;

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

                if (furthestBot == null)
                {
                    Logger.LogWarning("Furthest bot is null. No bots found in the list.");
                }
                else
                {
                    Logger.LogDebug($"Furthest bot found: {furthestBot.Profile.Info.Nickname} at distance {Mathf.Sqrt(maxDistance)}");
                }

                return furthestBot;
            });
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

            if (!await ShouldConsiderDespawning(bottype))
            {
                return;
            }

            Player furthestBot = await UpdateDistancesAndFindFurthestBot();

            if (furthestBot != null)
            {
                DespawnBot(furthestBot, bottype);
            }
            else
            {
                Logger.LogWarning("No bot found to despawn.");
            }
        }

        private void DespawnBot(Player furthestBot, string bottype)
        {
            if (furthestBot == null)
            {
                Logger.LogError("Attempted to despawn a null bot.");
                return;
            }

            BotOwner botOwner = furthestBot.AIData.BotOwner;
            if (botOwner == null)
            {
                Logger.LogError("BotOwner is null for the furthest bot.");
                return;
            }

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

        private async UniTask<bool> ShouldConsiderDespawning(string botType)
        {
            int botLimit = botType == "pmc" ? Initialization.PMCBotLimit : Initialization.SCAVBotLimit;
            int activeBotCount = await BotCountManager.GetAlivePlayers(botType);

            return activeBotCount > botLimit; // Only consider despawning if the number of active bots of the type exceeds the limit
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
