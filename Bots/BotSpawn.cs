using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System.Linq;
using static Donuts.DefaultPluginVars;
using static Donuts.DonutComponent;
using System;
using BepInEx.Logging;

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

        public DonutBotSpawn()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(DonutBotSpawn));
        }

        internal static async UniTask SpawnBotsFromInfo(List<BotSpawnInfo> botSpawnInfos, CancellationToken cancellationToken)
        {
            if (botSpawnInfos == null)
            {
                Debug.LogError("SpawnBotsFromInfo: botSpawnInfos list is null.");
                return;
            }

            if (!botSpawnInfos.Any())
            {
                Debug.LogError("SpawnBotsFromInfo: botSpawnInfos list is empty.");
                return;
            }

            Debug.Log("SpawnBotsFromInfo: Starting spawn tasks for botSpawnInfos.");
            var spawnTasks = botSpawnInfos.Select(botSpawnInfo =>
            {
                if (botSpawnInfo == null)
                {
                    Debug.LogError("SpawnBotsFromInfo: botSpawnInfo is null in the list.");
                    return UniTask.CompletedTask;
                }
                return SpawnStartingBots(botSpawnInfo, cancellationToken);
            }).ToArray();

            await UniTask.WhenAll(spawnTasks);
        }

        internal static async UniTask SpawnStartingBots(BotSpawnInfo botSpawnInfo, CancellationToken cancellationToken)
        {
            Debug.Log("SpawnStartingBots: Entering method.");

            if (botSpawnInfo == null)
            {
                Debug.LogError("SpawnStartingBots: BotSpawnInfo is null. Cannot proceed with spawning bots.");
                return;
            }
            if (botSpawnInfo.BotType == null)
            {
                Debug.LogError("SpawnStartingBots: BotType is null. Cannot proceed with spawning bots.");
                return;
            }
            if (botSpawnInfo.Faction == null)
            {
                Debug.LogError("SpawnStartingBots: Faction is null. Cannot proceed with spawning bots.");
                return;
            }
            if (botSpawnInfo.GroupSize == null)
            {
                Debug.LogError("SpawnStartingBots: GroupSize is null. Cannot proceed with spawning bots.");
                return;
            }
            if (botSpawnInfo.Coordinates == null || !botSpawnInfo.Coordinates.Any())
            {
                Debug.LogError("SpawnStartingBots: Coordinates list is null or empty. Cannot proceed with spawning bots.");
                return;
            }
            if (botSpawnInfo.Difficulty == null)
            {
                Debug.LogError("SpawnStartingBots: Difficulty is null. Cannot proceed with spawning bots.");
                return;
            }
            if (string.IsNullOrEmpty(botSpawnInfo.Zone))
            {
                Debug.LogError("SpawnStartingBots: Zone is null or empty. Cannot proceed with spawning bots.");
                return;
            }

            var wildSpawnType = botSpawnInfo.BotType;
            var side = botSpawnInfo.Faction;
            var groupSize = botSpawnInfo.GroupSize;
            var coordinates = botSpawnInfo.Coordinates;
            var botDifficulty = botSpawnInfo.Difficulty;
            var zone = botSpawnInfo.Zone;

            Debug.Log($"SpawnStartingBots: WildSpawnType: {wildSpawnType}, Side: {side}, GroupSize: {groupSize}, BotDifficulty: {botDifficulty}, Zone: {zone}");

            var cachedBotGroup = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, groupSize);
            if (cachedBotGroup == null)
            {
                Debug.Log($"SpawnStartingBots: No starting bots found in cache for this spawn, generating data on the fly, this may take some time.");
                var botInfo = new PrepBotInfo(wildSpawnType, botDifficulty, side, groupSize > 1, groupSize);
                await DonutsBotPrep.CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize, cancellationToken);
                DonutsBotPrep.BotInfos.Add(botInfo);
                cachedBotGroup = botInfo.Bots;
            }

            Debug.LogWarning("SpawnStartingBots: Starting bot group found in cache, proceeding with spawn checks.");

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();

            foreach (var coordinate in coordinates)
            {
                if (botSpawnInfo.BotType.IsBoss() || botSpawnInfo.BotType.IsFollower())
                {
                    Debug.Log($"SpawnStartingBots: Boss or Follower bot detected, skipping spawn checks.");
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

            Debug.Log($"SpawnStartingBots: All coordinates in zone {zone} failed for this spawn, skipping this spawn");
        }

        internal static async UniTask SpawnBots(BotWave botWave, string zone, Vector3 coordinate, string wildSpawnType, List<Vector3> coordinates, CancellationToken cancellationToken)
        {
            Debug.Log("Entering SpawnBots method.");

            if (botWave == null)
            {
                Debug.LogError("SpawnBots: botWave is null. Cannot proceed with spawning bots.");
                return;
            }

            if (string.IsNullOrEmpty(zone))
            {
                Debug.LogError("SpawnBots: Zone is null or empty. Cannot proceed with spawning bots.");
                return;
            }

            if (coordinates == null || !coordinates.Any())
            {
                Debug.LogError("SpawnBots: Coordinates list is null or empty. Cannot proceed with spawning bots.");
                return;
            }

            if (string.IsNullOrEmpty(wildSpawnType))
            {
                Debug.LogError("SpawnBots: wildSpawnType is null or empty. Cannot proceed with spawning bots.");
                return;
            }

            WildSpawnType actualWildSpawnType = BotSpawnHelper.DetermineWildSpawnType(wildSpawnType);
            int maxCount = BotSpawnHelper.DetermineMaxBotCount(wildSpawnType, botWave.MinGroupSize, botWave.MaxGroupSize);

            Debug.Log($"SpawnBots: WildSpawnType: {actualWildSpawnType}, MaxCount: {maxCount}, Zone: {zone}, Coordinate: {coordinate}");

            if (HardCapEnabled.Value)
            {
                Debug.Log("SpawnBots: HardCap is enabled. Adjusting max count based on active bot counts.");
                maxCount = await BotSpawnHelper.AdjustMaxCountForHardCap(wildSpawnType, maxCount, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log("SpawnBots: Cancellation requested. Exiting SpawnBots method.");
                return;
            }

            if (maxCount == 0)
            {
                Debug.Log("SpawnBots: Max bot count is 0, skipping spawn");
                return;
            }

            bool isGroup = maxCount > 1;
            Debug.Log($"SpawnBots: Setup spawn for {(isGroup ? "group" : "single")} with maxCount: {maxCount}");

            try
            {
                await BotSpawnHelper.SetupSpawn(botWave, maxCount, isGroup, actualWildSpawnType, coordinate, zone, coordinates, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SpawnBots: Error during SetupSpawn: {ex.Message}\n{ex.StackTrace}");
            }

            Debug.Log("Exiting SpawnBots method.");
        }

        private static void LogCancellation(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => Debug.Log("LogCancellation: Cancellation token was triggered."));
        }
    }
}
