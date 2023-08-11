using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aki.Common.Http;
using Aki.PrePatch;
using Aki.Reflection.Patching;
using Aki.Reflection.Utils;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Bots;
using EFT.Communications;
using HarmonyLib;
using Newtonsoft.Json;
using Systems.Effects;
using UnityEngine;
using UnityEngine.AI;
using static Streamer;

namespace Donuts
{
    public class DonutComponent : MonoBehaviour
    {
        internal static FightLocations fightLocations;
        internal static FightLocations sessionLocations;

        internal static List<List<Entry>> groupedFightLocations;
        internal static Dictionary<int, List<HotspotTimer>> groupedHotspotTimers;

        internal List<WildSpawnType> validDespawnListPMC = new List<WildSpawnType>()
        {
            (WildSpawnType)AkiBotsPrePatcher.sptUsecValue,
            (WildSpawnType)AkiBotsPrePatcher.sptBearValue
        };

        internal List<WildSpawnType> validDespawnListScav = new List<WildSpawnType>()
        {
            WildSpawnType.assault,
            WildSpawnType.cursedAssault
        };

        private bool fileLoaded = false;
        public static string maplocation;
        private int PMCBotLimit = 0;
        private int SCAVBotLimit = 0;
        public static GameWorld gameWorld;
        private static BotSpawnerClass botSpawnerClass;
        private static botClass myBotClass;

        private float PMCdespawnCooldown = 0f;
        private float PMCdespawnCooldownDuration = 10f;

        private float SCAVdespawnCooldown = 0f;
        private float SCAVdespawnCooldownDuration = 10f;

        internal static List<HotspotTimer> hotspotTimers;
        internal static Dictionary<string, MethodInfo> methodCache;
        private static MethodInfo displayMessageNotificationMethod;

        //gizmo stuff
        private bool isGizmoEnabled = false;
        internal static HashSet<Vector3> drawnCoordinates;
        internal static List<GameObject> gizmoSpheres;
        private static Coroutine gizmoUpdateCoroutine;
        internal static IBotCreator ibotCreator;

        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public DonutComponent()
        {
            if (Logger == null)
            {
                Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(DonutComponent));
            }

        }

        public void Awake()
        {
            botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            methodCache = new Dictionary<string, MethodInfo>();

            // Retrieve displayMessageNotification MethodInfo
            var displayMessageNotification = PatchConstants.EftTypes.Single(x => x.GetMethod("DisplayMessageNotification") != null).GetMethod("DisplayMessageNotification");
            if (displayMessageNotification != null)
            {
                displayMessageNotificationMethod = displayMessageNotification;
                methodCache["DisplayMessageNotification"] = displayMessageNotification;
            }

            var methodInfo = typeof(BotSpawnerClass).GetMethod("method_11", BindingFlags.Instance | BindingFlags.NonPublic);
            var methodInfo2 = typeof(BotSpawnerClass).GetMethod("method_2", BindingFlags.Instance | BindingFlags.NonPublic);
            if (methodInfo != null && methodInfo2 != null)
            {
                methodCache[methodInfo.Name] = methodInfo;
                methodCache[methodInfo2.Name] = methodInfo2;
            }

            // Remove despawned bots from bot EnemyInfos list.
            botSpawnerClass.OnBotRemoved += removedBot =>
            {
                // Clear the enemy list, and memory about the main player
                removedBot.Memory.DeleteInfoAboutEnemy(gameWorld.MainPlayer);
                removedBot.EnemiesController.EnemyInfos.Clear();

                // Loop through the rest of the bots on the map, andd clear this bot from its memory/group info

                foreach (var player in gameWorld.AllAlivePlayersList)
                {
                    if (!player.IsAI)
                    {
                        continue;
                    }

                    // Clear the bot from all other bots enemy info
                    var botOwner = player.AIData.BotOwner;
                    botOwner.Memory.DeleteInfoAboutEnemy(removedBot);
                    botOwner.BotsGroup.RemoveInfo(removedBot);
                    botOwner.BotsGroup.RemoveEnemy(removedBot);
                    botOwner.BotsGroup.RemoveAlly(removedBot);
                }
            };
        }

        private void Start()
        {
            // setup the rest of donuts for the selected folder
            InitializeStaticVariables();
            maplocation = gameWorld.MainPlayer.Location.ToLower();
            Logger.LogDebug("Setup maplocation: " + maplocation);
            LoadFightLocations();
            if (DonutsPlugin.PluginEnabled.Value && fileLoaded)
            {
                InitializeHotspotTimers();
            }
            
            Logger.LogDebug("Setup PMC Bot limit: " + PMCBotLimit);
            Logger.LogDebug("Setup SCAV Bot limit: " + SCAVBotLimit);
        }
        private void InitializeStaticVariables()
        {
            fightLocations = new FightLocations()
            {
                Locations = new List<Entry>()
            };

            sessionLocations = new FightLocations()
            {
                Locations = new List<Entry>()
            };

            groupedHotspotTimers = new Dictionary<int, List<HotspotTimer>>();
            groupedFightLocations = new List<List<Entry>>();
            hotspotTimers = new List<HotspotTimer>();

            drawnCoordinates = new HashSet<Vector3>();
            gizmoSpheres = new List<GameObject>();
            ibotCreator = AccessTools.Field(typeof(BotSpawnerClass), "ginterface17_0").GetValue(botSpawnerClass) as IBotCreator;
            myBotClass = new botClass();
        }
        private void SetupBotLimit(string folderName)
        {
            Folder raidFolderSelected = DonutsPlugin.GrabDonutsFolder(folderName);
            switch (maplocation)
            {
                case "factory4_day":
                case "factory4_night":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.FactoryBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.FactoryBotLimit;
                    break;
                case "bigmap":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.CustomsBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.CustomsBotLimit;
                    break;
                case "interchange":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.InterchangeBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.InterchangeBotLimit;
                    break;
                case "rezervbase":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.ReserveBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.ReserveBotLimit;
                    break;
                case "laboratory":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.LaboratoryBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.LaboratoryBotLimit;
                    break;
                case "lighthouse":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.LighthouseBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.LighthouseBotLimit;
                    break;
                case "shoreline":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.ShorelineBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.ShorelineBotLimit;
                    break;
                case "woods":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.WoodsBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.WoodsBotLimit;
                    break;
                case "tarkovstreets":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.TarkovStreetsBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.TarkovStreetsBotLimit;
                    break;
                default:
                    PMCBotLimit = 8;
                    SCAVBotLimit = 5;
                    break;
            }
        }

        public static void Enable()
        {
            if (Singleton<IBotGame>.Instantiated)
            {
                gameWorld = Singleton<GameWorld>.Instance;
                gameWorld.GetOrAddComponent<DonutComponent>();

                Logger.LogDebug("Donuts Enabled");
            }
        }

        private void InitializeHotspotTimers()
        {
            // Group the fight locations by groupNum
            foreach (var listHotspots in groupedFightLocations)
            {
                foreach (var hotspot in listHotspots)
                {
                    var hotspotTimer = new HotspotTimer(hotspot);

                    int groupNum = hotspot.GroupNum;

                    if (!groupedHotspotTimers.ContainsKey(groupNum))
                    {
                        groupedHotspotTimers[groupNum] = new List<HotspotTimer>();
                    }

                    groupedHotspotTimers[groupNum].Add(hotspotTimer);
                }
            }

            // Assign the groupedHotspotTimers dictionary back to hotspotTimers
            hotspotTimers = groupedHotspotTimers.SelectMany(kv => kv.Value).ToList();
        }


        private void LoadFightLocations()
        {
            if (!fileLoaded)
            {
                MethodInfo displayMessageNotificationMethod;
                methodCache.TryGetValue("DisplayMessageNotification", out displayMessageNotificationMethod);

                string dllPath = Assembly.GetExecutingAssembly().Location;
                string directoryPath = Path.GetDirectoryName(dllPath);

                string jsonFolderPath = Path.Combine(directoryPath, "patterns");

                //in SelectedPatternFolderPath, grab the folder name from DonutsPlugin.scenarioSelection.Value

                var selectionName = runWeightedScenarioSelection();

                SetupBotLimit(selectionName);

                if (selectionName == null)
                {
                    var txt = "Donuts Plugin: No valid Scenario Selection found for map";
                    Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    return;
                }

                string PatternFolderPath = Path.Combine(jsonFolderPath, selectionName);

                // Check if the folder exists
                if (!Directory.Exists(PatternFolderPath))
                {
                    var txt = ("Donuts Plugin: Folder from ScenarioConfig.json does not actually exist: " + PatternFolderPath + "\nDisabling the donuts plugin for this raid.");
                    Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                string[] jsonFiles = Directory.GetFiles(PatternFolderPath, "*.json");

                if (jsonFiles.Length == 0)
                {
                    var txt = ("Donuts Plugin: No JSON Pattern files found in folder: " + PatternFolderPath + "\nDisabling the donuts plugin for this raid.");
                    Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                List<Entry> combinedLocations = new List<Entry>();

                foreach (string file in jsonFiles)
                {
                    FightLocations fightfile = JsonConvert.DeserializeObject<FightLocations>(File.ReadAllText(file));
                    combinedLocations.AddRange(fightfile.Locations);
                }

                if (combinedLocations.Count == 0)
                {
                    var txt = "Donuts Plugin: No Entries found in JSON files, disabling plugin for raid.";
                    Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                Logger.LogDebug("Loaded " + combinedLocations.Count + " Bot Fight Entries");

                // Assign the combined fight locations to the fightLocations variable.
                fightLocations = new FightLocations { Locations = combinedLocations };

                //filter fightLocations for maplocation
                fightLocations.Locations.RemoveAll(x => x.MapName != maplocation);

                if (fightLocations.Locations.Count == 0)
                {
                    //show error message so user knows why donuts is not working
                    var txt = "Donuts Plugin: There are no valid Spawn Marker Entries for the current map. Disabling the plugin for this raid.";
                    Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                Logger.LogDebug("Valid Bot Fight Entries For Current Map: " + fightLocations.Locations.Count);

                fileLoaded = true;
            }

            //group fightLocations by groupnum
            foreach (Entry entry in fightLocations.Locations)
            {
                bool groupExists = false;
                foreach (List<Entry> group in groupedFightLocations)
                {
                    if (group.Count > 0 && group.First().GroupNum == entry.GroupNum)
                    {
                        group.Add(entry);
                        groupExists = true;
                        break;
                    }
                }

                if (!groupExists)
                {
                    groupedFightLocations.Add(new List<Entry> { entry });
                }
            }
        }

        private string runWeightedScenarioSelection()
        {
            if (DonutsPlugin.scenarioSelection.Value.ToLower() != "random")
            {
                Logger.LogDebug("Selected Folder: " + DonutsPlugin.scenarioSelection.Value);
                
                return DonutsPlugin.scenarioSelection.Value;
            }

            //filter out folders where folder.RandomSelection is false
            var filteredFolders = DonutsPlugin.scenarios.Where(folder => folder.RandomSelection);

            // Calculate the total weight of all folders minus the ones where folder.RandomSelection is false
            int totalWeight = filteredFolders.Sum(folder => folder.Weight);

            int randomWeight = UnityEngine.Random.Range(0, totalWeight);

            // Select the folder based on the random weight
            Folder selectedFolder = null;
            int accumulatedWeight = 0;

            foreach (Folder folder in filteredFolders)
            {
                accumulatedWeight += folder.Weight;
                if (randomWeight <= accumulatedWeight)
                {
                    selectedFolder = folder;
                    break;
                }
            }

            // Use the selected folder
            if (selectedFolder != null)
            {
                Console.WriteLine("Donuts: Random Selected Folder: " + selectedFolder.Name);

                if (DonutsPlugin.ShowRandomFolderChoice.Value)
                {
                    MethodInfo displayMessageNotificationMethod;
                    if (methodCache.TryGetValue("DisplayMessageNotification", out displayMessageNotificationMethod))
                    {
                        var txt = $"Donuts Random Selected Folder: {selectedFolder.Name}";
                        EFT.UI.ConsoleScreen.Log(txt);
                        displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });
                    }
                }

                return selectedFolder.Name;
            }

            return null;
        }

        private void Update()
        {
            if (DonutsPlugin.PluginEnabled.Value && fileLoaded)
            {
                //every hotspottimer should be updated every frame
                foreach (var hotspotTimer in hotspotTimers)
                {
                    hotspotTimer.UpdateTimer();
                }

                if (groupedHotspotTimers.Count > 0)
                {
                    foreach (var groupHotspotTimers in groupedHotspotTimers.Values)
                    {
                        //check if randomIndex is possible
                        if (!(groupHotspotTimers.Count > 0))
                        {
                            continue;
                        }

                        // Get a random hotspotTimer from the group (grouped by groupNum}
                        var randomIndex = UnityEngine.Random.Range(0, groupHotspotTimers.Count);
                        var hotspotTimer = groupHotspotTimers[randomIndex];


                        if (hotspotTimer.ShouldSpawn())
                        {
                            var hotspot = hotspotTimer.Hotspot;
                            var coordinate = new Vector3(hotspot.Position.x, hotspot.Position.y, hotspot.Position.z);

                            if (IsWithinBotActivationDistance(hotspot, coordinate) && maplocation == hotspot.MapName)
                            {
                                // Check if passes hotspot.spawnChance
                                if (UnityEngine.Random.Range(0, 100) >= hotspot.SpawnChance)
                                {
                                    Logger.LogDebug("SpawnChance of " + hotspot.SpawnChance + "% Failed for hotspot: " + hotspot.Name);

                                    //reset timer if spawn chance fails for all hotspots with same groupNum
                                    foreach (var timer in groupedHotspotTimers[hotspot.GroupNum])
                                    {
                                        timer.ResetTimer();

                                        if (timer.Hotspot.IgnoreTimerFirstSpawn)
                                        {
                                            timer.Hotspot.IgnoreTimerFirstSpawn = false;
                                        }

                                        Logger.LogDebug($"Resetting all grouped timers for groupNum: {hotspot.GroupNum} for hotspot: {timer.Hotspot.Name} at time: {timer.GetTimer()}");
                                    }
                                    continue;
                                }

                                if (hotspotTimer.inCooldown)
                                {
                                    Logger.LogDebug("Hotspot: " + hotspot.Name + " is in cooldown, skipping spawn");
                                    continue;
                                }

                                Logger.LogDebug("SpawnChance of " + hotspot.SpawnChance + "% Passed for hotspot: " + hotspot.Name);
                                SpawnBots(hotspotTimer, coordinate);
                                hotspotTimer.timesSpawned++;

                                // Make sure to check the times spawned in hotspotTimer and set cooldown bool if needed
                                if (hotspotTimer.timesSpawned >= hotspot.MaxSpawnsBeforeCoolDown)
                                {
                                    hotspotTimer.inCooldown = true;
                                    Logger.LogWarning("Hotspot: " + hotspot.Name + " is now in cooldown");
                                }
                                Logger.LogDebug("Resetting Regular Spawn Timer (after successful spawn): " + hotspotTimer.GetTimer() + " for hotspot: " + hotspot.Name);

                                //reset timer if spawn chance passes for all hotspots with same groupNum
                                foreach (var timer in groupedHotspotTimers[hotspot.GroupNum])
                                {
                                    timer.ResetTimer();

                                    if (timer.Hotspot.IgnoreTimerFirstSpawn)
                                    {
                                        timer.Hotspot.IgnoreTimerFirstSpawn = false;
                                    }

                                    Logger.LogDebug($"Resetting all grouped timers for groupNum: {hotspot.GroupNum} for hotspot: {timer.Hotspot.Name} at time: {timer.GetTimer()}");
                                }
                            }
                        }
                    }
                }

                DisplayMarkerInformation();

                if (DonutsPlugin.DespawnEnabled.Value)
                {
                    DespawnFurthestBot("pmc");
                    DespawnFurthestBot("scav");
                }
            }
        }

        private bool IsWithinBotActivationDistance(Entry hotspot, Vector3 position)
        {
            try
            {
                float distanceSquared = (gameWorld.MainPlayer.Position - position).sqrMagnitude;
                float activationDistanceSquared = hotspot.BotTriggerDistance * hotspot.BotTriggerDistance;
                return distanceSquared <= activationDistanceSquared;
            }
            catch { }

            return false;
        }
        private async Task SpawnBots(HotspotTimer hotspotTimer, Vector3 coordinate)
        {
            int count = 0;
            int maxSpawnAttempts = DonutsPlugin.maxSpawnTriesPerBot.Value;

            // Moved outside so all spawns for a point are on the same side
            WildSpawnType wildSpawnType = GetWildSpawnType(hotspotTimer.Hotspot.WildSpawnType);
            EPlayerSide side = GetSideForWildSpawnType(wildSpawnType);
            var cancellationToken = AccessTools.Field(typeof(BotSpawnerClass), "cancellationTokenSource_0").GetValue(botSpawnerClass) as CancellationTokenSource;

            while (count < UnityEngine.Random.Range(1, hotspotTimer.Hotspot.MaxRandomNumBots + 1))
            {
                Vector3? spawnPosition = await GetValidSpawnPosition(hotspotTimer.Hotspot, coordinate, maxSpawnAttempts);

                if (!spawnPosition.HasValue)
                {
                    // Failed to get a valid spawn position, move on to generating the next bot
                    Logger.LogDebug($"Actually Failed to get a valid spawn position for {hotspotTimer.Hotspot.Name} after {maxSpawnAttempts}, moving on to next bot anyways");
                    count++;
                    continue;
                }

                //check if array has a profile and activatebot and slice it.. otherwise use regular createbot
                var botdifficulty = botClass.grabBotDifficulty();
                var GClass628DataList = DonutsBotPrep.GetWildSpawnData(wildSpawnType, botdifficulty);
                if (GClass628DataList != null && GClass628DataList.Count > 0)
                {
                    //splice data from GClass628DataList and assign it to GClass628Data
                    var GClass628Data = GClass628DataList[0];
                    GClass628DataList.RemoveAt(0);

                    var closestBotZone = botSpawnerClass.GetClosestZone((Vector3)spawnPosition, out float dist);
                    GClass628Data.AddPosition((Vector3)spawnPosition);

                    DonutComponent.methodCache["method_11"].Invoke(botSpawnerClass, new object[] { closestBotZone, GClass628Data, null, cancellationToken.Token });

                    //method_2(Profile profile, Vector3 position, Action<BotOwner> callback, bool isLocalGame, CancellationToken cancellationToken)

                    DonutComponent.Logger.LogWarning($"Spawning bot at distance to player of: {Vector3.Distance((Vector3)spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                        $"of side: {GClass628Data.Side} and difficulty: {botdifficulty}");

                    
                    //private List<GClass628.Class266> list_0 = new List<GClass628.Class266>();
                    //this.list_0.Add(new GClass628.Class266(spawnPointPosition, false));
                }
                else
                {
                    await myBotClass.CreateBot(wildSpawnType, side, ibotCreator, botSpawnerClass, (Vector3)spawnPosition, cancellationToken);
                }

                count++;
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
                    return (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
                case "bear":
                    return (WildSpawnType)AkiBotsPrePatcher.sptBearValue;
                case "sptusec":
                    return (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
                case "sptbear":
                    return (WildSpawnType)AkiBotsPrePatcher.sptBearValue;
                case "followerbigpipe":
                    return WildSpawnType.followerBigPipe;
                case "followerbirdeye":
                    return WildSpawnType.followerBirdEye;
                case "bossknight":
                    return WildSpawnType.bossKnight;
                case "pmc":
                    //random wildspawntype is either assigned sptusec or sptbear at 50/50 chance
                    return (UnityEngine.Random.Range(0, 2) == 0) ? (WildSpawnType)AkiBotsPrePatcher.sptUsecValue : (WildSpawnType)AkiBotsPrePatcher.sptBearValue;
                default:
                    return WildSpawnType.assault;
            }

        }
        private EPlayerSide GetSideForWildSpawnType(WildSpawnType spawnType)
        {
            //define spt wildspawn
            WildSpawnType sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            WildSpawnType sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;

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

        private void DespawnFurthestBot(string bottype)
        {
            var bots = gameWorld.RegisteredPlayers;
            float maxDistance = -1f;
            Player furthestBot = null;
            var tempBotCount = 0;

            if (bottype == "pmc")
            {
                if (Time.time - PMCdespawnCooldown < PMCdespawnCooldownDuration)
                {
                    return; // Exit the method without despawning
                }

                //don't know distances so have to loop through all bots
                foreach (Player bot in bots)
                {
                    // Ignore bots on the invalid despawn list, and the player
                    if (bot.IsYourPlayer || !validDespawnListPMC.Contains(bot.Profile.Info.Settings.Role) || bot.AIData.BotOwner.BotState != EBotState.Active)
                    {
                        continue;
                    }


                    // Don't include bots that have spawned within the last 10 seconds
                    if (Time.time - 10 < bot.AIData.BotOwner.ActivateTime)
                    {
                        continue;
                    }

                    float distance = (bot.Position - gameWorld.MainPlayer.Position).sqrMagnitude;
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        furthestBot = bot;
                    }

                    //add bots that match criteria but distance doesn't matter
                    tempBotCount++;
                }


            }
            else if(bottype == "scav")
            {
                if (Time.time - SCAVdespawnCooldown < SCAVdespawnCooldownDuration) { 
                    return;
                }

                //don't know distances so have to loop through all bots
                foreach (Player bot in bots)
                {
                    // Ignore bots on the invalid despawn list, and the player
                    if (bot.IsYourPlayer || !validDespawnListScav.Contains(bot.Profile.Info.Settings.Role) || bot.AIData.BotOwner.BotState != EBotState.Active)
                    {
                        continue;
                    }

                    // Don't include bots that have spawned within the last 10 seconds
                    if (Time.time - 10 < bot.AIData.BotOwner.ActivateTime)
                    {
                        continue;
                    }

                    float distance = (bot.Position - gameWorld.MainPlayer.Position).sqrMagnitude;
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        furthestBot = bot;
                    }

                    //add bots that match criteria but distance doesn't matter
                    tempBotCount++;
                }
            }

            if (furthestBot != null)
            {
                if(bottype == "pmc" && tempBotCount <= PMCBotLimit)
                {
                    return;
                }
                else if (bottype == "scav" && tempBotCount <= SCAVBotLimit)
                {
                    return;
                }

                // Despawn the bot
                Logger.LogDebug($"Despawning bot: {furthestBot.Profile.Info.Nickname} ({furthestBot.name})");

                BotOwner botOwner = furthestBot.AIData.BotOwner;

                var botgame = Singleton<IBotGame>.Instance;
                Singleton<Effects>.Instance.EffectsCommutator.StopBleedingForPlayer(botOwner.GetPlayer);
                botOwner.Deactivate();
                botOwner.Dispose();
                botgame.BotsController.BotDied(botOwner);
                botgame.BotsController.DestroyInfo(botOwner.GetPlayer);
                DestroyImmediate(botOwner.gameObject);
                Destroy(botOwner);

                if (bottype == "pmc")
                {
                    PMCdespawnCooldown = Time.time;
                }
                else if (bottype == "scav")
                { 
                    SCAVdespawnCooldown = Time.time;
                }
            }
            
        }
        private async Task<Vector3?> GetValidSpawnPosition(Entry hotspot, Vector3 coordinate, int maxSpawnAttempts)
        {
            for (int i = 0; i < maxSpawnAttempts; i++)
            {
                Vector3 spawnPosition = GenerateRandomSpawnPosition(hotspot, coordinate);

                if (NavMesh.SamplePosition(spawnPosition, out var navHit, 2f, NavMesh.AllAreas))
                {
                    spawnPosition = navHit.position;

                    if (IsValidSpawnPosition(spawnPosition, hotspot))
                    {
                        Logger.LogDebug("Found spawn position at: " + spawnPosition);
                        return spawnPosition;
                    }
                }

                await Task.Delay(1);
            }

            return null;
        }

        private Vector3 GenerateRandomSpawnPosition(Entry hotspot, Vector3 coordinate)
        {
            float randomX = UnityEngine.Random.Range(-hotspot.MaxDistance, hotspot.MaxDistance);
            float randomZ = UnityEngine.Random.Range(-hotspot.MaxDistance, hotspot.MaxDistance);

            return new Vector3(coordinate.x + randomX, coordinate.y, coordinate.z + randomZ);
        }
        private bool IsValidSpawnPosition(Vector3 spawnPosition, Entry hotspot)
        {
            if (spawnPosition != null && hotspot != null)
            {
                return !IsSpawnPositionInsideWall(spawnPosition) &&
                       !IsSpawnPositionInPlayerLineOfSight(spawnPosition) &&
                       !IsSpawnInAir(spawnPosition) &&
                       !IsMinSpawnDistanceFromPlayerTooShort(spawnPosition, hotspot);
            }
            return false;
        }
        private bool IsSpawnPositionInPlayerLineOfSight(Vector3 spawnPosition)
        {
            //add try catch for when player is null at end of raid
            try
            {
                Vector3 playerPosition = gameWorld.MainPlayer.MainParts[BodyPartType.head].Position;
                Vector3 direction = (playerPosition - spawnPosition).normalized;
                float distance = Vector3.Distance(spawnPosition, playerPosition);

                RaycastHit hit;
                if (!Physics.Raycast(spawnPosition, direction, out hit, distance, LayerMaskClass.HighPolyWithTerrainMask))
                {
                    // No objects found between spawn point and player
                    return true;
                }
            }
            catch { }

            return false;
        }
        private bool IsSpawnPositionInsideWall(Vector3 position)
        {
            // Check if any game object parent has the name "WALLS" in it
            Vector3 boxSize = new Vector3(1f, 1f, 1f);
            Collider[] colliders = Physics.OverlapBox(position, boxSize, Quaternion.identity, LayerMaskClass.LowPolyColliderLayer);

            foreach (var collider in colliders)
            {
                Transform currentTransform = collider.transform;
                while (currentTransform != null)
                {
                    if (currentTransform.gameObject.name.ToUpper().Contains("WALLS"))
                    {
                        return true;
                    }
                    currentTransform = currentTransform.parent;
                }
            }

            return false;
        }
        private bool IsSpawnInAir(Vector3 position)
        {
            // Raycast down and determine if the position is in the air or not
            Ray ray = new Ray(position, Vector3.down);
            float distance = 10f;

            if (Physics.Raycast(ray, out RaycastHit hit, distance, LayerMaskClass.HighPolyWithTerrainMask))
            {
                // If the raycast hits a collider, it means the position is not in the air
                return false;
            }
            return true;
        }

        private bool IsMinSpawnDistanceFromPlayerTooShort(Vector3 position, Entry hotspot)
        {
            //if distance between player and spawn position is less than the hotspot min distance
            if (Vector3.Distance(gameWorld.MainPlayer.Position, position) < hotspot.MinSpawnDistanceFromPlayer)
            {
                return true;
            }

            return false;
        }

        private StringBuilder DisplayedMarkerInfo = new StringBuilder();
        private StringBuilder PreviousMarkerInfo = new StringBuilder();
        private Coroutine resetMarkerInfoCoroutine;
        private void DisplayMarkerInformation()
        {
            if (gizmoSpheres.Count == 0)
            {
                return;
            }

            GameObject closestShape = null;
            float closestDistanceSq = float.MaxValue;

            // Find the closest primitive shape game object to the player
            foreach (var shape in gizmoSpheres)
            {
                Vector3 shapePosition = shape.transform.position;
                float distanceSq = (shapePosition - gameWorld.MainPlayer.Transform.position).sqrMagnitude;
                if (distanceSq < closestDistanceSq)
                {
                    closestDistanceSq = distanceSq;
                    closestShape = shape;
                }
            }

            // Check if the closest shape is within 15m and directly visible to the player
            if (closestShape != null && closestDistanceSq <= 10f * 10f)
            {
                Vector3 direction = closestShape.transform.position - gameWorld.MainPlayer.Transform.position;
                float angle = Vector3.Angle(gameWorld.MainPlayer.Transform.forward, direction);

                if (angle < 20f)
                {
                    // Create a HashSet of positions for fast containment checks
                    var locationsSet = new HashSet<Vector3>();
                    foreach (var entry in fightLocations.Locations.Concat(sessionLocations.Locations))
                    {
                        locationsSet.Add(new Vector3(entry.Position.x, entry.Position.y, entry.Position.z));
                    }

                    // Check if the closest shape's position is contained in the HashSet
                    Vector3 closestShapePosition = closestShape.transform.position;
                    if (locationsSet.Contains(closestShapePosition))
                    {
                        if (displayMessageNotificationMethod != null)
                        {
                            Entry closestEntry = GetClosestEntry(closestShapePosition);
                            if (closestEntry != null)
                            {
                                PreviousMarkerInfo.Clear();
                                PreviousMarkerInfo.Append(DisplayedMarkerInfo);

                                DisplayedMarkerInfo.Clear();

                                DisplayedMarkerInfo.AppendLine("Donuts: Marker Info");
                                DisplayedMarkerInfo.AppendLine($"GroupNum: {closestEntry.GroupNum}");
                                DisplayedMarkerInfo.AppendLine($"Name: {closestEntry.Name}");
                                DisplayedMarkerInfo.AppendLine($"SpawnType: {closestEntry.WildSpawnType}");
                                DisplayedMarkerInfo.AppendLine($"Position: {closestEntry.Position.x}, {closestEntry.Position.y}, {closestEntry.Position.z}");
                                DisplayedMarkerInfo.AppendLine($"Bot Timer Trigger: {closestEntry.BotTimerTrigger}");
                                DisplayedMarkerInfo.AppendLine($"Spawn Chance: {closestEntry.SpawnChance}");
                                DisplayedMarkerInfo.AppendLine($"Max Random Number of Bots: {closestEntry.MaxRandomNumBots}");
                                DisplayedMarkerInfo.AppendLine($"Max Spawns Before Cooldown: {closestEntry.MaxSpawnsBeforeCoolDown}");
                                DisplayedMarkerInfo.AppendLine($"Ignore Timer for First Spawn: {closestEntry.IgnoreTimerFirstSpawn}");
                                DisplayedMarkerInfo.AppendLine($"Min Spawn Distance From Player: {closestEntry.MinSpawnDistanceFromPlayer}");
                                string txt = DisplayedMarkerInfo.ToString();

                                // Check if the marker info has changed since the last update
                                if (txt != PreviousMarkerInfo.ToString())
                                {
                                    MethodInfo displayMessageNotificationMethod;
                                    if (methodCache.TryGetValue("DisplayMessageNotification", out displayMessageNotificationMethod))
                                    {
                                        displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });
                                    }

                                    // Stop the existing coroutine if it's running
                                    if (resetMarkerInfoCoroutine != null)
                                    {
                                        StopCoroutine(resetMarkerInfoCoroutine);
                                    }

                                    // Start a new coroutine to reset the marker info after a delay
                                    resetMarkerInfoCoroutine = StartCoroutine(ResetMarkerInfoAfterDelay());
                                }
                            }
                        }
                    }
                }
            }
        }
        private IEnumerator ResetMarkerInfoAfterDelay()
        {
            yield return new WaitForSeconds(5f);

            // Reset the marker info
            DisplayedMarkerInfo.Clear();
            resetMarkerInfoCoroutine = null;
        }
        private Entry GetClosestEntry(Vector3 position)
        {
            Entry closestEntry = null;
            float closestDistanceSq = float.MaxValue;

            foreach (var entry in fightLocations.Locations.Concat(sessionLocations.Locations))
            {
                Vector3 entryPosition = new Vector3(entry.Position.x, entry.Position.y, entry.Position.z);
                float distanceSq = (entryPosition - position).sqrMagnitude;
                if (distanceSq < closestDistanceSq)
                {
                    closestDistanceSq = distanceSq;
                    closestEntry = entry;
                }
            }

            return closestEntry;
        }
        public static MethodInfo GetDisplayMessageNotificationMethod() => displayMessageNotificationMethod;
        //------------------------------------------------------------------------------------------------------------------------- Gizmo Stuff

        //update gizmo display periodically instead of having to toggle it on and off
        private IEnumerator UpdateGizmoSpheresCoroutine()
        {
            while (isGizmoEnabled)
            {
                RefreshGizmoDisplay(); // Refresh the gizmo display periodically

                yield return new WaitForSeconds(3f);
            }
        }
        private void DrawMarkers(List<Entry> locations, Color color, PrimitiveType primitiveType)
        {
            foreach (var hotspot in locations)
            {
                var newCoordinate = new Vector3(hotspot.Position.x, hotspot.Position.y, hotspot.Position.z);

                if (maplocation == hotspot.MapName && !drawnCoordinates.Contains(newCoordinate))
                {
                    var marker = GameObject.CreatePrimitive(primitiveType);
                    var material = marker.GetComponent<Renderer>().material;
                    material.color = color;
                    marker.GetComponent<Collider>().enabled = false;
                    marker.transform.position = newCoordinate;

                    if (DonutsPlugin.gizmoRealSize.Value)
                    {
                        marker.transform.localScale = new Vector3(hotspot.MaxDistance, 3f, hotspot.MaxDistance);
                    }
                    else
                    {
                        marker.transform.localScale = new Vector3(1f, 1f, 1f);
                    }

                    gizmoSpheres.Add(marker);
                    drawnCoordinates.Add(newCoordinate);
                }
            }
        }

        public void ToggleGizmoDisplay(bool enableGizmos)
        {
            isGizmoEnabled = enableGizmos;

            if (isGizmoEnabled && gizmoUpdateCoroutine == null)
            {
                RefreshGizmoDisplay(); // Refresh the gizmo display initially
                gizmoUpdateCoroutine = StartCoroutine(UpdateGizmoSpheresCoroutine());
            }
            else if (!isGizmoEnabled && gizmoUpdateCoroutine != null)
            {
                StopCoroutine(gizmoUpdateCoroutine);
                gizmoUpdateCoroutine = null;

                ClearGizmoMarkers(); // Clear the drawn markers
            }
        }

        private void RefreshGizmoDisplay()
        {
            ClearGizmoMarkers(); // Clear existing markers

            // Check the values of DebugGizmos and gizmoRealSize and redraw the markers accordingly
            if (DonutsPlugin.DebugGizmos.Value)
            {
                if (fightLocations != null && fightLocations.Locations != null && fightLocations.Locations.Count > 0)
                {
                    DrawMarkers(fightLocations.Locations, Color.green, PrimitiveType.Sphere);
                }

                if (sessionLocations != null && sessionLocations.Locations != null && sessionLocations.Locations.Count > 0)
                {
                    DrawMarkers(sessionLocations.Locations, Color.red, PrimitiveType.Cube);
                }
            }
        }

        private void ClearGizmoMarkers()
        {
            foreach (var marker in gizmoSpheres)
            {
                Destroy(marker);
            }
            gizmoSpheres.Clear();
            drawnCoordinates.Clear();
        }

        private void OnGUI() => ToggleGizmoDisplay(DonutsPlugin.DebugGizmos.Value);
    }


    //------------------------------------------------------------------------------------------------------------------------- Classes

    public class HotspotTimer
    {
        private Entry hotspot;
        private float timer;
        public bool inCooldown;
        public int timesSpawned;
        private float cooldownTimer;
        public Entry Hotspot => hotspot;

        public HotspotTimer(Entry hotspot)
        {
            this.hotspot = hotspot;
            this.timer = 0f;
            this.inCooldown = false;
            this.timesSpawned = 0;
            this.cooldownTimer = 0f;
        }

        public void UpdateTimer()
        {
            timer += Time.deltaTime;
            if (inCooldown)
            {
                cooldownTimer += Time.deltaTime;
                if (cooldownTimer >= DonutsPlugin.coolDownTimer.Value)
                {
                    inCooldown = false;
                    cooldownTimer = 0f;
                    timesSpawned = 0;
                }
            }
        }

        public float GetTimer() => timer;
        public bool ShouldSpawn()
        {
            if (hotspot.IgnoreTimerFirstSpawn == true)
            {
                return true;
            }
            return timer >= hotspot.BotTimerTrigger;
        }

        public void ResetTimer() => timer = 0f;
    }

    public class Entry
    {
        public string MapName
        {
            get; set;
        }
        public int GroupNum
        {
            get; set;
        }
        public string Name
        {
            get; set;
        }
        public Position Position
        {
            get; set;
        }
        public string WildSpawnType
        {
            get; set;
        }
        public float MinDistance
        {
            get; set;
        }
        public float MaxDistance
        {
            get; set;
        }

        public float BotTriggerDistance
        {
            get; set;
        }

        public float BotTimerTrigger
        {
            get; set;
        }
        public int MaxRandomNumBots
        {
            get; set;
        }

        public int SpawnChance
        {
            get; set;
        }

        public int MaxSpawnsBeforeCoolDown
        {
            get; set;
        }

        public bool IgnoreTimerFirstSpawn
        {
            get; set;
        }

        public float MinSpawnDistanceFromPlayer
        {
            get; set;
        }
    }

    public class Position
    {
        public float x
        {
            get; set;
        }
        public float y
        {
            get; set;
        }
        public float z
        {
            get; set;
        }
    }

    public class FightLocations
    {
        public List<Entry> Locations
        {
            get; set;
        }
    }

    //Thanks ARI - taken from SPAWN
    class HttpClient
    {
        public static Profile[] GetBots(List<WaveInfo> conditions)
        {
            var s = new ConditionsWrapper();
            s.conditions = conditions;

            var p = RequestHandler.PostJson("/client/game/bot/generate", s.ToJson());
            return p.ParseJsonTo<BotsResponseWrapper>().data;
        }

        struct ConditionsWrapper
        {
            public List<WaveInfo> conditions;
        }

        struct BotsResponseWrapper
        {
            public Profile[] data;
        }
    }

    // UseAKIHTTPForBotLoadingPatch overrides the normal Diz.Jobs-based
    // loading, which creates a ton of garbage due to how that background
    // job manager is implemented.
    class UseAKIHTTPForBotLoadingPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.TypeByName("Class222").GetMethod("LoadBots");
        }

        [PatchPrefix]
        public static bool Prefix(ref Task<Profile[]> __result, List<WaveInfo> conditions)
        {
            TaskCompletionSource<Profile[]> tcs = new TaskCompletionSource<Profile[]>();
            __result = tcs.Task;

            Task.Factory.StartNew(() =>
            {
                Logger.LogWarning($"Loading a new bot from the server: {conditions.ToJson()}");
                var p = HttpClient.GetBots(conditions);
                // TODO: error handling.
                tcs.SetResult(p);
            });

            return false;
        }
    }

    internal class botClass
    {
        public async Task CreateBot(WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator, BotSpawnerClass botSpawnerClass, Vector3 spawnPosition, CancellationTokenSource cancellationToken)
        {
            var botdifficulty = grabBotDifficulty();
            //IBotData botData = new GClass629(side, wildSpawnType, botdifficulty, 0f, null);
            IBotData botData = new GClass629(side, wildSpawnType, botdifficulty, 0f, null);
            GClass628 bot = await GClass628.Create(botData, ibotCreator, 1, botSpawnerClass);
            bot.AddPosition((Vector3)spawnPosition);

            var closestBotZone = botSpawnerClass.GetClosestZone((Vector3)spawnPosition, out float dist);
            DonutComponent.Logger.LogWarning($"Spawning bot at distance to player of: {Vector3.Distance((Vector3)spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                $"of side: {bot.Side} and difficulty: {botdifficulty}");

            DonutComponent.methodCache["method_11"].Invoke(botSpawnerClass, new object[] { closestBotZone, bot, null, cancellationToken.Token });
        }

        public static BotDifficulty grabBotDifficulty()
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
