using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aki.PrePatch;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;

//custom usings
using BotCacheClass = GClass513;
using IProfileData = GClass514;

#pragma warning disable IDE0007, CS4014


namespace Donuts
{
    internal class DonutsBotPrep : MonoBehaviour
    {
        private static GameWorld gameWorld;
        private static IBotCreator botCreator;
        private static BotSpawner botSpawnerClass;

        private static Dictionary<WildSpawnType, Dictionary<BotDifficulty, List<BotCacheClass>>> botLists;

        //use dictionary of profile.id and wildspawntype
        internal static Dictionary<string, WildSpawnType> OriginalBotSpawnTypes;

        private static WildSpawnType sptUsec;
        private static WildSpawnType sptBear;

        private int maxBotCount;
        private float replenishInterval;
        private float timeSinceLastReplenish;
        private int botsReplenishedCount;
        private int maxBotsToReplenish;
        private int maxGroupBotsToReplenish;


        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public DonutsBotPrep()
        {
            if (Logger == null)
            {
                Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(DonutsBotPrep));
            }
        }

        public static void Enable()
        {
            gameWorld = Singleton<GameWorld>.Instance;
            gameWorld.GetOrAddComponent<DonutsBotPrep>();

            Logger.LogDebug("DonutBotPrep Enabled");
        }

        public async void Awake()
        {
            //init the main vars
            botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            botCreator = AccessTools.Field(typeof(BotSpawner), "_botCreator").GetValue(botSpawnerClass) as IBotCreator;
            sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;
            replenishInterval = 60.0f;
            timeSinceLastReplenish = 0f;
            botsReplenishedCount = 0;
            maxBotsToReplenish = 2;
            maxGroupBotsToReplenish = 1;

            botLists = new Dictionary<WildSpawnType, Dictionary<BotDifficulty, List<BotCacheClass>>>();
            OriginalBotSpawnTypes = new Dictionary<string, WildSpawnType>();

            InitializeBotLists();
        }

        // maybe we can check here as well? for preset? so that we don't have to add so many?
        private void InitializeBotLists()
        {

            botLists.Add(WildSpawnType.assault, new Dictionary<BotDifficulty, List<BotCacheClass>>());
            botLists.Add(sptUsec, new Dictionary<BotDifficulty, List<BotCacheClass>>());
            botLists.Add(sptBear, new Dictionary<BotDifficulty, List<BotCacheClass>>());

            //create dictionary entries based on donuts difficulty settings
            switch (DonutsPlugin.botDifficultiesPMC.Value.ToLower())
            {
                case "asonline":
                    botLists[sptUsec].Add(BotDifficulty.easy, new List<BotCacheClass>());
                    botLists[sptUsec].Add(BotDifficulty.normal, new List<BotCacheClass>());
                    botLists[sptUsec].Add(BotDifficulty.hard, new List<BotCacheClass>());

                    botLists[sptBear].Add(BotDifficulty.easy, new List<BotCacheClass>());
                    botLists[sptBear].Add(BotDifficulty.normal, new List<BotCacheClass>());
                    botLists[sptBear].Add(BotDifficulty.hard, new List<BotCacheClass>());
                    break;
                case "easy":
                    botLists[sptUsec].Add(BotDifficulty.easy, new List<BotCacheClass>());
                    botLists[sptBear].Add(BotDifficulty.easy, new List<BotCacheClass>());
                    break;
                case "normal":
                    botLists[sptUsec].Add(BotDifficulty.normal, new List<BotCacheClass>());
                    botLists[sptBear].Add(BotDifficulty.normal, new List<BotCacheClass>());
                    break;
                case "hard":
                    botLists[sptUsec].Add(BotDifficulty.hard, new List<BotCacheClass>());
                    botLists[sptBear].Add(BotDifficulty.hard, new List<BotCacheClass>());
                    break;
                case "impossible":
                    botLists[sptUsec].Add(BotDifficulty.impossible, new List<BotCacheClass>());
                    botLists[sptBear].Add(BotDifficulty.impossible, new List<BotCacheClass>());
                    break;
                default:
                    Logger.LogWarning("Could not find a valid difficulty for PMC bots. Please check method.");
                    break;
            }

            switch (DonutsPlugin.botDifficultiesSCAV.Value.ToLower())
            {
                case "asonline":
                    botLists[WildSpawnType.assault].Add(BotDifficulty.easy, new List<BotCacheClass>());
                    botLists[WildSpawnType.assault].Add(BotDifficulty.normal, new List<BotCacheClass>());
                    botLists[WildSpawnType.assault].Add(BotDifficulty.hard, new List<BotCacheClass>());
                    break;
                case "easy":
                    botLists[WildSpawnType.assault].Add(BotDifficulty.easy, new List<BotCacheClass>());
                    break;
                case "normal":
                    botLists[WildSpawnType.assault].Add(BotDifficulty.normal, new List<BotCacheClass>());
                    break;
                case "hard":
                    botLists[WildSpawnType.assault].Add(BotDifficulty.hard, new List<BotCacheClass>());
                    break;
                case "impossible":
                    botLists[WildSpawnType.assault].Add(BotDifficulty.impossible, new List<BotCacheClass>());
                    break;
                default:
                    Logger.LogWarning("Could not find a valid difficulty for SCAV bots. Please check method.");
                    break;
            }

        }

        private async void Start()
        {
            botSpawnerClass.OnBotRemoved += (BotOwner bot) =>
            {
                //remove bot from originalbotspawntypes dictionary
                OriginalBotSpawnTypes.Remove(bot.Profile.Id);
            };

            // Initialize the bot pool at the beginning of the round
            await InitializeBotPool();
        }

        // maybe we can check the difficulty here? also preset? this happens pre-raid...
        private async Task InitializeBotPool()
        {
            Logger.LogWarning("Profile Generation is Creating for Donuts Difficulties");

            // testing, this is raid load here i think
            // we can probably decide what we need here before each raid
            selectedPreset = DonutsPlugin.scenarioSelection.Value.ToLower();
            pmcDifficulty = DonutsPlugin.botDifficultiesPMC.Value.ToLower();
            scavDifficulty = DonutsPlugin.botDifficultiesSCAV.Value.ToLower();
            pmcGroupChance = DonutsPlugin.pmcGroupChance.Value.ToLower();

            // Create bots for PMC difficulties
            foreach (var entry in botLists[sptBear])
            {
                if (pmcGroupChance == "none")
                {
                    CreateBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, maxBotsToReplenish);
                    continue;
                }
                else if (pmcGroupChance == "max")
                {
                    maxGroupBotsToReplenish = 3;
                    CreateGroupBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, new ShallBeGroupParams(true, true, 5), 5, maxGroupBotsToReplenish);
                    continue;
                }
                else
                {
                    CreateBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, maxBotsToReplenish);
                    CreateGroupBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, new ShallBeGroupParams(true, true, 2), 2, maxGroupBotsToReplenish);
                    CreateGroupBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, new ShallBeGroupParams(true, true, 3), 3, maxGroupBotsToReplenish);
                    CreateGroupBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, new ShallBeGroupParams(true, true, 4), 4, maxGroupBotsToReplenish);
                    CreateGroupBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, new ShallBeGroupParams(true, true, 5), 5, maxGroupBotsToReplenish);
                }
            }

            foreach (var entry in botLists[sptUsec])
            {
                if (pmcGroupChance == "none")
                {
                    CreateBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, maxBotsToReplenish);
                    continue;
                }
                else if (pmcGroupChance == "max")
                {
                    maxGroupBotsToReplenish = 3;
                    CreateGroupBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, new ShallBeGroupParams(true, true, 5), 5, maxGroupBotsToReplenish);
                    continue;
                }
                else
                {
                    CreateBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, maxBotsToReplenish);
                    CreateGroupBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, new ShallBeGroupParams(true, true, 2), 2, maxGroupBotsToReplenish);
                    CreateGroupBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, new ShallBeGroupParams(true, true, 3), 3, maxGroupBotsToReplenish);
                    CreateGroupBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, new ShallBeGroupParams(true, true, 4), 4, maxGroupBotsToReplenish);
                    CreateGroupBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, new ShallBeGroupParams(true, true, 5), 5, maxGroupBotsToReplenish);
                }
            }

            // Create bots for SCAV difficulties
            foreach (var entry in botLists[WildSpawnType.assault])
            {
                CreateBots(entry.Value, EPlayerSide.Savage, WildSpawnType.assault, entry.Key, scavMaxBotsToReplenish);
                CreateGroupBots(entry.Value, EPlayerSide.Savage, WildSpawnType.assault, entry.Key, new ShallBeGroupParams(true, true, 2), 2, scavMaxGroupBotsToReplenish);
            }

        }
        private async void Update()
        {
            timeSinceLastReplenish += Time.deltaTime;

            if (timeSinceLastReplenish >= replenishInterval)
            {
                timeSinceLastReplenish = 0f;
                Logger.LogWarning("Donuts: ReplenishAllBots() running");

                // Replenish bots for PMC difficulties
                foreach (var entry in botLists[sptBear])
                {
                    ReplenishBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key);
                    ReplenishGroupBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key);
                }

                foreach (var entry in botLists[sptUsec])
                {
                    ReplenishBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key);
                    ReplenishGroupBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key);
                }

                // Replenish bots for SCAV difficulties
                foreach (var entry in botLists[WildSpawnType.assault])
                {
                    ReplenishBots(entry.Value, EPlayerSide.Savage, WildSpawnType.assault, entry.Key);
                    ReplenishGroupBots(entry.Value, EPlayerSide.Savage, WildSpawnType.assault, entry.Key);
                }

                botsReplenishedCount = 0;
            }
        }


        private async Task ReplenishBots(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty, int maxCount = 5)
        {
            int currentCount = botList.Count;
            int botsToAdd = maxCount - currentCount;

            if (botsToAdd > 0 && botsReplenishedCount < maxBotsToReplenish)
            {
                await CreateBots(botList, side, spawnType, difficulty, botsToAdd);
                botsReplenishedCount += botsToAdd;
            }
        }

        private async Task ReplenishGroupBots(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty)
        {
            // Calculate the number of groups needed for 2, 3, and 4 bots
            int groupsOf2Needed = maxGroupBotsToReplenish - botList.Count(bot => bot.Profiles.Count == 2);
            int groupsOf3Needed = maxGroupBotsToReplenish - botList.Count(bot => bot.Profiles.Count == 3);
            int groupsOf4Needed = maxGroupBotsToReplenish - botList.Count(bot => bot.Profiles.Count == 4);
            int groupsOf5Needed = maxGroupBotsToReplenish - botList.Count(bot => bot.Profiles.Count == 5);

            int groupsNeeded = groupsOf2Needed + groupsOf3Needed + groupsOf4Needed + groupsOf5Needed;

            if (groupsNeeded > 0)
            {
                for (int i = 0; i < groupsOf2Needed && botsReplenishedCount < 5; i++)
                {
                    await CreateGroupBots(botList, side, spawnType, difficulty, new ShallBeGroupParams(true, true, 2), 2, 1);
                    botsReplenishedCount += 2;
                }

                for (int i = 0; i < groupsOf3Needed && botsReplenishedCount < 5; i++)
                {
                    await CreateGroupBots(botList, side, spawnType, difficulty, new ShallBeGroupParams(true, true, 3), 3, 1);
                    botsReplenishedCount += 3;
                }

                for (int i = 0; i < groupsOf4Needed && botsReplenishedCount < 5; i++)
                {
                    await CreateGroupBots(botList, side, spawnType, difficulty, new ShallBeGroupParams(true, true, 4), 4, 1);
                    botsReplenishedCount += 4;
                }

                for (int i = 0; i < groupsOf5Needed && botsReplenishedCount < 5; i++)
                {
                    await CreateGroupBots(botList, side, spawnType, difficulty, new ShallBeGroupParams(true, true, 5), 5, 1);
                    botsReplenishedCount += 5;
                }
            }
        }

        //regular create bots used internally within the component for caching
        private async Task CreateBots(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                CreateBot(botList, side, spawnType, difficulty);
            }
        }

        private async Task CreateBot(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty)
        {
            var botData = new IProfileData(side, spawnType, difficulty, 0f, null);
            var bot = await BotCacheClass.Create(botData, botCreator, 1, botSpawnerClass);
            botList.Add(bot);
        }

        internal static List<BotCacheClass> GetWildSpawnData(WildSpawnType spawnType, BotDifficulty botDifficulty)
        {
            return botLists[spawnType][botDifficulty];
        }

        // create cached bots for groups.
        internal static async Task CreateGroupBots(EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty,
    ShallBeGroupParams groupParams, int maxCount, int iterations)
        {
            List<BotCacheClass> botList = botLists[spawnType][difficulty];

            var botSpawnParams = new BotSpawnParams
            {
                TriggerType = SpawnTriggerType.none,
                ShallBeGroup = groupParams
            };

            for (int i = 0; i < iterations; i++)
            {
                var botData = new IProfileData(side, spawnType, difficulty, 0f, botSpawnParams);
                var botGroup = await BotCacheClass.Create(botData, botCreator, maxCount, botSpawnerClass);

                botList.Add(botGroup);

                //add all profiles to orignalbotspawntypes list but change role to spawnType
                foreach (var profile in botGroup.Profiles)
                {
                    profile.Info.Settings.Role = spawnType;

                    //add to originalbotspawntypes dictionary. profile.id is the key, spawnType is the value
                    OriginalBotSpawnTypes.Add(profile.Id, spawnType);
                }
            }
        }

        //overloaded method for if we know the botList for initial spawns
        internal static async Task CreateGroupBots(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty,
    ShallBeGroupParams groupParams, int maxCount, int iterations)
        {
            var botSpawnParams = new BotSpawnParams
            {
                TriggerType = SpawnTriggerType.none,
                ShallBeGroup = groupParams
            };

            for (int i = 0; i < iterations; i++)
            {
                var botData = new IProfileData(side, spawnType, difficulty, 0f, botSpawnParams);
                var botGroup = await BotCacheClass.Create(botData, botCreator, maxCount, botSpawnerClass);

                botList.Add(botGroup);

                //add all profiles to orignalbotspawntypes list but change role to spawnType
                foreach (var profile in botGroup.Profiles)
                {
                    profile.Info.Settings.Role = spawnType;
                    //Logger.LogWarning("Assigning Profile Role: " + profile.Info.Settings.Role.ToString() + " to OriginalBotSpawnTypes");
                    //add to originalbotspawntypes dictionary. profile.id is the key, spawnType is the value
                    OriginalBotSpawnTypes.Add(profile.Id, spawnType);
                }
            }
        }

        //find a botcacheclass list that has X amount of bots in the groupParams
        internal static BotCacheClass FindCachedBots(WildSpawnType spawnType, BotDifficulty botDifficulty, int targetCount)
        {
            var botList = botLists[spawnType][botDifficulty];
            Logger.LogWarning($"Trying to Find CachedBots that match: {targetCount} bot(s) for {spawnType} and difficulty: {botDifficulty}");

            var matchingEntry = botList.FirstOrDefault(entry => entry.Profiles.Count == targetCount);

            if (matchingEntry != null)
            {
                foreach (var profile in matchingEntry.Profiles)
                {
                    Logger.LogWarning($"Contained Profile[{matchingEntry.Profiles.IndexOf(profile)}]: {profile.Nickname} Difficulty: {profile.Info.Settings.BotDifficulty}, Role: {profile.Info.Settings.Role}");
                }
                return matchingEntry;
            }

            Logger.LogWarning("FindCachedBots: Did not find a group cached bot that matches the target count");
            return null;
        }

        //return the original wildspawntype of a bot that was converted to a group
        internal static WildSpawnType? GetOriginalSpawnTypeForBot(BotOwner bot)
        {
            //search originalspawntype dictionary for the bot's profile.id
            var originalProfile = OriginalBotSpawnTypes.FirstOrDefault(profile => profile.Key == bot.Profile.Id);

            //if we found the original profile, return the original role

            if (originalProfile.Key != null)
            {
                Logger.LogWarning("Found original profile for bot " + bot.Profile.Nickname + " as " + originalProfile.Value.ToString());
                return originalProfile.Value;
            }
            else
            {
                Logger.LogWarning("Could not find original profile for bot " + bot.Profile.Nickname);
                return null;
            }
        }
    }
}

