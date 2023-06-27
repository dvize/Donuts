using System.Reflection;
using Aki.Reflection.Patching;
using BepInEx;
using BepInEx.Configuration;
using EFT;
using Newtonsoft.Json;
using System.IO;
using Comfort.Common;
using EFT.UI;
using EFT.Communications;
using UnityEngine;
using Aki.Reflection.Utils;
using System.Linq;

namespace Donuts
{
    [BepInPlugin("com.dvize.Donuts", "dvize.Donuts", "1.0.0")]
    public class DonutsPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> PluginEnabled;
        public static ConfigEntry<float> SpawnTimer;
        public static ConfigEntry<float> botSpawnDistance;
        public static ConfigEntry<int> AbsMaxBotCount;
        public static ConfigEntry<bool> DespawnEnabled;
        public static ConfigEntry<bool> DebugGizmos;

        //menu vars
        public static ConfigEntry<string> spawnName;

        public ConfigEntry<string> wildSpawns;
        public string[] wildDropValues = new string[]
        {
            "assault",
            "assaultgroup",
            "bossbully",
            "bossgluhar",
            "bosskilla",
            "bossknight",
            "bosskojaniy",
            "bosssanitar",
            "bosstagilla",
            "bosszryachiy",
            "cursedassault",
            "exusec",
            "followerbigpipe",
            "followerbirdeye",
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
            "sectantpriest",
            "sectantwarrior",
            "sptusec",
            "sptbear"
        };
        public static ConfigEntry<float> minSpawnDist;
        public static ConfigEntry<float> maxSpawnDist;
        public static ConfigEntry<int> maxRandNumBots;
        public static ConfigEntry<int> spawnChance;

        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> CreateSpawnMarkerKey;

        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> WriteToFileKey;

        private MethodInfo displayMessageNotification;
        private void Awake()
        {
            PluginEnabled = Config.Bind(
                "Main Settings",
                "1. Plugin on/off",
                true,
                "");

            botSpawnDistance = Config.Bind(
                "Main Settings",
                "2. Bot Spawn Distance",
                150f,
                "Distance in which the player is away from the fight location point that it triggers bot spawn");

            SpawnTimer = Config.Bind(
                "Main Settings",
                "3. Bot Spawn Timer",
                180f,
                "In seconds before it spawns next wave while player in the fight zone area");

            AbsMaxBotCount = Config.Bind(
                "Main Settings",
                "4. Absolute Max Bot Count",
                18,
                "It will stop spawning bots over your maxbotcap limit once it hits this.");

            DespawnEnabled = Config.Bind(
                "Main Settings",
                "5. Despawn Option",
                true,
                "When enabled, removes furthest bots from player for each new dynamic spawn bot");

            DebugGizmos = Config.Bind(
                "Debugging",
                "1. Enable Debug Markers",
                false,
                "When enabled, draws debug spheres on set spawn from json");

            // Create a dropdown option for the selected option

            spawnName = Config.Bind(
                "Spawn Point Maker",
                "1. Name",
                "Spawn Name Here",
                "Name used to set the point in json file");

            wildSpawns = Config.Bind(
                "Spawn Point Maker",
                "2. Wild Spawn Type",
                "pmc",
                new ConfigDescription("Select an option.", new AcceptableValueList<string>(wildDropValues)));

            minSpawnDist = Config.Bind(
                "Spawn Point Maker",
                "3. Min Spawn Distance",
                1f,
                "Min Distance Bots will Spawn From Marker You Set.");

            maxSpawnDist = Config.Bind(
                "Spawn Point Maker",
                "4. Max Spawn Distance",
                20f,
                "Max Distance Bots will Spawn From Marker You Set.");

            maxRandNumBots = Config.Bind(
                "Spawn Point Maker",
                "5. Max Random Bots",
                2,
                "Maximum number of bots of Wild Spawn Type that can spawn on this marker");

            spawnChance = Config.Bind(
                "Spawn Point Maker",
                "6. Spawn Chance for Marker",
                50,
                "Chance bot will be spawn here after timer is reached");

            CreateSpawnMarkerKey = Config.Bind(
                "Spawn Point Maker",
                "7. Create Spawn Marker Key",
                new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.Insert),
                "Press this key to create a spawn marker at your current location");

            WriteToFileKey = Config.Bind(
                "Spawn Point Maker",
                "8. Create Temp Json File",
                new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.KeypadMinus),
                "Press this key to write the json file with all entries so far");

            new NewGamePatch().Enable();
        }
        private void Update()
        {
            if (displayMessageNotification == null)
            {
                displayMessageNotification = PatchConstants.EftTypes.Single(x => x.GetMethod("DisplayMessageNotification") != null).GetMethod("DisplayMessageNotification");
            }

            if (CreateSpawnMarkerKey.Value.IsDown())
            {
                CreateSpawnMarker();
            }
            if (WriteToFileKey.Value.IsDown())
            {
                WriteToJsonFile();
            }
        }

        private void CreateSpawnMarker()
        {
            // Check if any of the required objects are null
            if (Donuts.DonutComponent.gameWorld == null)
            {
                Logger.LogDebug("IBotGame Not Instantiated or gameWorld is null.");
                return;
            }

            // Create new Donuts.Entry
            Donuts.Entry newEntry = new Donuts.Entry
            {
                Name = spawnName.Value,
                MapName = Donuts.DonutComponent.maplocation,
                WildSpawnType = wildSpawns.Value,
                MinDistance = minSpawnDist.Value,
                MaxDistance = maxSpawnDist.Value,
                MaxRandomNumBots = maxRandNumBots.Value,
                SpawnChance = spawnChance.Value,
                Position = new Donuts.Position
                {
                    x = Donuts.DonutComponent.gameWorld.MainPlayer.Transform.position.x,
                    y = Donuts.DonutComponent.gameWorld.MainPlayer.Transform.position.y,
                    z = Donuts.DonutComponent.gameWorld.MainPlayer.Transform.position.z
                }
            };

            // Add new entry to fightLocations.locations list
            Donuts.DonutComponent.fightLocations.Locations.Add(newEntry);

            var txt = $"Donuts: Wrote Entry for {newEntry.Name}\n SpawnType: {newEntry.WildSpawnType}\n Position: {newEntry.Position.x}, {newEntry.Position.y}, {newEntry.Position.z}";

            if (displayMessageNotification != null)
            {
                displayMessageNotification.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });
            }
        }

        private void WriteToJsonFile()
        {
            // Check if any of the required objects are null
            if (Donuts.DonutComponent.gameWorld == null)
            {
                Logger.LogDebug("IBotGame Not Instantiated or gameWorld is null.");
                return;
            }

            string dllPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(dllPath);
            string jsonFolderPath = Path.Combine(directoryPath, "patterns");

            // take the fightLocations object and serialize it to json
            string json = JsonConvert.SerializeObject(Donuts.DonutComponent.fightLocations, Formatting.Indented);

            //write json to file with filename == Donuts.DonutComponent.maplocation + random number
            string fileName = Donuts.DonutComponent.maplocation + "_" + UnityEngine.Random.Range(0, 1000) + ".json";
            string jsonFilePath = Path.Combine(jsonFolderPath, fileName);
            File.WriteAllText(jsonFilePath, json);

            var txt = $"Donuts: Wrote Json File to: {jsonFilePath}";

            if (displayMessageNotification != null)
            {
                displayMessageNotification.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });
            }
        }
    }

    //re-initializes each new game
    internal class NewGamePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));

        [PatchPrefix]
        public static void PatchPrefix()
        {
            DonutComponent.Enable();
        }
    }


}
