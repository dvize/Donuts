using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using HarmonyLib;
using UnityEngine;
using static Donuts.DefaultPluginVars;

#pragma warning disable IDE0007, IDE0044
namespace Donuts
{
    public class DonutComponent : MonoBehaviour
    {
        internal CancellationTokenSource cts;

        internal static BotWavesConfig botWavesConfig;
        internal static List<BotWave> allBotWaves;
        internal static bool maxRespawnReachedPMC;
        internal static bool maxRespawnReachedSCAV;
        internal static bool hasSpawnedStartingBots;
        internal static bool fileLoaded = false;
        internal static Gizmos gizmos;
        internal static int currentInitialPMCs = 0;
        internal static int currentInitialSCAVs = 0;
        internal static int currentMaxPMC;
        internal static int currentMaxSCAV;

        internal static GameWorld gameWorld;
        internal static BotSpawner botSpawnerClass;
        internal static IBotCreator botCreator;
        internal static List<Player> playerList;

        //used in DonutInitialization
        internal static int PMCBotLimit;
        internal static int SCAVBotLimit;

        internal static float PMCdespawnCooldown = 0f;
        internal static float PMCdespawnCooldownDuration = despawnInterval.Value;

        internal static float SCAVdespawnCooldown = 0f;
        internal static float SCAVdespawnCooldownDuration = despawnInterval.Value;

        internal static Dictionary<string, List<Vector3>> spawnPointsDict = new Dictionary<string, List<Vector3>>();
        internal static List<BossSpawn> bossSpawns;

        internal static MapBotWaves botWaves;
        internal static Dictionary<string, MethodInfo> methodCache;
        internal static MethodInfo displayMessageNotificationMethod;

        internal static bool isInBattle;
        internal static float timeSinceLastHit = 0;
        internal static Player mainplayer;

        internal static Stopwatch spawnCheckTimer = new Stopwatch();
        private const int SpawnCheckInterval = 1000;

        internal static bool IsBotSpawningEnabled
        {
            get => (bool)AccessTools.Field(typeof(BotsController), "_botEnabled").GetValue(Singleton<IBotGame>.Instance.BotsController);
        }

        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public DonutComponent()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(DonutComponent));
        }

        private void ResetPlayerList()
        {
            playerList.Clear();
            if (gameWorld.RegisteredPlayers.Count > 0)
            {
                foreach (var player in gameWorld.AllPlayersEverExisted)
                {
                    if (!player.IsAI)
                    {
                        playerList.Add(player);
                    }
                }
            }
        }

        internal static void Enable()
        {
            if (Singleton<IBotGame>.Instantiated)
            {
                gameWorld = Singleton<GameWorld>.Instance;
                gameWorld.GetOrAddComponent<DonutComponent>();

                Logger.LogDebug("Donuts Enabled");
            }
        }

        private void Awake()
        {
            cts = new CancellationTokenSource();

            botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            botCreator = AccessTools.Field(botSpawnerClass.GetType(), "_botCreator").GetValue(botSpawnerClass) as IBotCreator;
            playerList = new List<Player>();
            botWavesConfig = new BotWavesConfig();

            DonutInitialization.InitializeComponent();

            botSpawnerClass.OnBotRemoved += removedBot =>
            {
                foreach (var player in playerList)
                {
                    removedBot.Memory.DeleteInfoAboutEnemy(player);
                }
                removedBot.EnemiesController.EnemyInfos.Clear();

                foreach (var player in gameWorld.AllAlivePlayersList)
                {
                    if (!player.IsAI)
                    {
                        continue;
                    }

                    var botOwner = player.AIData.BotOwner;
                    botOwner.Memory.DeleteInfoAboutEnemy(removedBot);
                    botOwner.BotsGroup.RemoveInfo(removedBot);
                    botOwner.BotsGroup.RemoveEnemy(removedBot, EBotEnemyCause.death);
                    botOwner.BotsGroup.RemoveAlly(removedBot);
                }

            };
        }

        private void Start()
        {
            if (!IsBotSpawningEnabled)
            {
                return;
            }

            DonutInitialization.SetupGame();
            allBotWaves = botWaves.PMC.Concat(botWaves.SCAV).ToList();

            mainplayer.BeingHitAction += BeingHitBattleCoolDown;
            ResetPlayerList();
        }

        private void BeingHitBattleCoolDown(DamageInfo info, EBodyPart part, float arg3)
        {
            switch (info.DamageType)
            {
                case EDamageType.Btr:
                case EDamageType.Melee:
                case EDamageType.Bullet:
                case EDamageType.Explosion:
                case EDamageType.GrenadeFragment:
                case EDamageType.Sniper:
                    isInBattle = true;
                    timeSinceLastHit = 0;
                    break;
                default:
                    break;
            }
        }

        private async void Update()
        {
            if (!PluginEnabled.Value || !fileLoaded || !IsBotSpawningEnabled)
                return;

            timeSinceLastHit += Time.deltaTime;

            foreach (var pmcWave in botWaves.PMC)
            {
                pmcWave.UpdateTimer(Time.deltaTime, DefaultPluginVars.coolDownTimer.Value);
            }

            foreach (var scavWave in botWaves.SCAV)
            {
                scavWave.UpdateTimer(Time.deltaTime, DefaultPluginVars.coolDownTimer.Value);
            }

            if (spawnCheckTimer.ElapsedMilliseconds >= SpawnCheckInterval)
            {
                spawnCheckTimer.Restart();
                await StartSpawnProcess(cts.Token);
            }

            Gizmos.DisplayMarkerInformation();
        }

        private async UniTask StartSpawnProcess(CancellationToken cancellationToken)
        {
            if (!hasSpawnedStartingBots)
            {
                hasSpawnedStartingBots = true;
                if (DonutsBotPrep.botSpawnInfos != null && DonutsBotPrep.botSpawnInfos.Any())
                {
                    // Log the contents of botSpawnInfos
                    foreach (var botSpawnInfo in DonutsBotPrep.botSpawnInfos)
                    {
                        Logger.LogDebug($"BotSpawnInfo: {botSpawnInfo.BotType.ToString()} , GroupSize: {botSpawnInfo.GroupSize}, CoordinateCount: {botSpawnInfo.Coordinates.Count()}");
                    }

                    await DonutBotSpawn.SpawnBotsFromInfo(DonutsBotPrep.botSpawnInfos, cancellationToken);
                }
            }

            if (DespawnEnabledPMC.Value)
            {
                await DonutDespawnLogic.DespawnFurthestBot("pmc", cancellationToken);
            }

            if (DespawnEnabledSCAV.Value)
            {
                await DonutDespawnLogic.DespawnFurthestBot("scav", cancellationToken);
            }

            await SpawnBotWaves(botWavesConfig.Maps[DonutsBotPrep.maplocation], cancellationToken);
            await SpawnBossWaves(botWavesConfig.Maps[DonutsBotPrep.maplocation], cancellationToken);
        }

        private async UniTask SpawnBotWaves(MapBotWaves botWaves, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                UnityEngine.Debug.Log("Cancellation requested, not proceeding with bot spawn checks.");
                return;
            }

            bool anySpawned = false;

            if (isInBattle && timeSinceLastHit < battleStateCoolDown.Value)
            {
                //Logger.LogDebug("In battle state cooldown, breaking the loop.");
                return;
            }

            foreach (var botWave in allBotWaves)
            {
                if (botWave.ShouldSpawn())
                {
                    var wildSpawnType = botWaves.PMC.Contains(botWave) ? "pmc" : "scav";

                    if (CanSpawn(botWave, wildSpawnType))
                    {
                        var spawnPointsDict = DonutComponent.GetSpawnPointsForZones(DonutsBotPrep.allMapsZoneConfig, DonutsBotPrep.maplocation, botWave.Zones);

                        if (spawnPointsDict.Any())
                        {
                            var random = new System.Random();
                            var zoneKeys = spawnPointsDict.Keys.OrderBy(_ => random.Next()).ToList();

                            if (zoneKeys.Any())
                            {
                                var randomZone = zoneKeys.First();
                                var coordinates = spawnPointsDict[randomZone].OrderBy(_ => random.Next()).ToList();

                                bool isHotspotZone = randomZone.IndexOf("hotspot", StringComparison.OrdinalIgnoreCase) >= 0;

                                if ((isHotspotZone && wildSpawnType == "pmc" && hotspotBoostPMC.Value) ||
                                    (isHotspotZone && wildSpawnType == "scav" && hotspotBoostSCAV.Value))
                                {
                                    Logger.LogDebug($"{randomZone} is a hotspot; hotspot boost is enabled, setting spawn chance to 100");
                                    botWave.SpawnChance = 100;
                                }

                                foreach (var coordinate in coordinates)
                                {
                                    if (BotSpawnHelper.IsWithinBotActivationDistance(botWave, coordinate))
                                    {
                                        Logger.LogDebug($"Triggering spawn for botWave: {botWave} at {randomZone}, {coordinate}");
                                        await TriggerSpawn(botWave, randomZone, coordinate, wildSpawnType, coordinates, cancellationToken);
                                        anySpawned = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // if CanSpawn if false then we need to reset the timers for this wave
                    ResetGroupTimers(botWave.GroupNum, wildSpawnType);
                }
            }

            await UniTask.Yield(PlayerLoopTiming.Update);

            if (!anySpawned)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: cancellationToken);
            }

        }
        private async UniTask SpawnBossWaves(MapBotWaves botWaves, CancellationToken cancellationToken)
        {
            string methodName = nameof(SpawnBossWaves);

            if (cancellationToken.IsCancellationRequested)
            {
                UnityEngine.Debug.Log($"{methodName}: Cancellation requested, not proceeding with boss spawn checks.");
                return;
            }

            if (isInBattle && timeSinceLastHit < battleStateCoolDown.Value)
            {
                //UnityEngine.Debug.Log($"{methodName}: In battle state cooldown, breaking the loop.");
                return;
            }

            var spawnTasks = new List<UniTask>();

            foreach (var bossSpawn in botWaves.BOSSES)
            {
                spawnTasks.Add(SpawnBossAsync(bossSpawn, cancellationToken));
            }

            // Run all spawn tasks concurrently
            await UniTask.WhenAll(spawnTasks);

            // Yield to ensure loop iteration respects the game's update cycle
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        // Boss Waves
        private async UniTask SpawnBossAsync(BossSpawn bossSpawn, CancellationToken cancellationToken)
        {
            string methodName = nameof(SpawnBossAsync);
            try
            {
                // Update cooldown for the boss
                bossSpawn.UpdateCooldown(Time.deltaTime, DefaultPluginVars.bossWaveCooldownTimer.Value);

                // Check if the boss should spawn and not already pending
                if (!bossSpawn.ShouldSpawn())
                {
                    //UnityEngine.Debug.Log($"{methodName}: Skipping spawn for {bossSpawn.BossName}: in cooldown or spawn already pending.");
                    return;
                }

                UnityEngine.Debug.Log($"{methodName}: Checking spawn chance for boss: {bossSpawn.BossName}");

                int spawnChance;
                if (DefaultPluginVars.BossUseGlobalSpawnChance[bossSpawn.BossName].Value)
                {
                    spawnChance = DefaultPluginVars.BossSpawnChances[bossSpawn.BossName][DonutsBotPrep.maplocation].Value;
                }
                else
                {
                    spawnChance = bossSpawn.BossChance;
                }

                // Check if the boss should spawn based on the new spawn chance
                var randomValue = UnityEngine.Random.Range(0, 100);
                if (randomValue >= spawnChance)
                {
                    UnityEngine.Debug.Log($"{methodName}: Boss spawn cancelled due to chance: {bossSpawn.BossName} (Chance: {spawnChance}%, Rolled: {randomValue})");
                    return;
                }

                UnityEngine.Debug.Log($"{methodName}: Scheduling boss spawn: {bossSpawn.BossName}");
                // Set the spawn as pending to prevent multiple delay timers
                bossSpawn.IsSpawnPending = true;

                // Delay before processing the spawn check unless IgnoreTimerFirstSpawn is true and it's the first spawn
                if (!(bossSpawn.IgnoreTimerFirstSpawn && bossSpawn.TimesSpawned == 0))
                {
                    UnityEngine.Debug.Log($"{methodName}: Waiting for delay: {bossSpawn.TimeDelay} seconds before spawning {bossSpawn.BossName}.");
                    await UniTask.Delay(TimeSpan.FromSeconds(bossSpawn.TimeDelay), cancellationToken: cancellationToken);
                }

                // Get potential spawn coordinates within the specified zones
                var spawnPointsDict = DonutComponent.GetSpawnPointsForZones(DonutsBotPrep.allMapsZoneConfig, DonutsBotPrep.maplocation, bossSpawn.Zones);

                if (spawnPointsDict.Any())
                {
                    UnityEngine.Debug.Log($"{methodName}: Found {spawnPointsDict.Count} potential zones for spawning {bossSpawn.BossName}.");

                    var random = new System.Random();
                    var zoneKeys = spawnPointsDict.Keys.OrderBy(_ => random.Next()).ToList();

                    if (zoneKeys.Any())
                    {
                        var randomZone = zoneKeys.First();
                        var coordinates = spawnPointsDict[randomZone].OrderBy(_ => random.Next()).ToList();

                        UnityEngine.Debug.Log($"{methodName}: Selected zone {randomZone} for boss {bossSpawn.BossName}, scheduling spawn.");

                        // Schedule the boss spawn using the wave-specific method
                        await DonutsBotPrep.ScheduleWaveBossSpawnDirectly(bossSpawn, coordinates, cancellationToken, randomZone);

                        // Increment TimesSpawned and trigger cooldown if necessary
                        bossSpawn.TimesSpawned++;
                        UnityEngine.Debug.Log($"{methodName}: {bossSpawn.BossName} spawned {bossSpawn.TimesSpawned} times.");
                        if (bossSpawn.TimesSpawned >= bossSpawn.MaxTriggersBeforeCooldown)
                        {
                            UnityEngine.Debug.Log($"{methodName}: {bossSpawn.BossName} reached max triggers, entering cooldown.");
                            bossSpawn.TriggerCooldown();
                        }
                        else
                        {
                            bossSpawn.IsSpawnPending = false; // Reset pending after successful spawn
                            UnityEngine.Debug.Log($"{methodName}: {bossSpawn.BossName} spawn pending reset after successful spawn.");
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"{methodName}: No valid zones found for spawning {bossSpawn.BossName}.");
                        bossSpawn.IsSpawnPending = false; // Reset pending if no valid zones
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"{methodName}: No spawn points available for {bossSpawn.BossName}.");
                    bossSpawn.IsSpawnPending = false; // Reset pending if no spawn points available
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{methodName}: Exception occurred while spawning boss: {bossSpawn.BossName}. Message: {ex.Message}");
                //UnityEngine.Debug.LogError($"{methodName}: Stack Trace: {ex.StackTrace}");
            }
        }


        // Checks trigger distance and spawn chance
        private bool CanSpawn(BotWave botWave, string wildSpawnType)
        {
            int randomValue = UnityEngine.Random.Range(0, 100);
            bool canSpawn = randomValue < botWave.SpawnChance;

            Logger.LogDebug($"SpawnChance: {botWave.SpawnChance}, RandomValue: {randomValue}, CanSpawn: {canSpawn}");

            return canSpawn;
        }

        // Checks certain spawn options, reset groups timers
        private async UniTask TriggerSpawn(BotWave botWave, string zone, Vector3 coordinate, string wildSpawnType, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            UnityEngine.Debug.Log($"TriggerSpawn started for wave {botWave} in zone {zone} with type {wildSpawnType}.");

            if (cancellationToken.IsCancellationRequested)
            {
                UnityEngine.Debug.Log("Cancellation requested before triggering spawn.");
                return;
            }

            if (forceAllBotType.Value != "Disabled")
            {
                wildSpawnType = forceAllBotType.Value.ToLower();
                UnityEngine.Debug.Log($"Forced all bot types to {wildSpawnType}.");
            }

            var tasks = new List<UniTask<bool>>();

            if (HardCapEnabled.Value)
            {
                UnityEngine.Debug.Log("Hard cap enabled, adding hard cap check task.");
                tasks.Add(CheckHardCap(wildSpawnType, cancellationToken));
            }

            UnityEngine.Debug.Log("Adding raid time check task.");
            tasks.Add(CheckRaidTime(wildSpawnType, cancellationToken));

            bool[] results;
            try
            {
                UnityEngine.Debug.Log("Awaiting tasks to complete.");
                results = await UniTask.WhenAll(tasks);
                UnityEngine.Debug.Log("Tasks completed successfully.");
            }
            catch (OperationCanceledException)
            {
                UnityEngine.Debug.Log("Cancellation requested during hard cap and raid time checks.");
                return;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"An exception occurred during task execution: {ex.Message}");
                return;
            }

            if (results.Any(result => !result))
            {
                UnityEngine.Debug.Log("Spawn conditions not met. Resetting group timers.");
                ResetGroupTimers(botWave.GroupNum, wildSpawnType); // Reset timer if the wave is hard capped
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                UnityEngine.Debug.Log("Cancellation requested after checks but before spawning.");
                return;
            }

            botWave.TimesSpawned++;
            UnityEngine.Debug.Log($"Bot wave times spawned incremented: {botWave.TimesSpawned}.");

            ResetGroupTimers(botWave.GroupNum, wildSpawnType);

            if (botWave.TimesSpawned >= botWave.MaxTriggersBeforeCooldown)
            {
                UnityEngine.Debug.Log($"Triggering cooldown for bot wave: {botWave.GroupNum}.");
                botWave.TriggerCooldown();
            }

            try
            {
                UnityEngine.Debug.Log("Attempting to spawn bots.");
                // await DonutBotSpawn.SpawnBots(botWave, zone, coordinate, wildSpawnType, coordinates, cancellationToken);
                await DonutBotSpawn.SpawnBots(botWave, zone, coordinate, wildSpawnType, coordinates, cancellationToken);
                UnityEngine.Debug.Log("Bot spawning completed successfully.");
            }
            catch (OperationCanceledException)
            {
                UnityEngine.Debug.Log("Cancellation requested during bot spawning.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"An exception occurred during bot spawning: {ex.Message}");
            }
        }


        internal static Dictionary<string, List<Vector3>> GetSpawnPointsForZones(AllMapsZoneConfig allMapsZoneConfig, string maplocation, List<string> zoneNames)
        {
            var spawnPointsDict = new Dictionary<string, List<Vector3>>();
            if (!allMapsZoneConfig.Maps.TryGetValue(maplocation, out var mapZoneConfig))
            {
                Logger.LogError($"Map location {maplocation} not found in zone configuration.");
                return spawnPointsDict;
            }
            foreach (var zoneName in zoneNames)
            {
                if (zoneName == "all")
                {
                    foreach (var zone in mapZoneConfig.Zones)
                    {
                        if (!spawnPointsDict.ContainsKey(zone.Key))
                        {
                            spawnPointsDict[zone.Key] = new List<Vector3>();
                        }
                        spawnPointsDict[zone.Key].AddRange(zone.Value.Select(c => new Vector3(c.X, c.Y, c.Z)));
                    }
                }
                else if (zoneName == "start" || zoneName == "hotspot" || zoneName == "boss")
                {
                    foreach (var zone in mapZoneConfig.Zones)
                    {
                        if (zone.Key.IndexOf(zoneName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!spawnPointsDict.ContainsKey(zone.Key))
                            {
                                spawnPointsDict[zone.Key] = new List<Vector3>();
                            }
                            spawnPointsDict[zone.Key].AddRange(zone.Value.Select(c => new Vector3(c.X, c.Y, c.Z)));
                        }
                    }
                }
                else
                {
                    if (mapZoneConfig.Zones.TryGetValue(zoneName, out var coordinates))
                    {
                        if (!spawnPointsDict.ContainsKey(zoneName))
                        {
                            spawnPointsDict[zoneName] = new List<Vector3>();
                        }
                        spawnPointsDict[zoneName].AddRange(coordinates.Select(c => new Vector3(c.X, c.Y, c.Z)));
                    }
                }
            }
            return spawnPointsDict;
        }

        public async UniTask<bool> CheckHardCap(string wildSpawnType, CancellationToken cancellationToken)
        {
            int activePMCs = await BotCountManager.GetAlivePlayers("pmc", cancellationToken);
            int activeSCAVs = await BotCountManager.GetAlivePlayers("scav", cancellationToken);

            if (wildSpawnType == "pmc" && activePMCs >= DonutComponent.PMCBotLimit && !hotspotIgnoreHardCapPMC.Value)
            {
                Logger.LogDebug($"PMC spawn not allowed due to PMC bot limit - skipping this spawn. Active PMCs: {activePMCs}, PMC Bot Limit: {DonutComponent.PMCBotLimit}");
                return false;
            }

            if (wildSpawnType == "scav" && activeSCAVs >= DonutComponent.SCAVBotLimit && !hotspotIgnoreHardCapSCAV.Value)
            {
                Logger.LogDebug($"SCAV spawn not allowed due to SCAV bot limit - skipping this spawn. Active SCAVs: {activeSCAVs}, SCAV Bot Limit: {DonutComponent.SCAVBotLimit}");
                return false;
            }

            return true;
        }

        private async UniTask<bool> CheckRaidTime(string wildSpawnType, CancellationToken cancellationToken)
        {
            if (wildSpawnType == "pmc" && hardStopOptionPMC.Value && !IsRaidTimeRemaining("pmc"))
            {
#if DEBUG
                Logger.LogDebug("PMC spawn not allowed due to raid time conditions - skipping this spawn");
#endif
                return false;
            }

            if (wildSpawnType == "scav" && hardStopOptionSCAV.Value && !IsRaidTimeRemaining("scav"))
            {
#if DEBUG
                Logger.LogDebug("SCAV spawn not allowed due to raid time conditions - skipping this spawn");
#endif
                return false;
            }

            return true;
        }
        private bool IsRaidTimeRemaining(string spawnType)
        {
            int hardStopTime;
            int hardStopPercent;

            if (spawnType == "pmc")
            {
                hardStopTime = hardStopTimePMC.Value;
                hardStopPercent = hardStopPercentPMC.Value;
            }
            else
            {
                hardStopTime = hardStopTimeSCAV.Value;
                hardStopPercent = hardStopPercentSCAV.Value;
            }

            int raidTimeLeftTime = (int)SPT.SinglePlayer.Utils.InRaid.RaidTimeUtil.GetRemainingRaidSeconds(); // Time left
            int raidTimeLeftPercent = (int)(SPT.SinglePlayer.Utils.InRaid.RaidTimeUtil.GetRaidTimeRemainingFraction() * 100f); // Percent left

            //why is this method failing?

            Logger.LogWarning("RaidTimeLeftTime: " + raidTimeLeftTime + " RaidTimeLeftPercent: " + raidTimeLeftPercent + " HardStopTime: " + hardStopTime + " HardStopPercent: " + hardStopPercent);
            return useTimeBasedHardStop.Value ? raidTimeLeftTime >= hardStopTime : raidTimeLeftPercent >= hardStopPercent;
        }

        public void ResetGroupTimers(int groupNum, string wildSpawnType)
        {
            var botWaves = wildSpawnType == "pmc" ? botWavesConfig.Maps[DonutsBotPrep.maplocation].PMC : botWavesConfig.Maps[DonutsBotPrep.maplocation].SCAV;

            foreach (var botWave in botWaves)
            {
                if (botWave.GroupNum == groupNum)
                {
                    botWave.ResetTimer();
#if DEBUG
                    Logger.LogDebug($"Resetting timer for GroupNum: {groupNum}, WildSpawnType: {wildSpawnType}");
#endif
                }
            }
        }
        private void OnGUI()
        {
            gizmos.ToggleGizmoDisplay(DebugGizmos.Value);
        }

        private void OnDestroy()
        {
            DisposeHandlersAndResetStatics();
            Logger.LogWarning("Donuts Component cleaned up and disabled.");
        }

        private void DisposeHandlersAndResetStatics()
        {
            // Cancel any ongoing tasks
            cts?.Cancel();
            cts?.Dispose();

            // Remove event handlers
            if (botSpawnerClass != null)
            {
                botSpawnerClass.OnBotRemoved -= HandleBotRemoved;
            }

            if (mainplayer != null && IsBotSpawningEnabled)
            {
                mainplayer.BeingHitAction -= BeingHitBattleCoolDown;
            }

            // Stop all coroutines
            StopAllCoroutines();

            // Reset static variables
            isInBattle = false;
            hasSpawnedStartingBots = false;
            maxRespawnReachedPMC = false;
            maxRespawnReachedSCAV = false;
            currentInitialPMCs = 0;
            currentInitialSCAVs = 0;
            currentMaxPMC = 0;
            currentMaxSCAV = 0;
            PMCdespawnCooldown = 0f;
            SCAVdespawnCooldown = 0f;
            timeSinceLastHit = 0;
            botWavesConfig = null;
            botWaves = null;
            methodCache = null;
            displayMessageNotificationMethod = null;
            playerList.Clear();
            spawnPointsDict.Clear();

            // Stop the spawn check timer
            if (spawnCheckTimer.IsRunning)
            {
                spawnCheckTimer.Stop();
                spawnCheckTimer.Reset();
            }

            // Release resources
            gizmos = null;
            bossSpawns = null;
            botSpawnerClass = null;
            botCreator = null;
            gameWorld = null;
            mainplayer = null;
        }

        private void HandleBotRemoved(BotOwner removedBot)
        {
            foreach (var player in playerList)
            {
                removedBot.Memory.DeleteInfoAboutEnemy(player);
            }
            removedBot.EnemiesController.EnemyInfos.Clear();

            foreach (var player in gameWorld.AllAlivePlayersList)
            {
                if (!player.IsAI)
                {
                    continue;
                }

                var botOwner = player.AIData.BotOwner;
                botOwner.Memory.DeleteInfoAboutEnemy(removedBot);
                botOwner.BotsGroup.RemoveInfo(removedBot);
                botOwner.BotsGroup.RemoveEnemy(removedBot, EBotEnemyCause.death);
                botOwner.BotsGroup.RemoveAlly(removedBot);
            }
        }
    }
}
