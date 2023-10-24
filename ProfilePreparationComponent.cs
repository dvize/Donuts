using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aki.PrePatch;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

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
        internal static List<Profile> OriginalBotSpawnTypes;

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
            replenishInterval = 30.0f;
            timeSinceLastReplenish = 0f;
            botsReplenishedCount = 0;
            maxBotsToReplenish = 3;
            maxGroupBotsToReplenish = 2;

            botLists = new Dictionary<WildSpawnType, Dictionary<BotDifficulty, List<BotCacheClass>>>();
            OriginalBotSpawnTypes = new List<Profile>();

            InitializeBotLists();
        }

        private void InitializeBotLists()
        {
            botLists.Add(WildSpawnType.assault, new Dictionary<BotDifficulty, List<BotCacheClass>>());
            botLists.Add(sptUsec, new Dictionary<BotDifficulty, List<BotCacheClass>>());
            botLists.Add(sptBear, new Dictionary<BotDifficulty, List<BotCacheClass>>());

            botLists[WildSpawnType.assault].Add(BotDifficulty.easy, new List<BotCacheClass>());
            botLists[WildSpawnType.assault].Add(BotDifficulty.normal, new List<BotCacheClass>());
            botLists[WildSpawnType.assault].Add(BotDifficulty.hard, new List<BotCacheClass>());
            botLists[WildSpawnType.assault].Add(BotDifficulty.impossible, new List<BotCacheClass>());

            botLists[sptUsec].Add(BotDifficulty.easy, new List<BotCacheClass>());
            botLists[sptUsec].Add(BotDifficulty.normal, new List<BotCacheClass>());
            botLists[sptUsec].Add(BotDifficulty.hard, new List<BotCacheClass>());
            botLists[sptUsec].Add(BotDifficulty.impossible, new List<BotCacheClass>());

            botLists[sptBear].Add(BotDifficulty.easy, new List<BotCacheClass>());
            botLists[sptBear].Add(BotDifficulty.normal, new List<BotCacheClass>());
            botLists[sptBear].Add(BotDifficulty.hard, new List<BotCacheClass>());
            botLists[sptBear].Add(BotDifficulty.impossible, new List<BotCacheClass>());
        }

        private async void Start()
        {
            // Initialize the bot pool at the beginning of the round
            await InitializeBotPool();
        }

        private async Task InitializeBotPool()
        {
            Logger.LogWarning("Profile Generation is Creating for Donuts Difficulties");

            // Create bots for PMC difficulties
            foreach (var entry in botLists[sptBear])
            {
                CreateBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, maxBotsToReplenish);
                CreateGroupBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, new ShallBeGroupParams(true, true, 2), 2, maxGroupBotsToReplenish);
                CreateGroupBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, new ShallBeGroupParams(true, true, 3), 3, maxGroupBotsToReplenish);
                CreateGroupBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key, new ShallBeGroupParams(true, true, 4), 4, maxGroupBotsToReplenish);
            }

            foreach (var entry in botLists[sptUsec])
            {
                CreateBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, maxBotsToReplenish);
                CreateGroupBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, new ShallBeGroupParams(true, true, 2), 2, maxGroupBotsToReplenish);
                CreateGroupBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, new ShallBeGroupParams(true, true, 3), 3, maxGroupBotsToReplenish);
                CreateGroupBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key, new ShallBeGroupParams(true, true, 4), 4, maxGroupBotsToReplenish);
            }

            // Create bots for SCAV difficulties
            foreach (var entry in botLists[WildSpawnType.assault])
            {
                CreateBots(entry.Value, EPlayerSide.Savage, WildSpawnType.assault, entry.Key, maxBotsToReplenish);
                CreateGroupBots(entry.Value, EPlayerSide.Savage, WildSpawnType.assault, entry.Key, new ShallBeGroupParams(true, true, 2), 2, maxGroupBotsToReplenish);
                CreateGroupBots(entry.Value, EPlayerSide.Savage, WildSpawnType.assault, entry.Key, new ShallBeGroupParams(true, true, 3), 3, maxGroupBotsToReplenish);
                CreateGroupBots(entry.Value, EPlayerSide.Savage, WildSpawnType.assault, entry.Key, new ShallBeGroupParams(true, true, 4), 4, maxGroupBotsToReplenish);
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

            int groupsNeeded = groupsOf2Needed + groupsOf3Needed + groupsOf4Needed;

            if (groupsNeeded > 0)
            {
                for (int i = 0; i < groupsOf2Needed && botsReplenishedCount < 5; i++)
                {
                    CreateGroupBots(botList, side, spawnType, difficulty, new ShallBeGroupParams(true, true, 2), 2, 1);
                    botsReplenishedCount += 2;
                }

                for (int i = 0; i < groupsOf3Needed && botsReplenishedCount < 5; i++)
                {
                    CreateGroupBots(botList, side, spawnType, difficulty, new ShallBeGroupParams(true, true, 3), 3, 1);
                    botsReplenishedCount += 3;
                }

                for (int i = 0; i < groupsOf4Needed && botsReplenishedCount < 5; i++)
                {
                    CreateGroupBots(botList, side, spawnType, difficulty, new ShallBeGroupParams(true, true, 4), 4, 1);
                    botsReplenishedCount += 4;
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
    ShallBeGroupParams groupParams, int maxCount, int iterations )
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
                    OriginalBotSpawnTypes.Add(profile);
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
                    OriginalBotSpawnTypes.Add(profile);
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
            var originalProfile = OriginalBotSpawnTypes.FirstOrDefault(profile => profile.Id == bot.Profile.Id);

            if (originalProfile != null)
            {
                Logger.LogWarning("Found original profile for bot " + bot.Profile.Nickname + " as " + originalProfile.Info.Settings.Role.ToString());
                return originalProfile.Info.Settings.Role;
            }
            else
            {
                Logger.LogWarning("Could not find original profile for bot " + bot.Profile.Nickname);
                return null;
            }
        }
    }
}

