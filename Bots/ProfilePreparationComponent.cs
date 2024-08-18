using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using IProfileData = GClass592;

#pragma warning disable IDE0007, CS4014

namespace Donuts
{
    internal class DonutsBotPrep : MonoBehaviour
    {
        internal static string selectionName;
        internal static string maplocation;
        internal static string mapName;

        internal CancellationTokenSource ctsprep;

        private static GameWorld gameWorld;
        private static IBotCreator botCreator;
        private static BotSpawner botSpawnerClass;
        private static Player mainplayer;

        internal static Dictionary<string, WildSpawnType> OriginalBotSpawnTypes;

        internal static ConcurrentBag<BotSpawnInfo> botSpawnInfos
        {
            get; set;
        }

        private HashSet<string> usedZonesPMC = new HashSet<string>();
        private HashSet<string> usedZonesSCAV = new HashSet<string>();
        private HashSet<string> usedZonesBoss = new HashSet<string>();

        public static ConcurrentBag<PrepBotInfo> BotInfos
        {
            get; set;
        }

        public static AllMapsZoneConfig allMapsZoneConfig;

        internal static float timeSinceLastReplenish = 0f;

        private bool isReplenishing = false;
        public static bool IsBotPreparationComplete { get; private set; } = false;

        internal static ManualLogSource Logger
        {
            get; private set;
        }

        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(5); // Limit to 5 concurrent tasks

        public DonutsBotPrep()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(DonutsBotPrep));
        }

        public async static void Enable()
        {
            gameWorld = Singleton<GameWorld>.Instance;
            var component = gameWorld.GetOrAddComponent<DonutsBotPrep>();

            // Await the initialization before proceeding
            await component.InitializeAsync();

            // After all initialization tasks are complete, set this flag
            IsBotPreparationComplete = true;
            Logger.LogInfo("DonutBotPrep Enabled");
        }

        public async UniTask InitializeAsync()
        {
            Logger.LogInfo("Initialization started.");

            var playerLoop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            Cysharp.Threading.Tasks.PlayerLoopHelper.Initialize(ref playerLoop);

            botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            botCreator = AccessTools.Field(typeof(BotSpawner), "_botCreator").GetValue(botSpawnerClass) as IBotCreator;
            mainplayer = gameWorld?.MainPlayer;
            OriginalBotSpawnTypes = new Dictionary<string, WildSpawnType>();
            BotInfos = new ConcurrentBag<PrepBotInfo>();
            botSpawnInfos = new ConcurrentBag<BotSpawnInfo>();
            timeSinceLastReplenish = 0;
            IsBotPreparationComplete = false;

            botSpawnerClass.OnBotRemoved += BotSpawnerClass_OnBotRemoved;
            botSpawnerClass.OnBotCreated += BotSpawnerClass_OnBotCreated;

            if (mainplayer != null)
            {
                Logger.LogInfo("Mainplayer is not null, attaching event handlers");
                mainplayer.BeingHitAction += Mainplayer_BeingHitAction;
            }

            // Get selected preset and setup bot limits now
            selectionName = DonutsPlugin.RunWeightedScenarioSelectionSync();
            DonutInitialization.SetupBotLimitSync(selectionName);

            Logger.LogWarning($"Selected selectionName: {selectionName}");

            DetermineMapLocationAndName();

            Logger.LogWarning($"Determined mapName: {mapName}");

            var startingBotConfig = GetStartingBotConfig(selectionName);

            if (startingBotConfig == null)
                return;

            allMapsZoneConfig = AllMapsZoneConfig.LoadFromDirectory(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "zoneSpawnPoints"));

            if (allMapsZoneConfig == null)
            {
                Logger.LogError("Failed to load AllMapsZoneConfig.");
                return;
            }

            if (string.IsNullOrEmpty(maplocation))
            {
                Logger.LogError("Map location is null or empty.");
                return;
            }

            // Use the ctsprep to cancel the preparation process if needed
            ctsprep = new CancellationTokenSource();

            // Gather tasks for initializing bot infos
            var botInitializationTasks = new List<UniTask>
            {
                InitializeBotInfos(startingBotConfig, maplocation, "PMC", ctsprep.Token),
                InitializeBotInfos(startingBotConfig, maplocation, "SCAV", ctsprep.Token),
                InitializeBossSpawns(startingBotConfig, maplocation, ctsprep.Token)
            };

            // Await all bot initialization tasks
            await UniTask.WhenAll(botInitializationTasks);

            Logger.LogInfo("Initialization completed.");
        }

        private void DetermineMapLocationAndName()
        {
            if (Singleton<GameWorld>.Instance.MainPlayer == null)
            {
                Logger.LogError("GameWorld or MainPlayer is null.");
                return;
            }

            string location = Singleton<GameWorld>.Instance.MainPlayer.Location.ToLower();

            if (location == "sandbox_high")
            {
                location = "sandbox";
            }

            maplocation = location;

            mapName = location switch
            {
                "bigmap" => "customs",
                "factory4_day" => "factory",
                "factory4_night" => "factory_night",
                "tarkovstreets" => "streets",
                "rezervbase" => "reserve",
                "interchange" => "interchange",
                "woods" => "woods",
                "sandbox" => "groundzero",
                "sandbox_high" => "groundzero",
                "laboratory" => "laboratory",
                "lighthouse" => "lighthouse",
                "shoreline" => "shoreline",
                _ => location
            };

            Logger.LogInfo($"Determined map location: {maplocation}, map name: {mapName}");
        }

        internal StartingBotConfig GetStartingBotConfig(string selectionName)
        {
            if (selectionName == null)
            {
                Logger.LogError("SelectionName is null");
                return null;
            }

            Logger.LogInfo($"SelectionName: {selectionName}");

            string dllPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(dllPath);
            string jsonFilePath = Path.Combine(directoryPath, "patterns", selectionName, $"{DonutsBotPrep.mapName}_start.json");

            Logger.LogInfo($"Expected JSON File Path: {jsonFilePath}");

            if (File.Exists(jsonFilePath))
            {
                var jsonString = File.ReadAllText(jsonFilePath);
                //Logger.LogInfo($"JSON Content: {jsonString}");

                try
                {
                    var startingBotsData = JsonConvert.DeserializeObject<StartingBotConfig>(jsonString);
                    if (startingBotsData == null)
                    {
                        Logger.LogError("Failed to deserialize starting bot config JSON file.");
                    }
                    else
                    {
                        Logger.LogInfo("Successfully deserialized starting bot config JSON file.");
                    }

                    return startingBotsData;
                }
                catch (JsonException jsonEx)
                {
                    Logger.LogError($"JSON Deserialization Error: {jsonEx.Message}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Unexpected Error during deserialization: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"{DonutsBotPrep.mapName}_start.json file not found at path: {jsonFilePath}");
            }

            return null;
        }

        private void BotSpawnerClass_OnBotRemoved(BotOwner bot)
        {
            bot.Memory.OnGoalEnemyChanged -= Memory_OnGoalEnemyChanged;
            OriginalBotSpawnTypes.Remove(bot.Profile.Id);
        }

        private void BotSpawnerClass_OnBotCreated(BotOwner bot)
        {
            bot.Memory.OnGoalEnemyChanged += Memory_OnGoalEnemyChanged;
        }

        private void Memory_OnGoalEnemyChanged(BotOwner owner)
        {
            if (owner != null && owner.Memory != null && owner.Memory.GoalEnemy != null && owner.Memory.HaveEnemy)
            {
                if (owner.Memory.GoalEnemy.Person == (IPlayer)gameWorld.MainPlayer.InteractablePlayer && owner.Memory.GoalEnemy.HaveSeenPersonal && owner.Memory.GoalEnemy.IsVisible)
                {
                    timeSinceLastReplenish = 0f;
                }
            }
        }

        private void Mainplayer_BeingHitAction(DamageInfo arg1, EBodyPart arg2, float arg3)
        {
            switch (arg1.DamageType)
            {
                case EDamageType.Btr:
                case EDamageType.Melee:
                case EDamageType.Bullet:
                case EDamageType.Explosion:
                case EDamageType.GrenadeFragment:
                case EDamageType.Sniper:
                    timeSinceLastReplenish = 0f;
                    break;
                default:
                    break;
            }
        }

        private async UniTask InitializeBotInfos(StartingBotConfig startingBotConfig, string maplocation, string botType, CancellationToken cancellationToken)
        {
            if (startingBotConfig == null)
            {
                Logger.LogError("startingBotConfig is null.");
                return;
            }
            try
            {
                Logger.LogInfo("Starting InitializeBotInfos");
                Logger.LogInfo($"Map Name is : {mapName}");
                Logger.LogInfo($"Map Location is : {maplocation}");

                botType = DefaultPluginVars.forceAllBotType.Value switch
                {
                    "PMC" => "PMC",
                    "SCAV" => "SCAV",
                    _ => botType
                };


                Logger.LogInfo($"Determined Bot Type: {botType}");

                string difficultySetting = botType == "PMC" ? DefaultPluginVars.botDifficultiesPMC.Value.ToLower() : DefaultPluginVars.botDifficultiesSCAV.Value.ToLower();
                Logger.LogInfo($"Difficulty Setting: {difficultySetting}");

                maplocation = maplocation == "sandbox_high" ? "sandbox" : maplocation;

                if (!startingBotConfig.Maps.TryGetValue(maplocation, out var mapConfig))
                {
                    Logger.LogError($"Maplocation {maplocation} not found in startingBotConfig.");
                    return;
                }
                Logger.LogInfo("Finished getting mapConfig");

                var mapBotConfig = botType == "PMC" ? mapConfig.PMC : mapConfig.SCAV;
                if (mapBotConfig == null)
                {
                    Logger.LogError($"Bot config for {botType} is null in maplocation {maplocation}.");
                    return;
                }

                Logger.LogInfo("Finished getting mapConfig of Bot Type");

                var difficultiesForSetting = GetDifficultiesForSetting(difficultySetting);
                if (difficultiesForSetting == null || difficultiesForSetting.Count == 0)
                {
                    Logger.LogError("No difficulties found for the setting.");
                    return;
                }

                Logger.LogInfo($"Number of Difficulties: {difficultiesForSetting.Count}");

                int maxBots = UnityEngine.Random.Range(mapBotConfig.MinCount, mapBotConfig.MaxCount + 1);
                maxBots = botType switch
                {
                    "PMC" when maxBots > DonutComponent.PMCBotLimit => DonutComponent.PMCBotLimit,
                    "SCAV" when maxBots > DonutComponent.SCAVBotLimit => DonutComponent.SCAVBotLimit,
                    _ => maxBots
                };

                Logger.LogInfo($"Max starting bots for {botType}: {maxBots}");

                var spawnPointsDict = DonutComponent.GetSpawnPointsForZones(allMapsZoneConfig, maplocation, mapBotConfig.Zones);
                if (spawnPointsDict == null || spawnPointsDict.Count == 0)
                {
                    Logger.LogError("No spawn points found.");
                    return;
                }

                Logger.LogInfo($"Number of Spawn Points: {spawnPointsDict.Count}");

                int totalBots = 0;
                var usedZones = botType == "PMC" ? usedZonesPMC : usedZonesSCAV;
                var random = new System.Random();
                var createBotTasks = new List<UniTask>();

                while (totalBots < maxBots)
                {
                    int groupSize = BotSpawnHelper.DetermineMaxBotCount(botType.ToLower(), mapBotConfig.MinGroupSize, mapBotConfig.MaxGroupSize);
                    groupSize = Math.Min(groupSize, maxBots - totalBots);

                    var wildSpawnType = botType == "PMC" ? GetPMCWildSpawnType() : WildSpawnType.assault;
                    var side = botType == "PMC" ? GetPMCSide(wildSpawnType) : EPlayerSide.Savage;

                    Logger.LogInfo($"Wild Spawn Type: {wildSpawnType}, Side: {side}");

                    var difficulty = difficultiesForSetting[UnityEngine.Random.Range(0, difficultiesForSetting.Count)];
                    Logger.LogInfo($"Selected Difficulty: {difficulty}");

                    var zoneKeys = spawnPointsDict.Keys.OrderBy(_ => random.Next()).ToList();
                    Logger.LogInfo($"Number of Zones: {zoneKeys.Count}");

                    string selectedZone = zoneKeys.FirstOrDefault(z => !usedZones.Contains(z));

                    if (selectedZone == null)
                    {
                        Logger.LogError("No available zones to select.");
                        usedZones.Clear();
                        selectedZone = zoneKeys.FirstOrDefault();

                        if (selectedZone == null)
                        {
                            Logger.LogError("No zones available even after clearing used zones.");
                            break;
                        }
                    }

                    Logger.LogInfo($"Selected Zone: {selectedZone}");

                    var coordinates = spawnPointsDict[selectedZone].OrderBy(_ => random.Next()).ToList();
                    if (coordinates == null || coordinates.Count == 0)
                    {
                        Logger.LogError($"No coordinates found in zone {selectedZone}.");
                        continue;
                    }

                    Logger.LogInfo($"Number of Coordinates in Zone: {coordinates.Count}");

                    usedZones.Add(selectedZone);

                    var botInfo = new PrepBotInfo(wildSpawnType, difficulty, side, groupSize > 1, groupSize);

                    // Control concurrency with SemaphoreSlim
                    await semaphore.WaitAsync(cancellationToken); // Acquire a slot
                    createBotTasks.Add(CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize, cancellationToken)
                        .ContinueWith(t => semaphore.Release())); // Release slot when task completes

                    BotInfos.Add(botInfo);

                    var botSpawnInfo = new BotSpawnInfo(wildSpawnType, groupSize, coordinates, difficulty, side, selectedZone);
                    botSpawnInfos.Add(botSpawnInfo);

                    totalBots += groupSize;

                    Logger.LogInfo($"Finished processing for Zone: {selectedZone}");
                }

                // Await all bot creation tasks
                await UniTask.WhenAll(createBotTasks);

                Logger.LogInfo("Finished InitializeBotInfos");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception in InitializeBotInfos: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async UniTask InitializeBossSpawns(StartingBotConfig startingBotConfig, string maplocation, CancellationToken cancellationToken)
        {
            Logger.LogInfo("Starting Initialize Boss Spawns");
            maplocation = maplocation == "sandbox_high" ? "sandbox" : maplocation;
            Logger.LogInfo($"InitializeBossSpawns: looking at maplocation: {maplocation}");
            var bossSpawnTasks = new List<UniTask>();

            if (startingBotConfig.Maps.TryGetValue(maplocation, out var MapBossConfig))
            {
                var bosses = MapBossConfig.BOSSES;
                if (bosses != null && bosses.Any())
                {
                    foreach (var bossSpawn in bosses)
                    {
                        int spawnChance;
                        string bossConfigName = WildSpawnTypeDictionaries.BossNameToConfigName.TryGetValue(bossSpawn.BossName, out var configName)
                            ? configName
                            : bossSpawn.BossName;

                        string mapConfigName = WildSpawnTypeDictionaries.MapNameToConfigName.TryGetValue(DonutsBotPrep.maplocation, out var mapName)
                            ? mapName
                            : DonutsBotPrep.maplocation;

                        if (DefaultPluginVars.BossUseGlobalSpawnChance.TryGetValue(bossConfigName, out var useGlobalChance) && useGlobalChance.Value)
                        {
                            if (DefaultPluginVars.BossSpawnChances.TryGetValue(bossConfigName, out var mapChances) &&
                                mapChances.TryGetValue(mapConfigName, out var chanceForMap))
                            {
                                spawnChance = chanceForMap.Value;
                            }
                            else
                            {
                                spawnChance = bossSpawn.BossChance;
                            }
                        }
                        else
                        {
                            spawnChance = bossSpawn.BossChance;
                        }

                        // Check if the boss should spawn based on the new spawn chance
                        var randomValue = UnityEngine.Random.Range(0, 100);
                        if (randomValue >= spawnChance)
                        {
                            Logger.LogInfo($"Boss spawn chance for {bossSpawn.BossName}: {spawnChance} failed (random value: {randomValue}) Not spawning this boss.");
                            return;
                        }

                        Logger.LogInfo($"Configuring boss spawn: {bossSpawn.BossName} with chance {bossSpawn.BossChance}");

                        // Use similar logic as InitializeBotInfos to get zone and coordinates
                        var spawnPointsDict = DonutComponent.GetSpawnPointsForZones(allMapsZoneConfig, maplocation, bossSpawn.Zones);
                        if (spawnPointsDict == null || spawnPointsDict.Count == 0)
                        {
                            Logger.LogError("No spawn points found.");
                            continue;
                        }

                        var random = new System.Random();
                        var zoneKeys = spawnPointsDict.Keys.OrderBy(_ => random.Next()).ToList();

                        string selectedZone = zoneKeys.FirstOrDefault(z => !usedZonesBoss.Contains(z));
                        if (selectedZone == null)
                        {
                            Logger.LogError("No available zones to select.");
                            usedZonesBoss.Clear();
                            selectedZone = zoneKeys.FirstOrDefault();

                            if (selectedZone == null)
                            {
                                Logger.LogError("No zones available even after clearing used zones.");
                                break;
                            }
                        }

                        var coordinates = spawnPointsDict[selectedZone].OrderBy(_ => random.Next()).ToList();
                        if (coordinates == null || coordinates.Count == 0)
                        {
                            Logger.LogError($"No coordinates found in zone {selectedZone}.");
                            continue;
                        }

                        usedZonesBoss.Add(selectedZone);

                        // Control concurrency with SemaphoreSlim
                        await semaphore.WaitAsync(cancellationToken); // Acquire a slot

                        // Add task to list
                        var task = ScheduleBossSpawn(bossSpawn, coordinates, cancellationToken, selectedZone)
                            .ContinueWith(async () => semaphore.Release());

                        bossSpawnTasks.Add(task);
                    }

                    // Await all boss spawn tasks
                    await UniTask.WhenAll(bossSpawnTasks);

                    Logger.LogInfo("Finished InitializeBossSpawns");
                }
                else
                {
                    Logger.LogWarning($"No boss spawns configured for map {maplocation}");
                }
            }
        }


        private WildSpawnType GetPMCWildSpawnType()
        {
            switch (DefaultPluginVars.pmcFaction.Value)
            {
                case "USEC":
                    return WildSpawnType.pmcUSEC;
                case "BEAR":
                    return WildSpawnType.pmcBEAR;
                default:
                    return DeterminePMCFactionBasedOnRatio();
            }
        }

        private EPlayerSide GetPMCSide(WildSpawnType wildSpawnType)
        {
            switch (wildSpawnType)
            {
                case WildSpawnType.pmcUSEC:
                    return EPlayerSide.Usec;
                case WildSpawnType.pmcBEAR:
                    return EPlayerSide.Bear;
                default:
                    return EPlayerSide.Usec;
            }
        }

        private static List<BotDifficulty> GetDifficultiesForSetting(string difficultySetting)
        {
            switch (difficultySetting)
            {
                case "asonline":
                    return new List<BotDifficulty> { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard };
                case "easy":
                    return new List<BotDifficulty> { BotDifficulty.easy };
                case "normal":
                    return new List<BotDifficulty> { BotDifficulty.normal };
                case "hard":
                    return new List<BotDifficulty> { BotDifficulty.hard };
                case "impossible":
                    return new List<BotDifficulty> { BotDifficulty.impossible };
                default:
                    Logger.LogError($"Unsupported difficulty setting: {difficultySetting}");
                    return new List<BotDifficulty>();
            }
        }

        internal static async UniTask<BotCreationDataClass> CreateBot(PrepBotInfo botInfo, bool isGroup, int groupSize, CancellationToken cancellationToken, bool isSupport = false, IProfileData profData = null)
        {
            if (botCreator == null)
            {
                Logger.LogError("Bot creator is not initialized.");
                return null;
            }

            IProfileData botData;
            if (profData == null)
            {
                botData = new IProfileData(botInfo.Side, botInfo.SpawnType, botInfo.Difficulty, 0f, null);
            }
            else
            {
                botData = profData;
            }

            BotCreationDataClass bot = await BotCreationDataClass.Create(botData, botCreator, groupSize, botSpawnerClass);

            if (bot == null || bot.Profiles == null || !bot.Profiles.Any())
            {
                Logger.LogError($"Failed to create or properly initialize bot for {botInfo.SpawnType}");
                return null;
            }

            botInfo.Bots = bot;
            Logger.LogInfo($"CreateBot: Bot created and assigned successfully: {bot.Profiles.Count} profiles loaded.");

            return bot;
        }

        private async UniTask ScheduleBossSpawn(BossSpawn bossSpawn, List<Vector3> coordinates, CancellationToken cancellationToken, string selectedZone)
        {
            Logger.LogInfo($"Attempting to schedule boss spawn: {bossSpawn.BossName} with delay of {bossSpawn.TimeDelay} seconds");

            // Delay before processing the spawn check
            if (bossSpawn.TimeDelay > 0)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(bossSpawn.TimeDelay), cancellationToken: cancellationToken);
            }

            // Check if the boss should spawn based on BossChance
            var randomValue = UnityEngine.Random.Range(0, 100);
            if (randomValue >= bossSpawn.BossChance)
            {
                Logger.LogInfo($"Boss spawn cancelled: {bossSpawn.BossName} (Chance: {bossSpawn.BossChance}%, Rolled: {randomValue})");
                return;
            }

            Logger.LogInfo($"Scheduling boss spawn: {bossSpawn.BossName} (Chance: {bossSpawn.BossChance}%, Rolled: {randomValue})");

            // Create boss and get the central position for supports
            var bossCreationData = await CreateBoss(bossSpawn, coordinates, cancellationToken, selectedZone);

            Logger.LogInfo($"ScheduleBossSpawn: Completed creating boss: {bossSpawn.BossName}");

            if (bossCreationData != null)
            {
                var centralPosition = bossCreationData.GetPosition();

                // Schedule support units
                if (bossSpawn.Supports != null && bossSpawn.Supports.Any())
                {
                    await ScheduleSupportsAsync(bossSpawn.Supports, centralPosition.position, coordinates, selectedZone, cancellationToken);
                }
            }
        }

        internal static async UniTask<BotCreationDataClass> CreateBoss(BossSpawn bossSpawn, List<Vector3> coordinates, CancellationToken cancellationToken, string selectedZone)
        {
            if (botCreator == null)
            {
                Logger.LogError("Bot creator is not initialized.");
                return null;
            }

            var bossWildSpawnType = WildSpawnTypeDictionaries.StringToWildSpawnType[bossSpawn.BossName.ToLower()];
            var bossSide = WildSpawnTypeDictionaries.WildSpawnTypeToEPlayerSide[bossWildSpawnType];
            var bossDifficulty = GetRandomDifficultyForBoss();
            var centralPosition = GetCentralPosition(coordinates);

            var bossData = CreateProfileData(bossSide, bossWildSpawnType, bossDifficulty);
            var botInfo = CreateBotInfo(bossWildSpawnType, bossDifficulty, bossSide);

            // Mark this bot as a boss
            bossData.SpawnParams = new BotSpawnParams();
            bossData.SpawnParams.ShallBeGroup = new ShallBeGroupParams(true, true);

            var boss = await CreateAndAddBot(botInfo, bossData, centralPosition.First(), cancellationToken, false);

            AddBotSpawnInfo(bossWildSpawnType, 1, centralPosition, bossDifficulty, bossSide, selectedZone);

            Logger.LogInfo($"Creating boss: Name={bossSpawn.BossName}, Difficulty={bossDifficulty}, Side={bossSide}");

            return boss;
        }

        private static async UniTask ScheduleSupportsAsync(List<Support> supports, Vector3 centralPosition, List<Vector3> coordinates, string selectedZone, CancellationToken cancellationToken)
        {
            foreach (var support in supports)
            {
                await CreateSupportAsync(support, centralPosition, coordinates, selectedZone, cancellationToken);
            }
        }

        private static async UniTask CreateSupportAsync(Support support, Vector3 centralPosition, List<Vector3> coordinates, string selectedZone, CancellationToken cancellationToken)
        {
            Logger.LogInfo($"Creating supportAsync Started for: Type={support.BossEscortType}, Amount={support.BossEscortAmount}");

            var supportWildSpawnType = WildSpawnTypeDictionaries.StringToWildSpawnType[support.BossEscortType.ToLower()];
            var supportSide = WildSpawnTypeDictionaries.WildSpawnTypeToEPlayerSide[supportWildSpawnType];
            var supportDifficulty = GetRandomDifficultyForSupport();

            for (int i = 0; i < support.BossEscortAmount; i++)
            {
                Logger.LogInfo($"Creating support bot {i + 1}/{support.BossEscortAmount}");

                // Create individual profile and info for each bot
                var supportData = CreateProfileData(supportSide, supportWildSpawnType, supportDifficulty);
                var supportInfo = CreateBotInfo(supportWildSpawnType, supportDifficulty, supportSide, 1);

                // Calculate offset position for each bot
                var offsetPosition = GetOffsetPosition(centralPosition);
                var offsetPositionList = new List<Vector3> { offsetPosition };

                // Mark this bot as a support
                supportData.SpawnParams = new BotSpawnParams();
                supportData.SpawnParams.ShallBeGroup = new ShallBeGroupParams(true, true, support.BossEscortAmount);

                AddBotSpawnInfo(supportWildSpawnType, 1, offsetPositionList, supportDifficulty, supportSide, selectedZone);

                // Control concurrency with SemaphoreSlim
                await semaphore.WaitAsync(cancellationToken); // Acquire a slot
                await CreateAndAddBot(supportInfo, supportData, offsetPosition, cancellationToken, true)
                    .ContinueWith(t => semaphore.Release()); // Release slot when task completes

                if (supportInfo.Bots == null)
                {
                    Logger.LogError("SupportInfo.Bots is null.");
                }
            }

            Logger.LogInfo($"Creating support completed: Type={support.BossEscortType}, Difficulty=normal, Amount={support.BossEscortAmount}");
        }

        private static BotDifficulty GetRandomDifficultyForBoss()
        {
            return GetRandomDifficulty(GetDifficultiesForSetting(DefaultPluginVars.botDifficultiesOther.Value.ToLower()));
        }

        private static BotDifficulty GetRandomDifficultyForSupport()
        {
            return GetRandomDifficulty(GetDifficultiesForSetting(DefaultPluginVars.botDifficultiesOther.Value.ToLower()));
        }

        private static IProfileData CreateProfileData(EPlayerSide side, WildSpawnType role, BotDifficulty difficulty)
        {
            return new IProfileData(side, role, difficulty, 0f, null);
        }

        private static PrepBotInfo CreateBotInfo(WildSpawnType role, BotDifficulty difficulty, EPlayerSide side, int groupSize = 1)
        {
            bool isGroup = groupSize > 1;
            return new PrepBotInfo(role, difficulty, side, isGroup, groupSize);
        }

        private static async UniTask<BotCreationDataClass> CreateAndAddBot(PrepBotInfo botInfo, IProfileData botData, Vector3 position, CancellationToken cancellationToken, bool isSupport)
        {
            var bot = await CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize, cancellationToken, isSupport, botData);
            botInfo.Bots = bot;
            botInfo.Bots.AddPosition(position, UnityEngine.Random.Range(0, 10000));
            BotInfos.Add(botInfo);
            return bot;
        }

        private static void AddBotSpawnInfo(WildSpawnType type, int amount, List<Vector3> positions, BotDifficulty difficulty, EPlayerSide side, string zone)
        {
            var botSpawnInfo = new BotSpawnInfo(type, amount, positions, difficulty, side, zone);
            botSpawnInfos.Add(botSpawnInfo);
        }

        private static List<Vector3> GetCentralPosition(List<Vector3> coordinates)
        {
            return new List<Vector3> { coordinates.Random() };
        }

        private static Vector3 GetOffsetPosition(Vector3 centralPosition)
        {
            float spreadRange = 5.0f; // Define a spread range for support units
            return centralPosition + new Vector3(
                UnityEngine.Random.Range(-spreadRange / 2, spreadRange / 2),
                0,
                UnityEngine.Random.Range(-spreadRange / 2, spreadRange / 2)
            );
        }

        internal static async UniTask ScheduleWaveBossSpawnDirectly(BossSpawn bossSpawn, List<Vector3> coordinates, CancellationToken cancellationToken, string selectedZone)
        {
            string methodName = nameof(ScheduleWaveBossSpawnDirectly);
            UnityEngine.Debug.Log($"{methodName}: Starting wave boss spawn for {bossSpawn.BossName}.");
            try
            {
                // Get random coordinate from coordinates
                if (coordinates == null || coordinates.Count == 0)
                {
                    UnityEngine.Debug.LogError($"{methodName}: No coordinates available for spawning.");
                    return;
                }

                var randomCoordinate = coordinates.Random();
                Vector3 centralPosition = randomCoordinate;

                UnityEngine.Debug.Log($"{methodName}: Selected random coordinate {randomCoordinate} for boss spawn.");


                // Create wave boss and get the central position for supports
                var waveBossCreationData = await CreateWaveBossProfile(bossSpawn, cancellationToken, selectedZone);

                if (waveBossCreationData == null)
                {
                    UnityEngine.Debug.LogError($"{methodName}: Failed to create wave boss profile for {bossSpawn.BossName}.");
                    return;
                }

                if (waveBossCreationData.Profiles.Count == 0)
                {
                    UnityEngine.Debug.LogError($"{methodName}: No profiles found for wave boss {bossSpawn.BossName}.");
                    return;
                }
                waveBossCreationData.AddPosition(centralPosition, UnityEngine.Random.Range(0, 10000));

                UnityEngine.Debug.Log($"{methodName}: Successfully created wave boss profile.");

                UnityEngine.Debug.Log($"{methodName}: Central position for spawning is {centralPosition}.");

                // Directly activate wave boss
                UnityEngine.Debug.Log($"{methodName}: Activating boss spawn: {bossSpawn.BossName} at position {centralPosition}.");

                var closestBotZone = botSpawnerClass?.GetClosestZone(waveBossCreationData.GetPosition().position, out _);
                var cts = new CancellationTokenSource();
                await BotSpawnHelper.ActivateBot(closestBotZone, waveBossCreationData, cts, cancellationToken);

                UnityEngine.Debug.Log($"{methodName}: Boss {bossSpawn.BossName} activated successfully.");

                // Schedule support units
                if (bossSpawn.Supports != null && bossSpawn.Supports.Any())
                {
                    UnityEngine.Debug.Log($"{methodName}: Scheduling support units for {bossSpawn.BossName}.");
                    await ScheduleWaveSupportsAsync(bossSpawn.Supports, centralPosition, coordinates, selectedZone, cancellationToken);
                }
                else
                {
                    UnityEngine.Debug.Log($"{methodName}: No support units to schedule for {bossSpawn.BossName}.");
                }

                UnityEngine.Debug.Log($"{methodName}: Completed wave boss spawn for {bossSpawn.BossName}.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{methodName}: Exception occurred: {ex.Message}\n{ex.StackTrace}");
            }
        }

        internal static async UniTask<BotCreationDataClass> CreateWaveBossProfile(BossSpawn bossSpawn, CancellationToken cancellationToken, string selectedZone)
        {
            if (botCreator == null)
            {
                Logger.LogError("Bot creator is not initialized.");
                return null;
            }

            var bossWildSpawnType = WildSpawnTypeDictionaries.StringToWildSpawnType[bossSpawn.BossName.ToLower()];
            var bossSide = WildSpawnTypeDictionaries.WildSpawnTypeToEPlayerSide[bossWildSpawnType];
            var bossDifficulty = GetRandomDifficultyForBoss();

            var bossData = CreateProfileData(bossSide, bossWildSpawnType, bossDifficulty);

            // Mark this bot as a boss for wave
            bossData.SpawnParams = new BotSpawnParams();
            bossData.SpawnParams.ShallBeGroup = new ShallBeGroupParams(true, true);


            Logger.LogInfo($"Creating wave boss profile: Name={bossSpawn.BossName}, Difficulty={bossDifficulty}, Side={bossSide}");

            var newBotCreationDataClass = await BotCreationDataClass.Create(bossData, DonutsBotPrep.botCreator, 1, DonutsBotPrep.botSpawnerClass);

            return newBotCreationDataClass;
        }

        private static async UniTask ScheduleWaveSupportsAsync(List<Support> supports, Vector3 centralPosition, List<Vector3> coordinates, string selectedZone, CancellationToken cancellationToken)
        {
            foreach (var support in supports)
            {
                await CreateWaveSupportAsync(support, centralPosition, coordinates, selectedZone, cancellationToken);
            }
        }

        private static async UniTask CreateWaveSupportAsync(Support support, Vector3 centralPosition, List<Vector3> coordinates, string selectedZone, CancellationToken cancellationToken)
        {
            Logger.LogInfo($"Creating wave support for: Type={support.BossEscortType}, Amount={support.BossEscortAmount}");

            var supportWildSpawnType = WildSpawnTypeDictionaries.StringToWildSpawnType[support.BossEscortType.ToLower()];
            var supportSide = WildSpawnTypeDictionaries.WildSpawnTypeToEPlayerSide[supportWildSpawnType];
            var supportDifficulty = GetRandomDifficultyForSupport();

            for (int i = 0; i < support.BossEscortAmount; i++)
            {
                Logger.LogInfo($"Creating wave support bot {i + 1}/{support.BossEscortAmount}");

                // Create individual profile and info for each bot
                var supportData = CreateProfileData(supportSide, supportWildSpawnType, supportDifficulty);

                var offsetPosition = GetOffsetPosition(centralPosition);

                supportData.SpawnParams = new BotSpawnParams();
                supportData.SpawnParams.ShallBeGroup = new ShallBeGroupParams(true, true, support.BossEscortAmount);

                var waveSupportCreationDataClass = await BotCreationDataClass.Create(supportData, DonutsBotPrep.botCreator, 1, DonutsBotPrep.botSpawnerClass);

                waveSupportCreationDataClass.AddPosition(offsetPosition, UnityEngine.Random.Range(0, 10000));

                var closestBotZone = botSpawnerClass?.GetClosestZone(waveSupportCreationDataClass.GetPosition().position, out _);
                var cts = new CancellationTokenSource();
                await BotSpawnHelper.ActivateBot(closestBotZone, waveSupportCreationDataClass, cts, cancellationToken);
            }

            Logger.LogInfo($"Creating wave support completed: Type={support.BossEscortType}, Difficulty=normal, Amount={support.BossEscortAmount}");
        }
        private void Update()
        {
            timeSinceLastReplenish += Time.deltaTime;
            if (timeSinceLastReplenish >= DefaultPluginVars.replenishInterval.Value && !isReplenishing)
            {
                timeSinceLastReplenish = 0f;
                ReplenishAllBots(this.GetCancellationTokenOnDestroy()).Forget();
            }
        }

        private async UniTask ReplenishAllBots(CancellationToken cancellationToken)
        {
            isReplenishing = true;

            var tasks = new List<UniTask>();
            var botsNeedingReplenishment = BotInfos.Where(NeedReplenishment).ToList();

            int singleBotsCount = 0;
            int groupBotsCount = 0;

            foreach (var botInfo in botsNeedingReplenishment)
            {
                if (botInfo.IsGroup && groupBotsCount < 1)
                {
#if DEBUG
                    Logger.LogWarning($"Replenishing group bot: {botInfo.SpawnType} {botInfo.Difficulty} {botInfo.Side} Count: {botInfo.GroupSize}");
#endif
                    // Control concurrency with SemaphoreSlim
                    await semaphore.WaitAsync(cancellationToken); // Acquire a slot
                    tasks.Add(CreateBot(botInfo, true, botInfo.GroupSize, cancellationToken)
                        .ContinueWith(t => semaphore.Release())); // Release slot when task completes

                    groupBotsCount++;
                }
                else if (!botInfo.IsGroup && singleBotsCount < 3)
                {
#if DEBUG
                    Logger.LogWarning($"Replenishing single bot: {botInfo.SpawnType} {botInfo.Difficulty} {botInfo.Side} Count: 1");
#endif
                    // Control concurrency with SemaphoreSlim
                    await semaphore.WaitAsync(cancellationToken); // Acquire a slot
                    tasks.Add(CreateBot(botInfo, false, 1, cancellationToken)
                        .ContinueWith(t => semaphore.Release())); // Release slot when task completes

                    singleBotsCount++;
                }

                if (singleBotsCount >= 3 && groupBotsCount >= 1)
                    break;
            }

            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }

            isReplenishing = false;
        }

        private static bool NeedReplenishment(PrepBotInfo botInfo)
        {
            return botInfo.Bots == null || botInfo.Bots.Profiles.Count == 0 || botInfo.Bots.Profiles == null;
        }

        internal static BotCreationDataClass FindCachedBots(WildSpawnType spawnType, BotDifficulty difficulty, int targetCount)
        {
            if (DonutsBotPrep.BotInfos == null)
            {
                Logger.LogError("FindCachedBots: BotInfos is null.");
                return null;
            }

            try
            {
                // Find the bot info that matches the spawn type, difficulty, and has the required profile count
                var botInfo = DonutsBotPrep.BotInfos.FirstOrDefault(b =>
                    b.SpawnType == spawnType &&
                    b.Difficulty == difficulty &&
                    b.Bots != null &&
                    b.Bots.Profiles != null &&
                    b.Bots.Profiles.Count == targetCount);

                if (botInfo != null)
                {
                    // Ensure that the profiles are not empty
                    if (botInfo.Bots.Profiles.Any())
                    {
                        return botInfo.Bots;
                    }
                    else
                    {
                        Logger.LogWarning($"Cached bot found but profiles are empty for spawn type {spawnType}, difficulty {difficulty}.");
                    }
                }
                else
                {
                    Logger.LogWarning($"No cached bots found for spawn type {spawnType}, difficulty {difficulty}, and target count {targetCount}.");
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception in FindCachedBots: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        internal static List<BotCreationDataClass> GetWildSpawnData(WildSpawnType spawnType, BotDifficulty botDifficulty)
        {
            return BotInfos
                .Where(b => b.SpawnType == spawnType && b.Difficulty == botDifficulty)
                .Select(b => b.Bots)
                .ToList();
        }

        internal static WildSpawnType? GetOriginalSpawnTypeForBot(BotOwner bot)
        {
            var originalProfile = OriginalBotSpawnTypes.First(profile => profile.Key == bot.Profile.Id);

            if (originalProfile.Key != null)
            {
#if DEBUG
                Logger.LogWarning($"Found original profile for bot {bot.Profile.Nickname} as {originalProfile.Value.ToString()}");
#endif
                return originalProfile.Value;
            }
            else
            {
#if DEBUG
                Logger.LogWarning($"Could not find original profile for bot {bot.Profile.Nickname}");
#endif
                return null;
            }
        }

        internal static WildSpawnType DeterminePMCFactionBasedOnRatio()
        {
            // int value between 0 and 100
            int pmcFactionRatio = DefaultPluginVars.pmcFactionRatio.Value;

            // Generate a random integer between 0 and 100
            int randomValue = UnityEngine.Random.Range(0, 101);

            // Determine the faction

            if (randomValue < pmcFactionRatio)
            {
                return WildSpawnType.pmcUSEC;
            }
            else
            {
                return WildSpawnType.pmcBEAR;
            }
        }
        public static BotDifficulty GetRandomDifficulty(List<BotDifficulty> difficulties)
        {
            if (difficulties == null || difficulties.Count == 0)
            {
                throw new ArgumentException("Difficulties list cannot be null or empty.");
            }

            System.Random random = new System.Random();
            int randomIndex = random.Next(difficulties.Count);
            return difficulties[randomIndex];
        }

        private void OnDestroy()
        {
            DisposeHandlersAndResetStatics();
            Logger.LogWarning("DonutsBotPrep component cleaned up and disabled.");
        }

        private void DisposeHandlersAndResetStatics()
        {
            // Cancel any ongoing tasks
            ctsprep?.Cancel();
            ctsprep?.Dispose();
            ctsprep = null; // Set to null to ensure proper garbage collection

            // Remove event handlers
            if (botSpawnerClass != null)
            {
                botSpawnerClass.OnBotRemoved -= BotSpawnerClass_OnBotRemoved;
                botSpawnerClass.OnBotCreated -= BotSpawnerClass_OnBotCreated;
            }

            if (mainplayer != null)
            {
                mainplayer.BeingHitAction -= Mainplayer_BeingHitAction;
            }

            // Stop all coroutines
            StopAllCoroutines();

            // Reset static variables
            timeSinceLastReplenish = 0f;
            isReplenishing = false;
            IsBotPreparationComplete = false;
            selectionName = null;
            maplocation = null;
            mapName = null;

            // Reset static collections
            OriginalBotSpawnTypes?.Clear();
            OriginalBotSpawnTypes = null;

            // Create new instances of ConcurrentBag to clear them
            botSpawnInfos = new ConcurrentBag<BotSpawnInfo>();
            BotInfos = new ConcurrentBag<PrepBotInfo>();

            // Clear other collections
            usedZonesPMC?.Clear();
            usedZonesSCAV?.Clear();
            usedZonesBoss?.Clear();

            // Release resources
            allMapsZoneConfig = null;
            gameWorld = null;
            botCreator = null;
            botSpawnerClass = null;
            mainplayer = null;
        }
    }
}
