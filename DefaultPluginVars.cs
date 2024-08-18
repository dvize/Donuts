using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Donuts.Models;
using Newtonsoft.Json;
using UnityEngine;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal static class DefaultPluginVars
    {
        // Main Settings
        internal static Setting<bool> PluginEnabled;
        internal static Setting<bool> DespawnEnabledPMC;
        internal static Setting<bool> DespawnEnabledSCAV;
        internal static Setting<bool> HardCapEnabled;
        internal static Setting<float> coolDownTimer;
        internal static Setting<int> bossWaveCooldownTimer;
        internal static Setting<string> pmcGroupChance;
        internal static Setting<string> scavGroupChance;
        internal static Setting<string> botDifficultiesPMC;
        internal static Setting<string> botDifficultiesSCAV;
        internal static Setting<string> botDifficultiesOther;
        internal static Setting<bool> ShowRandomFolderChoice;
        internal static Setting<string> pmcFaction;
        internal static Setting<string> forceAllBotType;

        internal static Setting<bool> useTimeBasedHardStop;
        internal static Setting<bool> hardStopOptionPMC;
        internal static Setting<int> hardStopTimePMC;
        internal static Setting<int> hardStopPercentPMC;
        internal static Setting<bool> hardStopOptionSCAV;
        internal static Setting<int> hardStopTimeSCAV;
        internal static Setting<int> hardStopPercentSCAV;

        internal static Setting<bool> hotspotBoostPMC;
        internal static Setting<bool> hotspotBoostSCAV;
        internal static Setting<bool> hotspotIgnoreHardCapPMC;
        internal static Setting<bool> hotspotIgnoreHardCapSCAV;
        internal static Setting<int> pmcFactionRatio;
        internal static Setting<float> battleStateCoolDown;
        internal static Setting<int> maxRespawnsPMC;
        internal static Setting<int> maxRespawnsSCAV;

        // Bosses
        internal static Dictionary<string, Setting<bool>> BossUseGlobalSpawnChance;
        internal static Dictionary<string, Dictionary<string, Setting<int>>> BossSpawnChances;

        // Global Minimum Spawn Distance From Player
        internal static Setting<bool> globalMinSpawnDistanceFromPlayerBool;
        internal static Setting<float> globalMinSpawnDistanceFromPlayerFactory;
        internal static Setting<float> globalMinSpawnDistanceFromPlayerCustoms;
        internal static Setting<float> globalMinSpawnDistanceFromPlayerReserve;
        internal static Setting<float> globalMinSpawnDistanceFromPlayerStreets;
        internal static Setting<float> globalMinSpawnDistanceFromPlayerWoods;
        internal static Setting<float> globalMinSpawnDistanceFromPlayerLaboratory;
        internal static Setting<float> globalMinSpawnDistanceFromPlayerShoreline;
        internal static Setting<float> globalMinSpawnDistanceFromPlayerGroundZero;
        internal static Setting<float> globalMinSpawnDistanceFromPlayerInterchange;
        internal static Setting<float> globalMinSpawnDistanceFromPlayerLighthouse;

        // Global Minimum Spawn Distance From Other Bots
        internal static Setting<bool> globalMinSpawnDistanceFromOtherBotsBool;
        internal static Setting<float> globalMinSpawnDistanceFromOtherBotsFactory;
        internal static Setting<float> globalMinSpawnDistanceFromOtherBotsCustoms;
        internal static Setting<float> globalMinSpawnDistanceFromOtherBotsReserve;
        internal static Setting<float> globalMinSpawnDistanceFromOtherBotsStreets;
        internal static Setting<float> globalMinSpawnDistanceFromOtherBotsWoods;
        internal static Setting<float> globalMinSpawnDistanceFromOtherBotsLaboratory;
        internal static Setting<float> globalMinSpawnDistanceFromOtherBotsShoreline;
        internal static Setting<float> globalMinSpawnDistanceFromOtherBotsGroundZero;
        internal static Setting<float> globalMinSpawnDistanceFromOtherBotsInterchange;
        internal static Setting<float> globalMinSpawnDistanceFromOtherBotsLighthouse;

        // Advanced Settings
        internal static Setting<float> replenishInterval;
        internal static Setting<int> maxSpawnTriesPerBot;
        internal static Setting<float> despawnInterval;
        internal static Setting<string> groupWeightDistroLow;
        internal static Setting<string> groupWeightDistroDefault;
        internal static Setting<string> groupWeightDistroHigh;
        internal static Setting<float> maxRaidDelay;

        // Debugging
        internal static Setting<bool> DebugGizmos;
        internal static Setting<bool> gizmoRealSize;

        // Spawn Point Maker
        internal static Setting<string> spawnName;
        internal static Setting<int> groupNum;
        internal static Setting<string> wildSpawns;
        internal static Setting<float> minSpawnDist;
        internal static Setting<float> maxSpawnDist;
        internal static Setting<float> botTriggerDistance;
        internal static Setting<float> botTimerTrigger;
        internal static Setting<int> maxRandNumBots;
        internal static Setting<int> spawnChance;
        internal static Setting<int> maxSpawnsBeforeCooldown;
        internal static Setting<bool> ignoreTimerFirstSpawn;
        internal static Setting<float> minSpawnDistanceFromPlayer;
        internal static Setting<KeyCode> CreateSpawnMarkerKey;
        internal static Setting<KeyCode> DeleteSpawnMarkerKey;

        internal static string[] wildSpawnTypes =
        {
            "arenafighterevent",
            "assault",
            "assaultgroup",
            "bossboar",
            "bossboarsniper",
            "bossbully",
            "bossgluhar",
            "bosskilla",
            "bossknight",
            "bosskojaniy",
            "bosssanitar",
            "bosstagilla",
            "bosszryachiy",
            "crazyassaultevent",
            "cursedassault",
            "exusec-rogues",
            "raiders",
            "followerbigpipe",
            "followerbirdeye",
            "followerboar",
            "followerbully",
            "followergluharassault",
            "followergluharscout",
            "followergluharsecurity",
            "followergluharsnipe",
            "followerkojaniy",
            "followersanitar",
            "followertagilla",
            "followerzryachiy",
            "gifter",
            "marksman",
            "pmc",
            "pmcBEAR",
            "pmcUSEC",
            "sectantpriest",
            "sectantwarrior",
        };


        // Save Settings
        internal static Setting<bool> saveNewFileOnly;
        internal static Setting<KeyCode> WriteToFileKey;

        public static Dictionary<string, int[]> groupChanceWeights = new Dictionary<string, int[]>
        {
            { "Low", new int[] { 400, 90, 9, 0, 0 } },
            { "Default", new int[] { 210, 210, 45, 25, 10 } },
            { "High", new int[] { 0, 75, 175, 175, 75 } }
        };

        static string defaultWeightsString = ConvertIntArrayToString(groupChanceWeights["Default"]);
        static string lowWeightsString = ConvertIntArrayToString(groupChanceWeights["Low"]);
        static string highWeightsString = ConvertIntArrayToString(groupChanceWeights["High"]);

        internal static string[] pmcGroupChanceList = { "None", "Default", "Low", "High", "Max", "Random" };
        internal static string[] scavGroupChanceList = { "None", "Default", "Low", "High", "Max", "Random" };
        internal static string[] pmcFactionList = { "Default", "USEC", "BEAR" };
        internal static string[] forceAllBotTypeList = { "Disabled", "SCAV", "PMC" };

        internal static string ConvertIntArrayToString(int[] array)
        {
            return string.Join(",", array);
        }

        //IMGUI Vars
        internal static int selectedTabIndex = 0;
        internal static int selectedSubTabIndex = 0;
        internal static string[] tabNames = { "Main Settings", "Spawn Settings", "Boss Settings", "Advanced Settings", "SpawnPoint Maker", "Debugging" };
        internal static bool showGUI = false;
        internal static string[] botDiffList = { "AsOnline", "Easy", "Normal", "Hard", "Impossible" };

        internal static Rect windowRect = new Rect(20, 20, 1664, 936);  // Default position and size

        //Scenario Selection
        internal static List<Folder> pmcScenarios = new List<Folder>();
        internal static List<Folder> pmcRandomScenarios = new List<Folder>();
        internal static List<Folder> scavScenarios = new List<Folder>();
        internal static List<Folder> randomScavScenarios = new List<Folder>();


        internal static Setting<string> pmcScenarioSelection;
        internal static Setting<string> scavScenarioSelection;
        internal static string[] pmcScenarioCombinedArray;
        internal static string[] scavScenarioCombinedArray;

        // Temporarily store the scenario selections to initialize them later
        internal static string pmcScenarioSelectionValue = null;
        internal static string scavScenarioSelectionValue = null;

            // Boss settings
        internal static string[] bossNames = {
                "Cultists", "Goons", "Glukhar", "Kaban", "Killa", "Kollontay",
                "Raiders", "Reshala", "Rogues", "Sanitar", "Shturman", "Tagilla", "Zryachiy"
            };

        internal static string[] mapNames = {
                "Factory", "Customs", "Reserve", "Streets", "Woods", "Laboratory",
                "Shoreline", "Ground Zero", "Interchange", "Lighthouse"
            };

        internal static Dictionary<string, Dictionary<string, int>> defaultSpawnChances = new Dictionary<string, Dictionary<string, int>>
            {
                {"Cultists", new Dictionary<string, int> {
                    {"Factory", 2},
                    {"Customs", 20},
                    {"Reserve", 0},
                    {"Streets", 0},
                    {"Woods", 20},
                    {"Laboratory", 0},
                    {"Shoreline", 15},
                    {"Ground Zero", 2},
                    {"Interchange", 0},
                    {"Lighthouse", 0}
                }},
                {"Goons", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 40},
                    {"Reserve", 0},
                    {"Streets", 0},
                    {"Woods", 40},
                    {"Laboratory", 0},
                    {"Shoreline", 40},
                    {"Ground Zero", 0},
                    {"Interchange", 0},
                    {"Lighthouse", 40}
                }},
                {"Glukhar", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 0},
                    {"Reserve", 40},
                    {"Streets", 0},
                    {"Woods", 0},
                    {"Laboratory", 0},
                    {"Shoreline", 0},
                    {"Ground Zero", 0},
                    {"Interchange", 0},
                    {"Lighthouse", 0}
                }},
                {"Kaban", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 0},
                    {"Reserve", 0},
                    {"Streets", 40},
                    {"Woods", 0},
                    {"Laboratory", 0},
                    {"Shoreline", 0},
                    {"Ground Zero", 0},
                    {"Interchange", 0},
                    {"Lighthouse", 0}
                }},
                {"Killa", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 0},
                    {"Reserve", 0},
                    {"Streets", 0},
                    {"Woods", 0},
                    {"Laboratory", 0},
                    {"Shoreline", 0},
                    {"Ground Zero", 0},
                    {"Interchange", 40},
                    {"Lighthouse", 0}
                }},
                {"Kollontay", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 0},
                    {"Reserve", 0},
                    {"Streets", 40},
                    {"Woods", 0},
                    {"Laboratory", 0},
                    {"Shoreline", 0},
                    {"Ground Zero", 40},
                    {"Interchange", 0},
                    {"Lighthouse", 0}
                }},
                {"Raiders", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 0},
                    {"Reserve", 0},
                    {"Streets", 0},
                    {"Woods", 0},
                    {"Laboratory", 0},
                    {"Shoreline", 0},
                    {"Ground Zero", 0},
                    {"Interchange", 0},
                    {"Lighthouse", 0}
                }},
                {"Reshala", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 40},
                    {"Reserve", 0},
                    {"Streets", 0},
                    {"Woods", 0},
                    {"Laboratory", 0},
                    {"Shoreline", 0},
                    {"Ground Zero", 0},
                    {"Interchange", 0},
                    {"Lighthouse", 0}
                }},
                {"Rogues", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 0},
                    {"Reserve", 0},
                    {"Streets", 0},
                    {"Woods", 0},
                    {"Laboratory", 0},
                    {"Shoreline", 0},
                    {"Ground Zero", 0},
                    {"Interchange", 0},
                    {"Lighthouse", 0}
                }},
                {"Sanitar", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 0},
                    {"Reserve", 0},
                    {"Streets", 0},
                    {"Woods", 0},
                    {"Laboratory", 0},
                    {"Shoreline", 40},
                    {"Ground Zero", 0},
                    {"Interchange", 0},
                    {"Lighthouse", 0}
                }},
                {"Shturman", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 0},
                    {"Reserve", 0},
                    {"Streets", 0},
                    {"Woods", 40},
                    {"Laboratory", 0},
                    {"Shoreline", 0},
                    {"Ground Zero", 0},
                    {"Interchange", 0},
                    {"Lighthouse", 0}
                }},
                {"Tagilla", new Dictionary<string, int> {
                    {"Factory", 40},
                    {"Customs", 0},
                    {"Reserve", 0},
                    {"Streets", 0},
                    {"Woods", 0},
                    {"Laboratory", 0},
                    {"Shoreline", 0},
                    {"Ground Zero", 0},
                    {"Interchange", 0},
                    {"Lighthouse", 0}
                }},
                {"Zryachiy", new Dictionary<string, int> {
                    {"Factory", 0},
                    {"Customs", 0},
                    {"Reserve", 0},
                    {"Streets", 0},
                    {"Woods", 0},
                    {"Laboratory", 0},
                    {"Shoreline", 0},
                    {"Ground Zero", 0},
                    {"Interchange", 0},
                    {"Lighthouse", 100}
                }}
            };

        //Default Constructor
        static DefaultPluginVars()
        {
            // Boss Settings
            BossUseGlobalSpawnChance = new Dictionary<string, Setting<bool>>();
            BossSpawnChances = new Dictionary<string, Dictionary<string, Setting<int>>>();

            foreach (var bossName in WildSpawnTypeDictionaries.BossNameToConfigName.Values)
            {
                BossUseGlobalSpawnChance[bossName] = new Setting<bool>(
                    "Use Global Spawn Chance",
                    $"Use Global Spawn Chance for {bossName}",
                    true,
                    true);

                BossSpawnChances[bossName] = new Dictionary<string, Setting<int>>();

                foreach (var mapName in WildSpawnTypeDictionaries.MapNameToConfigName.Values)
                {
                    int defaultChance = defaultSpawnChances.TryGetValue(bossName, out var bossChances) && 
                                        bossChances.TryGetValue(mapName, out var chance) ? chance : 0;

                    BossSpawnChances[bossName][mapName] = new Setting<int>(
                        $"{mapName}",
                        $"Spawn chance for {bossName} on {mapName}",
                        defaultChance,
                        defaultChance,
                        0,
                        100
                    );
                }
            }

            // Main Settings
            PluginEnabled = new Setting<bool>(
                "Donuts On",
                "Enable/Disable Spawning from Donuts Points",
                true,
                true);

            DespawnEnabledPMC = new Setting<bool>(
                "Despawn PMCs",
                "When enabled, removes furthest PMC bots from player for each new dynamic spawn bot that is over your Donuts bot caps (ScenarioConfig.json).",
                true,
                true);

            DespawnEnabledSCAV = new Setting<bool>(
                "Despawn SCAVs",
                "When enabled, removes furthest SCAV bots from player for each new dynamic spawn bot that is over your Donuts bot caps (ScenarioConfig.json).",
                true,
                true);

            HardCapEnabled = new Setting<bool>(
                "Bot Hard Cap Option",
                "When enabled, all bot spawns will be hard capped by your preset caps. In other words, if your bot count is at the total Donuts cap then no more bots will spawn until one dies (vanilla SPT behavior).",
                false,
                false);

            coolDownTimer = new Setting<float>(
                "Cool Down Timer",
                "Cool Down Timer for after a spawn has successfully spawned a bot the spawn marker's MaxSpawnsBeforeCoolDown",
                300f,
                300f,
                0f,
                1000f);

            bossWaveCooldownTimer = new Setting<int>(
                "Boss Wave Cool Down Timer",
                "How long to delay before it can check to see whether to spawn bosses after MaxSpawnsBeforeCoolDown is reached (should be a high value).",
                600,
                600,
                0,
                3000
                );

            pmcGroupChance = new Setting<string>(
                "Donuts PMC Group Chance",
                "Setting to determine the odds of PMC groups and group size. All odds are configurable, check Advanced Settings above. See mod page for more details.",
                "Default",
                "Default",
                null,
                null,
                pmcGroupChanceList);

            scavGroupChance = new Setting<string>(
                "Donuts SCAV Group Chance",
                "Setting to determine the odds of SCAV groups and group size. All odds are configurable, check Advanced Settings above. See mod page for more details. See mod page for more details.",
                "Default",
                "Default",
                null,
                null,
                scavGroupChanceList);

            botDifficultiesPMC = new Setting<string>(
                "Donuts PMC Spawn Difficulty",
                "Difficulty Setting for All PMC Donuts Related Spawns",
                "Normal",
                "Normal",
                null,
                null,
                botDiffList);

            botDifficultiesSCAV = new Setting<string>(
                "Donuts SCAV Spawn Difficulty",
                "Difficulty Setting for All SCAV Donuts Related Spawns",
                "Normal",
                "Normal",
                null,
                null,
                botDiffList
                );

            botDifficultiesOther = new Setting<string>(
                "Other Bot Type Spawn Difficulty",
                "Difficulty Setting for all other bot types spawned with Donuts, such as bosses, Rogues, Raiders, etc.",
                "Normal",
                "Normal",
                null,
                null,
                botDiffList
                );

            ShowRandomFolderChoice = new Setting<bool>(
                "Show Spawn Preset Selection",
                "Shows the Spawn Preset Selected on Raid Start in bottom right",
                true,
                true);

            pmcFaction = new Setting<string>(
                "Force PMC Faction",
                "Force a specific faction for all PMC spawns or use the default specified faction in the Donuts spawn files. Default is a random faction.",
                "Default",
                "Default",
                null,
                null,
                pmcFactionList
                );

            forceAllBotType = new Setting<string>(
                "Force Bot Type for All Spawns",
                "Force a specific bot type for all spawns - this option converts all defined starting spawns and waves to the specified bot type. Default is Disabled.",
                "Disabled",
                "Disabled",
                null,
                null,
                forceAllBotTypeList
                );

            useTimeBasedHardStop = new Setting<bool>(
                "Use Time-Based Hard Stop",
                "If enabled, the hard stop settings will be the time (in seconds) left in raid (configurable below). If disabled, the hard stop settings will be the percentage of time left in raid (configurable below).",
                true,
                true);

            hardStopOptionPMC = new Setting<bool>(
                "PMC Spawn Hard Stop",
                "If enabled, all PMC spawns stop completely once there is n time or percentage time left in your raid. This is configurable in either seconds or percentage (see below).",
                false,
                false);

            hardStopPercentPMC = new Setting<int>(
                "PMC Spawn Hard Stop: Percent Left of Raid",
                "The percentage of time left in your raid that will stop any further PMC spawns (if option is enabled). Default is 50 percent of the full raid time.",
                50,
                50,
                0,
                100);

            hardStopTimePMC = new Setting<int>(
                "PMC Spawn Hard Stop: Time Left in Raid",
                "The time (in seconds) left in your raid that will stop any further PMC spawns (if option is enabled). Default is 300 (5 minutes).",
                300,
                300);

            hardStopOptionSCAV = new Setting<bool>(
                "SCAV Spawn Hard Stop",
                "If enabled, all SCAV spawns stop completely once there is n time or percentage time left in your raid. This is configurable in either seconds or percentage (see below).",
                false,
                false);

            hardStopTimeSCAV = new Setting<int>(
                "SCAV Spawn Hard Stop: Time Left in Raid",
                "The time (in seconds) left in your raid that will stop any further SCAV spawns (if option is enabled). Default is 300 (5 minutes).",
                300,
                300);

            hardStopPercentSCAV = new Setting<int>(
                "SCAV Spawn Hard Stop: Percent Left of Raid",
                "The percentage of time left in your raid that will stop any further SCAV spawns (if option is enabled). Default is 10 percent of the full raid time.",
                10,
                10,
                0,
                100);

            hotspotBoostPMC = new Setting<bool>(
                "PMC Hot Spot Spawn Boost",
                "If enabled, all hotspot points have a much higher chance of spawning more PMCs.",
                false,
                false);

            hotspotBoostSCAV = new Setting<bool>(
                "SCAV Hot Spot Spawn Boost",
                "If enabled, all hotspot points have a much higher chance of spawning more SCAVs.",
                false,
                false);

            hotspotIgnoreHardCapPMC = new Setting<bool>(
                "PMC Hot Spot Ignore Hard Cap",
                "If enabled, all hotspot spawn points will ignore the hard cap (if enabled). This applies to any spawn points labeled with 'Hotspot'. I recommended using this option with Despawn + Hardcap + Boost for the best experience with more action in hot spot areas.",
                false,
                false);

            hotspotIgnoreHardCapSCAV = new Setting<bool>(
                "SCAV Hot Spot Ignore Hard Cap",
                "If enabled, all hotspot spawn points will ignore the hard cap (if enabled). This applies to any spawn points labeled with 'Hotspot'. I recommended using this option with Despawn + Hardcap + Boost for the best experience with more action in hot spot areas.",
                false,
                false);

            pmcFactionRatio = new Setting<int>(
                "PMC Faction Ratio",
                "USEC/Bear Default Ratio. Default is 50%. Lower value = lower USEC chance, so: 20 would be 20% USEC, 80% Bear, etc.",
                50,
                50);

            battleStateCoolDown = new Setting<float>(
                "Battlestate Spawn CoolDown",
                "It will stop spawning bots until you haven't been hit for X amount of seconds\nas you are still considered being in battle",
                20f,
                20f);

            maxRespawnsPMC = new Setting<int>(
                "Maximum number of PMC respawns per raid",
                "Once Donuts has spawned this many PMCs in a raid it will skip all subsequent triggered PMC spawns. Default is 0 (unlimited)",
                0,
                0);

            maxRespawnsSCAV = new Setting<int>(
                "Maximum number of SCAV respawns per raid",
                "Once Donuts has spawned this many SCAVs in a raid it will skip all subsequent triggered SCAV spawns. Default is 0 (unlimited)",
                0,
                0);

            // Global Minimum Spawn Distance From Player
            #region global mininum spawn distances
            globalMinSpawnDistanceFromPlayerBool = new Setting<bool>(
                "Use Global Min Distance From Player",
                "If enabled, all spawns on all presets will use the global minimum spawn distance from player for each map defined below.",
                true,
                true);

            globalMinSpawnDistanceFromPlayerFactory = new Setting<float>(
                "Factory",
                "Distance (in meters) that bots should spawn away from the player (you).",
                35f,
                35f);

            globalMinSpawnDistanceFromPlayerCustoms = new Setting<float>(
                "Customs",
                "Distance (in meters) that bots should spawn away from the player (you).",
                60f,
                60f);

            globalMinSpawnDistanceFromPlayerReserve = new Setting<float>(
                "Reserve",
                "Distance (in meters) that bots should spawn away from the player (you).",
                80f,
                80f);

            globalMinSpawnDistanceFromPlayerStreets = new Setting<float>(
                "Streets",
                "Distance (in meters) that bots should spawn away from the player (you).",
                80f,
                80f);

            globalMinSpawnDistanceFromPlayerWoods = new Setting<float>(
                "Woods",
                "Distance (in meters) that bots should spawn away from the player (you).",
                110f,
                110f);

            globalMinSpawnDistanceFromPlayerLaboratory = new Setting<float>(
                "Laboratory",
                "Distance (in meters) that bots should spawn away from the player (you).",
                40f,
                40f);

            globalMinSpawnDistanceFromPlayerShoreline = new Setting<float>(
                "Shoreline",
                "Distance (in meters) that bots should spawn away from the player (you).",
                100f,
                100f);

            globalMinSpawnDistanceFromPlayerGroundZero = new Setting<float>(
                "Ground Zero",
                "Distance (in meters) that bots should spawn away from the player (you).",
                50f,
                50f);

            globalMinSpawnDistanceFromPlayerInterchange = new Setting<float>(
                "Interchange",
                "Distance (in meters) that bots should spawn away from the player (you).",
                85f,
                85f);

            globalMinSpawnDistanceFromPlayerLighthouse = new Setting<float>(
                "Lighthouse",
                "Distance (in meters) that bots should spawn away from the player (you).",
                70f,
                70f);

            // Global Minimum Spawn Distance From Other Bots
            globalMinSpawnDistanceFromOtherBotsBool = new Setting<bool>(
                "Use Global Min Distance From Other Bots",
                "If enabled, all spawns on all presets will use the global minimum spawn distance from player for each map defined below.",
                true,
                true);

            globalMinSpawnDistanceFromOtherBotsFactory = new Setting<float>(
                "Factory",
                "Distance (in meters) that bots should spawn away from other alive bots.",
                15f,
                15f);

            globalMinSpawnDistanceFromOtherBotsCustoms = new Setting<float>(
                "Customs",
                "Distance (in meters) that bots should spawn away from other alive bots.",
                40f,
                50f);

            globalMinSpawnDistanceFromOtherBotsReserve = new Setting<float>(
                "Reserve",
                "Distance (in meters) that bots should spawn away from other alive bots.",
                50f,
                50f);

            globalMinSpawnDistanceFromOtherBotsStreets = new Setting<float>(
                "Streets",
                "Distance (in meters) that bots should spawn away from other alive bots.",
                50f,
                50f);

            globalMinSpawnDistanceFromOtherBotsWoods = new Setting<float>(
                "Woods",
                "Distance (in meters) that bots should spawn away from other alive bots.",
                80f,
                80f);

            globalMinSpawnDistanceFromOtherBotsLaboratory = new Setting<float>(
                "Laboratory",
                "Distance (in meters) that bots should spawn away from other alive bots.",
                20f,
                20f);

            globalMinSpawnDistanceFromOtherBotsShoreline = new Setting<float>(
                "Shoreline",
                "Distance (in meters) that bots should spawn away from other alive bots.",
                60f,
                60f);

            globalMinSpawnDistanceFromOtherBotsGroundZero = new Setting<float>(
                "Ground Zero",
                "Distance (in meters) that bots should spawn away from other alive bots.",
                30f,
                30f);

            globalMinSpawnDistanceFromOtherBotsInterchange = new Setting<float>(
                "Interchange",
                "Distance (in meters) that bots should spawn away from other alive bots.",
                65f,
                65f);

            globalMinSpawnDistanceFromOtherBotsLighthouse = new Setting<float>(
                "Lighthouse",
                "Distance (in meters) that bots should spawn away from other alive bots.",
                60f,
                60f);
            #endregion

            // Advanced Settings
            maxRaidDelay = new Setting<float>(
                "Raid Load Time Delay",
                "Max time (in seconds) that Donuts force delays raid load so it has time to generate bot data for all starting points. This is to avoid potential bot spawn delays on raid start. This may delay Default is 60 seconds.",
                60f,
                60f,
                0f,
                120f);

            replenishInterval = new Setting<float>(
                "Bot Cache Replenish Interval",
                "The time interval for Donuts to re-fill its bot data cache. Leave default unless you know what you're doing.",
                10f,
                10f,
                0f,
                300f);

            maxSpawnTriesPerBot = new Setting<int>(
                "Max Spawn Tries Per Bot",
                "It will stop trying to spawn one of the bots after this many attempts to find a good spawn point. Lower is better",
                1,
                1,
                0,
                10);

            despawnInterval = new Setting<float>(
                "Despawn Bot Interval",
                "This value is the number in seconds that Donuts should despawn bots. Default is 15 seconds. Note: decreasing this value may affect your performance.",
                15f,
                15f,
                5f,
                600f);

            groupWeightDistroLow = new Setting<string>(
                "Group Chance Weights: Low",
                "Weight Distribution for Group Chance 'Low'. Use relative weights for group sizes 1/2/3/4/5, respectively. Use this formula: group weight / total weight = % chance.",
                lowWeightsString,
                lowWeightsString);

            groupWeightDistroDefault = new Setting<string>(
                "Group Chance Weights: Default",
                "Weight Distribution for Group Chance 'Default'. Use relative weights for group sizes 1/2/3/4/5, respectively. Use this formula: group weight / total weight = % chance.",
                defaultWeightsString,
                defaultWeightsString);

            groupWeightDistroHigh = new Setting<string>(
                "Group Chance Weights: High",
                "Weight Distribution for Group Chance 'High'. Use relative weights for group sizes 1/2/3/4/5, respectively. Use this formula: group weight / total weight = % chance.",
                highWeightsString,
                highWeightsString);

            // Debugging
            DebugGizmos = new Setting<bool>(
                "Enable Debug Markers",
                "When enabled, draws debug spheres on set spawn from json",
                false,
                false);

            gizmoRealSize = new Setting<bool>(
                "Debug Sphere Real Size",
                "When enabled, debug spheres will be the real size of the spawn radius",
                false,
                false);

            // Spawn Point Maker
            #region Spawn Point maker
            spawnName = new Setting<string>(
                "Name",
                "Name used to identify the spawn marker",
                "Spawn Name Here",
                "Spawn Name Here");

            groupNum = new Setting<int>(
                "Group Number",
                "Group Number used to identify the spawn marker",
                1,
                1,
                1,
                100);

            wildSpawns = new Setting<string>(
                "Wild Spawn Type",
                "Select an option.",
                "pmc",
                "pmc",
                null,
                null,
                wildSpawnTypes);

            minSpawnDist = new Setting<float>(
                "Min Spawn Distance",
                "Min Distance Bots will Spawn From Marker You Set.",
                1f,
                1f,
                0f,
                500f);

            maxSpawnDist = new Setting<float>(
                "Max Spawn Distance",
                "Max Distance Bots will Spawn From Marker You Set.",
                20f,
                20f,
                1f,
                1000f);

            botTriggerDistance = new Setting<float>(
                "Bot Spawn Trigger Distance",
                "Distance in which the player is away from the fight location point that it triggers bot spawn",
                100f,
                100f,
                0.1f,
                1000f);

            botTimerTrigger = new Setting<float>(
                "Bot Spawn Timer Trigger",
                "In seconds before it spawns next wave while player in the fight zone area",
                180f,
                180f,
                0f,
                10000f);

            maxRandNumBots = new Setting<int>(
                "Max Random Bots",
                "Maximum number of bots of Wild Spawn Type that can spawn on this marker",
                2,
                2,
                1,
                5);

            spawnChance = new Setting<int>(
                "Spawn Chance for Marker",
                "Chance bot will be spawn here after timer is reached",
                50,
                50,
                0,
                100);

            maxSpawnsBeforeCooldown = new Setting<int>(
                "Max Spawns Before Cooldown",
                "Number of successful spawns before this marker goes in cooldown",
                5,
                5,
                1,
                30);

            ignoreTimerFirstSpawn = new Setting<bool>(
                "Ignore Timer for First Spawn",
                "When enabled for this point, it will still spawn even if timer is not ready for first spawn only",
                false,
                false);

            minSpawnDistanceFromPlayer = new Setting<float>(
                "Min Spawn Distance From Player",
                "How far the random selected spawn near the spawn marker needs to be from player",
                40f,
                40f,
                0f,
                500f);

            CreateSpawnMarkerKey = new Setting<KeyCode>(
                "Create Spawn Marker Key",
                "Press this key to create a spawn marker at your current location",
                KeyCode.None,
                KeyCode.None);

            DeleteSpawnMarkerKey = new Setting<KeyCode>(
                "Delete Spawn Marker Key",
                "Press this key to delete closest spawn marker within 5m of your player location",
                KeyCode.None,
                KeyCode.None);

            // Save Settings
            saveNewFileOnly = new Setting<bool>(
                "Save New Locations Only",
                "If enabled saves the raid session changes to a new file. Disabled saves all locations you can see to a new file.",
                false,
                false);

            WriteToFileKey = new Setting<KeyCode>(
                "Create Temp Json File",
                "Press this key to write the json file with all entries so far",
                KeyCode.KeypadMinus,
                KeyCode.KeypadMinus);

            #endregion
        }

        public static string ExportToJson()
        {
            var settingsDictionary = new Dictionary<string, object>();

            // Get all fields in DefaultPluginVars
            var fields = typeof(DefaultPluginVars).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var field in fields)
            {
                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Setting<>))
                {
                    var settingValue = field.GetValue(null);
                    if (settingValue != null)
                    {
                        var valueProperty = settingValue.GetType().GetProperty("Value");
                        if (valueProperty != null)
                        {
                            var value = valueProperty.GetValue(settingValue);
                            settingsDictionary[field.Name] = value;
                        }
                    }
                }
                else if (field.Name == "BossUseGlobalSpawnChance")
                {
                    var dict = (Dictionary<string, Setting<bool>>)field.GetValue(null);
                    settingsDictionary[field.Name] = dict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
                }
                else if (field.Name == "BossSpawnChances")
                {
                    var dict = (Dictionary<string, Dictionary<string, Setting<int>>>)field.GetValue(null);
                    settingsDictionary[field.Name] = dict.ToDictionary(
                        bossKvp => bossKvp.Key,
                        bossKvp => bossKvp.Value.ToDictionary(mapKvp => mapKvp.Key, mapKvp => mapKvp.Value.Value)
                    );
                }
            }

            // Add windowRect position and size to the dictionary
            settingsDictionary["windowRectX"] = windowRect.x;
            settingsDictionary["windowRectY"] = windowRect.y;
            settingsDictionary["windowRectWidth"] = windowRect.width;
            settingsDictionary["windowRectHeight"] = windowRect.height;

            return JsonConvert.SerializeObject(settingsDictionary, Formatting.Indented);
        }

        public static void ImportFromJson(string json)
        {
            var settingsDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            var fields = typeof(DefaultPluginVars).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var field in fields)
            {
                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Setting<>))
                {
                    if (settingsDictionary.TryGetValue(field.Name, out var value))
                    {
                        var settingValue = field.GetValue(null);
                        if (settingValue == null)
                        {
                            Debug.LogError($"Setting value for field {field.Name} is null.");
                            continue;
                        }

                        var valueProperty = settingValue.GetType().GetProperty("Value");
                        if (valueProperty == null)
                        {
                            Debug.LogError($"Value property for setting {field.Name} is not found.");
                            continue;
                        }

                        var fieldType = field.FieldType.GetGenericArguments()[0];

                        try
                        {
                            if (fieldType == typeof(KeyCode))
                            {
                                valueProperty.SetValue(settingValue, Enum.Parse(typeof(KeyCode), value.ToString()));
                            }
                            else
                            {
                                var convertedValue = Convert.ChangeType(value, fieldType);
                                valueProperty.SetValue(settingValue, convertedValue);
                            }

                            // Store the scenario selection values
                            if (field.Name == nameof(DefaultPluginVars.pmcScenarioSelection))
                            {
                                pmcScenarioSelectionValue = value.ToString();
                            }
                            else if (field.Name == nameof(DefaultPluginVars.scavScenarioSelection))
                            {
                                scavScenarioSelectionValue = value.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error setting value for field {field.Name}: {ex}");
                        }
                    }
                }
                else if (field.Name == "BossUseGlobalSpawnChance" && settingsDictionary.TryGetValue(field.Name, out var bossUseGlobalSpawnChanceObj))
                {
                    var dict = (Dictionary<string, Setting<bool>>)field.GetValue(null);
                    var loadedDict = JsonConvert.DeserializeObject<Dictionary<string, bool>>(JsonConvert.SerializeObject(bossUseGlobalSpawnChanceObj));
                    foreach (var kvp in loadedDict)
                    {
                        if (dict.TryGetValue(kvp.Key, out var setting))
                        {
                            setting.Value = kvp.Value;
                        }
                    }
                }
                else if (field.Name == "BossSpawnChances" && settingsDictionary.TryGetValue(field.Name, out var bossSpawnChancesObj))
                {
                    var dict = (Dictionary<string, Dictionary<string, Setting<int>>>)field.GetValue(null);
                    var loadedDict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(JsonConvert.SerializeObject(bossSpawnChancesObj));
                    foreach (var bossKvp in loadedDict)
                    {
                        if (dict.TryGetValue(bossKvp.Key, out var mapDict))
                        {
                            foreach (var mapKvp in bossKvp.Value)
                            {
                                if (mapDict.TryGetValue(mapKvp.Key, out var setting))
                                {
                                    setting.Value = mapKvp.Value;
                                }
                            }
                        }
                    }
                }
            }

            // Load windowRect position and size from the dictionary, with defaults if not present
            if (settingsDictionary.TryGetValue("windowRectX", out var windowRectX) &&
                settingsDictionary.TryGetValue("windowRectY", out var windowRectY) &&
                settingsDictionary.TryGetValue("windowRectWidth", out var windowRectWidth) &&
                settingsDictionary.TryGetValue("windowRectHeight", out var windowRectHeight))
            {
                windowRect = new Rect(
                    Convert.ToSingle(windowRectX),
                    Convert.ToSingle(windowRectY),
                    Convert.ToSingle(windowRectWidth),
                    Convert.ToSingle(windowRectHeight));
            }
            else
            {
                // Apply default values if any of the windowRect values are missing
                windowRect = new Rect(20, 20, 1664, 936);
            }

            // Ensure the arrays are initialized before creating the settings
            DefaultPluginVars.pmcScenarioCombinedArray ??= new string[0];
            DefaultPluginVars.scavScenarioCombinedArray ??= new string[0];

            // After loading all settings, initialize the scenario settings with the loaded values
            DefaultPluginVars.pmcScenarioSelection = new Setting<string>(
                "PMC Raid Spawn Preset Selection",
                "Select a preset to use when spawning as PMC",
                pmcScenarioSelectionValue,
                "live-like",
                null,
                null,
                DefaultPluginVars.pmcScenarioCombinedArray
            );

            DefaultPluginVars.scavScenarioSelection = new Setting<string>(
                "SCAV Raid Spawn Preset Selection",
                "Select a preset to use when spawning as SCAV",
                scavScenarioSelectionValue,
                "live-like",
                null,
                null,
                DefaultPluginVars.scavScenarioCombinedArray
            );
        }

    }
}

