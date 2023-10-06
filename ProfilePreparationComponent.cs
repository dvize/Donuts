using System.Collections;
using System.Collections.Generic;
using System.Threading;
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

        private static WildSpawnType sptUsec;
        private static WildSpawnType sptBear;

        internal static List<BotCacheClass> bearsEasy;
        internal static List<BotCacheClass> usecEasy;
        internal static List<BotCacheClass> assaultEasy;

        internal static List<BotCacheClass> bearsNormal;
        internal static List<BotCacheClass> usecNormal;
        internal static List<BotCacheClass> assaultNormal;

        internal static List<BotCacheClass> bearsHard;
        internal static List<BotCacheClass> usecHard;
        internal static List<BotCacheClass> assaultHard;

        internal static List<BotCacheClass> bearsImpossible;
        internal static List<BotCacheClass> usecImpossible;
        internal static List<BotCacheClass> assaultImpossible;

        private int maxBotCount;
        private int maxBotCountIfOnlyOneDifficulty;
        private float replenishInterval;
        private float timeSinceLastReplenish;
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
            replenishInterval = 60.0f;
            timeSinceLastReplenish = 0f;
        }

        private async void Start()
        {
            // Initialize the bot pool at the beginning of the round
            await InitializeBotPool();
        }

        private async Task InitializeBotPool()
        {
            // Initialize lists
            bearsEasy = new List<BotCacheClass>();
            usecEasy = new List<BotCacheClass>();
            assaultEasy = new List<BotCacheClass>();
            bearsNormal = new List<BotCacheClass>();
            usecNormal = new List<BotCacheClass>();
            assaultNormal = new List<BotCacheClass>();
            bearsHard = new List<BotCacheClass>();
            usecHard = new List<BotCacheClass>();
            assaultHard = new List<BotCacheClass>();
            bearsImpossible = new List<BotCacheClass>();
            usecImpossible = new List<BotCacheClass>();
            assaultImpossible = new List<BotCacheClass>();

            Logger.LogWarning("Profile Generation is Creating for Donuts Difficulties");

            // Create bots for PMC difficulties
            switch (DonutsPlugin.botDifficultiesPMC.Value.ToLower())
            {
                case "asonline":
                    CreateBots(bearsEasy, EPlayerSide.Bear, sptBear, BotDifficulty.easy, 3);
                    CreateBots(usecEasy, EPlayerSide.Usec, sptUsec, BotDifficulty.easy, 3);
                    CreateBots(bearsNormal, EPlayerSide.Bear, sptBear, BotDifficulty.normal, 3);
                    CreateBots(usecNormal, EPlayerSide.Usec, sptUsec, BotDifficulty.normal, 3);
                    CreateBots(bearsHard, EPlayerSide.Bear, sptBear, BotDifficulty.hard, 3);
                    CreateBots(usecHard, EPlayerSide.Usec, sptUsec, BotDifficulty.hard, 3);
                    break;
                case "easy":
                    CreateBots(bearsEasy, EPlayerSide.Bear, sptBear, BotDifficulty.easy, 5);
                    CreateBots(usecEasy, EPlayerSide.Usec, sptUsec, BotDifficulty.easy, 5);
                    break;
                case "normal":
                    CreateBots(bearsNormal, EPlayerSide.Bear, sptBear, BotDifficulty.normal, 5);
                    CreateBots(usecNormal, EPlayerSide.Usec, sptUsec, BotDifficulty.normal, 5);
                    break;
                case "hard":
                    CreateBots(bearsHard, EPlayerSide.Bear, sptBear, BotDifficulty.hard, 5);
                    CreateBots(usecHard, EPlayerSide.Usec, sptUsec, BotDifficulty.hard, 5);
                    break;
                case "impossible":
                    CreateBots(bearsImpossible, EPlayerSide.Bear, sptBear, BotDifficulty.impossible, 5);
                    CreateBots(usecImpossible, EPlayerSide.Usec, sptUsec, BotDifficulty.impossible, 5);
                    break;
            }

            // Create bots for SCAV difficulties
            switch (DonutsPlugin.botDifficultiesSCAV.Value.ToLower())
            {
                case "asonline":
                    CreateBots(assaultEasy, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.easy, 3);
                    CreateBots(assaultNormal, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, 3);
                    CreateBots(assaultHard, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard, 3);
                    break;
                case "easy":
                    CreateBots(assaultEasy, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.easy, 5);
                    break;
                case "normal":
                    CreateBots(assaultNormal, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, 5);
                    break;
                case "hard":
                    CreateBots(assaultHard, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard, 5);
                    break;
                case "impossible":
                    CreateBots(assaultImpossible, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.impossible, 5);
                    break;
            }

        }
        private async void Update()
        {
            timeSinceLastReplenish += Time.deltaTime;

            if (timeSinceLastReplenish >= replenishInterval)
            {
                timeSinceLastReplenish = 0f;
                Logger.LogWarning("Donuts: ReplenishAllBots() running");

                switch (DonutsPlugin.botDifficultiesPMC.Value.ToLower())
                {
                    case "asonline":
                        ReplenishBots(bearsEasy, EPlayerSide.Bear, sptBear, BotDifficulty.easy);
                        ReplenishBots(usecEasy, EPlayerSide.Usec, sptUsec, BotDifficulty.easy);
                        ReplenishBots(bearsNormal, EPlayerSide.Bear, sptBear, BotDifficulty.normal);
                        ReplenishBots(usecNormal, EPlayerSide.Usec, sptUsec, BotDifficulty.normal);
                        ReplenishBots(bearsHard, EPlayerSide.Bear, sptBear, BotDifficulty.hard);
                        ReplenishBots(usecHard, EPlayerSide.Usec, sptUsec, BotDifficulty.hard);
                        break;
                    case "easy":
                        ReplenishBots(bearsEasy, EPlayerSide.Bear, sptBear, BotDifficulty.easy, maxBotCountIfOnlyOneDifficulty);
                        ReplenishBots(usecEasy, EPlayerSide.Usec, sptUsec, BotDifficulty.easy, maxBotCountIfOnlyOneDifficulty);
                        break;
                    case "normal":
                        ReplenishBots(bearsNormal, EPlayerSide.Bear, sptBear, BotDifficulty.normal, maxBotCountIfOnlyOneDifficulty);
                        ReplenishBots(usecNormal, EPlayerSide.Usec, sptUsec, BotDifficulty.normal, maxBotCountIfOnlyOneDifficulty);
                        break;
                    case "hard":
                        ReplenishBots(bearsHard, EPlayerSide.Bear, sptBear, BotDifficulty.hard, maxBotCountIfOnlyOneDifficulty);
                        ReplenishBots(usecHard, EPlayerSide.Usec, sptUsec, BotDifficulty.hard, maxBotCountIfOnlyOneDifficulty);
                        break;
                    case "impossible":
                        ReplenishBots(bearsImpossible, EPlayerSide.Bear, sptBear, BotDifficulty.impossible, maxBotCountIfOnlyOneDifficulty);
                        ReplenishBots(usecImpossible, EPlayerSide.Usec, sptUsec, BotDifficulty.impossible, maxBotCountIfOnlyOneDifficulty);
                        break;
                }

                switch (DonutsPlugin.botDifficultiesSCAV.Value.ToLower())
                {
                    case "asonline":
                        ReplenishBots(assaultEasy, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.easy);
                        ReplenishBots(assaultNormal, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal);
                        ReplenishBots(assaultHard, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard);
                        break;
                    case "easy":
                        ReplenishBots(assaultEasy, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.easy, maxBotCountIfOnlyOneDifficulty);
                        break;
                    case "normal":
                        ReplenishBots(assaultNormal, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, maxBotCountIfOnlyOneDifficulty);
                        break;
                    case "hard":
                        ReplenishBots(assaultHard, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard, maxBotCountIfOnlyOneDifficulty);
                        break;
                    case "impossible":
                        ReplenishBots(assaultImpossible, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.impossible, maxBotCountIfOnlyOneDifficulty);
                        break;
                }
            }

        }

        private async Task ReplenishBots(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty)
        {
            int currentCount = botList.Count;
            int botsToAdd = maxBotCount - currentCount;

            if (botsToAdd > 0)
            {
                await CreateBots(botList, side, spawnType, difficulty, botsToAdd);
            }
        }

        //overload to specify maxcount if needed
        private async Task ReplenishBots(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty, int maxCount)
        {
            int currentCount = botList.Count;
            int botsToAdd = maxCount - currentCount;

            if (botsToAdd > 0)
            {
                await CreateBots(botList, side, spawnType, difficulty, botsToAdd);
            }
        }

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
            switch (botDifficulty)
            {
                case BotDifficulty.easy:
                    if (spawnType == WildSpawnType.assault)
                    {
                        return assaultEasy;
                    }
                    else if (spawnType == sptUsec)
                    {
                        return usecEasy;
                    }
                    else
                    {
                        return bearsEasy;
                    }

                case BotDifficulty.normal:
                    if (spawnType == WildSpawnType.assault)
                    {
                        return assaultNormal;
                    }
                    else if (spawnType == sptUsec)
                    {
                        return usecNormal;
                    }
                    else
                    {
                        return bearsNormal;
                    }

                case BotDifficulty.hard:
                    if (spawnType == WildSpawnType.assault)
                    {
                        return assaultHard;
                    }
                    else if (spawnType == sptUsec)
                    {
                        return usecHard;
                    }
                    else
                    {
                        return bearsHard;
                    }

                case BotDifficulty.impossible:
                    if (spawnType == WildSpawnType.assault)
                    {
                        return assaultImpossible;
                    }
                    else if (spawnType == sptUsec)
                    {
                        return usecImpossible;
                    }
                    else
                    {
                        return bearsImpossible;
                    }

                default:
                    return null;
            }


        }

    }
}
