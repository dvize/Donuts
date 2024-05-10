using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Aki.PrePatch;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using BotCacheClass = GClass591;
using IProfileData = GClass592;

#pragma warning disable IDE0007, CS4014

namespace Donuts
{
    internal class DonutsBotPrep : MonoBehaviour
    {
        internal static string selectionName;
        internal static string maplocation;
        private static GameWorld gameWorld;
        private static IBotCreator botCreator;
        private static BotSpawner botSpawnerClass;
        private static Player mainplayer;

        internal static Dictionary<string, WildSpawnType> OriginalBotSpawnTypes;

        internal static List<BotSpawnInfo> botSpawnInfos
        {
            get; set;
        }

        private static WildSpawnType sptUsec;
        private static WildSpawnType sptBear;

        private HashSet<string> usedZonesPMC = new HashSet<string>();
        private HashSet<string> usedZonesSCAV = new HashSet<string>();

        public static List<PrepBotInfo> BotInfos
        {
            get; set;
        }

        public static AllMapsZoneConfig allMapsZoneConfig;

        internal static float timeSinceLastReplenish = 0f;

        private bool isReplenishing = false;
        public static bool IsBotPreparationComplete { get; private set; } = false;

        private readonly Dictionary<WildSpawnType, EPlayerSide> spawnTypeToSideMapping = new Dictionary<WildSpawnType, EPlayerSide>
        {
            { WildSpawnType.arenaFighterEvent, EPlayerSide.Savage },
            { WildSpawnType.assault, EPlayerSide.Savage },
            { WildSpawnType.assaultGroup, EPlayerSide.Savage },
            { WildSpawnType.bossBoar, EPlayerSide.Savage },
            { WildSpawnType.bossBoarSniper, EPlayerSide.Savage },
            { WildSpawnType.bossBully, EPlayerSide.Savage },
            { WildSpawnType.bossGluhar, EPlayerSide.Savage },
            { WildSpawnType.bossKilla, EPlayerSide.Savage },
            { WildSpawnType.bossKojaniy, EPlayerSide.Savage },
            { WildSpawnType.bossSanitar, EPlayerSide.Savage },
            { WildSpawnType.bossTagilla, EPlayerSide.Savage },
            { WildSpawnType.bossZryachiy, EPlayerSide.Savage },
            { WildSpawnType.crazyAssaultEvent, EPlayerSide.Savage },
            { WildSpawnType.cursedAssault, EPlayerSide.Savage },
            { WildSpawnType.exUsec, EPlayerSide.Savage },
            { WildSpawnType.followerBoar, EPlayerSide.Savage },
            { WildSpawnType.followerBully, EPlayerSide.Savage },
            { WildSpawnType.followerGluharAssault, EPlayerSide.Savage },
            { WildSpawnType.followerGluharScout, EPlayerSide.Savage },
            { WildSpawnType.followerGluharSecurity, EPlayerSide.Savage },
            { WildSpawnType.followerGluharSnipe, EPlayerSide.Savage },
            { WildSpawnType.followerKojaniy, EPlayerSide.Savage },
            { WildSpawnType.followerSanitar, EPlayerSide.Savage },
            { WildSpawnType.followerTagilla, EPlayerSide.Savage },
            { WildSpawnType.followerZryachiy, EPlayerSide.Savage },
            { WildSpawnType.marksman, EPlayerSide.Savage },
            { WildSpawnType.pmcBot, EPlayerSide.Savage },
            { WildSpawnType.sectantPriest, EPlayerSide.Savage },
            { WildSpawnType.sectantWarrior, EPlayerSide.Savage },
            { WildSpawnType.followerBigPipe, EPlayerSide.Savage },
            { WildSpawnType.followerBirdEye, EPlayerSide.Savage },
            { WildSpawnType.bossKnight, EPlayerSide.Savage },
        };

        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public DonutsBotPrep()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(DonutsBotPrep));
        }

        public static void Enable()
        {
            gameWorld = Singleton<GameWorld>.Instance;
            gameWorld.GetOrAddComponent<DonutsBotPrep>();

            Logger.LogDebug("DonutBotPrep Enabled");
        }

        public async void Awake()
        {
            maplocation = gameWorld.MainPlayer.Location.ToLower();
            botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            botCreator = AccessTools.Field(typeof(BotSpawner), "_botCreator").GetValue(botSpawnerClass) as IBotCreator;
            mainplayer = gameWorld?.MainPlayer;
            OriginalBotSpawnTypes = new Dictionary<string, WildSpawnType>();
            BotInfos = new List<PrepBotInfo>();
            botSpawnInfos = new List<BotSpawnInfo>();
            timeSinceLastReplenish = 0;
            IsBotPreparationComplete = false;

            sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;

            botSpawnerClass.OnBotRemoved += (BotOwner bot) =>
            {
                bot.Memory.OnGoalEnemyChanged -= Memory_OnGoalEnemyChanged;
                OriginalBotSpawnTypes.Remove(bot.Profile.Id);
            };

            botSpawnerClass.OnBotCreated += (BotOwner bot) =>
            {
                bot.Memory.OnGoalEnemyChanged += Memory_OnGoalEnemyChanged;
            };

            if (mainplayer != null)
            {
                Logger.LogDebug("Mainplayer is not null, attaching event handlers");
                mainplayer.BeingHitAction += Mainplayer_BeingHitAction;
            }

            // Get selected preset and setup bot limits now
            selectionName = DonutsPlugin.RunWeightedScenarioSelection();
            Initialization.SetupBotLimit(selectionName);

            var startingBotConfig = DonutComponent.GetStartingBotConfig(selectionName);
            if (startingBotConfig != null)
            {
                Logger.LogDebug("startingBotConfig is not null: " + JsonConvert.SerializeObject(startingBotConfig));

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

                await InitializeAllBotInfos(startingBotConfig, maplocation);
            }
            else
            {
                Logger.LogError("startingBotConfig is null for selectionName: " + selectionName);
            }

            IsBotPreparationComplete = true;
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

        private async UniTask InitializeAllBotInfos(StartingBotConfig startingBotConfig, string maplocation)
        {
            await UniTask.WhenAll(InitializeBotInfos(startingBotConfig, maplocation), InitializeScavBotInfos(startingBotConfig, maplocation));
        }

        private async UniTask InitializeBotInfos(StartingBotConfig startingBotConfig, string maplocation)
        {
            WildSpawnType sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            WildSpawnType sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;

            if (DefaultPluginVars.forceAllBotType.Value == "SCAV")
            {
                await InitializeBotType(startingBotConfig, maplocation, WildSpawnType.assault, EPlayerSide.Savage, DefaultPluginVars.botDifficultiesSCAV.Value.ToLower(), "SCAV");
            }
            else
            {
                WildSpawnType wildSpawnType = GetPMCWildSpawnType(sptUsec, sptBear);
                EPlayerSide side = GetPMCSide(wildSpawnType, sptUsec, sptBear);

                await InitializeBotType(startingBotConfig, maplocation, wildSpawnType, side, DefaultPluginVars.botDifficultiesPMC.Value.ToLower(), "PMC");
            }
        }

        private async UniTask InitializeScavBotInfos(StartingBotConfig startingBotConfig, string maplocation)
        {
            WildSpawnType wildSpawnType;
            EPlayerSide side;
            string difficultySetting;

            if (DefaultPluginVars.forceAllBotType.Value == "PMC")
            {
                WildSpawnType sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
                WildSpawnType sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;

                wildSpawnType = GetPMCWildSpawnType(sptUsec, sptBear);
                side = GetPMCSide(wildSpawnType, sptUsec, sptBear);
                difficultySetting = DefaultPluginVars.botDifficultiesPMC.Value.ToLower();
            }
            else
            {
                wildSpawnType = WildSpawnType.assault;
                side = EPlayerSide.Savage;
                difficultySetting = DefaultPluginVars.botDifficultiesSCAV.Value.ToLower();
            }

            await InitializeBotType(startingBotConfig, maplocation, wildSpawnType, side, difficultySetting, "SCAV");
        }

        private async UniTask InitializeBotType(StartingBotConfig startingBotConfig, string maplocation, WildSpawnType wildSpawnType, EPlayerSide side, string difficultySetting, string botType)
        {
            var mapBotConfig = botType == "PMC" ? startingBotConfig.Maps[maplocation].PMC : startingBotConfig.Maps[maplocation].SCAV;
            var difficultiesForSetting = GetDifficultiesForSetting(difficultySetting);
            int maxBots = UnityEngine.Random.Range(mapBotConfig.MinCount, mapBotConfig.MaxCount + 1);

            if (botType == "PMC" && maxBots > Initialization.PMCBotLimit)
            {
                maxBots = Initialization.PMCBotLimit;
            }
            else if (botType == "SCAV" && maxBots > Initialization.SCAVBotLimit)
            {
                maxBots = Initialization.SCAVBotLimit;
            }

            int groupSize = BotSpawn.DetermineMaxBotCount(botType.ToLower(), mapBotConfig.MinGroupSize, mapBotConfig.MaxGroupSize);

            // Get random spawn points, depending on StartingBots cfg, for all starting bots
            var spawnPointsDict = DonutComponent.GetSpawnPointsForZones(allMapsZoneConfig, maplocation, mapBotConfig.Zones);

            int totalBots = 0;
            var usedZones = botType == "PMC" ? usedZonesPMC : usedZonesSCAV;

            while (totalBots < maxBots)
            {
                if ((totalBots + groupSize) > maxBots)
                {
                    groupSize = maxBots - totalBots;
                }

                var difficulty = difficultiesForSetting[UnityEngine.Random.Range(0, difficultiesForSetting.Count)];
                var coordinates = new List<Vector3>();
                string selectedZone = null;

                // Check if all zones are exhausted
                if (usedZones.Count >= spawnPointsDict.Count)
                {
                    // If all zones are exhausted, select a random zone
                    var randomZone = spawnPointsDict.Keys.ElementAt(UnityEngine.Random.Range(0, spawnPointsDict.Count));
                    coordinates.Add(spawnPointsDict[randomZone]);
                    selectedZone = randomZone;
                }
                else
                {
                    foreach (var zone in spawnPointsDict.Keys.ToList())
                    {
                        if (!usedZones.Contains(zone) && spawnPointsDict.TryGetValue(zone, out var coord))
                        {
                            coordinates.Add(coord);
                            selectedZone = zone;
                            usedZones.Add(zone); // Mark the zone as used
                            break;
                        }
                    }
                }

                if (!coordinates.Any())
                {
                    Logger.LogError("No spawn points available for bot spawn.");
                    break;
                }

                // Add data to bot cache, this is required
                var botInfo = new PrepBotInfo(wildSpawnType, difficulty, side, groupSize > 1, groupSize);
                await CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize);
                BotInfos.Add(botInfo);

                // Starting Bots data for actually spawning them into raids
                var botSpawnInfo = new BotSpawnInfo(wildSpawnType, groupSize, coordinates, difficulty, side, selectedZone);
                botSpawnInfos.Add(botSpawnInfo);

                totalBots += groupSize;
            }
        }

        private WildSpawnType GetPMCWildSpawnType(WildSpawnType sptUsec, WildSpawnType sptBear)
        {
            if (DefaultPluginVars.pmcFaction.Value == "Default")
            {
                return BotSpawn.DeterminePMCFactionBasedOnRatio(sptUsec, sptBear);
            }
            else if (DefaultPluginVars.pmcFaction.Value == "USEC")
            {
                return sptUsec;
            }
            else if (DefaultPluginVars.pmcFaction.Value == "BEAR")
            {
                return sptBear;
            }
            return BotSpawn.DeterminePMCFactionBasedOnRatio(sptUsec, sptBear); // Default fallback
        }

        private EPlayerSide GetPMCSide(WildSpawnType wildSpawnType, WildSpawnType sptUsec, WildSpawnType sptBear)
        {
            if (wildSpawnType == sptUsec)
            {
                return EPlayerSide.Usec;
            }
            else if (wildSpawnType == sptBear)
            {
                return EPlayerSide.Bear;
            }
            return EPlayerSide.Usec; // Default fallback
        }

        private List<BotDifficulty> GetDifficultiesForSetting(string difficultySetting)
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
                    Logger.LogError("Unsupported difficulty setting: " + difficultySetting);
                    return new List<BotDifficulty>();
            }
        }

        private int[] DetermineGroupSizes(string groupChance, string botType)
        {
            switch (botType.ToLower())
            {
                case "pmc":
                    return groupChance.ToLower() switch
                    {
                        "none" => Array.Empty<int>(),
                        "low" => new int[] { 1, 1, 2 },
                        "max" => new int[] { 5, 5 },
                        "high" => new int[] { 3, 4, 5 },
                        _ => new int[] { 1, 2, 3 },
                    };
                case "scav":
                    return groupChance.ToLower() switch
                    {
                        "none" => Array.Empty<int>(),
                        "low" => new int[] { 1, 2 },
                        "max" => new int[] { 3, 4 },
                        "high" => new int[] { 2, 3 },
                        _ => new int[] { 1, 1, 2 },
                    };
                default:
                    throw new ArgumentException("Invalid bot type provided.");
            }
        }

        private void Update()
        {
            timeSinceLastReplenish += Time.deltaTime;
            if (timeSinceLastReplenish >= DefaultPluginVars.replenishInterval.Value && !isReplenishing)
            {
                timeSinceLastReplenish = 0f;
                ReplenishAllBots().Forget();
            }
        }

        private async UniTask ReplenishAllBots()
        {
            isReplenishing = true;
            int singleBotsCount = 0;
            int groupBotsCount = 0;

            var safeBotInfos = new List<PrepBotInfo>(BotInfos);
            var tasks = new List<UniTask>();

            foreach (var botInfo in safeBotInfos)
            {
                if (NeedReplenishment(botInfo))
                {
                    if (botInfo.IsGroup && groupBotsCount < 1)
                    {
#if DEBUG
                        Logger.LogWarning($"Replenishing group bot: {botInfo.SpawnType} {botInfo.Difficulty} {botInfo.Side} Count: {botInfo.GroupSize}");
#endif
                        tasks.Add(CreateBot(botInfo, true, botInfo.GroupSize));
                        groupBotsCount++;
                    }
                    else if (!botInfo.IsGroup && singleBotsCount < 3)
                    {
#if DEBUG
                        Logger.LogWarning($"Replenishing single bot: {botInfo.SpawnType} {botInfo.Difficulty} {botInfo.Side} Count: 1");
#endif
                        tasks.Add(CreateBot(botInfo, false, 1));
                        singleBotsCount++;
                    }

                    if (singleBotsCount >= 3 && groupBotsCount >= 1)
                        break;
                }
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

        internal static async UniTask CreateBot(PrepBotInfo botInfo, bool isGroup, int groupSize)
        {
            var botData = new IProfileData(botInfo.Side, botInfo.SpawnType, botInfo.Difficulty, 0f, null);
#if DEBUG
            Logger.LogDebug($"Creating bot: Type={botInfo.SpawnType}, Difficulty={botInfo.Difficulty}, Side={botInfo.Side}, GroupSize={groupSize}");
#endif
            BotCacheClass bot = await BotCacheClass.Create(botData, botCreator, groupSize, botSpawnerClass);
            if (bot == null || bot.Profiles == null || !bot.Profiles.Any())
            {
#if DEBUG
                Logger.LogError($"Failed to create or properly initialize bot for {botInfo.SpawnType}");
#endif
                return;
            }

            botInfo.Bots = bot;
#if DEBUG
            Logger.LogDebug($"Bot created and assigned successfully: {bot.Profiles.Count} profiles loaded.");
#endif
        }

        public static BotCacheClass FindCachedBots(WildSpawnType spawnType, BotDifficulty difficulty, int targetCount)
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

        public static List<BotCacheClass> GetWildSpawnData(WildSpawnType spawnType, BotDifficulty botDifficulty)
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
                Logger.LogWarning("Found original profile for bot " + bot.Profile.Nickname + " as " + originalProfile.Value.ToString());
#endif
                return originalProfile.Value;
            }
            else
            {
#if DEBUG
                Logger.LogWarning("Could not find original profile for bot " + bot.Profile.Nickname);
#endif
                return null;
            }
        }

        private void OnDestroy()
        {
            if (botSpawnerClass != null)
            {
                botSpawnerClass.OnBotRemoved -= (BotOwner bot) =>
                {
                    bot.Memory.OnGoalEnemyChanged -= Memory_OnGoalEnemyChanged;
                    OriginalBotSpawnTypes.Remove(bot.Profile.Id);
                };

                botSpawnerClass.OnBotCreated -= (BotOwner bot) =>
                {
                    bot.Memory.OnGoalEnemyChanged -= Memory_OnGoalEnemyChanged;
                };
            }

            if (mainplayer != null)
            {
                mainplayer.BeingHitAction -= Mainplayer_BeingHitAction;
            }

            isReplenishing = false;
            timeSinceLastReplenish = 0;
            IsBotPreparationComplete = false;

            gameWorld = null;
            botCreator = null;
            botSpawnerClass = null;
            mainplayer = null;
            OriginalBotSpawnTypes = null;
            BotInfos = null;
            botSpawnInfos = null;

#if DEBUG
            Logger.LogWarning("DonutsBotPrep component cleaned up and disabled.");
#endif
        }
    }
}
