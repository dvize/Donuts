using System.Collections.Generic;
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

        private static WildSpawnType sptUsec;
        private static WildSpawnType sptBear;

        private int maxBotCount;
        private int maxBotCountIfOnlyOneDifficulty;
        private float replenishInterval;
        private float timeSinceLastReplenish;
        private int botsReplenishedCount;
        private int maxBotsToReplenish;

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
            maxBotCount = 5;
            maxBotCountIfOnlyOneDifficulty = 7;
            replenishInterval = 30.0f;
            timeSinceLastReplenish = 0f;
            botsReplenishedCount = 0;
            maxBotsToReplenish = 3;

            botLists = new Dictionary<WildSpawnType, Dictionary<BotDifficulty, List<BotCacheClass>>>();
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
                CreateBots(entry.Value, EPlayerSide.Bear, sptBear, entry.Key);
            }

            foreach (var entry in botLists[sptUsec])
            {
                CreateBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key);
            }

            // Create bots for SCAV difficulties
            foreach (var entry in botLists[WildSpawnType.assault])
            {
                CreateBots(entry.Value, EPlayerSide.Savage, WildSpawnType.assault, entry.Key);
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
                }

                foreach (var entry in botLists[sptUsec])
                {
                    ReplenishBots(entry.Value, EPlayerSide.Usec, sptUsec, entry.Key);
                }

                // Replenish bots for SCAV difficulties
                foreach (var entry in botLists[WildSpawnType.assault])
                {
                    ReplenishBots(entry.Value, EPlayerSide.Savage, WildSpawnType.assault, entry.Key);
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
        private async Task CreateGroupBots(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty, 
            ShallBeGroupParams groupParams, int count = 1)
        {
            var botSpawnParams = new BotSpawnParams
            {
                TriggerType = SpawnTriggerType.none,
                ShallBeGroup = groupParams
            };

            var botData = new IProfileData(side, spawnType, difficulty, 0f, botSpawnParams);
            var bot = await BotCacheClass.Create(botData, botCreator, count, botSpawnerClass);
            botList.Add(bot);

        }


    }


}
