using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
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

        private static Vector3 coordinate;
        private static Vector3 spawnPosition;

        private static GameWorld gameWorld;
        private Player player;

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
            SetupBotDistanceForMap();
            Logger.LogDebug("Setup Bot Min Distance for Map: " + botMinDistance);
            Logger.LogDebug("Setup Bot Max Distance for Map: " + botMaxDistance);

            LoadFightLocations();

            Logger.LogDebug("Loaded Bot Fight Locations: " + fightLocations.Locations.Count);
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

        private void SetupBotDistanceForMap()
        {
            Logger.LogDebug("Setup Bot Distance for Map");
            maplocation = gameWorld.MainPlayer.Location.ToLower();

            switch (maplocation)
            {
                case "factory4_day":
                case "factory4_night":
                    botMinDistance = DonutsPlugin.factoryMinDistance.Value;
                    botMaxDistance = DonutsPlugin.factoryMaxDistance.Value;
                    break;
                case "bigmap":
                    botMinDistance = DonutsPlugin.customsMinDistance.Value;
                    botMaxDistance = DonutsPlugin.customsMaxDistance.Value;
                    break;
                case "interchange":
                    botMinDistance = DonutsPlugin.interchangeMinDistance.Value;
                    botMaxDistance = DonutsPlugin.interchangeMaxDistance.Value;
                    break;
                case "rezervbase":
                    botMinDistance = DonutsPlugin.reserveMinDistance.Value;
                    botMaxDistance = DonutsPlugin.reserveMaxDistance.Value;
                    break;
                case "laboratory":
                    botMinDistance = DonutsPlugin.laboratoryMinDistance.Value;
                    botMaxDistance = DonutsPlugin.laboratoryMaxDistance.Value;
                    break;
                case "lighthouse":
                    botMinDistance = DonutsPlugin.lighthouseMinDistance.Value;
                    botMaxDistance = DonutsPlugin.lighthouseMaxDistance.Value;
                    break;
                case "shoreline":
                    botMinDistance = DonutsPlugin.shorelineMinDistance.Value;
                    botMaxDistance = DonutsPlugin.shorelineMaxDistance.Value;
                    break;
                case "woods":
                    botMinDistance = DonutsPlugin.woodsMinDistance.Value;
                    botMinDistance = DonutsPlugin.woodsMaxDistance.Value;
                    break;
                case "tarkovstreets":
                    botMinDistance = DonutsPlugin.tarkovstreetsMinDistance.Value;
                    botMaxDistance = DonutsPlugin.tarkovstreetsMaxDistance.Value;
                    break;
                default:
                    botMinDistance = 50.0f;
                    botMaxDistance = 150.0f;
                    break;
            }
        }

        private void LoadFightLocations()
        {
            if (!fileLoaded)
            {
                string dllPath = Assembly.GetExecutingAssembly().Location;
                string directoryPath = Path.GetDirectoryName(dllPath);

                // Get the path to the JSON file from the DLL file.
                string jsonPath = Path.Combine(directoryPath, "FightLocations.json");

                // Try to read the JSON file of FightLocations using Newtonsoft.Json into fightLocations, else show error and disable the plugin.
                if (!File.Exists(jsonPath))
                {
                    Logger.LogError("FightLocations.json not found, disabling plugin");
                    Debug.Break();
                }

                fightLocations = JsonConvert.DeserializeObject<FightLocations>(File.ReadAllText(jsonPath));
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

                        if (IsWithinBotDistance(coordinate) && maplocation == hotspot.MapName)
                        {
                            SpawnBots(coordinate);
                            botsSpawned = true;

                            //break after first point found to spawn
                            break;
                        }
                    }

                    if (botsSpawned)
                    {
                        timer = 0.0f; // Reset the timer only if bots were spawned
                    }
                }
            }
        }

        private bool IsWithinBotDistance(Vector3 position)
        {
            float distance = Vector3.Distance(gameWorld.MainPlayer.Position, position);
            return distance <= botMaxDistance;
        }

        private async void SpawnBots(Vector3 coordinate)
        {
            Logger.LogDebug("Timer: " + timer);
            Logger.LogDebug("Entered SpawnBots()");
            int count = 0;
            int botMin = DonutsPlugin.BotMin.Value;
            int botMax = DonutsPlugin.BotMax.Value;

            if (botMin >= botMax)
            {
                Logger.LogWarning("Invalid bot min and max values. BotMin should be less than BotMax.");
                return;
            }

            try
            {
                while (count < UnityEngine.Random.Range(botMin, botMax))
                {
                    string type = (UnityEngine.Random.Range(0, 2) == 0) ? "scav" : "pmc";
                    EPlayerSide side;
                    WildSpawnType wildSpawnType;

                    if (type == "scav")
                    {
                        side = EPlayerSide.Savage;
                        wildSpawnType = WildSpawnType.assault;
                    }
                    else
                    {
                        side = (UnityEngine.Random.Range(0, 2) == 0) ? EPlayerSide.Usec : EPlayerSide.Bear;
                        wildSpawnType = WildSpawnType.pmcBot;
                    }

                    var bot = new GClass624(side, wildSpawnType, BotDifficulty.normal, 0f, null);

                    spawnPosition = GetRandomSpawnPosition(coordinate, botMinDistance, botMaxDistance);

                    isOnNavMesh = NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 1f, NavMesh.AllAreas);
                    path = new NavMeshPath();

                    validNavPath = NavMesh.CalculatePath(spawnPosition, coordinate, NavMesh.AllAreas, path);

                   //update hit position
                   
                    notVisibleToPlayer = Physics.Linecast(gameWorld.MainPlayer.MainParts[BodyPartType.head].Position, spawnPosition, out RaycastHit hitInfo, LayerMaskClass.PlayerStaticCollisionsMask);

                    if (isOnNavMesh && notVisibleToPlayer && validNavPath)
                    {
                        spawnPosition = hit.position;
                        Ray ray = new Ray(spawnPosition, Vector3.down);
                        if (Physics.Raycast(ray, out RaycastHit heightHit, 100f, LayerMaskClass.HighPolyWithTerrainMaskAI))
                        {
                            float groundHeight = heightHit.point.y;

                            // Adjust the spawn position to the ground height if it's above the ground
                            if (spawnPosition.y > groundHeight)
                            {
                                spawnPosition.y = groundHeight;
                                var botZones = AccessTools.Field(typeof(BotSpawnerClass), "botZone_0").GetValue(botSpawnerClass) as BotZone[];
                                var cancellationToken = AccessTools.Field(typeof(BotSpawnerClass), "cancellationTokenSource_0").GetValue(botSpawnerClass) as CancellationTokenSource;

                                Logger.LogDebug("Spawning bot at distance of: " + Vector3.Distance(spawnPosition, gameWorld.MainPlayer.Position) + " of side: " + bot.Side);

                                AccessTools.Method(typeof(BotSpawnerClass), "method_12").Invoke(botSpawnerClass, new object[] { spawnPosition, botSpawnerClass.GetClosestZone(spawnPosition, out botMaxDistance), bot, null, cancellationToken.Token });
                                count++;
                            }
                        }
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
            Vector3 direction = UnityEngine.Random.insideUnitSphere;
            float distance = UnityEngine.Random.Range(minDistance, maxDistance);
            Vector3 spawnPosition = coordinate + direction.normalized * distance;

            return spawnPosition;
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
