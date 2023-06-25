using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AI;

namespace Donuts
{
    public class DonutComponent : MonoBehaviour
    {
        private static float botMinDistance;
        private static float botMaxDistance;
        private static float timer;
        private static FightLocations fightLocations;
        private static bool fileLoaded = false;
        private static string maplocation;
        private static bool isOnNavMesh;
        private static bool notVisibleToPlayer;
        private static bool validNavPath;
        private static NavMeshPath path;
        private static bool notARoof;
        private static float groundHeight;
        private static GClass624 bot;

        private static Vector3 coordinate;
        private static Vector3 spawnPosition;

        private static GameWorld gameWorld;

        private static BotSpawnerClass botSpawnerClass;
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

        private void Start()
        {
            Logger.LogDebug("Setup maplocation");
            maplocation = gameWorld.MainPlayer.Location.ToLower();

            LoadFightLocations();
        }
        public static void Enable()
        {
            if (Singleton<IBotGame>.Instantiated)
            {
                gameWorld = Singleton<GameWorld>.Instance;
                gameWorld.GetOrAddComponent<DonutComponent>();

                botSpawnerClass = (Singleton<IBotGame>.Instance).BotsController.BotSpawner;

                Logger.LogDebug("Donuts Enabled");
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

        private void Update()
        {
            if (DonutsPlugin.PluginEnabled.Value && fileLoaded)
            {
                timer += Time.deltaTime;

                if (timer >= DonutsPlugin.SpawnTimer.Value)
                {
                    bool botsSpawned = false;

                    foreach (var hotspot in fightLocations.Locations)
                    {
                        maplocation = gameWorld.MainPlayer.Location.ToLower();
                        coordinate = new Vector3(hotspot.Position.x, hotspot.Position.y, hotspot.Position.z);

                        if (IsWithinBotActivationDistance(coordinate, hotspot.MaxDistance) && maplocation == hotspot.MapName)
                        {
                            //check if passes hotspot.spawnChance
                            if (UnityEngine.Random.Range(0, 100) > hotspot.SpawnChance)
                            {
                                Logger.LogDebug("SpawnChance of " + hotspot.SpawnChance + "% Failed for hotspot : " + hotspot.Name);
                                continue;
                            }
                            SpawnBots(coordinate, hotspot);
                            botsSpawned = true;

                            //Remove Break as we want them to be able to define the same location but with bot specific changes.
                            //break;
                        }
                    }

                    if (botsSpawned)
                    {
                        timer = 0.0f; // Reset the timer only if bots were spawned
                    }
                }
            }
        }

        private bool IsWithinBotActivationDistance(Vector3 position, float MaxDistance)
        {
            float distance = Vector3.Distance(gameWorld.MainPlayer.Position, position);
            return distance <= MaxDistance;
        }

        private async void SpawnBots(Vector3 coordinate, Entry hotspot)
        {
            Logger.LogDebug("Timer: " + timer);
            Logger.LogDebug("Entered SpawnBots()");
            Logger.LogDebug("hotspot: " + hotspot.Name);

            int count = 0;

            try
            {
                while (count < UnityEngine.Random.Range(1, hotspot.MaxRandomNumBots))
                {
                    EPlayerSide side;
                    WildSpawnType wildSpawnType;
                    botMinDistance = hotspot.MinDistance;
                    botMaxDistance = hotspot.MaxDistance;

                    //define spt wildspawn
                    Type wildSpawnTypeEnum = typeof(EFT.WildSpawnType);

                    WildSpawnType sptUsec = (WildSpawnType)wildSpawnTypeEnum.GetField("sptUsec").GetValue(null);
                    WildSpawnType sptBear = (WildSpawnType)wildSpawnTypeEnum.GetField("sptBear").GetValue(null);

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
                        default:
                            side = EPlayerSide.Savage;
                            wildSpawnType = WildSpawnType.assault;
                            break;
                    }

                    //setup bot details
                    bot = new GClass624(side, wildSpawnType, BotDifficulty.normal, 0f, null);

                    spawnPosition = GetRandomSpawnPosition(coordinate, botMinDistance, botMaxDistance);

                    isOnNavMesh = NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 1f, NavMesh.AllAreas);

                    //move check up here to make sure roof isn't in gameobject name from collider
                    spawnPosition = hit.position;
                    Ray ray = new Ray(spawnPosition, Vector3.down);
                    if (Physics.Raycast(ray, out RaycastHit heightHit, 100f, LayerMaskClass.DefaultLayer))
                    {
                        groundHeight = heightHit.point.y;
                        notARoof = (!heightHit.collider.gameObject.name.ToLower().Contains("roof")) &&
                            (!heightHit.collider.gameObject.transform.parent.gameObject.name.ToLower().Contains("roof")) &&
                            (!heightHit.collider.gameObject.name.ToLower().Contains("blocker"));

                        // Adjust the spawn position to the ground height if it's above the ground
                        if (spawnPosition.y > groundHeight)
                        {
                            spawnPosition.y = groundHeight;
                        }

                    }

                    path = new NavMeshPath();

                    validNavPath = NavMesh.CalculatePath(hit.position, coordinate, NavMesh.AllAreas, path);

                    //update hit position

                    notVisibleToPlayer = Physics.Linecast(gameWorld.MainPlayer.MainParts[BodyPartType.head].Position, hit.position, out RaycastHit hitInfo, LayerMaskClass.PlayerStaticCollisionsMask);

                    if (isOnNavMesh && notVisibleToPlayer && validNavPath && notARoof && !IsSpawnPositionInsideWall(spawnPosition))
                    {
                        var botZones = AccessTools.Field(typeof(BotSpawnerClass), "botZone_0").GetValue(botSpawnerClass) as BotZone[];
                        var cancellationToken = AccessTools.Field(typeof(BotSpawnerClass), "cancellationTokenSource_0").GetValue(botSpawnerClass) as CancellationTokenSource;
                        var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
                        Logger.LogDebug("Spawning bot at distance of: " + Vector3.Distance(spawnPosition, gameWorld.MainPlayer.Position) + " of side: " + bot.Side);

                        AccessTools.Method(typeof(BotSpawnerClass), "method_12").Invoke(botSpawnerClass, new object[] { spawnPosition, closestBotZone, bot, null, cancellationToken.Token });
                        count++;
                    }

                    await Task.Delay(20);
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                Logger.LogError(ex.TargetSite.ToString());
            }
        }

        private Vector3 GetRandomSpawnPosition(Vector3 coordinate, float minDistance, float maxDistance)
        {

            Vector3 direction = UnityEngine.Random.onUnitSphere;
            float distance = UnityEngine.Random.Range(minDistance, maxDistance);

            if (distance > maxDistance)
                distance = maxDistance;

            Vector3 spawnPosition = coordinate + (direction * distance);

            return spawnPosition;
        }
        private bool IsSpawnPositionInsideWall(Vector3 position)
        {
            int layerMask = LayerMaskClass.DefaultLayer;

            //check if any gameobject parent has the name "WALLS" in it
            if (Physics.SphereCast(position, 1.0f, Vector3.zero, out RaycastHit hitInfo, 0f, layerMask))
            {
                Transform currentTransform = hitInfo.collider.gameObject.transform;

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
