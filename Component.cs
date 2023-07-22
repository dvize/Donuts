using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Aki.PrePatch;
using Aki.Reflection.Utils;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Communications;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AI;

namespace Donuts
{
    public class DonutComponent : MonoBehaviour
    {
        internal static FightLocations fightLocations;
        internal static FightLocations sessionLocations;

        internal static List<List<Entry>> groupedFightLocations;
        internal static Dictionary<int, List<HotspotTimer>> groupedHotspotTimers;

        internal List<WildSpawnType> validDespawnList = new List<WildSpawnType>()
        {
            WildSpawnType.assault,
            WildSpawnType.cursedAssault,
            (WildSpawnType)AkiBotsPrePatcher.sptUsecValue,
            (WildSpawnType)AkiBotsPrePatcher.sptBearValue
        };

        private bool fileLoaded = false;
        public static string maplocation;
        private int AbsBotLimit = 0;
        public static GameWorld gameWorld;
        private static BotSpawnerClass botSpawnerClass;
        private float despawnCooldown = 0f;
        private float despawnCooldownDuration = 10f;

        internal static List<HotspotTimer> hotspotTimers;
        private Dictionary<string, MethodInfo> methodCache;
        private static MethodInfo displayMessageNotificationMethod;

        //gizmo stuff
        private bool isGizmoEnabled = false;
        internal static HashSet<Vector3> drawnCoordinates;
        internal static List<GameObject> gizmoSpheres;
        private static Coroutine gizmoUpdateCoroutine;


        protected static ManualLogSource Logger
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
            methodCache = new Dictionary<string, MethodInfo>();

            // Retrieve displayMessageNotification MethodInfo
            var displayMessageNotification = PatchConstants.EftTypes.Single(x => x.GetMethod("DisplayMessageNotification") != null).GetMethod("DisplayMessageNotification");
            if (displayMessageNotification != null)
            {
                displayMessageNotificationMethod = displayMessageNotification;
                methodCache["DisplayMessageNotification"] = displayMessageNotification;
            }

            var methodInfo = typeof(BotSpawnerClass).GetMethod("method_12", BindingFlags.Instance | BindingFlags.NonPublic);

            if (methodInfo != null)
            {
                methodCache[methodInfo.Name] = methodInfo;
            }
        }
        private void Start()
        {
            botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;

            // setup the rest of donuts for the selected folder
            InitializeStaticVariables();
            maplocation = gameWorld.MainPlayer.Location.ToLower();
            Logger.LogDebug("Setup maplocation: " + maplocation);
            LoadFightLocations();
            if (DonutsPlugin.PluginEnabled.Value && fileLoaded)
            {
                InitializeHotspotTimers();
            }
            SetupBotLimit();
            Logger.LogDebug("Setup bot limit: " + AbsBotLimit);
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
        }
        private void SetupBotLimit()
        {
            switch (maplocation)
            {
                case "factory4_day":
                case "factory4_night":
                    AbsBotLimit = DonutsPlugin.factoryBotLimit.Value;
                    break;
                case "bigmap":
                    AbsBotLimit = DonutsPlugin.customsBotLimit.Value;
                    break;
                case "interchange":
                    AbsBotLimit = DonutsPlugin.interchangeBotLimit.Value;
                    break;
                case "rezervbase":
                    AbsBotLimit = DonutsPlugin.reserveBotLimit.Value;
                    break;
                case "laboratory":
                    AbsBotLimit = DonutsPlugin.laboratoryBotLimit.Value;
                    break;
                case "lighthouse":
                    AbsBotLimit = DonutsPlugin.lighthouseBotLimit.Value;
                    break;
                case "shoreline":
                    AbsBotLimit = DonutsPlugin.shorelineBotLimit.Value;
                    break;
                case "woods":
                    AbsBotLimit = DonutsPlugin.woodsBotLimit.Value;
                    break;
                case "tarkovstreets":
                    AbsBotLimit = DonutsPlugin.tarkovstreetsBotLimit.Value;
                    break;
                default:
                    AbsBotLimit = 18;
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
                Logger.LogDebug("Selected Folder: " + selectedFolder.Name);
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
                        var randomIndex = UnityEngine.Random.Range(0, (groupHotspotTimers.Count - 1));
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
                                StartCoroutine(SpawnBotsCoroutine(hotspotTimer, coordinate));
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
                    DespawnFurthestBot();
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
        private IEnumerator SpawnBotsCoroutine(HotspotTimer hotspotTimer, Vector3 coordinate)
        {
            int count = 0;
            int maxSpawnAttempts = DonutsPlugin.maxSpawnTriesPerBot.Value;

            // Moved outside so all spawns for a point are on the same side
            EPlayerSide side = GetSideForWildSpawnType(GetWildSpawnType(hotspotTimer.Hotspot.WildSpawnType));
            WildSpawnType wildSpawnType = GetWildSpawnType(hotspotTimer.Hotspot.WildSpawnType.ToLower());

            while (count < UnityEngine.Random.Range(1, hotspotTimer.Hotspot.MaxRandomNumBots))
            {
                Vector3? spawnPosition = GetValidSpawnPosition(hotspotTimer.Hotspot, coordinate, maxSpawnAttempts);

                if (!spawnPosition.HasValue)
                {
                    // Failed to get a valid spawn position, move on to generating the next bot
                    Logger.LogDebug($"Actually Failed to get a valid spawn position after {maxSpawnAttempts}, moving on to next bot anyways");
                    count++;
                    continue;
                }


                // Setup bot details
                var bot = new GClass624(side, wildSpawnType, BotDifficulty.normal, -1f, new GClass614 { TriggerType = SpawnTriggerType.none });

                var cancellationToken = AccessTools.Field(typeof(BotSpawnerClass), "cancellationTokenSource_0").GetValue(botSpawnerClass) as CancellationTokenSource;
                var closestBotZone = botSpawnerClass.GetClosestZone((Vector3)spawnPosition, out float dist);
                Logger.LogWarning("Spawning bot at distance to player of: " + Vector3.Distance((Vector3)spawnPosition, gameWorld.MainPlayer.Position) + " of side: " + bot.Side);

                methodCache["method_12"].Invoke(botSpawnerClass, new object[] { (Vector3)spawnPosition, closestBotZone, bot, null, cancellationToken.Token });


                count++;
                yield return new WaitForSeconds(0.16f);
            }
        }
        private WildSpawnType GetWildSpawnType(string spawnType)
        {

            switch (spawnType.ToLower())
            {
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

        private void DespawnFurthestBot()
        {
            if (Time.time - despawnCooldown < despawnCooldownDuration)
            {
                return; // Exit the method without despawning
            }

            //grab furthest bot in comparison to gameWorld.MainPlayer.Position and the bots position from registered players list in gameWorld
            var bots = gameWorld.RegisteredPlayers;

            if ((bots.Count - 1) >= AbsBotLimit)
            {
                float maxDistance = -1f;
                Player furthestBot = null;

                //filter out bots that are not in the valid despawnable list or is your own player
                bots = bots.Where(x => validDespawnList.Contains(x.Profile.Info.Settings.Role) && !x.IsYourPlayer && x.AIData.BotOwner.BotState == EBotState.Active).ToList();

                //don't know distances so have to loop through all bots
                foreach (Player bot in bots)
                {
                    float distance = Vector3.Distance(bot.Position, gameWorld.MainPlayer.Position);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        furthestBot = bot;
                    }
                }

                if (furthestBot != null)
                {
                    // Despawn the bot
                    Logger.LogDebug($"Despawning bot: {furthestBot.Profile.Info.Nickname} ({furthestBot.name})");

                    BotOwner botOwner = furthestBot.AIData.BotOwner;

                    var botgame = Singleton<IBotGame>.Instance;
                    botOwner.Deactivate();
                    botOwner.Dispose();
                    botgame.BotUnspawn(botOwner);
                    Destroy(botOwner);

                    despawnCooldown = Time.time;
                }
            }
        }
        private Vector3? GetValidSpawnPosition(Entry hotspot, Vector3 coordinate, int maxSpawnAttempts)
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


}
