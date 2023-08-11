using System.Collections.Generic;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using Aki.PrePatch;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Bots;
using HarmonyLib;
using UnityEngine;

namespace Donuts
{
    internal class DonutsBotPrep : MonoBehaviour
    {
        private static GameWorld gameWorld;
        private static IBotCreator botCreator;
        private static BotSpawnerClass botSpawnerClass;
        private static CancellationTokenSource cancellationToken;

        private static WildSpawnType sptUsec;
        private static WildSpawnType sptBear;

        internal static List<GClass628> bearsEasy;
        internal static List<GClass628> usecEasy;
        internal static List<GClass628> assaultEasy;

        internal static List<GClass628> bearsNormal;
        internal static List<GClass628> usecNormal;
        internal static List<GClass628> assaultNormal;

        internal static List<GClass628> bearsHard;
        internal static List<GClass628> usecHard;
        internal static List<GClass628> assaultHard;

        internal static List<GClass628> bearsImpossible;
        internal static List<GClass628> usecImpossible;
        internal static List<GClass628> assaultImpossible;
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
            botCreator = AccessTools.Field(typeof(BotSpawnerClass), "ginterface17_0").GetValue(botSpawnerClass) as IBotCreator;
            cancellationToken = AccessTools.Field(typeof(BotSpawnerClass), "cancellationTokenSource_0").GetValue(botSpawnerClass) as CancellationTokenSource;
            sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;

            //read the DonutsPlugin
            Logger.LogWarning("Profile Generation is Creating for Donuts Difficulty: " + DonutsPlugin.botDifficulties.Value.ToLower());
            if (DonutsPlugin.botDifficulties.Value.ToLower() == "asonline")
            {
                //create bears bots of normal difficulty
                bearsNormal = new List<GClass628>();
                for (int i = 0; i < 3; i++)
                {
                    IBotData sptBearDataNormal = new GClass629(EPlayerSide.Bear, sptBear, BotDifficulty.normal, 0f, null);
                    var sptbearNormal = await GClass628.Create(sptBearDataNormal, botCreator, 1, botSpawnerClass);

                    bearsNormal.Add(sptbearNormal);
                }

                //create usec bots of normal difficulty
                usecNormal = new List<GClass628>();
                for (int i = 0; i < 3; i++)
                {
                    IBotData sptUsecDataNormal = new GClass629(EPlayerSide.Usec, sptUsec, BotDifficulty.normal, 0f, null);
                    var sptusecNormal = await GClass628.Create(sptUsecDataNormal, botCreator, 1, botSpawnerClass);

                    usecNormal.Add(sptusecNormal);
                }

                //create assault bots of normal difficulty
                assaultNormal = new List<GClass628>();
                for (int i = 0; i < 3; i++)
                {
                    IBotData assaultDataNormal = new GClass629(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, 0f, null);
                    var assaultNormal1 = await GClass628.Create(assaultDataNormal, botCreator, 1, botSpawnerClass);

                    assaultNormal.Add(assaultNormal1);
                }


                //create bear bots of hard difficulty
                bearsHard = new List<GClass628>();
                for (int i = 0; i < 2; i++)
                {
                    IBotData sptBearDataHard = new GClass629(EPlayerSide.Bear, sptBear, BotDifficulty.hard, 0f, null);
                    var sptbearHard = await GClass628.Create(sptBearDataHard, botCreator, 1, botSpawnerClass);

                    bearsHard.Add(sptbearHard);
                }

                //create usec bots of hard difficulty
                usecHard = new List<GClass628>();
                for (int i = 0; i < 2; i++)
                {
                    IBotData sptUsecDataHard = new GClass629(EPlayerSide.Usec, sptUsec, BotDifficulty.hard, 0f, null);
                    var sptusecHard = await GClass628.Create(sptUsecDataHard, botCreator, 1, botSpawnerClass);

                    usecHard.Add(sptusecHard);
                }

                //create assault bots of hard difficulty
                assaultHard = new List<GClass628>();
                for (int i = 0; i < 2; i++)
                {
                    IBotData assaultDataHard = new GClass629(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard, 0f, null);
                    var assaultHard1 = await GClass628.Create(assaultDataHard, botCreator, 1, botSpawnerClass);

                    assaultHard.Add(assaultHard1);
                }
            }
            
            else if (DonutsPlugin.botDifficulties.Value.ToLower() == "easy")
            {
                //create bears bots of easy difficulty
                bearsEasy = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData sptBearDataEasy = new GClass629(EPlayerSide.Bear, sptBear, BotDifficulty.normal, 0f, null);
                    var sptbearEasy = await GClass628.Create(sptBearDataEasy, botCreator, 1, botSpawnerClass);

                    bearsEasy.Add(sptbearEasy);
                }

                //create usec bots of easy difficulty
                usecNormal = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData sptUsecDataEasy = new GClass629(EPlayerSide.Usec, sptUsec, BotDifficulty.normal, 0f, null);
                    var sptusecEasy = await GClass628.Create(sptUsecDataEasy, botCreator, 1, botSpawnerClass);

                    usecEasy.Add(sptusecEasy);
                }

                //create assault bots of easy difficulty
                assaultEasy = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData assaultDataEasy = new GClass629(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, 0f, null);
                    var assaultEasy1 = await GClass628.Create(assaultDataEasy, botCreator, 1, botSpawnerClass);

                    assaultEasy.Add(assaultEasy1);
                }
            }

            else if (DonutsPlugin.botDifficulties.Value.ToLower() == "normal")
            {
                //create bears bots of normal difficulty
                bearsNormal = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData sptBearDataNormal = new GClass629(EPlayerSide.Bear, sptBear, BotDifficulty.normal, 0f, null);
                    var sptbearNormal = await GClass628.Create(sptBearDataNormal, botCreator, 1, botSpawnerClass);

                    bearsNormal.Add(sptbearNormal);
                }

                //create usec bots of normal difficulty
                usecNormal = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData sptUsecDataNormal = new GClass629(EPlayerSide.Usec, sptUsec, BotDifficulty.normal, 0f, null);
                    var sptusecNormal = await GClass628.Create(sptUsecDataNormal, botCreator, 1, botSpawnerClass);

                    usecNormal.Add(sptusecNormal);
                }

                //create assault bots of normal difficulty
                assaultNormal = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData assaultDataNormal = new GClass629(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, 0f, null);
                    var assaultNormal1 = await GClass628.Create(assaultDataNormal, botCreator, 1, botSpawnerClass);

                    assaultNormal.Add(assaultNormal1);
                }
            }

            else if (DonutsPlugin.botDifficulties.Value.ToLower() == "hard")
            {
                //create bears bots of hard difficulty
                bearsHard = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData sptBearDataHard = new GClass629(EPlayerSide.Bear, sptBear, BotDifficulty.hard, 0f, null);
                    var sptbearHard = await GClass628.Create(sptBearDataHard, botCreator, 1, botSpawnerClass);
                
                    bearsHard.Add(sptbearHard);
                }

                //create usec bots of hard difficulty
                usecHard = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData sptUsecDataHard = new GClass629(EPlayerSide.Usec, sptUsec, BotDifficulty.hard, 0f, null);
                    var sptusecHard = await GClass628.Create(sptUsecDataHard, botCreator, 1, botSpawnerClass);
                
                    usecHard.Add(sptusecHard);
                }

                //create assault bots of hard difficulty
                assaultHard = new List<GClass628>();
                for(int i =0; i < 5; i++)
                {
                    IBotData assaultDataHard = new GClass629(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard, 0f, null);
                    var assaultHard1 = await GClass628.Create(assaultDataHard, botCreator, 1, botSpawnerClass);

                    assaultHard.Add(assaultHard1);
                }

            }

            else if (DonutsPlugin.botDifficulties.Value.ToLower() == "impossible")
            {
                //create bears bots of impossible difficulty
                bearsImpossible = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData sptBearDataImpossible = new GClass629(EPlayerSide.Bear, sptBear, BotDifficulty.impossible, 0f, null);
                    var sptbearImpossible = await GClass628.Create(sptBearDataImpossible, botCreator, 1, botSpawnerClass);
                
                    bearsImpossible.Add(sptbearImpossible);
                }

                //create usec bots of impossible difficulty
                usecImpossible = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData sptUsecDataImpossible = new GClass629(EPlayerSide.Usec, sptUsec, BotDifficulty.impossible, 0f, null);
                    var sptusecImpossible = await GClass628.Create(sptUsecDataImpossible, botCreator, 1, botSpawnerClass);
                               
                    usecImpossible.Add(sptusecImpossible);
                }

                //create assault bots of impossible difficulty
                assaultImpossible = new List<GClass628>();
                for (int i = 0; i < 5; i++)
                {
                    IBotData assaultDataImpossible = new GClass629(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.impossible, 0f, null);
                    var assaultImpossible1 = await GClass628.Create(assaultDataImpossible, botCreator, 1, botSpawnerClass);
                
                    assaultImpossible.Add(assaultImpossible1);
                }
            }

        }

        internal static List<GClass628> GetWildSpawnData(WildSpawnType spawnType, BotDifficulty botDifficulty)
        {
            switch (botDifficulty)
            {
                case BotDifficulty.easy:
                    if(spawnType == WildSpawnType.assault)
                    {
                        return assaultEasy;
                    }
                    else if (spawnType == sptUsec)
                    {
                        return usecNormal;
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

        private WildSpawnType GetWildSpawnType(string spawnType)
        {
            switch (spawnType.ToLower())
            {
                case "arenafighterevent":
                    return WildSpawnType.arenaFighterEvent;
                case "assault":
                    return WildSpawnType.assault;
                case "assaultgroup":
                    return WildSpawnType.assaultGroup;
                case "bossbully":
                    return WildSpawnType.bossBully;
                case "bossgluhar":
                    return WildSpawnType.bossGluhar;
                case "bosskilla":
                    return WildSpawnType.bossKilla;
                case "bosskojaniy":
                    return WildSpawnType.bossKojaniy;
                case "bosssanitar":
                    return WildSpawnType.bossSanitar;
                case "bosstagilla":
                    return WildSpawnType.bossTagilla;
                case "bosszryachiy":
                    return WildSpawnType.bossZryachiy;
                case "crazyassaultevent":
                    return WildSpawnType.crazyAssaultEvent;
                case "cursedassault":
                    return WildSpawnType.cursedAssault;
                case "exusec-rogues":
                    return WildSpawnType.exUsec;
                case "followerbully":
                    return WildSpawnType.followerBully;
                case "followergluharassault":
                    return WildSpawnType.followerGluharAssault;
                case "followergluharscout":
                    return WildSpawnType.followerGluharScout;
                case "followergluharsecurity":
                    return WildSpawnType.followerGluharSecurity;
                case "followergluharsnipe":
                    return WildSpawnType.followerGluharSnipe;
                case "followerkojaniy":
                    return WildSpawnType.followerKojaniy;
                case "followersanitar":
                    return WildSpawnType.followerSanitar;
                case "followertagilla":
                    return WildSpawnType.followerTagilla;
                case "followerzryachiy":
                    return WildSpawnType.followerZryachiy;
                case "gifter":
                    return WildSpawnType.gifter;
                case "marksman":
                    return WildSpawnType.marksman;
                case "raiders":
                    return WildSpawnType.pmcBot;
                case "sectantpriest":
                    return WildSpawnType.sectantPriest;
                case "sectantwarrior":
                    return WildSpawnType.sectantWarrior;
                case "usec":
                    return sptUsec;
                case "bear":
                    return sptBear;
                case "sptusec":
                    return sptUsec;
                case "sptbear":
                    return sptBear;
                case "followerbigpipe":
                    return WildSpawnType.followerBigPipe;
                case "followerbirdeye":
                    return WildSpawnType.followerBirdEye;
                case "bossknight":
                    return WildSpawnType.bossKnight;
                case "pmc":
                    //random wildspawntype is either assigned sptusec or sptbear at 50/50 chance
                    return (UnityEngine.Random.Range(0, 2) == 0) ? sptUsec : sptBear;
                default:
                    return WildSpawnType.assault;
            }

        }
        private EPlayerSide GetSideForWildSpawnType(WildSpawnType spawnType)
        {
            if (spawnType == WildSpawnType.pmcBot || spawnType == sptUsec)
            {
                return EPlayerSide.Usec;
            }
            else if (spawnType == sptBear)
            {
                return EPlayerSide.Bear;
            }
            else
            {
                return EPlayerSide.Savage;
            }
        }

        private BotDifficulty grabBotDifficulty()
        {
            switch (DonutsPlugin.botDifficulties.Value.ToLower())
            {
                case "asonline":
                    //return random difficulty from array of easy, normal, hard
                    BotDifficulty[] randomDifficulty = {
                        BotDifficulty.easy,
                        BotDifficulty.normal,
                        BotDifficulty.hard
                    };
                    var diff = UnityEngine.Random.Range(0, 3);
                    return randomDifficulty[diff];
                case "easy":
                    return BotDifficulty.easy;
                case "normal":
                    return BotDifficulty.normal;
                case "hard":
                    return BotDifficulty.hard;
                case "impossible":
                    return BotDifficulty.impossible;
                default:
                    return BotDifficulty.normal;
            }

        }

    }
}
