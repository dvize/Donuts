using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using EFT;
using EFT.Communications;
using Newtonsoft.Json;
using UnityEngine;
using Donuts.Models;
using Cysharp.Threading.Tasks;
using static Donuts.DefaultPluginVars;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class EditorFunctions
    {
        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public EditorFunctions()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(EditorFunctions));
        }

        internal static void DeleteSpawnMarker()
        {
            // Check if any of the required objects are null
            if (Donuts.DonutComponent.gameWorld == null)
            {
                Logger.LogDebug("IBotGame Not Instantiated or gameWorld is null.");
                return;
            }

            // Need to be able to see it to delete it
            if (DebugGizmos.Value)
            {
                // If the list is empty already, return
                if (Gizmos.spawnmarkers.Count == 0)
                {
                    return;
                }

                // Get the closest spawn marker to the player
                var closestEntry = Gizmos.spawnmarkers
                    .OrderBy(x => Vector3.Distance(Donuts.DonutComponent.gameWorld.MainPlayer.Position, new Vector3(x.Position.x, x.Position.y, x.Position.z)))
                    .FirstOrDefault();

                // Check if the closest entry is null
                if (closestEntry == null)
                {
                    DisplayNotification("The Spawn Marker could not be deleted because the closest entry could not be found.", Color.grey);
                    return;
                }

                // Remove the entry from the list if the distance from the player is less than 5m
                if (Vector3.Distance(Donuts.DonutComponent.gameWorld.MainPlayer.Position, new Vector3(closestEntry.Position.x, closestEntry.Position.y, closestEntry.Position.z)) < 5f)
                {
                    // Remove the entry from the spawnmarkers list
                    Gizmos.spawnmarkers.Remove(closestEntry);

                    // Display a message to the player
                    var txt = $"Spawn Marker Deleted for \n {closestEntry.Name}\n SpawnType: {closestEntry.WildSpawnType}\n Position: {closestEntry.Position.x}, {closestEntry.Position.y}, {closestEntry.Position.z}";
                    DisplayNotification(txt, Color.yellow);

                    // Remove the marker from gizmoMarkers
                    var coordinate = new Vector3(closestEntry.Position.x, closestEntry.Position.y, closestEntry.Position.z);
                    if (Gizmos.gizmoMarkers.TryRemove(coordinate, out var sphere))
                    {
                        GameWorld.Destroy(sphere);
                    }
                }
            }
        }

        internal static void CreateSpawnMarker()
        {
            // Check if any of the required objects are null
            if (DonutComponent.gameWorld == null)
            {
                Logger.LogDebug("IBotGame Not Instantiated or gameWorld is null.");
                return;
            }

            // Create new Donuts.Entry
            Entry newEntry = new Entry
            {
                Name = spawnName.Value,
                GroupNum = groupNum.Value,
                MapName = DonutsBotPrep.maplocation,
                WildSpawnType = wildSpawns.Value,
                MinDistance = minSpawnDist.Value,
                MaxDistance = maxSpawnDist.Value,
                MaxRandomNumBots = maxRandNumBots.Value,
                SpawnChance = spawnChance.Value,
                BotTimerTrigger = botTimerTrigger.Value,
                BotTriggerDistance = botTriggerDistance.Value,
                Position = new Position
                {
                    x = DonutComponent.gameWorld.MainPlayer.Position.x,
                    y = DonutComponent.gameWorld.MainPlayer.Position.y,
                    z = DonutComponent.gameWorld.MainPlayer.Position.z
                },

                MaxSpawnsBeforeCoolDown = maxSpawnsBeforeCooldown.Value,
                IgnoreTimerFirstSpawn = ignoreTimerFirstSpawn.Value,
                MinSpawnDistanceFromPlayer = minSpawnDistanceFromPlayer.Value
            };

            // Add new entry to the spawnmarkers list
            Gizmos.spawnmarkers.Add(newEntry);

            // Notify user of the creation
            var txt = $"Wrote Entry for {newEntry.Name}\n SpawnType: {newEntry.WildSpawnType}\n Position: {newEntry.Position.x}, {newEntry.Position.y}, {newEntry.Position.z}";
            DisplayNotification(txt, Color.yellow);
        }

        internal static async UniTask WriteToJsonFile()
        {
            // Check if any of the required objects are null
            if (Donuts.DonutComponent.gameWorld == null)
            {
                Logger.LogDebug("IBotGame Not Instantiated or gameWorld is null.");
                return;
            }

            string dllPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(dllPath);
            string jsonFolderPath = Path.Combine(directoryPath, "patterns");
            string json = JsonConvert.SerializeObject(Gizmos.spawnmarkers, Formatting.Indented);
            string fileName = DonutsBotPrep.maplocation + "_" + UnityEngine.Random.Range(0, 1000) + "_Markers.json";

            // Write JSON to file with filename
            string jsonFilePath = Path.Combine(jsonFolderPath, fileName);

            await UniTask.SwitchToThreadPool();
            using (StreamWriter writer = new StreamWriter(jsonFilePath, false))
            {
                await writer.WriteAsync(json);
            }
            await UniTask.SwitchToMainThread();

            DisplayNotification($"Wrote Json File to: {jsonFilePath}", Color.yellow);
        }

        private static void DisplayNotification(string message, Color color)
        {
            var displayMessageNotificationMethod = Gizmos.GetDisplayMessageNotificationMethod();
            if (displayMessageNotificationMethod != null)
            {
                displayMessageNotificationMethod.Invoke(null, new object[] { message, ENotificationDurationType.Long, ENotificationIconType.Default, color });
            }
        }
    }
}
