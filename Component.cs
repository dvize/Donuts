using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aki.Reflection.Utils;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Communications;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace Donuts
{
    public class DonutComponent : MonoBehaviour
    {
        private float botMinDistance;
        private float botMaxDistance;

        internal static FightLocations fightLocations = new FightLocations()
        {
            Locations = new List<Entry>()
        };

        internal static FightLocations sessionLocations = new FightLocations()
        {
            Locations = new List<Entry>()
        };

        private bool fileLoaded = false;
        public static string maplocation;

        public static GameWorld gameWorld;
        private static BotSpawnerClass botSpawnerClass;

        internal static List<HotspotTimer> hotspotTimers = new List<HotspotTimer>();
        private Dictionary<string, object> fieldCache;
        private Dictionary<string, MethodInfo> methodCache;
        private static MethodInfo displayMessageNotificationMethod;

        //gizmo stuff
        private bool isGizmoEnabled = false;
        internal static HashSet<Vector3> drawnCoordinates = new HashSet<Vector3>();
        internal static List<GameObject> gizmoSpheres = new List<GameObject>();
        private static Coroutine gizmoUpdateCoroutine;

        //game flow
        private int MaxSpawnAttempts = DonutsPlugin.maxSpawnTriesPerBot.Value;
        private float cooldownTime = 60f;
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
            fieldCache = new Dictionary<string, object>();
            methodCache = new Dictionary<string, MethodInfo>();

            // Cache the field and method lookups
            Type wildSpawnTypeEnum = typeof(EFT.WildSpawnType);
            var wildSpawnTypeInstance = Activator.CreateInstance(wildSpawnTypeEnum);
            var fieldInfos = wildSpawnTypeEnum.GetFields();

            // Retrieve displayMessageNotification MethodInfo
            var displayMessageNotification = PatchConstants.EftTypes.Single(x => x.GetMethod("DisplayMessageNotification") != null).GetMethod("DisplayMessageNotification");
            if (displayMessageNotification != null)
            {
                displayMessageNotificationMethod = displayMessageNotification;
                methodCache["DisplayMessageNotification"] = displayMessageNotification;
            }

            foreach (var fieldInfo in fieldInfos)
            {
                fieldCache[fieldInfo.Name] = fieldInfo.GetValue(wildSpawnTypeInstance);
            }

            var methodInfos = typeof(BotSpawnerClass).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var methodInfo in methodInfos)
            {
                if (methodInfo.Name == "method_12")
                {
                    methodCache[methodInfo.Name] = methodInfo;
                    break;
                }
            }
        }
        private void Start()
        {
            botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;

            maplocation = gameWorld.MainPlayer.Location.ToLower();
            Logger.LogDebug("Setup maplocation: " + maplocation);
            LoadFightLocations();
            if (DonutsPlugin.PluginEnabled.Value && fileLoaded)
            {
                InitializeHotspotTimers();
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
            foreach (var hotspot in fightLocations.Locations)
            {
                var hotspotTimer = new HotspotTimer(hotspot);
                hotspotTimers.Add(hotspotTimer);

            }
        }
        private void LoadFightLocations()
        {
            if (!fileLoaded)
            {
                string dllPath = Assembly.GetExecutingAssembly().Location;
                string directoryPath = Path.GetDirectoryName(dllPath);

                string jsonFolderPath = Path.Combine(directoryPath, "patterns");
                string[] jsonFiles = Directory.GetFiles(jsonFolderPath, "*.json");

                List<Entry> combinedLocations = new List<Entry>();

                foreach (string file in jsonFiles)
                {
                    FightLocations fightfile = JsonConvert.DeserializeObject<FightLocations>(File.ReadAllText(file));
                    combinedLocations.AddRange(fightfile.Locations);
                }

                if (combinedLocations.Count == 0)
                {
                    Logger.LogError("No Bot Fight Entries found in JSON files, disabling plugin");
                    Debug.Break();
                }

                Logger.LogDebug("Loaded " + combinedLocations.Count + " Bot Fight Entries");

                // Assign the combined fight locations to the fightLocations variable.
                fightLocations = new FightLocations { Locations = combinedLocations };

                //filter fightLocations for maplocation
                fightLocations.Locations.RemoveAll(x => x.MapName != maplocation);
                Logger.LogDebug("Valid Bot Fight Entries For Current Map: " + fightLocations.Locations.Count);

                fileLoaded = true;
            }
        }

        private async void Update()
        {
            if (DonutsPlugin.PluginEnabled.Value && fileLoaded)
            {
                foreach (var hotspotTimer in hotspotTimers)
                {
                    hotspotTimer.UpdateTimer();

                    if (hotspotTimer.ShouldSpawn())
                    {
                        var hotspot = hotspotTimer.Hotspot;
                        var coordinate = new Vector3(hotspot.Position.x, hotspot.Position.y, hotspot.Position.z);
                        if (IsWithinBotActivationDistance(hotspot, coordinate) && maplocation == hotspot.MapName)
                        {
                            // Check if passes hotspot.spawnChance
                            if (UnityEngine.Random.Range(0, 100) > hotspot.SpawnChance)
                            {
                                Logger.LogDebug("SpawnChance of " + hotspot.SpawnChance + "% Failed for hotspot: " + hotspot.Name);
                                hotspotTimer.ResetTimer();
                                Logger.LogDebug("Resetting Timer: " + hotspotTimer.GetTimer() + " for hotspot: " + hotspot.Name);
                                continue;
                            }

                            if (hotspotTimer.inCooldown)
                            {
                                Logger.LogDebug("Hotspot: " + hotspot.Name + " is in cooldown, skipping spawn");
                                continue;
                            }

                            Logger.LogDebug("Timer: " + hotspotTimer.GetTimer() + " Spawned Bots at: " + coordinate + " for hotspot: " + hotspot.Name);
                            await SpawnBots(hotspot, coordinate);
                            //make sure to check the times spawned in hotspotTimer and set cooldown bool if needed
                            if (hotspotTimer.timesSpawned >= hotspot.MaxSpawnsBeforeCoolDown)
                            {
                                hotspotTimer.inCooldown = true;
                            }
                            hotspotTimer.ResetTimer();
                            Logger.LogDebug("Resetting Timer: " + hotspotTimer.GetTimer() + " for hotspot: " + hotspot.Name);
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
            float distanceSquared = (gameWorld.MainPlayer.Position - position).sqrMagnitude;
            float activationDistanceSquared = hotspot.BotTriggerDistance * hotspot.BotTriggerDistance;
            return distanceSquared <= activationDistanceSquared;
        }
        private async Task SpawnBots(Entry hotspot, Vector3 coordinate)
        {
            Logger.LogDebug("Entered SpawnBots()");
            Logger.LogDebug("hotspot: " + hotspot.Name);

            int count = 0;
            int maxSpawnAttempts = 10;

            while (count < UnityEngine.Random.Range(1, hotspot.MaxRandomNumBots))
            {
                Vector3 spawnPosition = await getRandomSpawnPosition(hotspot, coordinate, maxSpawnAttempts);

                if (spawnPosition == Vector3.negativeInfinity)
                {
                    //need to count failed attempt and move to generating next bot
                    count++;
                    await Task.Delay(5);
                    continue;
                }

                EPlayerSide side = GetSideForWildSpawnType(GetWildSpawnType(hotspot.WildSpawnType));
                WildSpawnType wildSpawnType = GetWildSpawnType(hotspot.WildSpawnType.ToLower());
                botMinDistance = hotspot.MinDistance;
                botMaxDistance = hotspot.MaxDistance;

                // Setup bot details
                var bot = new GClass624(side, wildSpawnType, BotDifficulty.normal, 0f, null);

                var cancellationToken = AccessTools.Field(typeof(BotSpawnerClass), "cancellationTokenSource_0").GetValue(botSpawnerClass) as CancellationTokenSource;
                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
                Logger.LogDebug("Spawning bot at distance to player of: " + Vector3.Distance(spawnPosition, gameWorld.MainPlayer.Position) + " of side: " + bot.Side);

                methodCache["method_12"].Invoke(botSpawnerClass, new object[] { spawnPosition, closestBotZone, bot, null, cancellationToken.Token });

                count++;
                await Task.Delay(10);
            }
        }

        private WildSpawnType GetWildSpawnType(string spawnType)
        {
            //define spt wildspawn
            WildSpawnType sptUsec = (WildSpawnType)fieldCache["sptUsec"];
            WildSpawnType sptBear = (WildSpawnType)fieldCache["sptBear"];

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
                case "exusec":
                    return WildSpawnType.pmcBot;
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
                case "pmcbot":
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
            //define spt wildspawn
            WildSpawnType sptUsec = (WildSpawnType)fieldCache["sptUsec"];
            WildSpawnType sptBear = (WildSpawnType)fieldCache["sptBear"];

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
            //grab furthest bot in comparison to gameWorld.MainPlayer.Position and the bots position from registered players list in gameWorld
            var bots = gameWorld.RegisteredPlayers;
            if (bots.Count >= DonutsPlugin.AbsMaxBotCount.Value)
            {
                float maxDistance = -1f;
                Player furthestBot = null;

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
                    Logger.LogDebug("Despawning bot: " + furthestBot.Profile.Info.Nickname);

                    BotOwner botOwner = furthestBot.AIData.BotOwner;
                    BotControllerClass botControllerClass = botOwner.BotsController;
                    botControllerClass.BotDied(botOwner);
                    botControllerClass.DestroyInfo(furthestBot);
                    botOwner.Dispose();
                    furthestBot.Dispose();
                    Destroy(furthestBot);
                }
            }
        }
        private async Task<Vector3> getRandomSpawnPosition(Entry hotspot, Vector3 coordinate, int maxSpawnAttempts)
        {
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                Vector3 spawnPosition = GenerateRandomSpawnPosition(hotspot, coordinate);

                if (await IsValidSpawnPosition(spawnPosition))
                {
                    Logger.LogDebug("Found spawn position at: " + spawnPosition);
                    return spawnPosition;
                }

                await Task.Delay(5);
            }

            // Return an invalid position if maximum spawn attempts reached
            return Vector3.negativeInfinity;
        }

        private Vector3 GenerateRandomSpawnPosition(Entry hotspot, Vector3 coordinate)
        {
            Vector3 spawnPosition = coordinate;
            spawnPosition.x += UnityEngine.Random.Range(-hotspot.MaxDistance, hotspot.MaxDistance);
            spawnPosition.z += UnityEngine.Random.Range(-hotspot.MaxDistance, hotspot.MaxDistance);
            return spawnPosition;
        }

        private async Task<bool> IsValidSpawnPosition(Vector3 spawnPosition)
        {
            return !await IsSpawnPositionInvalid(spawnPosition);
        }
        private Task<bool> IsSpawnPositionInvalid(Vector3 spawnPosition)
        {
            return Task.FromResult(IsSpawnPositionInsideWall(spawnPosition) ||
                IsSpawnPositionInPlayerLineOfSight(spawnPosition) ||
                IsSpawnInAir(spawnPosition));
        }
        private bool IsSpawnPositionInPlayerLineOfSight(Vector3 spawnPosition)
        {
            Vector3 direction = (gameWorld.MainPlayer.MainParts[BodyPartType.head].Position - spawnPosition).normalized;
            Ray ray = new Ray(spawnPosition, direction);
            RaycastHit hit;
            float distance = Vector3.Distance(spawnPosition, gameWorld.MainPlayer.MainParts[BodyPartType.head].Position);
            if (Physics.Raycast(ray, out hit, distance, LayerMaskClass.HighPolyWithTerrainMask))
            {
                // If hit has something in it and it does not have a player component in it then return true
                if (hit.collider != null && !hit.collider.GetComponentInParent<Player>())
                {
                    return true;
                }
            }

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
            float distance = 100f;

            if (Physics.Raycast(ray, out RaycastHit hit, distance, LayerMaskClass.HighPolyWithTerrainMask))
            {
                // If the raycast hits a collider, it means the position is not in the air
                return false;
            }
            return true;
        }


        private string lastDisplayedMarkerInfo = string.Empty;
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

            // Check if the closest shape is within 20m and directly visible to the player
            if (closestShape != null && closestDistanceSq <= 20f * 20f)
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
                                var txt =
                                    $"Donuts: Marker Info\n" +
                                    $"Name: {closestEntry.Name}\n" +
                                    $"SpawnType: {closestEntry.WildSpawnType}\n" +
                                    $"Position: {closestEntry.Position.x}, {closestEntry.Position.y}, {closestEntry.Position.z}\n" +
                                    $"Bot Timer Trigger: {closestEntry.BotTimerTrigger}\n" +
                                    $"Spawn Chance: {closestEntry.SpawnChance}\n" +
                                    $"Max Random Number of Bots: {closestEntry.MaxRandomNumBots}";

                                // Check if the marker info has changed since the last update
                                if (txt != lastDisplayedMarkerInfo)
                                {
                                    lastDisplayedMarkerInfo = txt;

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
            lastDisplayedMarkerInfo = string.Empty;
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
        public static MethodInfo GetDisplayMessageNotificationMethod()
        {
            return displayMessageNotificationMethod;
        }
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

        private void OnGUI()
        {
            ToggleGizmoDisplay(DonutsPlugin.DebugGizmos.Value);
        }
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
                }
            }
        }

        public float GetTimer()
        {
            return timer;
        }
        public bool ShouldSpawn()
        {
            return timer >= hotspot.BotTimerTrigger;
        }

        public void ResetTimer()
        {
            timer = 0f;
        }
    }

    public class Entry
    {
        public string MapName
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
