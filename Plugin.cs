using System.IO;
using System.Linq;
using System.Reflection;
using Aki.Reflection.Patching;
using Aki.Reflection.Utils;
using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.Communications;
using Newtonsoft.Json;
using UnityEngine;

namespace Donuts
{
    [BepInPlugin("com.dvize.Donuts", "dvize.Donuts", "1.0.0")]
    public class DonutsPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> PluginEnabled;
        public static ConfigEntry<float> SpawnTimer;

        public static ConfigEntry<int> AbsMaxBotCount;
        public static ConfigEntry<bool> DespawnEnabled;
        public static ConfigEntry<bool> DebugGizmos;
        public static ConfigEntry<float> DebugOpacity;

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
        public static ConfigEntry<float> botTriggerDistance;
        public static ConfigEntry<int> maxRandNumBots;
        public static ConfigEntry<int> spawnChance;

        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> CreateSpawnMarkerKey;

        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> WriteToFileKey;

        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> DeleteSpawnMarkerKey;

        private MethodInfo displayMessageNotification;
        private void Awake()
        {
            //Main Settings
            PluginEnabled = Config.Bind(
                "Main Settings",
                "Donut On/Off",
                true,
                new ConfigDescription("Enable/Disable Spawning from Donut Points",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 3 }));

            AbsMaxBotCount = Config.Bind(
                "Main Settings",
                "Absolute Max Bot Count",
                18,
                new ConfigDescription("It will stop spawning bots over your maxbotcap limit once it hits this.",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            DespawnEnabled = Config.Bind(
                "Main Settings",
                "Despawn Option",
                true,
                new ConfigDescription("When enabled, removes furthest bots from player for each new dynamic spawn bot",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 1 }));

            //Debugging 
            DebugGizmos = Config.Bind(
                "Debugging",
                "Enable Debug Markers",
                false,
                new ConfigDescription("When enabled, draws debug spheres on set spawn from json",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            DebugOpacity = Config.Bind(
                "Debugging",
                "Debug Sphere Opacity",
                100f,
                new ConfigDescription("Sets how much you can see through a Debug Sphere",
                new AcceptableValueRange<float>(0f, 100f),
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 1 }));

            // Spawn Point Maker
            spawnName = Config.Bind(
                "Spawn Point Maker",
                "Name",
                "Spawn Name Here",
                new ConfigDescription("Name used to identify the spawn marker",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 11 }));

            wildSpawns = Config.Bind(
                "Spawn Point Maker",
                "Wild Spawn Type",
                "pmc",
                new ConfigDescription("Select an option.",
                new AcceptableValueList<string>(wildDropValues),
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 10 }));

            minSpawnDist = Config.Bind(
                "Spawn Point Maker",
                "Min Spawn Distance",
                1f,
                new ConfigDescription("Min Distance Bots will Spawn From Marker You Set.",
                new AcceptableValueRange<float>(0f, 500f),
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 9 }));

            maxSpawnDist = Config.Bind(
                "Spawn Point Maker",
                "Max Spawn Distance",
                20f,
                new ConfigDescription("Max Distance Bots will Spawn From Marker You Set.",
                new AcceptableValueRange<float>(1f, 1000f),
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 8 }));

            botTriggerDistance = Config.Bind(
                "Spawn Point Maker",
                "Bot Spawn Trigger Distance",
                150f,
                new ConfigDescription("Distance in which the player is away from the fight location point that it triggers bot spawn",
                new AcceptableValueRange<float>(0.1f, 1000f),
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 7 }));

            SpawnTimer = Config.Bind(
                "Spawn Point Maker",
                "Bot Spawn Timer Trigger",
                180f,
                new ConfigDescription("In seconds before it spawns next wave while player in the fight zone area",
                new AcceptableValueRange<float>(0f, 10000f),
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 6 }));

            maxRandNumBots = Config.Bind(
                "Spawn Point Maker",
                "Max Random Bots",
                2,
                new ConfigDescription("Maximum number of bots of Wild Spawn Type that can spawn on this marker",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 5 }));

            spawnChance = Config.Bind(
                "Spawn Point Maker",
                "Spawn Chance for Marker",
                50,
                new ConfigDescription("Chance bot will be spawn here after timer is reached",
                new AcceptableValueRange<int>(0, 100),
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 4 }));

            CreateSpawnMarkerKey = Config.Bind(
                "Spawn Point Maker",
                "Create Spawn Marker Key",
                new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.Insert),
                new ConfigDescription("Press this key to create a spawn marker at your current location",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 3 }));

            DeleteSpawnMarkerKey = Config.Bind(
                "Spawn Point Maker",
                "Delete Spawn Marker Key",
                new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.Delete),
                new ConfigDescription("Press this key to delete closest spawn marker within 5m of your player location",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            WriteToFileKey = Config.Bind(
                "Spawn Point Maker",
                "Create Temp Json File",
                new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.KeypadMinus),
                new ConfigDescription("Press this key to write the json file with all entries so far",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 1 }));

            //Patches
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
            if (DeleteSpawnMarkerKey.Value.IsDown())
            {
                DeleteSpawnMarker();
            }
        }

        private void DeleteSpawnMarker()
        {
            // Check if any of the required objects are null
            if (Donuts.DonutComponent.gameWorld == null)
            {
                Logger.LogDebug("IBotGame Not Instantiated or gameWorld is null.");
                return;
            }

            // Get the closest spawn marker to the player
            Donuts.Entry closestEntry = Donuts.DonutComponent.fightLocations.Locations.OrderBy(x =>
                Vector3.Distance(Donuts.DonutComponent.gameWorld.MainPlayer.Position, new Vector3(x.Position.x, x.Position.y, x.Position.z))).FirstOrDefault();

            // Check if the closest entry is null
            if (closestEntry == null)
            {
                Logger.LogDebug("No spawn markers found.");
                return;
            }

            // Remove the entry from the list if the distance from the player is less than 5m
            if (Vector3.Distance(Donuts.DonutComponent.gameWorld.MainPlayer.Position, new Vector3(closestEntry.Position.x, closestEntry.Position.y, closestEntry.Position.z)) < 5f)
            {
                Donuts.DonutComponent.fightLocations.Locations.Remove(closestEntry);
            }
            
            // Display a message to the player
            if (displayMessageNotification != null)
            {
                var txt = $"Donuts: Spawn Marker Deleted for {closestEntry.Name}\n SpawnType: {closestEntry.WildSpawnType}\n Position: {closestEntry.Position.x}, {closestEntry.Position.y}, {closestEntry.Position.z}";
                displayMessageNotification.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });
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
