using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aki.PrePatch;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
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

        private int pmcBotCount = 15;
        private int scavBotCount = 10;
        private int bossBotCount = 1;

        internal static List<Profile> sptbear;
        internal static List<Profile> sptusec;
        internal static List<Profile> assault;
        internal static List<Profile> bossbully;
        internal static List<Profile> bossgluhar;
        internal static List<Profile> bosskilla;
        internal static List<Profile> bosskojaniy;
        internal static List<Profile> bosssanitar;
        internal static List<Profile> bosstagilla;
        internal static List<Profile> bosszryachiy;
        internal static List<Profile> followerbigpipe;
        internal static List<Profile> followerbirdeye;
        internal static List<Profile> bossknight;
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

            sptbear = new List<Profile>();
            sptusec = new List<Profile>();
            assault = new List<Profile>();
            bossbully = new List<Profile>();
            bossgluhar = new List<Profile>();
            bosskilla = new List<Profile>();
            bosskojaniy = new List<Profile>();
            bosssanitar = new List<Profile>();
            bosstagilla = new List<Profile>();
            bosszryachiy = new List<Profile>();
            followerbigpipe = new List<Profile>();
            followerbirdeye = new List<Profile>();
            bossknight = new List<Profile>();

            //generate the starting profiles needed
            for (int i = 0; i < pmcBotCount; i++)
            {
                await FillProfile(sptUsec);
                await FillProfile(sptBear);
            }
            for (int i = 0; i < scavBotCount; i++)
            {
                await FillProfile(WildSpawnType.assault);
            }

            for (int i = 0; i < bossBotCount; i++)
            {
                await FillProfile(WildSpawnType.bossBully);
                await FillProfile(WildSpawnType.bossGluhar);
                await FillProfile(WildSpawnType.bossKilla);
                await FillProfile(WildSpawnType.bossKojaniy);
                await FillProfile(WildSpawnType.bossSanitar);
                await FillProfile(WildSpawnType.bossTagilla);
                await FillProfile(WildSpawnType.bossZryachiy);
                await FillProfile(WildSpawnType.followerBigPipe);
                await FillProfile(WildSpawnType.followerBirdEye);
                await FillProfile(WildSpawnType.bossKnight);
            }
        }
        private async Task FillProfile(WildSpawnType wildSpawnType)
        {
            //assumes limits were checked so just generate a profile to be used
            var botdifficulty = grabBotDifficulty();
            var side = GetSideForWildSpawnType(wildSpawnType);

            IBotData botData = new GClass629(side, wildSpawnType, botdifficulty, 0f, null);
            GClass628 bot = await GClass628.Create(botData, botCreator, 1, botSpawnerClass);

            var profile = await botCreator.GenerateProfile(bot, cancellationToken.Token, false);

            Logger.LogDebug("Generating Profile: " + profile.Info.Settings.Role + " and side: " + side);
            GetWildSpawnArray(wildSpawnType).Add(profile);
        }

        private async void Update()
        {
            timeSinceLastProfileGeneration += Time.deltaTime;

            if (timeSinceLastProfileGeneration >= profileGenerationInterval)
            {
                timeSinceLastProfileGeneration = 0.0f;
                //check what profiles are low and add to queue to generate profile every 20 seconds?
                await CheckAndGenerateProfiles(sptbear, pmcBotCount);
                await CheckAndGenerateProfiles(sptusec, pmcBotCount);
                await CheckAndGenerateProfiles(assault, scavBotCount);

                //don't care after initial spawn for now
                /*await CheckAndGenerateProfiles(bossbully, bossBotCount);
                await CheckAndGenerateProfiles(bossgluhar, bossBotCount);
                await CheckAndGenerateProfiles(bosskilla, bossBotCount);
                await CheckAndGenerateProfiles(bosskojaniy, bossBotCount);
                await CheckAndGenerateProfiles(bosssanitar, bossBotCount);
                await CheckAndGenerateProfiles(bosstagilla, bossBotCount);
                await CheckAndGenerateProfiles(bosszryachiy, bossBotCount);
                await CheckAndGenerateProfiles(followerbigpipe, bossBotCount);
                await CheckAndGenerateProfiles(followerbirdeye, bossBotCount);
                await CheckAndGenerateProfiles(bossknight, bossBotCount);*/
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
        }

        internal static List<Profile> GetWildSpawnArray(WildSpawnType spawnType)
        {
            switch (spawnType)
            {
                case WildSpawnType.assault:
                    return assault;
                case WildSpawnType.bossBully:
                    return bossbully;
                case WildSpawnType.bossGluhar:
                    return bossgluhar;
                case WildSpawnType.bossKilla:
                    return bosskilla;
                case WildSpawnType.bossKojaniy:
                    return bosskojaniy;
                case WildSpawnType.bossSanitar:
                    return bosssanitar;
                case WildSpawnType.bossTagilla:
                    return bosstagilla;
                case WildSpawnType.bossZryachiy:
                    return bosszryachiy;
                case WildSpawnType.followerBigPipe:
                    return followerbigpipe;
                case WildSpawnType.followerBirdEye:
                    return followerbirdeye;
                case WildSpawnType.bossKnight:
                    return bossknight;
                default:
                    if (spawnType == sptUsec)
                    {
                        return sptusec;
                    }
                    else if (spawnType == sptBear)
                    {
                        return sptbear;
                    }
                    else if (spawnType == WildSpawnType.pmcBot)
                    {
                        return sptusec;
                    }
                    else
                    {
                        return assault;
                    }
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
