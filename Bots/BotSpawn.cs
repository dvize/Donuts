using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System.Linq;
using static Donuts.DefaultPluginVars;
using static Donuts.DonutComponent;
using static Donuts.DonutsBotPrep;
using System;
using BepInEx.Logging;
using System.Collections.Concurrent;

namespace Donuts
{
    internal class DonutBotSpawn : MonoBehaviour
    {
        private const string PmcSpawnTypes = "pmc,pmcUSEC,pmcBEAR";
        private const string ScavSpawnType = "assault";

        internal static ManualLogSource Logger
        {
            get; private set;
        }

        internal static async UniTask SpawnBotsFromInfo(ConcurrentBag<BotSpawnInfo> botSpawnInfos, CancellationToken cancellationToken)
        {
            if (botSpawnInfos == null)
            {
                DonutComponent.Logger.LogError("SpawnBotsFromInfo: botSpawnInfos list is null.");
                return;
            }

            if (!botSpawnInfos.Any())
            {
                DonutComponent.Logger.LogError("SpawnBotsFromInfo: botSpawnInfos list is empty.");
                return;
            }

            DonutComponent.Logger.LogInfo("SpawnBotsFromInfo: Starting spawn tasks for botSpawnInfos.");
            var spawnTasks = botSpawnInfos.Select(botSpawnInfo =>
            {
                if (botSpawnInfo == null)
                {
                    DonutComponent.Logger.LogError("SpawnBotsFromInfo: botSpawnInfo is null in the list.");
                    return UniTask.CompletedTask;
                }
                return SpawnStartingBots(botSpawnInfo, cancellationToken);
            }).ToArray();

            await UniTask.WhenAll(spawnTasks);
        }

        internal static async UniTask SpawnStartingBots(BotSpawnInfo botSpawnInfo, CancellationToken cancellationToken)
        {
            DonutComponent.Logger.LogDebug("SpawnStartingBots: Entering method.");

            if (botSpawnInfo == null)
            {
                DonutComponent.Logger.LogError("SpawnStartingBots: BotSpawnInfo is null. Cannot proceed with spawning bots.");
                return;
            }
            if (botSpawnInfo.BotType == null)
            {
                DonutComponent.Logger.LogError("SpawnStartingBots: BotType is null. Cannot proceed with spawning bots.");
                return;
            }
            if (botSpawnInfo.Faction == null)
            {
                DonutComponent.Logger.LogError("SpawnStartingBots: Faction is null. Cannot proceed with spawning bots.");
                return;
            }
            if (botSpawnInfo.GroupSize == null)
            {
                DonutComponent.Logger.LogError("SpawnStartingBots: GroupSize is null. Cannot proceed with spawning bots.");
                return;
            }
            if (botSpawnInfo.Coordinates == null || !botSpawnInfo.Coordinates.Any())
            {
                DonutComponent.Logger.LogError("SpawnStartingBots: Coordinates list is null or empty. Cannot proceed with spawning bots.");
                return;
            }
            if (botSpawnInfo.Difficulty == null)
            {
                DonutComponent.Logger.LogError("SpawnStartingBots: Difficulty is null. Cannot proceed with spawning bots.");
                return;
            }
            if (string.IsNullOrEmpty(botSpawnInfo.Zone))
            {
                DonutComponent.Logger.LogError("SpawnStartingBots: Zone is null or empty. Cannot proceed with spawning bots.");
                return;
            }

            var wildSpawnType = botSpawnInfo.BotType;
            var side = botSpawnInfo.Faction;
            var groupSize = botSpawnInfo.GroupSize;
            var coordinates = botSpawnInfo.Coordinates;
            var botDifficulty = botSpawnInfo.Difficulty;
            var zone = botSpawnInfo.Zone;

            DonutComponent.Logger.LogInfo($"SpawnStartingBots: WildSpawnType: {wildSpawnType}, Side: {side}, GroupSize: {groupSize}, BotDifficulty: {botDifficulty}, Zone: {zone}");

            var cachedBotGroup = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, groupSize);
            if (cachedBotGroup == null)
            {
                DonutComponent.Logger.LogInfo($"SpawnStartingBots: No starting bots found in cache for this spawn, generating data on the fly, this may take some time.");
                var botInfo = new PrepBotInfo(wildSpawnType, botDifficulty, side, groupSize > 1, groupSize);
                await DonutsBotPrep.CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize, cancellationToken);
                DonutsBotPrep.BotInfos.Add(botInfo);
                cachedBotGroup = botInfo.Bots;
            }

            DonutComponent.Logger.LogWarning("SpawnStartingBots: Starting bot group found in cache, proceeding with spawn checks.");

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();

            foreach (var coordinate in coordinates)
            {
                if (WildSpawnTypeDictionaries.IsBoss(wildSpawnType) || WildSpawnTypeDictionaries.IsFollower(wildSpawnType))
                {
                    DonutComponent.Logger.LogInfo($"SpawnStartingBots: Boss or Follower bot detected, skipping spawn checks.");
                    await BotSpawnHelper.ActivateStartingBots(cachedBotGroup, wildSpawnType, side, botCreator, botSpawnerClass, coordinate, botDifficulty, groupSize, zone, cancellationToken);
                    return;
                }

                Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coordinate, maxSpawnTriesPerBot.Value, cancellationToken);
                if (spawnPosition.HasValue)
                {
                    await BotSpawnHelper.ActivateStartingBots(cachedBotGroup, wildSpawnType, side, botCreator, botSpawnerClass, spawnPosition.Value, botDifficulty, groupSize, zone, cancellationToken);
                    return;
                }
            }

            DonutComponent.Logger.LogInfo($"SpawnStartingBots: All coordinates in zone {zone} failed for this spawn, skipping this spawn");
        }

        internal static async UniTask SpawnBots(BotWave botWave, string zone, Vector3 coordinate, string wildSpawnType, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            DonutComponent.Logger.LogInfo("Entering SpawnBots method.");

            if (botWave == null)
            {
                DonutComponent.Logger.LogError("SpawnBots: botWave is null. Cannot proceed with spawning bots.");
                return;
            }

            if (string.IsNullOrEmpty(zone))
            {
                DonutComponent.Logger.LogError("SpawnBots: Zone is null or empty. Cannot proceed with spawning bots.");
                return;
            }

            if (coordinates == null || !coordinates.Any())
            {
                DonutComponent.Logger.LogError("SpawnBots: Coordinates list is null or empty. Cannot proceed with spawning bots.");
                return;
            }

            if (string.IsNullOrEmpty(wildSpawnType))
            {
                DonutComponent.Logger.LogError("SpawnBots: wildSpawnType is null or empty. Cannot proceed with spawning bots.");
                return;
            }

            WildSpawnType actualWildSpawnType = BotSpawnHelper.DetermineWildSpawnType(wildSpawnType);
            int maxCount = BotSpawnHelper.DetermineMaxBotCount(wildSpawnType, botWave.MinGroupSize, botWave.MaxGroupSize);

            DonutComponent.Logger.LogInfo($"SpawnBots: WildSpawnType: {actualWildSpawnType}, MaxCount: {maxCount}, Zone: {zone}, Coordinate: {coordinate}");

            if (HardCapEnabled.Value)
            {
                DonutComponent.Logger.LogInfo("SpawnBots: HardCap is enabled. Adjusting max count based on active bot counts.");
                maxCount = await BotSpawnHelper.AdjustMaxCountForHardCap(wildSpawnType, maxCount, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                DonutComponent.Logger.LogInfo("SpawnBots: Cancellation requested. Exiting SpawnBots method.");
                return;
            }

            if (maxCount == 0)
            {
                DonutComponent.Logger.LogInfo("SpawnBots: Max bot count is 0, skipping spawn");
                return;
            }

            bool isGroup = maxCount > 1;
            DonutComponent.Logger.LogInfo($"SpawnBots: Setup spawn for {(isGroup ? "group" : "single")} with maxCount: {maxCount}");

            try
            {
                await BotSpawnHelper.SetupSpawn(botWave, maxCount, isGroup, actualWildSpawnType, coordinate, zone, coordinates, cancellationToken);
            }
            catch (Exception ex)
            {
                DonutComponent.Logger.LogError($"SpawnBots: Error during SetupSpawn: {ex.Message}\n{ex.StackTrace}");
            }

            DonutComponent.Logger.LogInfo("Exiting SpawnBots method.");
        }

        private static void LogCancellation(CancellationToken cancellationToken)
        {
            DonutComponent.Logger.LogInfo(() => Debug.Log("LogCancellation: Cancellation token was triggered."));
        }
    }
}
