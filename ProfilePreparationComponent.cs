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
        private static CancellationTokenSource cancellationToken;

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
            cancellationToken = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
            sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;
            maxBotCount = 5;
            maxBotCountIfOnlyOneDifficulty = 7;
            replenishInterval = 60.0f;
        }

        private async void Start()
        {
            // Initialize the bot pool at the beginning of the round
            await InitializeBotPool();
            ReplenishLoop();
        }

        private async Task InitializeBotPool()
        {
            //init bots for diff difficulties
            //read the DonutsPlugin
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
            if (DonutsPlugin.botDifficultiesPMC.Value.ToLower() == "asonline")
            {
                //create as online mix
                CreateBots(bearsEasy, EPlayerSide.Bear, sptBear, BotDifficulty.easy, 3);
                CreateBots(usecEasy, EPlayerSide.Usec, sptUsec, BotDifficulty.easy, 3);
                CreateBots(bearsNormal, EPlayerSide.Bear, sptBear, BotDifficulty.normal, 3);
                CreateBots(usecNormal, EPlayerSide.Usec, sptUsec, BotDifficulty.normal, 3);
                CreateBots(bearsHard, EPlayerSide.Bear, sptBear, BotDifficulty.hard, 3);
                CreateBots(usecHard, EPlayerSide.Usec, sptUsec, BotDifficulty.hard, 3);
            }
            else if (DonutsPlugin.botDifficultiesPMC.Value.ToLower() == "easy")
            {
                //create pmc Easy bots to spawn with
                CreateBots(bearsEasy, EPlayerSide.Bear, sptBear, BotDifficulty.easy, 5);
                CreateBots(usecEasy, EPlayerSide.Usec, sptUsec, BotDifficulty.easy, 5);
            }
            else if (DonutsPlugin.botDifficultiesPMC.Value.ToLower() == "normal")
            {
                //create pmc bots of normal difficulty
                CreateBots(bearsNormal, EPlayerSide.Bear, sptBear, BotDifficulty.normal, 5);
                CreateBots(usecNormal, EPlayerSide.Usec, sptUsec, BotDifficulty.normal, 5);
            }
            else if (DonutsPlugin.botDifficultiesPMC.Value.ToLower() == "hard")
            {
                //create pmc bots of hard difficulty
                CreateBots(bearsHard, EPlayerSide.Bear, sptBear, BotDifficulty.hard, 5);
                CreateBots(usecHard, EPlayerSide.Usec, sptUsec, BotDifficulty.hard, 5);

            }
            else if (DonutsPlugin.botDifficultiesPMC.Value.ToLower() == "impossible")
            {
                //create pmc bots of impossible difficulty
                CreateBots(bearsImpossible, EPlayerSide.Bear, sptBear, BotDifficulty.impossible, 5);
                CreateBots(usecImpossible, EPlayerSide.Usec, sptUsec, BotDifficulty.impossible, 5);
            }

            if (DonutsPlugin.botDifficultiesSCAV.Value.ToLower() == "asonline") {
                //create as online mix
                CreateBots(assaultEasy, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.easy, 3);
                CreateBots(assaultNormal, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, 3);
                CreateBots(assaultHard, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard, 3);
            }
            else if (DonutsPlugin.botDifficultiesSCAV.Value.ToLower() == "easy")
            {
                //create Easy bots to spawn with
                CreateBots(assaultEasy, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.easy, 5);
            }
            else if (DonutsPlugin.botDifficultiesSCAV.Value.ToLower() == "normal")
            {
                //create scav bots of normal difficulty
                CreateBots(assaultNormal, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, 5);
            }
            else if (DonutsPlugin.botDifficultiesSCAV.Value.ToLower() == "hard")
            {
                //create scav bots of hard difficulty
                CreateBots(assaultHard, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard, 5);

            }
            else if (DonutsPlugin.botDifficultiesSCAV.Value.ToLower() == "impossible")
            {
                //create scav bots of impossible difficulty
                CreateBots(assaultImpossible, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.impossible, 5);
            }

        }
       

        private async Task ReplenishLoop()
        {
            while (true)
            {
                //task.delay for replenish interval
                await Task.Delay((int)(replenishInterval * 1000));

                await ReplenishAllBots();
            }
        }

        private async Task ReplenishAllBots()
        {
            Logger.LogWarning("Donuts: ReplenishAllBots() running");
            if (DonutsPlugin.botDifficultiesPMC.Value.ToLower() == "asonline")
            {
                await ReplenishBots(bearsEasy, EPlayerSide.Bear, sptBear, BotDifficulty.easy);
                await ReplenishBots(usecEasy, EPlayerSide.Usec, sptUsec, BotDifficulty.easy);
                await ReplenishBots(bearsNormal, EPlayerSide.Bear, sptBear, BotDifficulty.normal);
                await ReplenishBots(usecNormal, EPlayerSide.Usec, sptUsec, BotDifficulty.normal);
                await ReplenishBots(bearsHard, EPlayerSide.Bear, sptBear, BotDifficulty.hard);
                await ReplenishBots(usecHard, EPlayerSide.Usec, sptUsec, BotDifficulty.hard);
            }
            else if (DonutsPlugin.botDifficultiesPMC.Value.ToLower() == "easy")
            {
                await ReplenishBots(bearsEasy, EPlayerSide.Bear, sptBear, BotDifficulty.easy, maxBotCountIfOnlyOneDifficulty);
                await ReplenishBots(usecEasy, EPlayerSide.Usec, sptUsec, BotDifficulty.easy, maxBotCountIfOnlyOneDifficulty);
            }
            else if (DonutsPlugin.botDifficultiesPMC.Value.ToLower() == "normal")
            {
                await ReplenishBots(bearsNormal, EPlayerSide.Bear, sptBear, BotDifficulty.normal, maxBotCountIfOnlyOneDifficulty);
                await ReplenishBots(usecNormal, EPlayerSide.Usec, sptUsec, BotDifficulty.normal, maxBotCountIfOnlyOneDifficulty);
            }
            else if (DonutsPlugin.botDifficultiesPMC.Value.ToLower() == "hard")
            {
                await ReplenishBots(bearsHard, EPlayerSide.Bear, sptBear, BotDifficulty.hard, maxBotCountIfOnlyOneDifficulty);
                await ReplenishBots(usecHard, EPlayerSide.Usec, sptUsec, BotDifficulty.hard, maxBotCountIfOnlyOneDifficulty);
            }
            else if (DonutsPlugin.botDifficultiesPMC.Value.ToLower() == "impossible")
            {
                await ReplenishBots(bearsImpossible, EPlayerSide.Bear, sptBear, BotDifficulty.impossible, maxBotCountIfOnlyOneDifficulty);
                await ReplenishBots(usecImpossible, EPlayerSide.Usec, sptUsec, BotDifficulty.impossible, maxBotCountIfOnlyOneDifficulty);
            }
            
            //separate scav logic
            if (DonutsPlugin.botDifficultiesSCAV.Value.ToLower() == "asonline")
            {
                await ReplenishBots(assaultEasy, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.easy);
                await ReplenishBots(assaultNormal, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal);
                await ReplenishBots(assaultHard, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard);
            }
            else if (DonutsPlugin.botDifficultiesSCAV.Value.ToLower() == "easy")
            {
                await ReplenishBots(assaultEasy, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.easy, maxBotCountIfOnlyOneDifficulty);
            }
            else if (DonutsPlugin.botDifficultiesSCAV.Value.ToLower() == "normal")
            {
                await ReplenishBots(assaultNormal, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, maxBotCountIfOnlyOneDifficulty);
            }
            else if (DonutsPlugin.botDifficultiesSCAV.Value.ToLower() == "hard")
            {
                await ReplenishBots(assaultHard, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard, maxBotCountIfOnlyOneDifficulty);
            }
            else if (DonutsPlugin.botDifficultiesSCAV.Value.ToLower() == "impossible")
            {
                await ReplenishBots(assaultImpossible, EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.impossible, maxBotCountIfOnlyOneDifficulty);
            }
        }

        private async Task ReplenishBots(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty)
        {
            int currentCount = botList.Count;
            int botsToAdd = maxBotCount - currentCount;

            if (botsToAdd > 0)
            {
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < botsToAdd; i++)
                {
                    tasks.Add(CreateBot(botList, side, spawnType, difficulty));
                }

                await Task.WhenAll(tasks);
            }
        }

        //overload to specify maxcount if needed
        private async Task ReplenishBots(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty, int maxCount)
        {
            int currentCount = botList.Count;
            int botsToAdd = maxCount - currentCount;

            if (botsToAdd > 0)
            {
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < botsToAdd; i++)
                {
                    tasks.Add(CreateBot(botList, side, spawnType, difficulty));
                }

                await Task.WhenAll(tasks);
            }
        }

        private async Task CreateBots(List<BotCacheClass> botList, EPlayerSide side, WildSpawnType spawnType, BotDifficulty difficulty, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                await CreateBot(botList, side, spawnType, difficulty);
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
