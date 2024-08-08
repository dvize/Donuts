using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFT;

namespace Donuts.Models
{
    internal class WildSpawnTypeDictionaries
    {
        internal static readonly Dictionary<WildSpawnType, EPlayerSide> WildSpawnTypeToEPlayerSide = new Dictionary<WildSpawnType, EPlayerSide>
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
            { WildSpawnType.gifter, EPlayerSide.Savage },
            { WildSpawnType.marksman, EPlayerSide.Savage },
            { WildSpawnType.pmcBot, EPlayerSide.Savage },
            { WildSpawnType.sectantPriest, EPlayerSide.Savage },
            { WildSpawnType.sectantWarrior, EPlayerSide.Savage },
            { WildSpawnType.followerBigPipe, EPlayerSide.Savage },
            { WildSpawnType.followerBirdEye, EPlayerSide.Savage },
            { WildSpawnType.bossKnight, EPlayerSide.Savage },
            { WildSpawnType.pmcUSEC, EPlayerSide.Usec },
            { WildSpawnType.pmcBEAR, EPlayerSide.Bear }
        };


        // Static dictionary mapping string representation to WildSpawnType
        internal static readonly Dictionary<string, EFT.WildSpawnType> StringToWildSpawnType = new Dictionary<string, EFT.WildSpawnType>
        {
            { "arenafighterevent", EFT.WildSpawnType.arenaFighterEvent },
            { "assault", EFT.WildSpawnType.assault },
            { "assaultgroup", EFT.WildSpawnType.assaultGroup },
            { "bossboar", EFT.WildSpawnType.bossBoar },
            { "bossboarsniper", EFT.WildSpawnType.bossBoarSniper },
            { "bossbully", EFT.WildSpawnType.bossBully },
            { "bossgluhar", EFT.WildSpawnType.bossGluhar },
            { "bosskilla", EFT.WildSpawnType.bossKilla },
            { "bosskojaniy", EFT.WildSpawnType.bossKojaniy },
            { "bosssanitar", EFT.WildSpawnType.bossSanitar },
            { "bosstagilla", EFT.WildSpawnType.bossTagilla },
            { "bosszryachiy", EFT.WildSpawnType.bossZryachiy },
            { "crazyassaultevent", EFT.WildSpawnType.crazyAssaultEvent },
            { "cursedassault", EFT.WildSpawnType.cursedAssault },
            { "exusec", EFT.WildSpawnType.exUsec },
            { "followerboar", EFT.WildSpawnType.followerBoar },
            { "followerbully", EFT.WildSpawnType.followerBully },
            { "followergluharassault", EFT.WildSpawnType.followerGluharAssault },
            { "followergluharscout", EFT.WildSpawnType.followerGluharScout },
            { "followergluharsecurity", EFT.WildSpawnType.followerGluharSecurity },
            { "followergluharsnipe", EFT.WildSpawnType.followerGluharSnipe },
            { "followerkojaniy", EFT.WildSpawnType.followerKojaniy },
            { "followersanitar", EFT.WildSpawnType.followerSanitar },
            { "followertagilla", EFT.WildSpawnType.followerTagilla },
            { "followerzryachiy", EFT.WildSpawnType.followerZryachiy },
            { "marksman", EFT.WildSpawnType.marksman },
            { "raiders", EFT.WildSpawnType.pmcBot },
            { "sectantpriest", EFT.WildSpawnType.sectantPriest },
            { "sectantwarrior", EFT.WildSpawnType.sectantWarrior },
            { "usec", EFT.WildSpawnType.pmcUSEC },
            { "pmcusec", EFT.WildSpawnType.pmcUSEC },
            { "bear", EFT.WildSpawnType.pmcBEAR },
            { "pmcbear", EFT.WildSpawnType.pmcBEAR },
            { "followerbigpipe", EFT.WildSpawnType.followerBigPipe },
            { "followerbirdeye", EFT.WildSpawnType.followerBirdEye },
            { "bossknight", EFT.WildSpawnType.bossKnight },
            { "gifter", EFT.WildSpawnType.gifter }
        };

        internal List<WildSpawnType> validDespawnListPMC = new List<WildSpawnType>()
        {
            WildSpawnType.pmcUSEC,
            WildSpawnType.pmcBEAR
        };

        internal List<WildSpawnType> validDespawnListScav = new List<WildSpawnType>()
        {
            WildSpawnType.assault,
            WildSpawnType.cursedAssault
        };

    }
}
