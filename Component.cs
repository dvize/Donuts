using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aki.Reflection.Utils;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Communications;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using SAIN;
using SAIN.Components;

namespace Donuts
{
    public class DonutComponent : MonoBehaviour
    {
        private float botMinDistance;
        private float botMaxDistance;
        private bool isSainEnabled = false;
        private SAINComponent sainComponent;

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

        //add plugin checker from chainloader
        public static bool IsPluginEnabled(string pluginGuid)
        {
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                if (plugin.Metadata.GUID == pluginGuid && plugin.Instance.enabled)
                {
                    return true;
                }
            }
            return false;
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

            isSainEnabled = IsPluginEnabled("me.sol.sain");
            Logger.LogDebug("SAIN Enabled: " + isSainEnabled);

            if (isSainEnabled)
            {
                // SAIN plugin is loaded, you can access its components or perform related actions
                sainComponent = Chainloader.PluginInfos.Values
                    .FirstOrDefault(pluginInfo => pluginInfo.Metadata.GUID == "me.sol.sain")
                    ?.Instance
                    ?.GetComponent<SAINComponent>();
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

                            Logger.LogDebug("Timer: " + hotspotTimer.GetTimer() + " Spawned Bots at: " + coordinate + " for hotspot: " + hotspot.Name);
                            await SpawnBots(hotspot, coordinate);
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

            while (count < UnityEngine.Random.Range(1, hotspot.MaxRandomNumBots))
            {
                EPlayerSide side;
                WildSpawnType wildSpawnType;
                botMinDistance = hotspot.MinDistance;
                botMaxDistance = hotspot.MaxDistance;

                //define spt wildspawn

                WildSpawnType sptUsec = (WildSpawnType)fieldCache["sptUsec"];
                WildSpawnType sptBear = (WildSpawnType)fieldCache["sptBear"];

                switch (hotspot.WildSpawnType.ToLower())
                {
                    case "assault":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.assault;
                        break;
                    case "assaultgroup":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.assaultGroup;
                        break;
                    case "bossbully":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.bossBully;
                        break;
                    case "bossgluhar":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.bossGluhar;
                        break;
                    case "bosskilla":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.bossKilla;
                        break;
                    case "bosskojaniy":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.bossKojaniy;
                        break;
                    case "bosssanitar":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.bossSanitar;
                        break;
                    case "bosstagilla":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.bossTagilla;
                        break;
                    case "bosszryachiy":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.bossZryachiy;
                        break;
                    case "cursedassault":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.cursedAssault;
                        break;
                    case "exusec":
                        side = EPlayerSide.Usec;
                        wildSpawnType = WildSpawnType.pmcBot;
                        break;
                    case "followerbully":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerBully;
                        break;
                    case "followergluharassault":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerGluharAssault;
                        break;
                    case "followergluharscout":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerGluharScout;
                        break;
                    case "followergluharsecurity":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerGluharSecurity;
                        break;
                    case "followergluharsnipe":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerGluharSnipe;
                        break;
                    case "followerkojaniy":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerKojaniy;
                        break;
                    case "followersanitar":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerSanitar;
                        break;
                    case "followertagilla":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerTagilla;
                        break;
                    case "followerzryachiy":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerZryachiy;
                        break;
                    case "gifter":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.gifter;
                        break;
                    case "marksman":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.marksman;
                        break;
                    case "pmcbot":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.pmcBot;
                        break;
                    case "sectantpriest":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.sectantPriest;
                        break;
                    case "sectantwarrior":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.sectantWarrior;
                        break;
                    case "usec":
                        side = EPlayerSide.Usec;
                        wildSpawnType = sptUsec;
                        break;
                    case "bear":
                        side = EPlayerSide.Bear;
                        wildSpawnType = sptBear;
                        break;
                    case "sptusec":
                        side = EPlayerSide.Usec;
                        wildSpawnType = sptUsec;
                        break;
                    case "sptbear":
                        side = EPlayerSide.Bear;
                        wildSpawnType = sptBear;
                        break;
                    case "followerbigpipe":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerBigPipe;
                        break;
                    case "followerbirdeye":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.followerBirdEye;
                        break;
                    case "bossknight":
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.bossKnight;
                        break;
                    case "pmc":
                        //random wildspawntype is either assigned sptusec or sptbear at 50/50 chance
                        wildSpawnType = UnityEngine.Random.Range(0, 2) == 0 ? sptUsec : sptBear;
                        side = (wildSpawnType == sptUsec ? EPlayerSide.Usec : EPlayerSide.Bear);
                        break;
                    default:
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.assault;
                        break;
                }

                Vector3 spawnPosition = await getRandomSpawnPosition(hotspot, coordinate);

                //setup bot details
                var bot = new GClass624(side, wildSpawnType, BotDifficulty.normal, 0f, null);

                var cancellationToken = AccessTools.Field(typeof(BotSpawnerClass), "cancellationTokenSource_0").GetValue(botSpawnerClass) as CancellationTokenSource;
                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
                Logger.LogDebug("Spawning bot at distance to player of: " + Vector3.Distance(spawnPosition, gameWorld.MainPlayer.Position) + " of side: " + bot.Side);

                methodCache["method_12"].Invoke(botSpawnerClass, new object[] { spawnPosition, closestBotZone, bot, null, cancellationToken.Token });

                await Task.Delay(5);
                count++;
            }


        }

        private void DespawnFurthestBot()
        {
            //grab furthest bot in comparison to gameWorld.MainPlayer.Position and the bots position from registered players list in gameWorld
            var bots = gameWorld.RegisteredPlayers;
            if (gameWorld.RegisteredPlayers.Count >= DonutsPlugin.AbsMaxBotCount.Value)
            {
                var furthestBot = bots.OrderByDescending(x => Vector3.Distance(x.Position, gameWorld.MainPlayer.Position)).FirstOrDefault();
                if (furthestBot != null)
                {
                    //despawn the bot

                    if (sainComponent != null)
                    {
                        Logger.LogDebug("Despawning bot: " + furthestBot.Profile.Info.Nickname);
                        sainComponent.BotController.RemoveBot(furthestBot.ProfileId);
                    }
                    else
                    {
                        Logger.LogDebug("Despawning bot: " + furthestBot.Profile.Info.Nickname);
                        Singleton<IBotGame>.Instance.BotUnspawn(furthestBot.AIData.BotOwner);
                    }
                }
            }
        }
        private async Task<Vector3> getRandomSpawnPosition(Entry hotspot, Vector3 coordinate)
        {
            Vector3 spawnPosition;

            //keep grabbing random spawn positions on the same hotspot.Position.y until we find one that is not inside a wall
            do
            {
                spawnPosition = coordinate;
                spawnPosition.x += UnityEngine.Random.Range(-hotspot.MaxDistance, hotspot.MaxDistance);
                spawnPosition.z += UnityEngine.Random.Range(-hotspot.MaxDistance, hotspot.MaxDistance);
                await Task.Delay(5);
            } while (!IsValidSpawnPosition(spawnPosition));

            Logger.LogDebug("Found spawn position at: " + spawnPosition);
            return spawnPosition;
        }

        private bool IsValidSpawnPosition(Vector3 spawnPosition)
        {
            return !IsSpawnPositionInsideWall(spawnPosition) && !IsSpawnPositionInPlayerLineOfSight(spawnPosition) && !IsSpawnInAir(spawnPosition);
        }
        private bool IsSpawnPositionInPlayerLineOfSight(Vector3 spawnPosition)
        {
            Vector3 direction = (gameWorld.MainPlayer.MainParts[BodyPartType.head].Position - spawnPosition).normalized;
            Ray ray = new Ray(spawnPosition, direction);
            RaycastHit hit;
            float Distance = Vector3.Distance(spawnPosition, gameWorld.MainPlayer.MainParts[BodyPartType.head].Position);
            if (Physics.Raycast(ray, out hit, Distance, LayerMaskClass.HighPolyWithTerrainMask))
            {
                //if hit has something in it and it does not have a player component in it then return false
                if (hit.collider != null && !hit.collider.GetComponentInParent<Player>())
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsSpawnPositionInsideWall(Vector3 position)
        {
            //check if any gameobject parent has the name "WALLS" in it
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

            // If the raycast does not hit any collider, the position is in the air
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
        public Entry Hotspot => hotspot;

        public HotspotTimer(Entry hotspot)
        {
            this.hotspot = hotspot;
            this.timer = 0f;
        }

        public void UpdateTimer()
        {
            timer += Time.deltaTime;
        }

        public float GetTimer()
        {
            return timer;
        }
        public bool ShouldSpawn()
        {
            if (timer >= hotspot.BotTimerTrigger)
            {
                return true;
            }
            return false;
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
