using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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

#pragma warning disable IDE0007, IDE0044
namespace Donuts
{
    public class DonutComponent : MonoBehaviour
    {
        internal CancellationTokenSource cts;

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

        internal static bool maxRespawnReachedPMC;
        internal static bool maxRespawnReachedSCAV;
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

        internal static bool IsBotSpawningEnabled { get => (bool)AccessTools.Field(typeof(BotsController), "_botEnabled").GetValue(Singleton<IBotGame>.Instance.BotsController); }

        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public DonutComponent()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(DonutComponent));
        }

        private void ResetPlayerList()
        {
            playerList.Clear();
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
            cts = new CancellationTokenSource();

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
            if (!IsBotSpawningEnabled)
            {
                return;
            }

            Initialization.InitializeStaticVariables();
            mainplayer = gameWorld.MainPlayer;
            isInBattle = false;
            Logger.LogDebug("Setup maplocation: " + DonutsBotPrep.maplocation);

            Initialization.LoadFightLocations(cts.Token).Forget();

            botWaveConfig = GetBotWavesConfig(DonutsBotPrep.selectionName);
            botWaves = botWaveConfig.Maps[DonutsBotPrep.maplocation];

            // reset starting bots boolean each raid
            hasSpawnedStartingBots = false;

            // reset max respawns each raid
            maxRespawnReachedPMC = false;
            maxRespawnReachedSCAV = false;

            // reset current max bot counts each raid
            currentMaxPMC = 0;
            currentMaxSCAV = 0;

            Logger.LogDebug("Setup PMC Bot limit: " + Initialization.PMCBotLimit);
            Logger.LogDebug("Setup SCAV Bot limit: " + Initialization.SCAVBotLimit);

            spawnCheckTimer.Start();

            mainplayer.BeingHitAction += BeingHitBattleCoolDown;

            ResetPlayerList();
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
            if (!PluginEnabled.Value || !fileLoaded || !IsBotSpawningEnabled)
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
                StartSpawnProcess(cts.Token).Forget();
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
            var allBotWaves = botWaves.PMC.Concat(botWaves.SCAV).ToList();

            while (!cancellationToken.IsCancellationRequested)
            {
                bool anySpawned = false;

                foreach (var botWave in allBotWaves)
                {
                    if (botWave.ShouldSpawn())
                    {
                        if (isInBattle && timeSinceLastHit < battleStateCoolDown.Value)
                        {
                            Logger.LogDebug("In battle state cooldown, breaking the loop.");
                            break;
                        }

                        var wildSpawnType = botWaves.PMC.Contains(botWave) ? "pmc" : "scav";

                        if (CanSpawn(botWave, wildSpawnType))
                        {
                            var spawnPointsDict = DonutComponent.GetSpawnPointsForZones(DonutsBotPrep.allMapsZoneConfig, DonutsBotPrep.maplocation, botWave.Zones);

                            if (spawnPointsDict.Any())
                            {
                                var random = new System.Random();
                                var zoneKeys = spawnPointsDict.Keys.OrderBy(_ => random.Next()).ToList();

                                if (zoneKeys.Any())
                                {
                                    var randomZone = zoneKeys.First();
                                    var coordinates = spawnPointsDict[randomZone];

                                    bool isHotspotZone = randomZone.IndexOf("hotspot", StringComparison.OrdinalIgnoreCase) >= 0;

                                    if ((isHotspotZone && wildSpawnType == "pmc" && hotspotBoostPMC.Value) ||
                                        (isHotspotZone && wildSpawnType == "scav" && hotspotBoostSCAV.Value))
                                    {
                                        Logger.LogDebug($"{randomZone} is a hotspot; hotspot boost is enabled, setting spawn chance to 100");
                                        botWave.SpawnChance = 100;
                                    }

                                    foreach (var coordinate in coordinates)
                                    {
                                        if (BotSpawn.IsWithinBotActivationDistance(botWave, coordinate))
                                        {
                                            Logger.LogDebug($"Triggering spawn for botWave: {botWave} at {randomZone}, {coordinate}");
                                            await TriggerSpawn(botWave, randomZone, coordinate, wildSpawnType, coordinates, cancellationToken);
                                            anySpawned = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        // if CanSpawn if false then we need to reset the timers for this wave
                        ResetGroupTimers(botWave.GroupNum, wildSpawnType);
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update);

                if (!anySpawned)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: cancellationToken);
                }
            }
        }

        // Checks trigger distance and spawn chance
        private bool CanSpawn(BotWave botWave, string wildSpawnType)
        {
            int randomValue = UnityEngine.Random.Range(0, 100);
            bool canSpawn = randomValue < botWave.SpawnChance;

            Logger.LogDebug($"SpawnChance: {botWave.SpawnChance}, RandomValue: {randomValue}, CanSpawn: {canSpawn}");

            return canSpawn;
        }

        // Checks certain spawn options, reset groups timers
        private async UniTask TriggerSpawn(BotWave botWave, string zone, Vector3 coordinate, string wildSpawnType, List<Vector3> coordinates, CancellationToken cancellationToken)
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

            await BotSpawn.SpawnBots(botWave, zone, coordinate, wildSpawnType, coordinates, cancellationToken);
        }

        // Get the spawn wave configs from the waves json files
        public static BotWavesConfig GetBotWavesConfig(string selectionName)
        {
            string mapName = DonutsBotPrep.mapName;
            string dllPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(dllPath);
            string jsonFilePath = Path.Combine(directoryPath, "patterns", selectionName, $"{mapName}_waves.json");

            if (File.Exists(jsonFilePath))
            {
                var jsonString = File.ReadAllText(jsonFilePath);
                var botWavesData = JsonConvert.DeserializeObject<BotWavesConfig>(jsonString);
                if (botWavesData != null)
                {
                    Logger.LogDebug($"Successfully loaded {mapName}_waves.json for preset: {selectionName}");
                    EnsureUniqueGroupNumsForWave(botWavesData);
                    return botWavesData;
                }
                else
                {
                    Logger.LogError($"Failed to deserialize {mapName}_waves.json for preset: {selectionName}");
                    return null;
                }
            }
            else
            {
                Logger.LogError($"{mapName}_waves.json file not found at path: {jsonFilePath}");
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
            string dllPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(dllPath);
            string jsonFilePath = Path.Combine(directoryPath, "patterns", selectionName, $"{DonutsBotPrep.mapName}_start.json");

            if (File.Exists(jsonFilePath))
            {
                var jsonString = File.ReadAllText(jsonFilePath);
                var startingBotsData = JsonConvert.DeserializeObject<StartingBotConfig>(jsonString);
                return startingBotsData;
            }
            else
            {
                Logger.LogError($"{DonutsBotPrep.mapName}_start.json file not found.");
                return null;
            }
        }

        public static Dictionary<string, List<Vector3>> GetSpawnPointsForZones(AllMapsZoneConfig allMapsZoneConfig, string maplocation, List<string> zoneNames)
        {
            var spawnPointsDict = new Dictionary<string, List<Vector3>>();

            if (!allMapsZoneConfig.Maps.TryGetValue(maplocation, out var mapZoneConfig))
            {
                Logger.LogError($"Map location {maplocation} not found in zone configuration.");
                return spawnPointsDict;
            }

            foreach (var zoneName in zoneNames)
            {
                if (zoneName == "all")
                {
                    foreach (var zone in mapZoneConfig.Zones)
                    {
                        if (!spawnPointsDict.ContainsKey(zone.Key))
                        {
                            spawnPointsDict[zone.Key] = new List<Vector3>();
                        }
                        spawnPointsDict[zone.Key].AddRange(zone.Value.Select(c => new Vector3(c.x, c.y, c.z)));
                    }
                }
                else if (zoneName == "start" || zoneName == "hotspot")
                {
                    foreach (var zone in mapZoneConfig.Zones)
                    {
                        if (zone.Key.IndexOf(zoneName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!spawnPointsDict.ContainsKey(zone.Key))
                            {
                                spawnPointsDict[zone.Key] = new List<Vector3>();
                            }
                            spawnPointsDict[zone.Key].AddRange(zone.Value.Select(c => new Vector3(c.x, c.y, c.z)));
                        }
                    }
                }
                else
                {
                    if (mapZoneConfig.Zones.TryGetValue(zoneName, out var coordinates))
                    {
                        if (!spawnPointsDict.ContainsKey(zoneName))
                        {
                            spawnPointsDict[zoneName] = new List<Vector3>();
                        }
                        spawnPointsDict[zoneName].AddRange(coordinates.Select(c => new Vector3(c.x, c.y, c.z)));
                    }
                }
            }

            return spawnPointsDict;
        }

        public async UniTask<bool> CheckHardCap(string wildSpawnType, CancellationToken cancellationToken)
        {
            int activePMCs = await BotCountManager.GetAlivePlayers("pmc", cancellationToken);
            int activeSCAVs = await BotCountManager.GetAlivePlayers("scav", cancellationToken);

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
            var botWaves = wildSpawnType == "pmc" ? botWaveConfig.Maps[DonutsBotPrep.maplocation].PMC : botWaveConfig.Maps[DonutsBotPrep.maplocation].SCAV;

            foreach (var botWave in botWaves)
            {
                if (botWave.GroupNum == groupNum)
                {
                    botWave.ResetTimer();
#if DEBUG
                    DonutComponent.Logger.LogDebug($"Resetting timer for GroupNum: {groupNum}, WildSpawnType: {wildSpawnType}");
#endif
                }
            }
        }

        private UniTask<Player> UpdateDistancesAndFindFurthestBot(string bottype)
        {
            return UniTask.Create(async () =>
            {
                float maxDistance = float.MinValue;
                Player furthestBot = null;

                foreach (var bot in gameWorld.AllAlivePlayersList)
                {
                    if (!bot.IsYourPlayer && bot.AIData.BotOwner != null && IsBotType(bot, bottype))
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

        private bool IsBotType(Player bot, string bottype)
        {
            switch (bottype)
            {
                case "scav":
                    return BotCountManager.IsSCAV(bot.Profile.Info.Settings.Role);
                case "pmc":
                    return BotCountManager.IsPMC(bot.Profile.Info.Settings.Role);
                default:
                    throw new ArgumentException("Invalid bot type", nameof(bottype));
            }
        }

        private async UniTask DespawnFurthestBot(string bottype, CancellationToken cancellationToken)
        {
            float despawnCooldown = bottype == "pmc" ? PMCdespawnCooldown : SCAVdespawnCooldown;
            float despawnCooldownDuration = bottype == "pmc" ? PMCdespawnCooldownDuration : SCAVdespawnCooldownDuration;

            float currentTime = Time.time;
            float timeSinceLastDespawn = currentTime - despawnCooldown;

            if (timeSinceLastDespawn < despawnCooldownDuration)
            {
                return;
            }

            if (!await ShouldConsiderDespawning(bottype, cancellationToken))
            {
                return;
            }

            Player furthestBot = await UpdateDistancesAndFindFurthestBot(bottype);

            if (furthestBot != null)
            {
                DespawnBot(furthestBot, bottype);
            }
            else
            {
                Logger.LogWarning($"No {bottype} bot found to despawn.");
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
            int activeBotCount = await BotCountManager.GetAlivePlayers(botType, cancellationToken);

            return activeBotCount > botLimit; // Only consider despawning if the number of active bots of the type exceeds the limit
        }

        private void OnGUI()
        {
            gizmos.ToggleGizmoDisplay(DebugGizmos.Value);
        }

        private void OnDestroy()
        {
            // Cancel any ongoing tasks
            cts?.Cancel();
            cts?.Dispose();

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

            if (IsBotSpawningEnabled)
            {
                mainplayer.BeingHitAction -= BeingHitBattleCoolDown;
            }

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
