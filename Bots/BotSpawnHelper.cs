using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using HarmonyLib;
using UnityEngine;
using static Donuts.DefaultPluginVars;
using static Donuts.DonutComponent;
using IProfileData = GClass592;

namespace Donuts
{
    internal class BotSpawnHelper
    {
        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public BotSpawnHelper()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(BotSpawnHelper));
        }

        internal static AICorePoint GetClosestCorePoint(Vector3 position)
        {
            var botGame = Singleton<IBotGame>.Instance;
            var coversData = botGame.BotsController.CoversData;
            var groupPoint = coversData.GetClosest(position);
            return groupPoint.CorePointInGame;
        }

        internal static async UniTask ActivateStartingBots(BotCreationDataClass botCacheElement, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 spawnPosition, BotDifficulty botDifficulty, int maxCount, string zone, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            bool isFollowerOrBoss = false;

            //check to see if the bot is a boss or follower so we can skip adding another position since we already handled that in the group creation
            foreach (var profile in botCacheElement.Profiles)
            {
                if (profile.Info.Settings.Role.IsBoss() || profile.Info.Settings.Role.IsFollower() || BotSupportTracker.botSourceTypeMap.ContainsKey(profile.Id))
                {
                    isFollowerOrBoss = true;
                    break;
                }
            }

            var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;

            if (botCacheElement != null && !isFollowerOrBoss)
            {
                var closestBotZone = botSpawnerClass?.GetClosestZone(spawnPosition, out _);
                var closestCorePoint = GetClosestCorePoint(spawnPosition);

                if (closestCorePoint == null || closestBotZone == null)
                {
                    Logger.LogError($"ActivateStartingBots: Failed to find closest core point or bot zone for activating bot.");
                    return;
                }

                botCacheElement.AddPosition(spawnPosition, closestCorePoint.Id);

                Logger.LogWarning($"ActivateStartingBots: Spawning bots at distance to player of: {Vector3.Distance(spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                  $"of side: {botCacheElement.Side} and difficulty: {botDifficulty} in spawn zone: {zone}");

                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
            }
            else if (isFollowerOrBoss)
            {
                var closestBotZone = botSpawnerClass?.GetClosestZone(spawnPosition, out _);
                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
            }
            else
            {
                Logger.LogError($"ActivateStartingBots: Attempted to spawn a group bot but the botCacheElement was null.");
            }
        }

        internal static WildSpawnType DetermineWildSpawnType(string spawnType)
        {
            WildSpawnType determinedSpawnType = GetWildSpawnType(
                forceAllBotType.Value == "PMC" ? "pmc" :
                forceAllBotType.Value == "SCAV" ? "assault" :
                spawnType);

            Logger.LogInfo($"DetermineWildSpawnType: Initial Spawn Type: {determinedSpawnType}");

            if (determinedSpawnType == GetWildSpawnType("pmc"))
            {
                determinedSpawnType = DeterminePMCFactionBasedOnRatio();
            }

            Logger.LogInfo($"DetermineWildSpawnType: Final Spawn Type: {determinedSpawnType}");

            return determinedSpawnType;
        }

        public static WildSpawnType DeterminePMCFactionBasedOnRatio()
        {
            int factionRatio = pmcFactionRatio.Value;
            int randomValue = UnityEngine.Random.Range(0, 100);

            Logger.LogInfo($"DeterminePMCFactionBasedOnRatio: Random Value: {randomValue}, Faction Ratio: {factionRatio}");

            WildSpawnType chosenFaction = randomValue < factionRatio ? WildSpawnType.pmcUSEC : WildSpawnType.pmcBEAR;
            Logger.LogInfo($"DeterminePMCFactionBasedOnRatio: Chosen PMC Faction: {chosenFaction.ToString()}");

            return chosenFaction;
        }

        internal static async UniTask<int> AdjustMaxCountForHardCap(string wildSpawnType, int maxCount, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return maxCount;

            int activePMCs = await BotCountManager.GetAlivePlayers("pmc", cancellationToken);
            int activeSCAVs = await BotCountManager.GetAlivePlayers("scav", cancellationToken);

            if (wildSpawnType == "pmc")
            {
                if (activePMCs + maxCount > PMCBotLimit)
                {
                    maxCount = PMCBotLimit - activePMCs;
                }
            }
            else if (wildSpawnType == "scav")
            {
                if (activeSCAVs + maxCount > SCAVBotLimit)
                {
                    maxCount = SCAVBotLimit - activeSCAVs;
                }
            }

            return maxCount;
        }

        internal static int AdjustMaxCountForRespawnLimits(string wildSpawnType, int maxCount)
        {
            if (wildSpawnType == "pmc" && !maxRespawnReachedPMC)
            {
                if (currentMaxPMC + maxCount >= DefaultPluginVars.maxRespawnsPMC.Value)
                {
                    Logger.LogInfo($"AdjustMaxCountForRespawnLimits: Max PMC respawn limit reached: {DefaultPluginVars.maxRespawnsPMC.Value}. Current PMCs respawns this raid: {currentMaxPMC + maxCount}");

                    if (currentMaxPMC < DefaultPluginVars.maxRespawnsPMC.Value)
                    {
                        maxCount = DefaultPluginVars.maxRespawnsPMC.Value - currentMaxPMC;
                        maxRespawnReachedPMC = true;
                    }
                    else
                    {
                        maxRespawnReachedPMC = true;
                        return 0;
                    }
                }
                currentMaxPMC += maxCount;
                return maxCount;
            }

            if (wildSpawnType == "scav" && !maxRespawnReachedSCAV)
            {
                if (currentMaxSCAV + maxCount >= maxRespawnsSCAV.Value)
                {
                    Logger.LogInfo($"AdjustMaxCountForRespawnLimits: Max SCAV respawn limit reached: {maxRespawnsSCAV.Value}. Current SCAVs respawns this raid: {currentMaxPMC + maxCount}");

                    if (currentMaxSCAV < maxRespawnsSCAV.Value)
                    {
                        maxCount = maxRespawnsSCAV.Value - currentMaxSCAV;
                        maxRespawnReachedSCAV = true;
                    }
                    else
                    {
                        maxRespawnReachedSCAV = true;
                        return 0;
                    }
                }
                currentMaxSCAV += maxCount;
                return maxCount;
            }

            return maxCount;
        }

        public static int DetermineMaxBotCount(string spawnType, int defaultMinCount, int defaultMaxCount)
        {
            string groupChance = spawnType == "scav" ? scavGroupChance.Value : pmcGroupChance.Value;
            return getActualBotCount(groupChance, defaultMinCount, defaultMaxCount);
        }

        internal static async UniTask SetupSpawn(BotWave botWave, int maxCount, bool isGroup, WildSpawnType wildSpawnType, Vector3 coordinate, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            Logger.LogInfo($"SetupSpawn: Entering SetupSpawn method.");

            if (botWave == null)
            {
                Logger.LogError($"SetupSpawn: botWave is null. Cannot proceed with setup spawn.");
                return;
            }

            if (coordinates == null || !coordinates.Any())
            {
                Logger.LogError($"SetupSpawn: Coordinates list is null or empty. Cannot proceed with setup spawn.");
                return;
            }

            Logger.LogInfo($"SetupSpawn: Attempting to spawn {(isGroup ? "group" : "solo")} with bot count {maxCount} in spawn zone {zone}");

            try
            {
                if (isGroup)
                {
                    await SpawnGroupBots(botWave, maxCount, wildSpawnType, coordinate, zone, coordinates, cancellationToken);
                }
                else
                {
                    await SpawnSingleBot(botWave, wildSpawnType, coordinate, zone, coordinates, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SetupSpawn: Exception in SetupSpawn: {ex.Message}");
            }

            Logger.LogInfo($"SetupSpawn: Exiting SetupSpawn method.");
        }

        private static async UniTask SpawnGroupBots(BotWave botWave, int count, WildSpawnType wildSpawnType, Vector3 coordinate, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInfo($"SpawnGroupBots: Cancellation requested, aborting.");
                return;
            }

            if (botSpawnerClass == null)
            {
                Logger.LogError($"SpawnGroupBots: botSpawnerClass is null. Cannot proceed with spawning group bots.");
                return;
            }

            if (botCreator == null)
            {
                Logger.LogError($"SpawnGroupBots: botCreator is null. Cannot proceed with spawning group bots.");
                return;
            }

            if (coordinates == null || !coordinates.Any())
            {
                Logger.LogError($"SpawnGroupBots: Coordinates list is null or empty. Cannot proceed with spawning group bots.");
                return;
            }

            Logger.LogInfo($"SpawnGroupBots: Spawning a group of {count} bots.");

            EPlayerSide side = GetSideForWildSpawnType(wildSpawnType);
            Logger.LogInfo($"SpawnGroupBots: Determined side: {side}");

            var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
            if (cancellationTokenSource == null)
            {
                Logger.LogError($"SpawnGroupBots: Unable to retrieve CancellationTokenSource from botSpawnerClass.");
                return;
            }

            BotDifficulty botDifficulty = GetBotDifficulty(wildSpawnType);
            Logger.LogInfo($"SpawnGroupBots: Determined bot difficulty: {botDifficulty}");

            var cachedBotGroup = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, count);
            if (cachedBotGroup == null)
            {
                Logger.LogWarning($"SpawnGroupBots: No cached bots found for this spawn, generating on the fly for {count} bots - this may take some time.");
                var botInfo = new PrepBotInfo(wildSpawnType, botDifficulty, side, true, count);
                await DonutsBotPrep.CreateBot(botInfo, true, count, cancellationToken);
                DonutsBotPrep.BotInfos.Add(botInfo);
                cachedBotGroup = botInfo.Bots;
                Logger.LogInfo($"SpawnGroupBots: Created new bot group.");
            }
            else
            {
                Logger.LogWarning($"SpawnGroupBots: Found grouped cached bots, spawning them.");
            }

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();
            Logger.LogInfo($"SpawnGroupBots: Minimum spawn distance from player: {minSpawnDistFromPlayer}");

            bool spawned = false;

            foreach (var coord in coordinates)
            {
                Logger.LogInfo($"SpawnGroupBots: Checking coordinate {coord} for valid spawn position.");

                Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coord, maxSpawnTriesPerBot.Value, cancellationToken);
                if (spawnPosition.HasValue)
                {
                    Logger.LogInfo($"SpawnGroupBots: Valid spawn position found at {spawnPosition.Value}");

                    if (cachedBotGroup == null)
                    {
                        Logger.LogError($"SpawnGroupBots: cachedBotGroup is null. Cannot proceed with spawning bot group.");
                        break;
                    }

                    Logger.LogInfo($"SpawnGroupBots: Spawning bot group at position {spawnPosition.Value}");
                    await SpawnBotForGroup(cachedBotGroup, wildSpawnType, side, botCreator, botSpawnerClass, spawnPosition.Value, cancellationTokenSource, botDifficulty, count, botWave, zone, cancellationToken);
                    spawned = true;
                    break;
                }
                else
                {
                    Logger.LogInfo($"SpawnGroupBots: No valid spawn position at coordinate {coord}, checking next.");
                }
            }

            if (!spawned)
            {
                Logger.LogInfo($"SpawnGroupBots: No valid spawn position found after retries - skipping this spawn");
            }
            else
            {
                Logger.LogInfo($"SpawnGroupBots: Successfully spawned bot group.");
            }
        }


        private static async UniTask SpawnSingleBot(BotWave botWave, WildSpawnType wildSpawnType, Vector3 coordinate, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInfo($"SpawnSingleBot: Cancellation requested, aborting.");
                return;
            }

            if (botSpawnerClass == null)
            {
                Logger.LogError($"SpawnSingleBot: botSpawnerClass is null. Cannot proceed with spawning a single bot.");
                return;
            }

            if (botCreator == null)
            {
                Logger.LogError($"SpawnSingleBot: botCreator is null. Cannot proceed with spawning a single bot.");
                return;
            }

            if (coordinates == null || !coordinates.Any())
            {
                Logger.LogError($"SpawnSingleBot: Coordinates list is null or empty. Cannot proceed with spawning a single bot.");
                return;
            }

            Logger.LogInfo($"SpawnSingleBot: Attempting to spawn a single bot.");

            EPlayerSide side = GetSideForWildSpawnType(wildSpawnType);
            Logger.LogInfo($"SpawnSingleBot: Determined side: {side}");

            var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
            if (cancellationTokenSource == null)
            {
                Logger.LogError($"SpawnSingleBot: Unable to retrieve CancellationTokenSource from botSpawnerClass.");
                return;
            }

            BotDifficulty botDifficulty = GetBotDifficulty(wildSpawnType);
            Logger.LogInfo($"SpawnSingleBot: Determined bot difficulty: {botDifficulty}");

            var BotCacheDataList = DonutsBotPrep.GetWildSpawnData(wildSpawnType, botDifficulty);
            if (BotCacheDataList == null)
            {
                Logger.LogError($"SpawnSingleBot: BotCacheDataList is null. Cannot proceed with spawning a single bot.");
                return;
            }

            Logger.LogInfo($"SpawnSingleBot: Retrieved BotCacheDataList with {BotCacheDataList.Count} entries.");

            try
            {
                Logger.LogInfo($"SpawnSingleBot: Attempting to spawn bot from cache or create new.");
                await SpawnBotFromCacheOrCreateNew(BotCacheDataList, wildSpawnType, side, botCreator, botSpawnerClass, coordinate, cancellationTokenSource, botDifficulty, botWave, zone, coordinates, cancellationToken);
                Logger.LogInfo($"SpawnSingleBot: Spawned bot successfully.");
            }
            catch (Exception ex)
            {

                Logger.LogError($"SpawnSingleBot: Exception while spawning bot: {ex.Message}\n{ex.StackTrace}");
            }
        }

        internal static async UniTask SpawnBotFromCacheOrCreateNew(List<BotCreationDataClass> botCacheList, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 coordinate, CancellationTokenSource cancellationTokenSource, BotDifficulty botDifficulty, BotWave botWave, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            Logger.LogInfo($"SpawnBotFromCacheOrCreateNew: Finding Cached Bot");

            var botCacheElement = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, 1);

            if (botCacheElement != null)
            {
                await ActivateBotFromCache(botCacheElement, coordinate, cancellationTokenSource, botWave, zone, coordinates, cancellationToken);
            }
            else
            {
                Logger.LogInfo($"SpawnBotFromCacheOrCreateNew: Bot Cache is empty for solo bot. Creating a new bot.");
                await CreateNewBot(wildSpawnType, side, ibotCreator, botSpawnerClass, coordinate, cancellationTokenSource, zone, coordinates, cancellationToken);
            }
        }

        private static async UniTask ActivateBotFromCache(BotCreationDataClass botCacheElement, Vector3 coordinate, CancellationTokenSource cancellationTokenSource, BotWave botWave, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();
            Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coordinate, maxSpawnTriesPerBot.Value, cancellationToken);

            if (!spawnPosition.HasValue)
            {
                foreach (var coord in coordinates)
                {
                    spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coord, maxSpawnTriesPerBot.Value, cancellationToken);
                    if (spawnPosition.HasValue)
                    {
                        break;
                    }
                }
            }

            if (spawnPosition.HasValue)
            {
                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition.Value, out float dist);
                var closestCorePoint = GetClosestCorePoint(spawnPosition.Value);
                botCacheElement.AddPosition(spawnPosition.Value, closestCorePoint.Id);

                Logger.LogWarning($"ActivateBotFromCache: Spawning bot at distance to player of: {Vector3.Distance(spawnPosition.Value, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                  $"of side: {botCacheElement.Side} in spawn zone {zone}");

                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
            }
            else
            {
                Logger.LogInfo($"ActivateBotFromCache: No valid spawn position found - skipping this spawn");
                return;
            }
        }

        internal static async UniTask SpawnBotForGroup(BotCreationDataClass botCacheElement, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 spawnPosition, CancellationTokenSource cancellationTokenSource, BotDifficulty botDifficulty, int maxCount, BotWave botWave, string zone, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            if (botCacheElement != null)
            {
                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
                var closestCorePoint = GetClosestCorePoint(spawnPosition);
                botCacheElement.AddPosition(spawnPosition, closestCorePoint.Id);

                Logger.LogWarning($"SpawnBotForGroup: Spawning grouped bots at distance to player of: {Vector3.Distance(spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                  $"of side: {botCacheElement.Side} and difficulty: {botDifficulty} in spawn zone {zone}");

                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
            }
            else
            {
                Logger.LogError($"SpawnBotForGroup: Attempted to spawn a group bot but the botCacheElement was null.");
            }
        }

        internal static async UniTask CreateNewBot(WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator, BotSpawner botSpawnerClass, Vector3 coordinate, CancellationTokenSource cancellationTokenSource, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInfo($"CreateNewBot: Cancellation requested, exiting method.");
                return;
            }

            Logger.LogInfo($"CreateNewBot: Starting bot creation process.");

            BotDifficulty botdifficulty = GetBotDifficulty(wildSpawnType);

            IProfileData botData = new IProfileData(side, wildSpawnType, botdifficulty, 0f, null);
            BotCreationDataClass bot = await BotCreationDataClass.Create(botData, ibotCreator, 1, botSpawnerClass);

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();
            Logger.LogInfo($"CreateNewBot: Min spawn distance from player: {minSpawnDistFromPlayer}");

            Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coordinate, maxSpawnTriesPerBot.Value, cancellationToken);

            if (!spawnPosition.HasValue)
            {
                Logger.LogInfo($"CreateNewBot: Initial spawn position not found, checking alternative coordinates.");

                foreach (var coord in coordinates)
                {
                    spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coord, maxSpawnTriesPerBot.Value, cancellationToken);
                    if (spawnPosition.HasValue)
                    {
                        Logger.LogInfo($"CreateNewBot: Found spawn position at alternative coordinate.");
                        break;
                    }
                }
            }

            if (spawnPosition.HasValue)
            {
                var closestCorePoint = GetClosestCorePoint(spawnPosition.Value);
                bot.AddPosition(spawnPosition.Value, closestCorePoint.Id);

                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition.Value, out float dist);

                Logger.LogWarning($"CreateNewBot: Spawning bot at distance to player of: {Vector3.Distance(spawnPosition.Value, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                  $"of side: {bot.Side} and difficulty: {botdifficulty} in spawn zone {zone}");

                await ActivateBot(closestBotZone, bot, cancellationTokenSource, cancellationToken);
            }
            else
            {
                Logger.LogInfo($"CreateNewBot: No valid spawn position found - skipping this spawn");
            }
        }


        internal static async UniTask ActivateBot(BotZone botZone, BotCreationDataClass botData, CancellationTokenSource cancellationTokenSource, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            CreateBotCallbackWrapper createBotCallbackWrapper = new CreateBotCallbackWrapper
            {
                botData = botData
            };

            GetGroupWrapper getGroupWrapper = new GetGroupWrapper(botSpawnerClass, gameWorld);

            botCreator.ActivateBot(botData, botZone, false, new Func<BotOwner, BotZone, BotsGroup>(getGroupWrapper.GetGroupAndSetEnemies), new Action<BotOwner>(createBotCallbackWrapper.CreateBotCallback), cancellationTokenSource.Token);
            await ClearBotCacheAfterActivation(botData);

            if (BotSupportTracker.GetBotSourceType(botData.Id.ToString()) == BotSourceType.Support)
            {
                BotSupportTracker.RemoveBot(botData.Id.ToString());
            }
            
        }

        internal static async UniTask<bool> ClearBotCacheAfterActivation(BotCreationDataClass botData)
        {
            var botInfo = DonutsBotPrep.BotInfos.FirstOrDefault(b => b.Bots == botData);
            if (botInfo != null)
            {
                DonutsBotPrep.timeSinceLastReplenish = 0f;
                botInfo.Bots = null;
                Logger.LogInfo($"ClearBotCacheAfterActivation: Cleared cached bot info for bot type: {botInfo.SpawnType}");
            }


            return true;
        }

        internal static bool IsWithinBotActivationDistance(BotWave botWave, Vector3 position)
        {
            try
            {
                foreach (var player in playerList)
                {
                    if (player?.HealthController == null || !player.HealthController.IsAlive) continue;

                    float distanceSquared = (player.Position - position).sqrMagnitude;
                    float activationDistanceSquared = botWave.TriggerDistance * botWave.TriggerDistance;

                    if (distanceSquared <= activationDistanceSquared)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        internal static WildSpawnType GetWildSpawnType(string spawnType)
        {
            spawnType = spawnType.ToLower();

            if (WildSpawnTypeDictionaries.StringToWildSpawnType.TryGetValue(spawnType, out var wildSpawnType))
            {
                return wildSpawnType;
            }

            if (spawnType == "pmc")
            {
                return UnityEngine.Random.Range(0, 2) == 0 ? WildSpawnType.pmcUSEC : WildSpawnType.pmcBEAR;
            }

            return WildSpawnType.assault;
        }

        internal static EPlayerSide GetSideForWildSpawnType(WildSpawnType spawnType)
        {
            return WildSpawnTypeDictionaries.WildSpawnTypeToEPlayerSide.TryGetValue(spawnType, out var side) ? side : EPlayerSide.Savage;
        }

        internal static int getActualBotCount(string pluginGroupChance, int minGroupSize, int maxGroupSize)
        {
            InitializeGroupChanceWeights();

            if (pluginGroupChance == "Random")
            {
                string[] groupChances = { "None", "Low", "Default", "High", "Max" };
                pluginGroupChance = groupChances[UnityEngine.Random.Range(0, groupChances.Length)];
            }

            return pluginGroupChance switch
            {
                "None" => minGroupSize,
                "Max" => maxGroupSize,
                _ => getGroupChance(pluginGroupChance, minGroupSize, maxGroupSize)
            };
        }

        internal static int getGroupChance(string pmcGroupChance, int minGroupSize, int maxGroupSize)
        {
            double[] probabilities = GetProbabilityArray(pmcGroupChance) ?? GetDefaultProbabilityArray(pmcGroupChance);
            System.Random random = new System.Random();
            return getOutcomeWithProbability(random, probabilities, minGroupSize, maxGroupSize) + minGroupSize;
        }

        internal static double[] GetProbabilityArray(string pmcGroupChance)
        {
            if (groupChanceWeights.TryGetValue(pmcGroupChance, out var relativeWeights))
            {
                double totalWeight = relativeWeights.Sum();
                return relativeWeights.Select(weight => weight / totalWeight).ToArray();
            }

            throw new ArgumentException($"Invalid pmcGroupChance: {pmcGroupChance}");
        }

        internal static double[] GetDefaultProbabilityArray(string pmcGroupChance)
        {
            if (groupChanceWeights.TryGetValue(pmcGroupChance, out var relativeWeights))
            {
                double totalWeight = relativeWeights.Sum();
                return relativeWeights.Select(weight => weight / totalWeight).ToArray();
            }

            throw new ArgumentException($"Invalid pmcGroupChance: {pmcGroupChance}");
        }

        internal static int getOutcomeWithProbability(System.Random random, double[] probabilities, int minGroupSize, int maxGroupSize)
        {
            double probabilitySum = probabilities.Sum();
            if (Math.Abs(probabilitySum - 1.0) > 0.0001)
            {
                throw new InvalidOperationException("Probabilities should sum up to 1.");
            }

            double probabilityThreshold = random.NextDouble();
            double cumulative = 0.0;
            int adjustedMaxCount = maxGroupSize - minGroupSize;
            for (int i = 0; i <= adjustedMaxCount; i++)
            {
                cumulative += probabilities[i];
                if (probabilityThreshold < cumulative)
                {
                    return i;
                }
            }
            return adjustedMaxCount;
        }

        internal static void InitializeGroupChanceWeights()
        {
            int[] defaultWeights = ParseGroupWeightDistro(groupWeightDistroDefault.Value);
            int[] lowWeights = ParseGroupWeightDistro(groupWeightDistroLow.Value);
            int[] highWeights = ParseGroupWeightDistro(groupWeightDistroHigh.Value);

            groupChanceWeights["Default"] = defaultWeights;
            groupChanceWeights["Low"] = lowWeights;
            groupChanceWeights["High"] = highWeights;
        }

        internal static int[] ParseGroupWeightDistro(string weightsString)
        {
            return weightsString.Split(new char[] { ',' })
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(int.Parse)
                                .ToArray();
        }

        internal static BotDifficulty GetBotDifficulty(WildSpawnType wildSpawnType)
        {
            if (wildSpawnType == WildSpawnType.assault)
            {
                return grabSCAVDifficulty();
            }
            else if (wildSpawnType == WildSpawnType.pmcUSEC || wildSpawnType == WildSpawnType.pmcBEAR || wildSpawnType == WildSpawnType.pmcBot)
            {
                return grabPMCDifficulty();
            }
            else
            {
                return grabOtherDifficulty();
            }
        }

        internal static BotDifficulty grabPMCDifficulty()
        {
            switch (botDifficultiesPMC.Value.ToLower())
            {
                case "asonline":
                    BotDifficulty[] randomDifficulty = { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard };
                    return randomDifficulty[UnityEngine.Random.Range(0, 3)];
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

        internal static BotDifficulty grabSCAVDifficulty()
        {
            switch (botDifficultiesSCAV.Value.ToLower())
            {
                case "asonline":
                    BotDifficulty[] randomDifficulty = { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard };
                    return randomDifficulty[UnityEngine.Random.Range(0, 3)];
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

        internal static BotDifficulty grabOtherDifficulty()
        {
            switch (botDifficultiesOther.Value.ToLower())
            {
                case "asonline":
                    BotDifficulty[] randomDifficulty = { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard };
                    return randomDifficulty[UnityEngine.Random.Range(0, 3)];
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
