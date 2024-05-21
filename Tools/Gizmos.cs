using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.Communications;
using UnityEngine;
using Donuts.Models;
using static Donuts.DonutComponent;
using System.Threading;
using System;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class Gizmos
    {
        internal bool isGizmoEnabled = false;
        internal static HashSet<Vector3> drawnCoordinates = new HashSet<Vector3>();
        internal static List<GameObject> gizmoSpheres = new List<GameObject>();
        internal static MonoBehaviour monoBehaviourRef;
        internal static string displayedMarkerInfo = string.Empty;
        internal static string previousMarkerInfo = string.Empty;
        internal static CancellationTokenSource resetMarkerInfoCts;

        internal Gizmos(MonoBehaviour monoBehaviour)
        {
            monoBehaviourRef = monoBehaviour;
        }

        private async UniTask UpdateGizmoSpheres()
        {
            while (isGizmoEnabled)
            {
                RefreshGizmoDisplay(); // Refresh the gizmo display periodically

                await UniTask.Delay(3000);
            }
        }

        internal static void DrawMarkers(List<Entry> locations, Color color, PrimitiveType primitiveType)
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

                    if (DefaultPluginVars.gizmoRealSize.Value)
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

            if (isGizmoEnabled)
            {
                RefreshGizmoDisplay(); // Refresh the gizmo display initially
                UpdateGizmoSpheres().Forget(); // Use UniTask and Forget to avoid unobserved exceptions
            }
            else
            {
                ClearGizmoMarkers(); // Clear the drawn markers
            }
        }

        internal static void RefreshGizmoDisplay()
        {
            ClearGizmoMarkers(); // Clear existing markers

            // Check the values of DebugGizmos and gizmoRealSize and redraw the markers accordingly
            if (DefaultPluginVars.DebugGizmos.Value)
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

        internal static void ClearGizmoMarkers()
        {
            foreach (var marker in gizmoSpheres)
            {
                GameWorld.Destroy(marker);
            }
            gizmoSpheres.Clear();
            drawnCoordinates.Clear();
        }

        internal static void DisplayMarkerInformation()
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

            // Check if the closest shape is within 15m and directly visible to the player
            if (closestShape != null && closestDistanceSq <= 10f * 10f)
            {
                Vector3 direction = closestShape.transform.position - gameWorld.MainPlayer.Transform.position;
                float angle = Vector3.Angle(gameWorld.MainPlayer.Transform.forward, direction);

                if (angle < 20f)
                {
                    var locationsSet = new HashSet<Vector3>(fightLocations.Locations.Select(e => new Vector3(e.Position.x, e.Position.y, e.Position.z))
                        .Concat(sessionLocations.Locations.Select(e => new Vector3(e.Position.x, e.Position.y, e.Position.z))));

                    Vector3 closestShapePosition = closestShape.transform.position;
                    if (locationsSet.Contains(closestShapePosition))
                    {
                        Entry closestEntry = GetClosestEntry(closestShapePosition);
                        if (closestEntry != null)
                        {
                            previousMarkerInfo = displayedMarkerInfo;

                            // Use a single string interpolation to construct the final string
                            displayedMarkerInfo = string.Format(
                                "Donuts: Marker Info\n" +
                                "GroupNum: {0}\n" +
                                "Name: {1}\n" +
                                "SpawnType: {2}\n" +
                                "Position: {3}, {4}, {5}\n" +
                                "Bot Timer Trigger: {6}\n" +
                                "Spawn Chance: {7}\n" +
                                "Max Random Number of Bots: {8}\n" +
                                "Max Spawns Before Cooldown: {9}\n" +
                                "Ignore Timer for First Spawn: {10}\n" +
                                "Min Spawn Distance From Player: {11}\n",
                                closestEntry.GroupNum,
                                closestEntry.Name,
                                closestEntry.WildSpawnType,
                                closestEntry.Position.x, closestEntry.Position.y, closestEntry.Position.z,
                                closestEntry.BotTimerTrigger,
                                closestEntry.SpawnChance,
                                closestEntry.MaxRandomNumBots,
                                closestEntry.MaxSpawnsBeforeCoolDown,
                                closestEntry.IgnoreTimerFirstSpawn,
                                closestEntry.MinSpawnDistanceFromPlayer
                            );

                            if (displayedMarkerInfo != previousMarkerInfo)
                            {
                                displayMessageNotificationMethod?.Invoke(null, new object[] { displayedMarkerInfo, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });

                                resetMarkerInfoCts?.Cancel();
                                resetMarkerInfoCts?.Dispose();

                                resetMarkerInfoCts = new CancellationTokenSource();
                                ResetMarkerInfoAfterDelay(resetMarkerInfoCts.Token).Forget();
                            }
                        }
                    }
                }
            }
        }


        internal static async UniTask ResetMarkerInfoAfterDelay(CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(5000, cancellationToken: cancellationToken);
                displayedMarkerInfo = string.Empty;
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
        }

        internal static Entry GetClosestEntry(Vector3 position)
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

        public static MethodInfo GetDisplayMessageNotificationMethod() => displayMessageNotificationMethod;
    }
}
