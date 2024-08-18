using System;
using System.Collections.Generic;
using EFT;

namespace Donuts.Models
{
    internal class WildSpawnTypeDictionaries
    {
        internal static readonly Dictionary<string, string> BossNameToConfigName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "bossboar", "Kaban" },
            { "bossbully", "Reshala" },
            { "bossgluhar", "Glukhar" },
            { "bosskilla", "Killa" },
            { "bosskojaniy", "Reshala" },
            { "bosskolontay", "Kollontay" },
            { "bosssanitar", "Sanitar" },
            { "bosstagilla", "Tagilla" },
            { "bosszryachiy", "Zryachiy" },
            { "exusec", "Rogues" },
            { "pmcbot", "Raiders" },
            { "sectantpriest", "Cultists" },
            { "bossknight", "Knight" }
        };

        internal static readonly Dictionary<string, string> MapNameToConfigName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "factory4_day", "Factory" },
            { "factory4_night", "Factory Night" },
            { "bigmap", "Customs" },
            { "woods", "Woods" },
            { "shoreline", "Shoreline" },
            { "lighthouse", "Lighthouse" },
            { "rezervbase", "Reserve" },
            { "interchange", "Interchange" },
            { "laboratory", "Laboratory" },
            { "tarkovstreets", "Streets" },
            { "sandbox", "Ground Zero" },
            { "sandbox_high", "Ground Zero High" }
        };

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
            { WildSpawnType.bossKolontay, EPlayerSide.Savage },
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
            { WildSpawnType.followerKolontaySecurity, EPlayerSide.Savage },
            { WildSpawnType.followerKolontayAssault, EPlayerSide.Savage },
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
            { "bosskolontay", EFT.WildSpawnType.bossKolontay },
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
            { "followerkolontayassault", EFT.WildSpawnType.followerKolontayAssault },
            { "followerkolontaysecurity", EFT.WildSpawnType.followerKolontaySecurity },
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

        private static Dictionary<WildSpawnType, (bool IsBoss, bool IsFollower)> wildSpawnTypeDictionary = new Dictionary<WildSpawnType, (bool, bool)>
        {
            { WildSpawnType.crazyAssaultEvent, (false, false) },
            { WildSpawnType.assault, (false, false) },
            { WildSpawnType.skier, (false, false) },
            { WildSpawnType.peacemaker, (false, false) },
            { WildSpawnType.cursedAssault, (false, false) },
            { WildSpawnType.test, (false, false) },
            { WildSpawnType.spiritWinter, (false, false) },
            { WildSpawnType.spiritSpring, (false, false) },
            { WildSpawnType.marksman, (false, false) },
            { WildSpawnType.assaultGroup, (false, false) },
            { WildSpawnType.sectantPriest, (true, false) },
            { WildSpawnType.sectantWarrior, (false, true) },
            { WildSpawnType.arenaFighterEvent, (true, true) },
            { WildSpawnType.pmcUSEC, (false, false) },
            { WildSpawnType.pmcBEAR, (false, false) },
            { WildSpawnType.pmcBot, (true, true) },
            { WildSpawnType.arenaFighter, (true, true) },
            { WildSpawnType.exUsec, (true, true) },
            { WildSpawnType.shooterBTR, (true, true) },
            { WildSpawnType.bossBoarSniper, (false, true) },
            { WildSpawnType.bossBoar, (true, false) },
            { WildSpawnType.bossKolontay, (true, false) },
            { WildSpawnType.bossBully, (true, false) },
            { WildSpawnType.bossGluhar, (true, false) },
            { WildSpawnType.bossKilla, (true, false) },
            { WildSpawnType.bossKnight, (true, false) },
            { WildSpawnType.bossKojaniy, (true, false) },
            { WildSpawnType.bossSanitar, (true, false) },
            { WildSpawnType.bossTagilla, (true, false) },
            { WildSpawnType.bossZryachiy, (true, true) },
            { WildSpawnType.peacefullZryachiyEvent, (true, true) },
            { WildSpawnType.ravangeZryachiyEvent, (true, true) },
            { WildSpawnType.sectactPriestEvent, (true, true) },
            { WildSpawnType.bossTest, (true, false) },
            { WildSpawnType.gifter, (true, false) },
            { WildSpawnType.followerBoarClose1, (false, true) },
            { WildSpawnType.followerBoarClose2, (false, true) },
            { WildSpawnType.followerBoar, (false, true) },
            { WildSpawnType.followerZryachiy, (true, true) },
            { WildSpawnType.followerBigPipe, (true, true) },
            { WildSpawnType.followerBirdEye, (true, true) },
            { WildSpawnType.followerBully, (false, true) },
            { WildSpawnType.followerKolontaySecurity, (false, true) },
            { WildSpawnType.followerKolontayAssault, (false, true) },
            { WildSpawnType.followerGluharAssault, (false, true) },
            { WildSpawnType.followerGluharScout, (false, true) },
            { WildSpawnType.followerGluharSecurity, (false, true) },
            { WildSpawnType.followerGluharSnipe, (false, true) },
            { WildSpawnType.followerKojaniy, (false, true) },
            { WildSpawnType.followerSanitar, (false, true) },
            { WildSpawnType.followerTagilla, (false, true) },
            { WildSpawnType.followerTest, (false, true) }
        };

        // Example usage:
        public static bool IsBoss(WildSpawnType type) => wildSpawnTypeDictionary.TryGetValue(type, out var result) && result.IsBoss;

        public static bool IsFollower(WildSpawnType type) => wildSpawnTypeDictionary.TryGetValue(type, out var result) && result.IsFollower;

    }
}
