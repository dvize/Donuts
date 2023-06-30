using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Aki.Reflection.Patching;
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
        public static ConfigEntry<float> botTimerTrigger;
        public static ConfigEntry<float> coolDownTimer;
        public static ConfigEntry<int> AbsMaxBotCount;
        public static ConfigEntry<bool> DespawnEnabled;
        public static ConfigEntry<bool> DebugGizmos;
        public static ConfigEntry<bool> gizmoRealSize;
        public static ConfigEntry<int> maxSpawnTriesPerBot;

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
        public static ConfigEntry<int> maxSpawnsBeforeCooldown;

        public static ConfigEntry<bool> saveNewFileOnly;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> CreateSpawnMarkerKey;

        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> WriteToFileKey;

        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> DeleteSpawnMarkerKey;

        private void Awake()
        {
            //Main Settings
            PluginEnabled = Config.Bind(
                "Main Settings",
                "Donut On/Off",
                true,
                new ConfigDescription("Enable/Disable Spawning from Donut Points",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 5 }));

            AbsMaxBotCount = Config.Bind(
                "Main Settings",
                "Absolute Max Bot Count",
                18,
                new ConfigDescription("It will stop spawning bots over your maxbotcap limit once it hits this.",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 4 }));

            DespawnEnabled = Config.Bind(
                "Main Settings",
                "Despawn Option",
                true,
                new ConfigDescription("When enabled, removes furthest bots from player for each new dynamic spawn bot",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 3 }));

            coolDownTimer = Config.Bind(
                "Main Settings",
                "Cool Down Timer",
                180f,
                new ConfigDescription("Cool Down Timer for after a spawn has successfully spawned a bot the spawn marker's MaxSpawnsBeforeCoolDown",
                new AcceptableValueRange<float>(0f, 1000f),
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            maxSpawnTriesPerBot = Config.Bind(
                "Main Settings",
                "Max Spawn Tries Per Bot",
                10,
                new ConfigDescription("It will stop trying to spawn one of the bots after this many attempts to find a good spawn point",
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

            gizmoRealSize = Config.Bind(
                "Debugging",
                "Debug Sphere Real Size",
                false,
                new ConfigDescription("When enabled, debug spheres will be the real size of the spawn radius",
                null,
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

            botTimerTrigger = Config.Bind(
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

            maxSpawnsBeforeCooldown = Config.Bind(
                "Spawn Point Maker",
                "Max Spawns Before Cooldown",
                5,
                new ConfigDescription("Number of successful spawns before this marker goes in cooldown",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 3 }));

            CreateSpawnMarkerKey = Config.Bind(
                "Spawn Point Maker",
                "Create Spawn Marker Key",
                new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.Insert),
                new ConfigDescription("Press this key to create a spawn marker at your current location",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            DeleteSpawnMarkerKey = Config.Bind(
                "Spawn Point Maker",
                "Delete Spawn Marker Key",
                new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.Delete),
                new ConfigDescription("Press this key to delete closest spawn marker within 5m of your player location",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 1 }));



            //Save Settings
            saveNewFileOnly = Config.Bind(
                "Save Settings",
                "Save New Locations Only",
                false,
                new ConfigDescription("If enabled saves the raid session changes to a new file. Disabled saves all locations you can see to a new file.",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));

            WriteToFileKey = Config.Bind(
                "Save Settings",
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

            //need to be able to see it to delete it
            if (DonutsPlugin.DebugGizmos.Value)
            {
                //temporarily combine fightLocations and sessionLocations so i can find the closest entry
                var combinedLocations = Donuts.DonutComponent.fightLocations.Locations.Concat(Donuts.DonutComponent.sessionLocations.Locations).ToList();

                // Get the closest spawn marker to the player
                var closestEntry = combinedLocations.OrderBy(x => Vector3.Distance(Donuts.DonutComponent.gameWorld.MainPlayer.Position, new Vector3(x.Position.x, x.Position.y, x.Position.z))).FirstOrDefault();

                // Check if the closest entry is null
                if (closestEntry == null)
                {
                    var displayMessageNotificationMethod = DonutComponent.GetDisplayMessageNotificationMethod();
                    if (displayMessageNotificationMethod != null)
                    {
                        var txt = $"Donuts: The Spawn Marker could not be deleted because closest entry could not be found";
                        displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Default, Color.grey });
                    }
                    return;
                }

                // Remove the entry from the list if the distance from the player is less than 5m
                if (Vector3.Distance(Donuts.DonutComponent.gameWorld.MainPlayer.Position, new Vector3(closestEntry.Position.x, closestEntry.Position.y, closestEntry.Position.z)) < 5f)
                {
                    // check which list the entry is in and remove it from that list
                    if (Donuts.DonutComponent.fightLocations.Locations.Contains(closestEntry))
                    {

                        Donuts.DonutComponent.fightLocations.Locations.Remove(closestEntry);
                    }
                    else if (Donuts.DonutComponent.sessionLocations.Locations.Contains(closestEntry))
                    {

                        Donuts.DonutComponent.sessionLocations.Locations.Remove(closestEntry);
                    }

                    // Display a message to the player
                    var displayMessageNotificationMethod = DonutComponent.GetDisplayMessageNotificationMethod();
                    if (displayMessageNotificationMethod != null)
                    {
                        var txt = $"Donuts: Spawn Marker Deleted for \n {closestEntry.Name}\n SpawnType: {closestEntry.WildSpawnType}\n Position: {closestEntry.Position.x}, {closestEntry.Position.y}, {closestEntry.Position.z}";
                        displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });
                    }

                    // Edit the DonutComponent.drawnCoordinates and gizmoSpheres list to remove the objects
                    var coordinate = new Vector3(closestEntry.Position.x, closestEntry.Position.y, closestEntry.Position.z);
                    DonutComponent.drawnCoordinates.Remove(coordinate);

                    var sphere = DonutComponent.gizmoSpheres.FirstOrDefault(x => x.transform.position == coordinate);
                    DonutComponent.gizmoSpheres.Remove(sphere);

                    // Destroy the sphere game object in the actual game world
                    if (sphere != null)
                    {
                        Destroy(sphere);
                    }
                }
            }
        }

        private void CreateSpawnMarker()
        {
            // Check if any of the required objects are null
            if (DonutComponent.gameWorld == null)
            {
                Logger.LogDebug("IBotGame Not Instantiated or gameWorld is null.");
                return;
            }

            // Create new Donuts.Entry
            Entry newEntry = new Entry
            {
                Name = spawnName.Value,
                MapName = DonutComponent.maplocation,
                WildSpawnType = wildSpawns.Value,
                MinDistance = minSpawnDist.Value,
                MaxDistance = maxSpawnDist.Value,
                MaxRandomNumBots = maxRandNumBots.Value,
                SpawnChance = spawnChance.Value,
                BotTimerTrigger = botTimerTrigger.Value,
                BotTriggerDistance = botTriggerDistance.Value,
                Position = new Position
                {
                    x = DonutComponent.gameWorld.MainPlayer.Position.x,
                    y = DonutComponent.gameWorld.MainPlayer.Position.y,
                    z = DonutComponent.gameWorld.MainPlayer.Position.z
                },

                MaxSpawnsBeforeCoolDown = maxSpawnsBeforeCooldown.Value

            };

            // Add new entry to sessionLocations.Locations list since we adding new ones

            // Check if Locations is null
            if (DonutComponent.sessionLocations.Locations == null)
            {
                DonutComponent.sessionLocations.Locations = new List<Entry>();
            }

            DonutComponent.sessionLocations.Locations.Add(newEntry);

            // make it testable immediately by adding the timer needed
            var hotspotTimer = new HotspotTimer(newEntry);
            DonutComponent.hotspotTimers.Add(hotspotTimer);

            var txt = $"Donuts: Wrote Entry for {newEntry.Name}\n SpawnType: {newEntry.WildSpawnType}\n Position: {newEntry.Position.x}, {newEntry.Position.y}, {newEntry.Position.z}";
            var displayMessageNotificationMethod = DonutComponent.GetDisplayMessageNotificationMethod();
            if (displayMessageNotificationMethod != null)
            {
                displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });
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
            string json = "";
            string fileName = "";

            //check if saveNewFileOnly is true then we use the sessionLocations object to serialize.  Otherwise we use combinedLocations
            if (saveNewFileOnly.Value)
            {
                // take the sessionLocations object only and serialize it to json
                json = JsonConvert.SerializeObject(Donuts.DonutComponent.sessionLocations, Formatting.Indented);
                fileName = Donuts.DonutComponent.maplocation + "_" + UnityEngine.Random.Range(0, 1000) + "_NewLocOnly.json";
            }
            else
            {
                //combine the fightLocations and sessionLocations objects into one variable
                FightLocations combinedLocations = new Donuts.FightLocations
                {
                    Locations = Donuts.DonutComponent.fightLocations.Locations.Concat(Donuts.DonutComponent.sessionLocations.Locations).ToList()
                };

                json = JsonConvert.SerializeObject(combinedLocations, Formatting.Indented);
                fileName = Donuts.DonutComponent.maplocation + "_" + UnityEngine.Random.Range(0, 1000) + "_All.json";
            }

            //write json to file with filename == Donuts.DonutComponent.maplocation + random number
            string jsonFilePath = Path.Combine(jsonFolderPath, fileName);
            File.WriteAllText(jsonFilePath, json);

            var txt = $"Donuts: Wrote Json File to: {jsonFilePath}";
            var displayMessageNotificationMethod = DonutComponent.GetDisplayMessageNotificationMethod();
            if (displayMessageNotificationMethod != null)
            {
                displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });
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
