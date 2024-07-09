using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using SPT.PrePatch;
using SPT.Reflection.Utils;
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
using System.Threading;

#pragma warning disable IDE0007, IDE0044
namespace Donuts
{
    public class DonutComponent : MonoBehaviour
    {
        internal static FightLocations fightLocations;
        internal static FightLocations sessionLocations;

        internal static List<List<Entry>> groupedFightLocations;
        internal static BotWavesConfig botWaveConfig;
        internal static Dictionary<int, List<HotspotTimer>> groupedHotspotTimers;

        internal List<WildSpawnType> validDespawnListPMC = new List<WildSpawnType>()
        {
            WildSpawnType.pmcUSEC,
            WildSpawnType.pmcBEAR
        };

        internal List<WildSpawnType> validDespawnListScav = new List<WildSpawnType>()
        {
            WildSpawnType.assault,
            WildSpawnType.cursedAssault
        };

        internal static Dictionary<string, string> mapLocationDict = new Dictionary<string, string>
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
        internal static int currentMaxPMC;
        internal static int currentMaxSCAV;

        internal static GameWorld gameWorld;
        internal static BotSpawner botSpawnerClass;
        internal static IBotCreator botCreator;
        internal static List<Player> playerList = new List<Player>();

        internal float PMCdespawnCooldown = 0f;
        internal float PMCdespawnCooldownDuration = despawnInterval.Value;

        internal float SCAVdespawnCooldown = 0f;
        internal float SCAVdespawnCooldownDuration = despawnInterval.Value;

        internal static List<HotspotTimer> hotspotTimers;

        internal static MapBotWaves botWaves;
        internal static Dictionary<string, MethodInfo> methodCache;
        internal static MethodInfo displayMessageNotificationMethod;

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
            Initialization.LoadFightLocations(this.GetCancellationTokenOnDestroy());

            botWaveConfig = GetBotWavesConfig(DonutsBotPrep.selectionName);
            botWaves = botWaveConfig.Maps[DonutsBotPrep.maplocation];

            // reset starting bots boolean each raid
            hasSpawnedStartingBots = false;

            // reset current max bot counts each raid
            currentMaxPMC = 0;
            currentMaxSCAV = 0;

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
                StartSpawnProcess(this.GetCancellationTokenOnDestroy()).Forget();
            }

            Gizmos.DisplayMarkerInformation();
        }

        private async UniTask StartSpawnProcess(CancellationToken cancellationToken)
        {
            if (!hasSpawnedStartingBots)
            {
                if (DonutsBotPrep.botSpawnInfos != null && DonutsBotPrep.botSpawnInfos.Any())
                {
                    await BotSpawn.SpawnBotsFromInfo(DonutsBotPrep.botSpawnInfos, cancellationToken);
                    hasSpawnedStartingBots = true;
                }
            }

            if (DespawnEnabledPMC.Value)
            {
                await DespawnFurthestBot("pmc", cancellationToken);
            }

            if (DespawnEnabledSCAV.Value)
            {
                await DespawnFurthestBot("scav", cancellationToken);
            }

            await SpawnBotWaves(botWaveConfig.Maps[DonutsBotPrep.maplocation], cancellationToken);
        }

        private async UniTask SpawnBotWaves(MapBotWaves botWaves, CancellationToken cancellationToken)
        {
            foreach (var botWave in botWaves.PMC.Concat(botWaves.SCAV))
            {
                if (botWave.ShouldSpawn())
                {
                    if (isInBattle && timeSinceLastHit < battleStateCoolDown.Value)
                    {
                        Logger.LogDebug($"Skipping spawn due to battle cooldown. Time since last hit: {timeSinceLastHit}");
                        break;
                    }

                    // Get coordinates
                    var spawnPointsDict = DonutComponent.GetSpawnPointsForZones(DonutsBotPrep.allMapsZoneConfig, DonutsBotPrep.maplocation, botWave.Zones);

                    if (spawnPointsDict.Any())
                    {
                        // Select a random coordinate from any zone
                        var randomZone = spawnPointsDict.Keys.ElementAt(UnityEngine.Random.Range(0, spawnPointsDict.Count));
                        var coordinate = spawnPointsDict[randomZone];

                        var wildSpawnType = botWaves.PMC.Contains(botWave) ? "pmc" : "scav";

                        if (CanSpawn(botWave, randomZone, coordinate, wildSpawnType))
                        {
                            await TriggerSpawn(botWave, randomZone, coordinate, wildSpawnType, cancellationToken: this.GetCancellationTokenOnDestroy());
                            break;
                        }
                    }
                }
            }
        }

        // Checks trigger distance and spawn chance
        private bool CanSpawn(BotWave botWave, string zone, Vector3 coordinate, string wildSpawnType)
        {
            if (BotSpawn.IsWithinBotActivationDistance(botWave, coordinate))
            {
                bool isHotspotZone = zone.IndexOf("hotspot", StringComparison.OrdinalIgnoreCase) >= 0;

                if ((isHotspotZone && wildSpawnType == "pmc" && hotspotBoostPMC.Value) ||
                    (isHotspotZone && wildSpawnType == "scav" && hotspotBoostSCAV.Value))
                {
                    botWave.SpawnChance = 100;
                }

                return UnityEngine.Random.Range(0, 100) < botWave.SpawnChance;
            }
            return false;
        }

        // Checks certain spawn options, reset groups timers
        private async UniTask TriggerSpawn(BotWave botWave, string zone, Vector3 coordinate, string wildSpawnType, CancellationToken cancellationToken)
        {
            if (forceAllBotType.Value != "Disabled")
            {
                wildSpawnType = forceAllBotType.Value.ToLower();
            }

            var tasks = new List<UniTask<bool>>();

            if (HardCapEnabled.Value)
            {
                tasks.Add(CheckHardCap(wildSpawnType, cancellationToken));
            }

            tasks.Add(CheckRaidTime(wildSpawnType, cancellationToken));

            bool[] results = await UniTask.WhenAll(tasks);

            if (results.Any(result => !result))
            {
                ResetGroupTimers(botWave.GroupNum, wildSpawnType); // Reset timer if the wave is hard capped
                return;
            }

            botWave.TimesSpawned++;
            ResetGroupTimers(botWave.GroupNum, wildSpawnType);

            if (botWave.TimesSpawned >= botWave.MaxTriggersBeforeCooldown)
            {
                botWave.TriggerCooldown();
            }

            await BotSpawn.SpawnBots(botWave, zone, coordinate, wildSpawnType, cancellationToken);
        }

        // Get the spawn wave configs from the waves json files
        public static BotWavesConfig GetBotWavesConfig(string selectionName)
        {
            var mapKey = mapLocationDict.FirstOrDefault(x => x.Value == DonutsBotPrep.maplocation).Key;

            if (mapKey == null)
            {
                Logger.LogError($"Map location {DonutsBotPrep.maplocation} not found in dictionary.");
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
                    EnsureUniqueGroupNumsForWave(botWavesData);
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

        private static void EnsureUniqueGroupNumsForWave(BotWavesConfig botWavesConfig)
        {
            foreach (var map in botWavesConfig.Maps.Values)
            {
                var uniqueScavWaves = EnsureUniqueGroupNums(map.SCAV);
                map.SCAV = uniqueScavWaves;

                var uniquePmcWaves = EnsureUniqueGroupNums(map.PMC);
                map.PMC = uniquePmcWaves;
            }
        }

        private static List<BotWave> EnsureUniqueGroupNums(List<BotWave> botWaves)
        {
            var uniqueWavesDict = new Dictionary<int, BotWave>();
            var groupedByGroupNum = botWaves.GroupBy(wave => wave.GroupNum);

            foreach (var group in groupedByGroupNum)
            {
                if (group.Count() > 1)
                {
                    var selectedWave = group.OrderBy(_ => UnityEngine.Random.value).First();
                    uniqueWavesDict[group.Key] = selectedWave;
                }
                else
                {
                    uniqueWavesDict[group.Key] = group.First();
                }
            }

            return uniqueWavesDict.Values.ToList();
        }

        public static StartingBotConfig GetStartingBotConfig(string selectionName)
        {

            // I have to do this because I get NREs for some reason otherwise
            var mapName = "";

            if (DonutsBotPrep.maplocation == "bigmap")
            {
                mapName = "customs";
            }
            else if (DonutsBotPrep.maplocation == "factory4_day")
            {
                mapName = "factory";
            }
            else if (DonutsBotPrep.maplocation == "factory4_night")
            {
                mapName = "factory_night";
            }
            else if (DonutsBotPrep.maplocation == "tarkovstreets")
            {
                mapName = "streets";
            }
            else if (DonutsBotPrep.maplocation == "rezervbase")
            {
                mapName = "reserve";
            }
            else if (DonutsBotPrep.maplocation == "interchange")
            {
                mapName = "interchange";
            }
            else if (DonutsBotPrep.maplocation == "woods")
            {
                mapName = "woods";
            }
            else if (DonutsBotPrep.maplocation == "sandbox")
            {
                mapName = "groundzero";
            }
            else if (DonutsBotPrep.maplocation == "laboratory")
            {
                mapName = "laboratory";
            }
            else if (DonutsBotPrep.maplocation == "lighthouse")
            {
                mapName = "lighthouse";
            }
            else if (DonutsBotPrep.maplocation == "shoreline")
            {
                mapName = "shoreline";
            }
            else
            {
                Logger.LogError($"Map location '{DonutsBotPrep.maplocation}' is not recognized.");
            }

            string dllPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(dllPath);
            string jsonFilePath = Path.Combine(directoryPath, "patterns", selectionName, $"{mapName}_start.json");

            if (File.Exists(jsonFilePath))
            {
                var jsonString = File.ReadAllText(jsonFilePath);
                var startingBotsData = JsonConvert.DeserializeObject<StartingBotConfig>(jsonString);
                return startingBotsData;
            }
            else
            {
                Logger.LogError($"{mapName}_start.json file not found.");
                return null;
            }
        }

        // Gets a list of spawn points for defined zones. Checks for certain keywords.
        public static Dictionary<string, Vector3> GetSpawnPointsForZones(AllMapsZoneConfig allMapsZoneConfig, string maplocation, List<string> zones)
        {
            var spawnPointsDict = new Dictionary<string, Vector3>();

            if (allMapsZoneConfig == null)
            {
                Logger.LogError("allMapsZoneConfig is null.");
                return spawnPointsDict;
            }

            var lowerCaseZones = zones.Select(z => z.ToLowerInvariant()).ToList();

            if (lowerCaseZones.Contains("start"))
            {
                if (!allMapsZoneConfig.StartZones.TryGetValue(maplocation, out var startZoneConfig))
                {
                    Logger.LogError($"Start zones for map location '{maplocation}' not found in allMapsZoneConfig.");
                    return spawnPointsDict;
                }

                foreach (var zone in startZoneConfig)
                {
                    var randomCoord = zone.Value.OrderBy(_ => UnityEngine.Random.value).FirstOrDefault();
                    if (randomCoord != null)
                    {
                        spawnPointsDict[zone.Key] = new Vector3(randomCoord.x, randomCoord.y, randomCoord.z);
                    }
                }
            }
            else if (lowerCaseZones.Contains("all"))
            {
                if (!allMapsZoneConfig.Maps.TryGetValue(maplocation, out var mapConfig))
                {
                    Logger.LogError($"Map location '{maplocation}' not found in allMapsZoneConfig.");
                    return spawnPointsDict;
                }

                foreach (var zone in mapConfig.Zones)
                {
                    var randomCoord = zone.Value.OrderBy(_ => UnityEngine.Random.value).FirstOrDefault();
                    if (randomCoord != null)
                    {
                        spawnPointsDict[zone.Key] = new Vector3(randomCoord.x, randomCoord.y, randomCoord.z);
                    }
                }
            }
            else if (lowerCaseZones.Contains("hotspot"))
            {
                if (!allMapsZoneConfig.Maps.TryGetValue(maplocation, out var mapConfig))
                {
                    Logger.LogError($"Map location '{maplocation}' not found in allMapsZoneConfig.");
                    return spawnPointsDict;
                }

                foreach (var zone in mapConfig.Zones)
                {
                    if (zone.Key.ToLowerInvariant().Contains("hotspot"))
                    {
                        var randomCoord = zone.Value.OrderBy(_ => UnityEngine.Random.value).FirstOrDefault();
                        if (randomCoord != null)
                        {
                            spawnPointsDict[zone.Key] = new Vector3(randomCoord.x, randomCoord.y, randomCoord.z);
                        }
                    }
                }
            }
            else
            {
                if (!allMapsZoneConfig.Maps.TryGetValue(maplocation, out var mapConfig))
                {
                    Logger.LogError($"Map location '{maplocation}' not found in allMapsZoneConfig.");
                    return spawnPointsDict;
                }

                foreach (var zoneName in lowerCaseZones)
                {
                    if (mapConfig.Zones.TryGetValue(zoneName, out var zonePoints))
                    {
                        var randomCoord = zonePoints.OrderBy(_ => UnityEngine.Random.value).FirstOrDefault();
                        if (randomCoord != null)
                        {
                            spawnPointsDict[zoneName] = new Vector3(randomCoord.x, randomCoord.y, randomCoord.z);
                        }
                    }
                }
            }

            return spawnPointsDict;
        }

        public async UniTask<bool> CheckHardCap(string wildSpawnType, CancellationToken cancellationToken)
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

        private async UniTask<bool> CheckRaidTime(string wildSpawnType, CancellationToken cancellationToken)
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

            int raidTimeLeftTime = (int)SPT.SinglePlayer.Utils.InRaid.RaidTimeUtil.GetRemainingRaidSeconds(); // Time left
            int raidTimeLeftPercent = (int)(SPT.SinglePlayer.Utils.InRaid.RaidTimeUtil.GetRaidTimeRemainingFraction() * 100f); // Percent left

            //why is this method failing?

            Logger.LogWarning("RaidTimeLeftTime: " + raidTimeLeftTime + " RaidTimeLeftPercent: " + raidTimeLeftPercent + " HardStopTime: " + hardStopTime + " HardStopPercent: " + hardStopPercent);
            return useTimeBasedHardStop.Value ? raidTimeLeftTime >= hardStopTime : raidTimeLeftPercent >= hardStopPercent;
        }

        public void ResetGroupTimers(int groupNum, string wildSpawnType)
        {
            DonutComponent.Logger.LogDebug($"ResetGroupTimers called for GroupNum: {groupNum}, WildSpawnType: {wildSpawnType}");

            var botWaves = wildSpawnType == "pmc" ? botWaveConfig.Maps[DonutsBotPrep.maplocation].PMC : botWaveConfig.Maps[DonutsBotPrep.maplocation].SCAV;

            foreach (var botWave in botWaves)
            {
                if (botWave.GroupNum == groupNum)
                {
                    botWave.ResetTimer();
                    DonutComponent.Logger.LogDebug($"Resetting timer for GroupNum: {groupNum}, BotWave: {botWave}");
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

        private async UniTask DespawnFurthestBot(string bottype, CancellationToken cancellationToken)
        {
            if (bottype != "pmc" && bottype != "scav")
                return;

            float despawnCooldown = bottype == "pmc" ? PMCdespawnCooldown : SCAVdespawnCooldown;
            float despawnCooldownDuration = bottype == "pmc" ? PMCdespawnCooldownDuration : SCAVdespawnCooldownDuration;

            if (Time.time - despawnCooldown < despawnCooldownDuration)
            {
                return;
            }

            if (!await ShouldConsiderDespawning(bottype, cancellationToken))
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

        private async UniTask<bool> ShouldConsiderDespawning(string botType, CancellationToken cancellationToken)
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
