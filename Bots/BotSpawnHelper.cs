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
            Debug.Log($"GetClosestCorePoint: Finding closest core point for position {position}.");
            var botGame = Singleton<IBotGame>.Instance;
            var coversData = botGame.BotsController.CoversData;
            var groupPoint = coversData.GetClosest(position);
            return groupPoint.CorePointInGame;
        }

        internal static async UniTask ActivateStartingBots(BotCreationDataClass botCacheElement, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 spawnPosition, BotDifficulty botDifficulty, int maxCount, string zone, CancellationToken cancellationToken)
        {
            Debug.Log("ActivateStartingBots: Method entered.");

            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log("ActivateStartingBots: Cancellation requested, exiting method.");
                return;
            }

            bool isFollowerOrBoss = false;

            if (botCacheElement == null)
            {
                Debug.LogError("ActivateStartingBots: botCacheElement is null.");
            }
            else
            {
                Debug.Log($"ActivateStartingBots: botCacheElement is valid with {botCacheElement.Profiles.Count} profiles.");
            }


            if (WildSpawnTypeDictionaries.IsBoss(wildSpawnType) || WildSpawnTypeDictionaries.IsFollower(wildSpawnType))
            {
                isFollowerOrBoss = true;
                Debug.Log("ActivateStartingBots: Bot is a boss or follower.");
            }


            var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
            if (cancellationTokenSource == null)
            {
                Debug.LogError("ActivateStartingBots: cancellationTokenSource is null.");
            }

            if (botCacheElement != null && !isFollowerOrBoss)
            {
                var closestBotZone = botSpawnerClass?.GetClosestZone(spawnPosition, out _);
                var closestCorePoint = GetClosestCorePoint(spawnPosition);

                if (closestCorePoint == null)
                {
                    Debug.LogError("ActivateStartingBots: closestCorePoint is null.");
                }
                if (closestBotZone == null)
                {
                    Debug.LogError("ActivateStartingBots: closestBotZone is null.");
                }

                if (closestCorePoint == null || closestBotZone == null)
                {
                    Debug.LogError($"ActivateStartingBots: Failed to find closest core point or bot zone for activating bot.");
                    return;
                }

                Debug.Log($"ActivateStartingBots: Adding position {spawnPosition} with core point ID {closestCorePoint.Id}.");
                botCacheElement.AddPosition(spawnPosition, closestCorePoint.Id);

                Debug.Log($"ActivateStartingBots: Spawning bots at distance to player of: {Vector3.Distance(spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                          $"of side: {botCacheElement.Side} and difficulty: {botDifficulty} in spawn zone: {zone}");

                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
            }
            else if (isFollowerOrBoss)
            {
                Debug.Log("ActivateStartingBots: Bot is a follower or boss, proceeding with activation.");
                var closestBotZone = botSpawnerClass?.GetClosestZone(spawnPosition, out _);
                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
            }
            else
            {
                Debug.LogError($"ActivateStartingBots: Attempted to spawn a group bot but the botCacheElement was null.");
            }

            Debug.Log("ActivateStartingBots: Method exiting.");
        }

        internal static WildSpawnType DetermineWildSpawnType(string spawnType)
        {
            WildSpawnType determinedSpawnType = GetWildSpawnType(
                forceAllBotType.Value == "PMC" ? "pmc" :
                forceAllBotType.Value == "SCAV" ? "assault" :
                spawnType);

            Debug.Log($"DetermineWildSpawnType: Initial Spawn Type: {determinedSpawnType}");

            if (determinedSpawnType == GetWildSpawnType("pmc"))
            {
                determinedSpawnType = DeterminePMCFactionBasedOnRatio();
            }

            Debug.Log($"DetermineWildSpawnType: Final Spawn Type: {determinedSpawnType}");

            return determinedSpawnType;
        }

        public static WildSpawnType DeterminePMCFactionBasedOnRatio()
        {
            int factionRatio = pmcFactionRatio.Value;
            int randomValue = UnityEngine.Random.Range(0, 100);

            Debug.Log($"DeterminePMCFactionBasedOnRatio: Random Value: {randomValue}, Faction Ratio: {factionRatio}");

            WildSpawnType chosenFaction = randomValue < factionRatio ? WildSpawnType.pmcUSEC : WildSpawnType.pmcBEAR;
            Debug.Log($"DeterminePMCFactionBasedOnRatio: Chosen PMC Faction: {chosenFaction.ToString()}");

            return chosenFaction;
        }

        internal static async UniTask<int> AdjustMaxCountForHardCap(string wildSpawnType, int maxCount, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log("AdjustMaxCountForHardCap: Cancellation requested, returning maxCount.");
                return maxCount;
            }

            int activePMCs = await BotCountManager.GetAlivePlayers("pmc", cancellationToken);
            int activeSCAVs = await BotCountManager.GetAlivePlayers("scav", cancellationToken);

            Debug.Log($"AdjustMaxCountForHardCap: Active PMCs: {activePMCs}, Active SCAVs: {activeSCAVs}");

            if (wildSpawnType == "pmc")
            {
                if (activePMCs + maxCount > PMCBotLimit)
                {
                    maxCount = PMCBotLimit - activePMCs;
                    Debug.Log($"AdjustMaxCountForHardCap: Adjusted maxCount for PMC: {maxCount}");
                }
            }
            else if (wildSpawnType == "scav")
            {
                if (activeSCAVs + maxCount > SCAVBotLimit)
                {
                    maxCount = SCAVBotLimit - activeSCAVs;
                    Debug.Log($"AdjustMaxCountForHardCap: Adjusted maxCount for SCAV: {maxCount}");
                }
            }

            return maxCount;
        }

        internal static int AdjustMaxCountForRespawnLimits(string wildSpawnType, int maxCount)
        {
            Debug.Log($"AdjustMaxCountForRespawnLimits: Checking respawn limits for {wildSpawnType} with maxCount {maxCount}");

            if (wildSpawnType == "pmc" && !maxRespawnReachedPMC)
            {
                if (currentMaxPMC + maxCount >= DefaultPluginVars.maxRespawnsPMC.Value)
                {
                    Debug.Log($"AdjustMaxCountForRespawnLimits: Max PMC respawn limit reached: {DefaultPluginVars.maxRespawnsPMC.Value}. Current PMCs respawns this raid: {currentMaxPMC + maxCount}");

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
                    Debug.Log($"AdjustMaxCountForRespawnLimits: Max SCAV respawn limit reached: {maxRespawnsSCAV.Value}. Current SCAVs respawns this raid: {currentMaxSCAV + maxCount}");

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
            Debug.Log($"DetermineMaxBotCount: Determining max bot count for spawnType {spawnType} with defaultMinCount {defaultMinCount} and defaultMaxCount {defaultMaxCount}");

            string groupChance = spawnType == "scav" ? scavGroupChance.Value : pmcGroupChance.Value;
            return getActualBotCount(groupChance, defaultMinCount, defaultMaxCount);
        }

        internal static async UniTask SetupSpawn(BotWave botWave, int maxCount, bool isGroup, WildSpawnType wildSpawnType, Vector3 coordinate, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log("SetupSpawn: Cancellation requested, exiting method.");
                return;
            }

            Debug.Log($"SetupSpawn: Entering SetupSpawn method.");

            if (botWave == null)
            {
                Debug.LogError($"SetupSpawn: botWave is null. Cannot proceed with setup spawn.");
                return;
            }

            if (coordinates == null || !coordinates.Any())
            {
                Debug.LogError($"SetupSpawn: Coordinates list is null or empty. Cannot proceed with setup spawn.");
                return;
            }

            Debug.Log($"SetupSpawn: Attempting to spawn {(isGroup ? "group" : "solo")} with bot count {maxCount} in spawn zone {zone}");

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
                Debug.LogError($"SetupSpawn: Exception in SetupSpawn: {ex.Message}\n{ex.StackTrace}");
            }

            Debug.Log($"SetupSpawn: Exiting SetupSpawn method.");
        }

        private static async UniTask SpawnGroupBots(BotWave botWave, int count, WildSpawnType wildSpawnType, Vector3 coordinate, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"SpawnGroupBots: Cancellation requested, aborting.");
                return;
            }

            if (botSpawnerClass == null)
            {
                Debug.LogError($"SpawnGroupBots: botSpawnerClass is null. Cannot proceed with spawning group bots.");
                return;
            }

            if (botCreator == null)
            {
                Debug.LogError($"SpawnGroupBots: botCreator is null. Cannot proceed with spawning group bots.");
                return;
            }

            if (coordinates == null || !coordinates.Any())
            {
                Debug.LogError($"SpawnGroupBots: Coordinates list is null or empty. Cannot proceed with spawning group bots.");
                return;
            }

            Debug.Log($"SpawnGroupBots: Spawning a group of {count} bots.");

            EPlayerSide side = GetSideForWildSpawnType(wildSpawnType);
            Debug.Log($"SpawnGroupBots: Determined side: {side}");

            var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
            if (cancellationTokenSource == null)
            {
                Debug.LogError($"SpawnGroupBots: Unable to retrieve CancellationTokenSource from botSpawnerClass.");
                return;
            }

            BotDifficulty botDifficulty = GetBotDifficulty(wildSpawnType);
            Debug.Log($"SpawnGroupBots: Determined bot difficulty: {botDifficulty}");

            var cachedBotGroup = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, count);
            if (cachedBotGroup == null)
            {
                Debug.LogWarning($"SpawnGroupBots: No cached bots found for this spawn, generating on the fly for {count} bots - this may take some time.");
                var botInfo = new PrepBotInfo(wildSpawnType, botDifficulty, side, true, count);
                await DonutsBotPrep.CreateBot(botInfo, true, count, cancellationToken);
                DonutsBotPrep.BotInfos.Add(botInfo);
                cachedBotGroup = botInfo.Bots;
                Debug.Log($"SpawnGroupBots: Created new bot group.");
            }
            else
            {
                Debug.LogWarning($"SpawnGroupBots: Found grouped cached bots, spawning them.");
            }

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();
            Debug.Log($"SpawnGroupBots: Minimum spawn distance from player: {minSpawnDistFromPlayer}");

            bool spawned = false;

            foreach (var coord in coordinates)
            {
                Debug.Log($"SpawnGroupBots: Checking coordinate {coord} for valid spawn position.");

                Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coord, maxSpawnTriesPerBot.Value, cancellationToken);
                if (spawnPosition.HasValue)
                {
                    Debug.Log($"SpawnGroupBots: Valid spawn position found at {spawnPosition.Value}");

                    if (cachedBotGroup == null)
                    {
                        Debug.LogError($"SpawnGroupBots: cachedBotGroup is null. Cannot proceed with spawning bot group.");
                        break;
                    }

                    Debug.Log($"SpawnGroupBots: Spawning bot group at position {spawnPosition.Value}");
                    await SpawnBotForGroup(cachedBotGroup, wildSpawnType, side, botCreator, botSpawnerClass, spawnPosition.Value, cancellationTokenSource, botDifficulty, count, botWave, zone, cancellationToken);
                    spawned = true;
                    break;
                }
                else
                {
                    Debug.Log($"SpawnGroupBots: No valid spawn position at coordinate {coord}, checking next.");
                }
            }

            if (!spawned)
            {
                Debug.Log($"SpawnGroupBots: No valid spawn position found after retries - skipping this spawn");
            }
            else
            {
                Debug.Log($"SpawnGroupBots: Successfully spawned bot group.");
            }
        }


        private static async UniTask SpawnSingleBot(BotWave botWave, WildSpawnType wildSpawnType, Vector3 coordinate, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"SpawnSingleBot: Cancellation requested, aborting.");
                return;
            }

            if (botSpawnerClass == null)
            {
                Debug.LogError($"SpawnSingleBot: botSpawnerClass is null. Cannot proceed with spawning a single bot.");
                return;
            }

            if (botCreator == null)
            {
                Debug.LogError($"SpawnSingleBot: botCreator is null. Cannot proceed with spawning a single bot.");
                return;
            }

            if (coordinates == null || !coordinates.Any())
            {
                Debug.LogError($"SpawnSingleBot: Coordinates list is null or empty. Cannot proceed with spawning a single bot.");
                return;
            }

            Debug.Log($"SpawnSingleBot: Attempting to spawn a single bot.");

            EPlayerSide side = GetSideForWildSpawnType(wildSpawnType);
            Debug.Log($"SpawnSingleBot: Determined side: {side}");

            var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
            if (cancellationTokenSource == null)
            {
                Debug.LogError($"SpawnSingleBot: Unable to retrieve CancellationTokenSource from botSpawnerClass.");
                return;
            }

            BotDifficulty botDifficulty = GetBotDifficulty(wildSpawnType);
            Debug.Log($"SpawnSingleBot: Determined bot difficulty: {botDifficulty}");

            var BotCacheDataList = DonutsBotPrep.GetWildSpawnData(wildSpawnType, botDifficulty);
            if (BotCacheDataList == null)
            {
                Debug.LogError($"SpawnSingleBot: BotCacheDataList is null. Cannot proceed with spawning a single bot.");
                return;
            }

            Debug.Log($"SpawnSingleBot: Retrieved BotCacheDataList with {BotCacheDataList.Count()} entries.");

            try
            {
                Debug.Log($"SpawnSingleBot: Attempting to spawn bot from cache or create new.");
                await SpawnBotFromCacheOrCreateNew(BotCacheDataList, wildSpawnType, side, botCreator, botSpawnerClass, coordinate, cancellationTokenSource, botDifficulty, botWave, zone, coordinates, cancellationToken);
                Debug.Log($"SpawnSingleBot: Spawned bot successfully.");
            }
            catch (Exception ex)
            {

                Debug.LogError($"SpawnSingleBot: Exception while spawning bot: {ex.Message}\n{ex.StackTrace}");
            }
        }

        internal static async UniTask SpawnBotFromCacheOrCreateNew(List<BotCreationDataClass> botCacheList, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 coordinate, CancellationTokenSource cancellationTokenSource, BotDifficulty botDifficulty, BotWave botWave, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log("SpawnBotFromCacheOrCreateNew: Cancellation requested, exiting method.");
                return;
            }

            Debug.Log($"SpawnBotFromCacheOrCreateNew: Finding Cached Bot");

            var botCacheElement = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, 1);

            if (botCacheElement != null)
            {
                await ActivateBotFromCache(botCacheElement, coordinate, cancellationTokenSource, botWave, zone, coordinates, cancellationToken);
            }
            else
            {
                Debug.Log($"SpawnBotFromCacheOrCreateNew: Bot Cache is empty for solo bot. Creating a new bot.");
                await CreateNewBot(wildSpawnType, side, ibotCreator, botSpawnerClass, coordinate, cancellationTokenSource, zone, coordinates, cancellationToken);
            }
        }

        private static async UniTask ActivateBotFromCache(BotCreationDataClass botCacheElement, Vector3 coordinate, CancellationTokenSource cancellationTokenSource, BotWave botWave, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log("ActivateBotFromCache: Cancellation requested, exiting method.");
                return;
            }

            Debug.Log($"ActivateBotFromCache: Attempting to activate bot from cache for coordinate {coordinate}.");

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();
            Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coordinate, maxSpawnTriesPerBot.Value, cancellationToken);

            if (!spawnPosition.HasValue)
            {
                Debug.Log("ActivateBotFromCache: Initial spawn position not found, checking alternative coordinates.");

                foreach (var coord in coordinates)
                {
                    spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coord, maxSpawnTriesPerBot.Value, cancellationToken);
                    if (spawnPosition.HasValue)
                    {
                        Debug.Log($"ActivateBotFromCache: Found spawn position at alternative coordinate {coord}.");
                        break;
                    }
                }
            }

            if (spawnPosition.HasValue)
            {
                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition.Value, out float dist);
                var closestCorePoint = GetClosestCorePoint(spawnPosition.Value);
                botCacheElement.AddPosition(spawnPosition.Value, closestCorePoint.Id);

                Debug.Log($"ActivateBotFromCache: Spawning bot at distance to player of: {Vector3.Distance(spawnPosition.Value, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                  $"of side: {botCacheElement.Side} in spawn zone {zone}");

                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
            }
            else
            {
                Debug.Log($"ActivateBotFromCache: No valid spawn position found - skipping this spawn");
                return;
            }
        }

        internal static async UniTask SpawnBotForGroup(BotCreationDataClass botCacheElement, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 spawnPosition, CancellationTokenSource cancellationTokenSource, BotDifficulty botDifficulty, int maxCount, BotWave botWave, string zone, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log("SpawnBotForGroup: Cancellation requested, exiting method.");
                return;
            }

            Debug.Log($"SpawnBotForGroup: Attempting to spawn bot group for spawn position {spawnPosition}.");

            if (botCacheElement != null)
            {
                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
                var closestCorePoint = GetClosestCorePoint(spawnPosition);
                botCacheElement.AddPosition(spawnPosition, closestCorePoint.Id);

                Debug.Log($"SpawnBotForGroup: Spawning grouped bots at distance to player of: {Vector3.Distance(spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                  $"of side: {botCacheElement.Side} and difficulty: {botDifficulty} in spawn zone {zone}");

                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
            }
            else
            {
                Debug.LogError($"SpawnBotForGroup: Attempted to spawn a group bot but the botCacheElement was null.");
            }
        }

        internal static async UniTask CreateNewBot(WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator, BotSpawner botSpawnerClass, Vector3 coordinate, CancellationTokenSource cancellationTokenSource, string zone, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"CreateNewBot: Cancellation requested, exiting method.");
                return;
            }

            Debug.Log($"CreateNewBot: Starting bot creation process.");

            BotDifficulty botdifficulty = GetBotDifficulty(wildSpawnType);

            IProfileData botData = new IProfileData(side, wildSpawnType, botdifficulty, 0f, null);
            BotCreationDataClass bot = await BotCreationDataClass.Create(botData, ibotCreator, 1, botSpawnerClass);

            if (bot == null)
            {
                Debug.LogError("CreateNewBot: BotCreationDataClass.Create returned null.");
                return;
            }

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();
            Debug.Log($"CreateNewBot: Min spawn distance from player: {minSpawnDistFromPlayer}");

            Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coordinate, maxSpawnTriesPerBot.Value, cancellationToken);

            if (!spawnPosition.HasValue)
            {
                Debug.Log($"CreateNewBot: Initial spawn position not found, checking alternative coordinates.");

                foreach (var coord in coordinates)
                {
                    spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coord, maxSpawnTriesPerBot.Value, cancellationToken);
                    if (spawnPosition.HasValue)
                    {
                        Debug.Log($"CreateNewBot: Found spawn position at alternative coordinate.");
                        break;
                    }
                }
            }

            if (spawnPosition.HasValue)
            {
                var closestCorePoint = GetClosestCorePoint(spawnPosition.Value);
                bot.AddPosition(spawnPosition.Value, closestCorePoint.Id);

                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition.Value, out float dist);

                Debug.Log($"CreateNewBot: Spawning bot at distance to player of: {Vector3.Distance(spawnPosition.Value, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                  $"of side: {bot.Side} and difficulty: {botdifficulty} in spawn zone {zone}");

                await ActivateBot(closestBotZone, bot, cancellationTokenSource, cancellationToken);
            }
            else
            {
                Debug.Log($"CreateNewBot: No valid spawn position found - skipping this spawn");
            }
        }


        internal static async UniTask ActivateBot(BotZone botZone, BotCreationDataClass botData, CancellationTokenSource cancellationTokenSource, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log("ActivateBot: Cancellation requested, exiting method.");
                return;
            }

            // Ensure botData and profile data are not null
            if (botData == null || botData._profileData == null)
            {
                Debug.LogError("ActivateBot: BotCreationDataClass or ProfileData is null.");
                return;
            }

            Debug.Log($"ActivateBot: BotCreationDataClass data initialized: {botData != null}");
            Debug.Log($"ActivateBot: ProfileData initialized: {botData._profileData != null}");

            // Ensure profiles list is not null and has at least one profile
            if (botData.Profiles == null || botData.Profiles.Count == 0)
            {
                Debug.LogError("ActivateBot: Profiles list is null or empty.");
                return;
            }

            // Use game's method_9 for activation, which handles necessary setup
            //botCreator.ActivateBot(botData, botZone, false, new Func<BotOwner, BotZone, BotsGroup>(getGroupWrapper.GetGroupAndSetEnemies), new Action<BotOwner>(CreateBotCallbackWrapper.CreateBotCallback), cancellationTokenSource.Token);

            //grab private CancellationTokenSource _cancellationTokenSource from BotSpawner using accesstools
            var cancTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;

            var role = botData.Profiles[0].Info.Settings.Role;
            if (WildSpawnTypeDictionaries.IsBoss(role) || WildSpawnTypeDictionaries.IsFollower(role))
            {
                botSpawnerClass.method_9(botZone, botData, null, cancTokenSource.Token);
            }
            else
            {
                //use old method for bots that aren't bosses or followers
                CreateBotCallbackWrapper createBotCallbackWrapper = new CreateBotCallbackWrapper
                {
                    botData = botData
                };

                GetGroupWrapper getGroupWrapper = new GetGroupWrapper();

                botCreator.ActivateBot(botData, botZone, false, new Func<BotOwner, BotZone, BotsGroup>(getGroupWrapper.GetGroupAndSetEnemies), new Action<BotOwner>(createBotCallbackWrapper.CreateBotCallback), cancTokenSource.Token);
            }


            // Clear bot cache after activation to ensure proper cleanup
            await ClearBotCacheAfterActivation(botData);

        }

        internal static async UniTask ClearBotCacheAfterActivation(BotCreationDataClass botData)
        {
            DonutsBotPrep.timeSinceLastReplenish = 0f;

            if (botData == null)
            {
                Debug.LogError("ClearBotCacheAfterActivation: BotCreationDataClass is null.");
                return;
            }

            if (botData.Profiles == null)
            {
                Debug.LogError("ClearBotCacheAfterActivation: BotCreationDataClass.Profiles is null.");
                return;
            }

            //Check to make sure DonutBotPrep.BotInfos is not null
            if (DonutsBotPrep.BotInfos == null)
            {
                Debug.LogError("ClearBotCacheAfterActivation: DonutsBotPrep.BotInfos is null.");
                return;
            }

            //search DonutsBotPrep.BotInfos.SpawnType == botData.SpawnType
            foreach (var botInfo in DonutsBotPrep.BotInfos)
            {
                if (botInfo == null)
                {
                    Debug.LogError("ClearBotCacheAfterActivation: BotInfo is null.");
                    continue;
                }

                if (botInfo.Bots == botData)
                {
                    //botInfo.Bots.Profiles.Clear();
                    botInfo.Bots = null;
                    Debug.Log("ClearBotCacheAfterActivation: Bot cache cleared.");
                    break;
                }
            }

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
                        Debug.Log($"IsWithinBotActivationDistance: Player within activation distance at {position}.");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"IsWithinBotActivationDistance: Exception encountered - {ex.Message}\n{ex.StackTrace}");
            }

            return false;
        }

        internal static WildSpawnType GetWildSpawnType(string spawnType)
        {
            spawnType = spawnType.ToLower();
            Debug.Log($"GetWildSpawnType: Determining WildSpawnType for spawnType {spawnType}");

            if (spawnType == "pmc")
            {
                return UnityEngine.Random.Range(0, 2) == 0 ? WildSpawnType.pmcUSEC : WildSpawnType.pmcBEAR;
            }

            if (WildSpawnTypeDictionaries.StringToWildSpawnType.TryGetValue(spawnType, out var wildSpawnType))
            {
                return wildSpawnType;
            }

            return WildSpawnType.assault;
        }

        internal static EPlayerSide GetSideForWildSpawnType(WildSpawnType spawnType)
        {
            Debug.Log($"GetSideForWildSpawnType: Getting side for WildSpawnType {spawnType}");
            return WildSpawnTypeDictionaries.WildSpawnTypeToEPlayerSide.TryGetValue(spawnType, out var side) ? side : EPlayerSide.Savage;
        }

        internal static int getActualBotCount(string pluginGroupChance, int minGroupSize, int maxGroupSize)
        {
            Debug.Log($"getActualBotCount: Calculating actual bot count with group chance {pluginGroupChance}");

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
            Debug.Log($"getGroupChance: Calculating group chance for {pmcGroupChance}");

            double[] probabilities = GetProbabilityArray(pmcGroupChance) ?? GetDefaultProbabilityArray(pmcGroupChance);
            System.Random random = new System.Random();
            return getOutcomeWithProbability(random, probabilities, minGroupSize, maxGroupSize) + minGroupSize;
        }

        internal static double[] GetProbabilityArray(string pmcGroupChance)
        {
            if (groupChanceWeights.TryGetValue(pmcGroupChance, out var relativeWeights))
            {
                double totalWeight = relativeWeights.Sum();
                Debug.Log($"GetProbabilityArray: Calculating probabilities for {pmcGroupChance} with total weight {totalWeight}");
                return relativeWeights.Select(weight => weight / totalWeight).ToArray();
            }

            throw new ArgumentException($"Invalid pmcGroupChance: {pmcGroupChance}");
        }

        internal static double[] GetDefaultProbabilityArray(string pmcGroupChance)
        {
            if (groupChanceWeights.TryGetValue(pmcGroupChance, out var relativeWeights))
            {
                double totalWeight = relativeWeights.Sum();
                Debug.Log($"GetDefaultProbabilityArray: Calculating default probabilities for {pmcGroupChance} with total weight {totalWeight}");
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
            Debug.Log($"getOutcomeWithProbability: Calculating outcome with probability threshold {probabilityThreshold}");

            double cumulative = 0.0;
            int adjustedMaxCount = maxGroupSize - minGroupSize;
            for (int i = 0; i <= adjustedMaxCount; i++)
            {
                cumulative += probabilities[i];
                if (probabilityThreshold < cumulative)
                {
                    Debug.Log($"getOutcomeWithProbability: Outcome determined at index {i}");
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

            Debug.Log("InitializeGroupChanceWeights: Initializing group chance weights.");

            groupChanceWeights["Default"] = defaultWeights;
            groupChanceWeights["Low"] = lowWeights;
            groupChanceWeights["High"] = highWeights;
        }

        internal static int[] ParseGroupWeightDistro(string weightsString)
        {
            Debug.Log($"ParseGroupWeightDistro: Parsing group weight distribution {weightsString}");
            return weightsString.Split(new char[] { ',' })
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(int.Parse)
                                .ToArray();
        }

        internal static BotDifficulty GetBotDifficulty(WildSpawnType wildSpawnType)
        {
            Debug.Log($"GetBotDifficulty: Getting difficulty for WildSpawnType {wildSpawnType}");

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
            Debug.Log($"grabPMCDifficulty: Grabbing PMC difficulty");

            switch (botDifficultiesPMC.Value.ToLower())
            {
                case "asonline":
                    BotDifficulty[] randomDifficulty = { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard, BotDifficulty.impossible };
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
            Debug.Log($"grabSCAVDifficulty: Grabbing SCAV difficulty");

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
            Debug.Log($"grabOtherDifficulty: Grabbing difficulty for other types");

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
