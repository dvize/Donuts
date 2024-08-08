using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using EFT.InventoryLogic;
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

        internal static List<BotSpawnInfo> botSpawnInfos
        {
            get; set;
        }

        private HashSet<string> usedZonesPMC = new HashSet<string>();
        private HashSet<string> usedZonesSCAV = new HashSet<string>();
        private HashSet<string> usedZonesBoss = new HashSet<string>();

        public static List<PrepBotInfo> BotInfos
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
            BotInfos = new List<PrepBotInfo>();
            botSpawnInfos = new List<BotSpawnInfo>();
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
                //InitializeBotInfos(startingBotConfig, maplocation, "PMC", ctsprep.Token),
                //InitializeBotInfos(startingBotConfig, maplocation, "SCAV", ctsprep.Token),
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
                    createBotTasks.Add(CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize, cancellationToken));

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


                        // Create Boss and Support Bots
                        bossSpawnTasks.Add(ScheduleBossSpawn(bossSpawn, coordinates, cancellationToken, selectedZone));
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
                    return BotSpawnHelper.DeterminePMCFactionBasedOnRatio();
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

            // Tag each bot in the group as a support if applicable
            if (isSupport)
            {
                foreach (var profile in bot.Profiles)
                {
                    BotSupportTracker.AddBot(profile.Id, BotSourceType.Support);
                }
            }

            botInfo.Bots = bot;
            Logger.LogInfo($"CreateBot: Bot created and assigned successfully: {bot.Profiles.Count} profiles loaded.");

            return bot;
        }

        private async UniTask ScheduleBossSpawn(BossSpawn bossSpawn, List<Vector3> coordinates, CancellationToken cancellationToken, string selectedZone)
        {
            Logger.LogInfo($"Scheduling boss spawn: {bossSpawn.BossName}");

            // Get the closest bot zone to the first coordinate
            var closestBotZone = botSpawnerClass.GetClosestZone(coordinates.FirstOrDefault(), out float dist);

            // Create boss and get the central position for supports
            var bossCreationData = await CreateBoss(bossSpawn, coordinates, cancellationToken, selectedZone);

            if (bossCreationData != null)
            {
                var centralPosition = bossCreationData.GetPosition();

                // Schedule support units
                if (bossSpawn.Supports != null && bossSpawn.Supports.Any())
                {
                    await ScheduleSupportsAsync(bossSpawn.Supports, centralPosition.position, coordinates, selectedZone, cancellationToken);
                }

                // Activate the boss - Disable as it needs to be handled after game starts
                //botCreator.ActivateBot(bossCreationData, closestBotZone, true, BossGroupAction, null, cancellationToken);
            }
        }

        private BotsGroup BossGroupAction(BotOwner botOwner, BotZone botZone)
        {
            // Implement group action logic for when the boss is spawned
            return null;
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

            // Randomize and select boss difficulty
            var bossDifficultyList = GetDifficultiesForSetting(DefaultPluginVars.botDifficultiesOther.Value.ToLower())
                .OrderBy(x => UnityEngine.Random.value)
                .ToList();
            var bossDifficulty = bossDifficultyList.FirstOrDefault();

            var bossData = new IProfileData(
                side: bossSide,
                role: bossWildSpawnType,
                botDifficulty: bossDifficulty,
                0f,
                null
            );

            // Generate the bot info needed for spawning
            var botInfo = new PrepBotInfo(bossWildSpawnType, bossDifficulty, bossSide);
            var boss = await CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize, cancellationToken, false, bossData);

            // Assign a random position for the boss spawn
            var centralPosition = new List<Vector3> { coordinates.Random() };

            Logger.LogInfo($"Central Position Count In Boss Creation: {centralPosition.Count()}");
            //think there were 12 given and i passed the whole list instead of the one element
            botInfo.Bots.AddPosition(centralPosition.First(), UnityEngine.Random.Range(0, 10000));
            BotInfos.Add(botInfo);

            //handle for 
            var botSpawnInfo = new BotSpawnInfo(bossWildSpawnType, 1, centralPosition, bossDifficulty, bossSide, selectedZone);
            botSpawnInfos.Add(botSpawnInfo);

#if DEBUG
            Logger.LogInfo($"Creating boss: Name={bossSpawn.BossName}, Difficulty={bossDifficulty}, Side={bossSide}");
#endif

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
            if (botCreator == null)
            {
                Logger.LogError("Bot creator is not initialized.");
                return;
            }

            var supportWildSpawnType = WildSpawnTypeDictionaries.StringToWildSpawnType[support.BossEscortType.ToLower()];
            var supportSide = WildSpawnTypeDictionaries.WildSpawnTypeToEPlayerSide[supportWildSpawnType];

            var supportData = new IProfileData(
                side: supportSide,
                role: supportWildSpawnType,
                botDifficulty: BotDifficulty.normal,
                0f,
                null
            );

            // Generate the bot info needed for spawning
            var groupsize = support.BossEscortAmount;
            bool isgroup = groupsize > 1;

            var supportInfo = new PrepBotInfo(supportWildSpawnType, BotDifficulty.normal, supportSide, isgroup, groupsize);

            //create bot with isSupport parameter set to true
            await CreateBot(supportInfo, supportInfo.IsGroup, supportInfo.GroupSize, cancellationToken, true, supportData);
          
            // Assign positions around the central position for support units
            float spreadRange = 5.0f; // Define a spread range for support units

            var offsetPosition = centralPosition + new Vector3(
                UnityEngine.Random.Range(-spreadRange / 2, spreadRange / 2),
                0,
                UnityEngine.Random.Range(-spreadRange / 2, spreadRange / 2)
            );

            //edit the position of supportInfo.Bots[i].AddPosition
            supportInfo.Bots.AddPosition(offsetPosition, UnityEngine.Random.Range(0, 10000));
            var offsetPositionList = new List<Vector3> { offsetPosition };
            BotInfos.Add(supportInfo);
            var supportSpawnInfo = new BotSpawnInfo(supportWildSpawnType, support.BossEscortAmount, offsetPositionList, BotDifficulty.normal, supportSide, selectedZone);
            botSpawnInfos.Add(supportSpawnInfo);

#if DEBUG
            Logger.LogInfo($"Creating support: Type={support.BossEscortType}, Difficulty=normal, Amount={support.BossEscortAmount}");
#endif
            return ;
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
                    tasks.Add(CreateBot(botInfo, true, botInfo.GroupSize, cancellationToken));
                    groupBotsCount++;
                }
                else if (!botInfo.IsGroup && singleBotsCount < 3)
                {
#if DEBUG
                    Logger.LogWarning($"Replenishing single bot: {botInfo.SpawnType} {botInfo.Difficulty} {botInfo.Side} Count: 1");
#endif
                    tasks.Add(CreateBot(botInfo, false, 1, cancellationToken));
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
            return botInfo.Bots == null || botInfo.Bots.Profiles.Count == 0;
        }

        internal static BotCreationDataClass FindCachedBots(WildSpawnType spawnType, BotDifficulty difficulty, int targetCount)
        {
            if (DonutsBotPrep.BotInfos == null)
            {
                Logger.LogError("BotInfos is null");
                return null;
            }

            try
            {
                // Find the bot info that matches the spawn type and difficulty
                var botInfo = DonutsBotPrep.BotInfos.FirstOrDefault(b => b.SpawnType == spawnType && b.Difficulty == difficulty && b.Bots != null && b.Bots.Profiles.Count == targetCount);

                if (botInfo != null)
                {
                    return botInfo.Bots;
                }

                Logger.LogWarning($"No cached bots found for spawn type {spawnType}, difficulty {difficulty}, and target count {targetCount}");
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
            OriginalBotSpawnTypes = null;
            botSpawnInfos = null;
            BotInfos = null;
            allMapsZoneConfig = null;

            // Clear collections
            usedZonesPMC.Clear();
            usedZonesSCAV.Clear();

            // Release resources
            gameWorld = null;
            botCreator = null;
            botSpawnerClass = null;
            mainplayer = null;
        }
    }

    public enum BotSourceType
    {
        None,
        Support
    }

    public static class BotSupportTracker
    {
        internal static Dictionary<string, BotSourceType> botSourceTypeMap = new Dictionary<string, BotSourceType>();

        public static void AddBot(string botId, BotSourceType sourceType)
        {
            if (!botSourceTypeMap.ContainsKey(botId))
            {
                botSourceTypeMap[botId] = sourceType;
            }
        }

        //remove bot method
        public static void RemoveBot(string botId)
        {
            if (botSourceTypeMap.ContainsKey(botId))
            {
                botSourceTypeMap.Remove(botId);
            }
        }

        public static BotSourceType GetBotSourceType(string botId)
        {
            return botSourceTypeMap.TryGetValue(botId, out var sourceType) ? sourceType : BotSourceType.None;
        }
    }
}
