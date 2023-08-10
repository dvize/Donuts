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

        private float timeSinceLastProfileGeneration = 0.0f;
        private float profileGenerationInterval = 20.0f;

        private static WildSpawnType sptUsec;
        private static WildSpawnType sptBear;

        private int pmcBotCount = 7;
        private int scavBotCount = 5;
        private int bossBotCount = 1;

        internal static GClass628 sptbearEasy;
        internal static GClass628 sptbearNormal;
        internal static GClass628 sptbearHard;
        internal static GClass628 sptbearImpossible;

        internal static GClass628 sptusecEasy;
        internal static GClass628 sptusecNormal;
        internal static GClass628 sptusecHard;
        internal static GClass628 sptusecImpossible;

        internal static GClass628 assaultEasy;
        internal static GClass628 assaultNormal;
        internal static GClass628 assaultHard;
        internal static GClass628 assaultImpossible;

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

            IBotData sptUsecDataEasy = new GClass629(EPlayerSide.Usec, sptUsec, BotDifficulty.easy, 0f, null);
            sptusecEasy = await GClass628.Create(sptUsecDataEasy, botCreator, 5, botSpawnerClass);

            IBotData sptUsecDataNormal = new GClass629(EPlayerSide.Usec, sptUsec, BotDifficulty.normal, 0f, null);
            sptusecNormal = await GClass628.Create(sptUsecDataNormal, botCreator, 5, botSpawnerClass);

            IBotData sptUsecDataHard = new GClass629(EPlayerSide.Usec, sptUsec, BotDifficulty.hard, 0f, null);
            sptusecHard = await GClass628.Create(sptUsecDataHard, botCreator, 5, botSpawnerClass);

            IBotData sptUsecDataImpossible = new GClass629(EPlayerSide.Usec, sptUsec, BotDifficulty.impossible, 0f, null);
            sptusecImpossible = await GClass628.Create(sptUsecDataImpossible, botCreator, 5, botSpawnerClass);

            IBotData sptBearDataEasy = new GClass629(EPlayerSide.Bear, sptBear, BotDifficulty.easy, 0f, null);
            sptbearEasy = await GClass628.Create(sptBearDataEasy, botCreator, 5, botSpawnerClass);

            IBotData sptBearDataNormal = new GClass629(EPlayerSide.Bear, sptBear, BotDifficulty.normal, 0f, null);
            sptbearNormal = await GClass628.Create(sptBearDataNormal, botCreator, 5, botSpawnerClass);

            IBotData sptBearDataHard = new GClass629(EPlayerSide.Bear, sptBear, BotDifficulty.hard, 0f, null);
            sptbearHard = await GClass628.Create(sptBearDataHard, botCreator, 5, botSpawnerClass);

            IBotData sptBearDataImpossible = new GClass629(EPlayerSide.Bear, sptBear, BotDifficulty.impossible, 0f, null);
            sptbearImpossible = await GClass628.Create(sptBearDataImpossible, botCreator, 5, botSpawnerClass);

            IBotData assaultDataEasy = new GClass629(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.easy, 0f, null);
            assaultEasy = await GClass628.Create(assaultDataEasy, botCreator, 4, botSpawnerClass);

            IBotData assaultDataNormal = new GClass629(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, 0f, null);
            assaultNormal = await GClass628.Create(assaultDataNormal, botCreator, 4, botSpawnerClass);

            IBotData assaultDataHard = new GClass629(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard, 0f, null);
            assaultHard = await GClass628.Create(assaultDataHard, botCreator, 3, botSpawnerClass);

            IBotData assaultDataImpossible = new GClass629(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.impossible, 0f, null);
            assaultImpossible = await GClass628.Create(assaultDataImpossible, botCreator, 3, botSpawnerClass);
        }
        /*private async Task FillProfile(WildSpawnType wildSpawnType)
        {
            //assumes limits were checked so just generate a profile to be used
            var botdifficulty = grabBotDifficulty();
            var side = GetSideForWildSpawnType(wildSpawnType);

            IBotData botData = new GClass629(side, wildSpawnType, botdifficulty, 0f, null);
            GClass628 bot = await GClass628.Create(botData, botCreator, 1, botSpawnerClass);

            var profile = await botCreator.GenerateProfile(bot, cancellationToken.Token, true);

            Logger.LogDebug("Generating Profile: " + profile.Info.Settings.Role + " and side: " + side);
            GetWildSpawnArray(wildSpawnType).Add(profile);
        }

        private async void Update()
        {
            timeSinceLastProfileGeneration += Time.deltaTime;

            if (timeSinceLastProfileGeneration >= profileGenerationInterval)
            {
                timeSinceLastProfileGeneration = 0.0f;
                await CheckAndGenerateProfiles(sptbear, pmcBotCount);
                await CheckAndGenerateProfiles(sptusec, pmcBotCount);
                await CheckAndGenerateProfiles(assault, scavBotCount);

                
            }

            //need to check if its not in raid and then destroy the component for second raid
        }

        private async Task CheckAndGenerateProfiles(List<Profile> profileList, int botCount)
        {
            if (profileList.Count != botCount)
            {
                if (profileList[0].Info.Settings.Role == sptUsec)
                {
                    await FillProfile(sptUsec);
                }
                else if (profileList[0].Info.Settings.Role == sptBear)
                {
                    await FillProfile(sptBear);
                }
                else if (profileList[0].Info.Settings.Role == WildSpawnType.assault)
                {
                    await FillProfile(WildSpawnType.assault);
                }
            }
        }*/

        internal static GClass628 GetWildSpawnData(WildSpawnType spawnType, BotDifficulty botDifficulty)
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
                        return sptusecEasy;
                    }
                    else
                    {
                        return sptbearEasy;
                    }

                case BotDifficulty.normal:
                    if (spawnType == WildSpawnType.assault)
                    {
                        return assaultNormal;
                    }
                    else if (spawnType == sptUsec)
                    {
                        return sptusecNormal;
                    }
                    else
                    {
                        return sptbearNormal;
                    }

                case BotDifficulty.hard:
                    if (spawnType == WildSpawnType.assault)
                    {
                        return assaultHard;
                    }
                    else if (spawnType == sptUsec)
                    {
                        return sptusecHard;
                    }
                    else
                    {
                        return sptbearHard;
                    }

                case BotDifficulty.impossible:
                    if (spawnType == WildSpawnType.assault)
                    {
                        return assaultImpossible;
                    }
                    else if (spawnType == sptUsec)
                    {
                        return sptusecImpossible;
                    }
                    else
                    {
                        return sptbearImpossible;
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
