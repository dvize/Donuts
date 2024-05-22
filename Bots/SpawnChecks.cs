using System.Collections.Generic;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using Donuts.Models;
using static Donuts.DonutComponent;
using System.Linq;
using Cysharp.Threading.Tasks;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class SpawnChecks
    {
        #region spawnchecks

        internal static async Task<Vector3?> GetValidSpawnPosition(Entry hotspot, Vector3 coordinate, int maxSpawnAttempts)
        {
            for (int i = 0; i < maxSpawnAttempts; i++)
            {
                Vector3 spawnPosition = GenerateRandomSpawnPosition(hotspot, coordinate);

                if (NavMesh.SamplePosition(spawnPosition, out var navHit, 2f, NavMesh.AllAreas))
                {
                    spawnPosition = navHit.position;

                    if (await IsValidSpawnPosition(spawnPosition, hotspot))
                    {
#if DEBUG
                        DonutComponent.Logger.LogDebug("Found spawn position at: " + spawnPosition);
#endif
                        return spawnPosition;
                    }
                }

                await Task.Delay(1);
            }

            return null;
        }

        private static Vector3 GenerateRandomSpawnPosition(Entry hotspot, Vector3 coordinate)
        {
            float randomX = Random.Range(-hotspot.MaxDistance, hotspot.MaxDistance);
            float randomZ = Random.Range(-hotspot.MaxDistance, hotspot.MaxDistance);

            return new Vector3(coordinate.x + randomX, coordinate.y, coordinate.z + randomZ);
        }

        internal static async Task<bool> IsValidSpawnPosition(Vector3 spawnPosition, Entry hotspot)
        {
            if (spawnPosition == null || hotspot == null)
            {
                DonutComponent.Logger.LogDebug("Spawn position or hotspot is null.");
                return false;
            }

            var tasks = new List<UniTask<bool>>
            {
                IsSpawnPositionInsideWall(spawnPosition),
                IsSpawnPositionInPlayerLineOfSight(spawnPosition),
                IsSpawnInAir(spawnPosition)
            };

            var tasks2 = new List<Task<bool>>
            {
            };

            if (DefaultPluginVars.globalMinSpawnDistanceFromPlayerBool.Value)
            {
                tasks2.Add(IsMinSpawnDistanceFromPlayerTooShort(spawnPosition, hotspot));
            }

            if (DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsBool.Value)
            {
                tasks2.Add(IsPositionTooCloseToOtherBots(spawnPosition, hotspot));
            }

            bool[] results = await UniTask.WhenAll(tasks);

            //add to results if tasks2 not empty
            if (tasks2.Count > 0)
            {
                bool[] results2 = await Task.WhenAll(tasks2);
                results = results.Concat(results2).ToArray();
            }

            if (results.Any(result => result))
            {
                DonutComponent.Logger.LogDebug("Spawn position failed one or more checks.");
                return false;
            }

            return true;
        }

        internal static async UniTask<bool> IsSpawnPositionInPlayerLineOfSight(Vector3 spawnPosition)
        {
            foreach (var player in playerList)
            {
                if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
                {
                    continue;
                }
                Vector3 playerPosition = player.MainParts[BodyPartType.head].Position;
                Vector3 direction = (playerPosition - spawnPosition).normalized;
                float distance = Vector3.Distance(spawnPosition, playerPosition);
                if (!Physics.Raycast(spawnPosition, direction, distance, LayerMaskClass.HighPolyWithTerrainMask))
                {
                    return true;
                }
            }
            return false;
        }

        internal static async UniTask<bool> IsSpawnPositionInsideWall(Vector3 position)
        {
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

        internal static async UniTask<bool> IsSpawnInAir(Vector3 position)
        {
            Ray ray = new Ray(position, Vector3.down);
            float distance = 10f;

            return !Physics.Raycast(ray, distance, LayerMaskClass.HighPolyWithTerrainMask);
        }

        private static float GetMinDistanceFromPlayer(Entry hotspot)
        {
            if (DefaultPluginVars.globalMinSpawnDistanceFromPlayerBool.Value)
            {
                switch (hotspot.MapName.ToLower())
                {
                    case "bigmap": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerCustoms.Value;
                    case "factory4_day": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerFactory.Value;
                    case "factory4_night": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerFactory.Value;
                    case "tarkovstreets": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerStreets.Value;
                    case "sandbox": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerGroundZero.Value;
                    case "rezervbase": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerReserve.Value;
                    case "lighthouse": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerLighthouse.Value;
                    case "shoreline": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerShoreline.Value;
                    case "woods": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerWoods.Value;
                    case "laboratory": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerLaboratory.Value;
                    case "interchange": return DefaultPluginVars.globalMinSpawnDistanceFromPlayerInterchange.Value;
                    default: return hotspot.MinSpawnDistanceFromPlayer;
                }
            }
            else
            {
                return hotspot.MinSpawnDistanceFromPlayer;
            }
        }

        private static float GetMinDistanceFromOtherBots(Entry hotspot)
        {
            switch (hotspot.MapName.ToLower())
            {
                case "bigmap": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsCustoms.Value;
                case "factory4_day": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsFactory.Value;
                case "factory4_night": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsFactory.Value;
                case "tarkovstreets": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsStreets.Value;
                case "sandbox": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsGroundZero.Value;
                case "rezervbase": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsReserve.Value;
                case "lighthouse": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsLighthouse.Value;
                case "shoreline": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsShoreline.Value;
                case "woods": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsWoods.Value;
                case "laboratory": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsLaboratory.Value;
                case "interchange": return DefaultPluginVars.globalMinSpawnDistanceFromOtherBotsInterchange.Value;
                default: return 0f;
            }
        }

        internal static async Task<bool> IsMinSpawnDistanceFromPlayerTooShort(Vector3 position, Entry hotspot)
        {
            float minDistanceFromPlayer = GetMinDistanceFromPlayer(hotspot);

            var tasks = playerList
                .Where(player => player != null && player.HealthController != null && player.HealthController.IsAlive)
                .Select(player => Task.Run(() =>
                {
                    if ((player.Position - position).sqrMagnitude < (minDistanceFromPlayer * minDistanceFromPlayer))
                    {
                        return true;
                    }
                    return false;
                }))
                .ToList();

            bool[] results = await Task.WhenAll(tasks);
            return results.Any(result => result);
        }

        internal static async Task<bool> IsPositionTooCloseToOtherBots(Vector3 position, Entry hotspot)
        {
            float minDistanceFromOtherBots = GetMinDistanceFromOtherBots(hotspot);
            List<Player> players = Singleton<GameWorld>.Instance.AllAlivePlayersList;

            var tasks = players
                .Where(player => player != null && player.HealthController.IsAlive && !player.IsYourPlayer)
                .Select(player => Task.Run(() =>
                {
                    if ((player.Position - position).sqrMagnitude < (minDistanceFromOtherBots * minDistanceFromOtherBots))
                    {
                        return true;
                    }
                    return false;
                }))
                .ToList();

            bool[] results = await Task.WhenAll(tasks);
            return results.Any(result => result);
        }


        #endregion
    }
}
