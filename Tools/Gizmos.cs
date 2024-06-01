using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using EFT;
using EFT.Communications;
using UnityEngine;
using Donuts.Models;
using static Donuts.DonutComponent;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class Gizmos
    {
        internal bool isGizmoEnabled = false;
        internal static ConcurrentDictionary<Vector3, GameObject> gizmoMarkers = new ConcurrentDictionary<Vector3, GameObject>();
        internal static Coroutine gizmoUpdateCoroutine;
        internal static MonoBehaviour monoBehaviourRef;
        internal static StringBuilder DisplayedMarkerInfo = new StringBuilder();
        internal static StringBuilder PreviousMarkerInfo = new StringBuilder();
        internal static Coroutine resetMarkerInfoCoroutine;

        internal Gizmos(MonoBehaviour monoBehaviour)
        {
            monoBehaviourRef = monoBehaviour;
        }

        private IEnumerator UpdateGizmoSpheresCoroutine()
        {
            while (isGizmoEnabled)
            {
                RefreshGizmoDisplay();
                yield return new WaitForSeconds(3f);
            }
        }

        internal static void DrawMarkers(IEnumerable<Entry> locations, Color color, PrimitiveType primitiveType)
        {
            foreach (var hotspot in locations)
            {
                var newCoordinate = new Vector3(hotspot.Position.x, hotspot.Position.y, hotspot.Position.z);

                if (DonutsBotPrep.maplocation == hotspot.MapName && !gizmoMarkers.ContainsKey(newCoordinate))
                {
                    var marker = CreateMarker(newCoordinate, color, primitiveType, hotspot.MaxDistance);
                    gizmoMarkers[newCoordinate] = marker;
                }
            }
        }

        private static GameObject CreateMarker(Vector3 position, Color color, PrimitiveType primitiveType, float size)
        {
            var marker = GameObject.CreatePrimitive(primitiveType);
            var material = marker.GetComponent<Renderer>().material;
            material.color = color;
            marker.GetComponent<Collider>().enabled = false;
            marker.transform.position = position;
            marker.transform.localScale = DefaultPluginVars.gizmoRealSize.Value ? new Vector3(size, 3f, size) : Vector3.one;
            return marker;
        }

        public void ToggleGizmoDisplay(bool enableGizmos)
        {
            isGizmoEnabled = enableGizmos;

            if (isGizmoEnabled && gizmoUpdateCoroutine == null)
            {
                RefreshGizmoDisplay();
                gizmoUpdateCoroutine = monoBehaviourRef.StartCoroutine(UpdateGizmoSpheresCoroutine());
            }
            else if (!isGizmoEnabled && gizmoUpdateCoroutine != null)
            {
                monoBehaviourRef.StopCoroutine(gizmoUpdateCoroutine);
                gizmoUpdateCoroutine = null;
                ClearGizmoMarkers();
            }
        }

        internal static void RefreshGizmoDisplay()
        {
            ClearGizmoMarkers();

            if (DefaultPluginVars.DebugGizmos.Value)
            {
                DrawMarkers(fightLocations?.Locations ?? Enumerable.Empty<Entry>(), Color.green, PrimitiveType.Sphere);
                DrawMarkers(sessionLocations?.Locations ?? Enumerable.Empty<Entry>(), Color.red, PrimitiveType.Cube);
            }
        }

        internal static void ClearGizmoMarkers()
        {
            foreach (var marker in gizmoMarkers.Values)
            {
                GameWorld.Destroy(marker);
            }
            gizmoMarkers.Clear();
        }

        internal static void DisplayMarkerInformation()
        {
            if (gizmoMarkers.Count == 0) return;

            var closestShape = gizmoMarkers.Values
                .OrderBy(shape => (shape.transform.position - gameWorld.MainPlayer.Transform.position).sqrMagnitude)
                .FirstOrDefault();

            if (closestShape == null || !IsShapeVisible(closestShape.transform.position)) return;

            UpdateDisplayedMarkerInfo(closestShape.transform.position);
        }

        private static bool IsShapeVisible(Vector3 shapePosition)
        {
            var direction = shapePosition - gameWorld.MainPlayer.Transform.position;
            return direction.sqrMagnitude <= 10f * 10f && Vector3.Angle(gameWorld.MainPlayer.Transform.forward, direction) < 20f;
        }

        private static void UpdateDisplayedMarkerInfo(Vector3 closestShapePosition)
        {
            var closestEntry = GetClosestEntry(closestShapePosition);
            if (closestEntry == null) return;

            PreviousMarkerInfo.Clear().Append(DisplayedMarkerInfo.ToString());

            DisplayedMarkerInfo.Clear()
                .AppendLine("Donuts: Marker Info")
                .AppendLine($"GroupNum: {closestEntry.GroupNum}")
                .AppendLine($"Name: {closestEntry.Name}")
                .AppendLine($"SpawnType: {closestEntry.WildSpawnType}")
                .AppendLine($"Position: {closestEntry.Position.x}, {closestEntry.Position.y}, {closestEntry.Position.z}")
                .AppendLine($"Bot Timer Trigger: {closestEntry.BotTimerTrigger}")
                .AppendLine($"Spawn Chance: {closestEntry.SpawnChance}")
                .AppendLine($"Max Random Number of Bots: {closestEntry.MaxRandomNumBots}")
                .AppendLine($"Max Spawns Before Cooldown: {closestEntry.MaxSpawnsBeforeCoolDown}")
                .AppendLine($"Ignore Timer for First Spawn: {closestEntry.IgnoreTimerFirstSpawn}")
                .AppendLine($"Min Spawn Distance From Player: {closestEntry.MinSpawnDistanceFromPlayer}");

            if (DisplayedMarkerInfo.ToString() != PreviousMarkerInfo.ToString())
            {
                ShowMarkerNotification(DisplayedMarkerInfo.ToString());
                StartResetMarkerInfoCoroutine();
            }
        }

        private static void ShowMarkerNotification(string message)
        {
            if (methodCache.TryGetValue("DisplayMessageNotification", out var displayMessageNotificationMethod))
            {
                displayMessageNotificationMethod.Invoke(null, new object[] { message, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });
            }
        }

        private static void StartResetMarkerInfoCoroutine()
        {
            if (resetMarkerInfoCoroutine != null)
            {
                monoBehaviourRef.StopCoroutine(resetMarkerInfoCoroutine);
            }

            resetMarkerInfoCoroutine = monoBehaviourRef.StartCoroutine(ResetMarkerInfoAfterDelay());
        }

        internal static IEnumerator ResetMarkerInfoAfterDelay()
        {
            yield return new WaitForSeconds(5f);
            DisplayedMarkerInfo.Clear();
            resetMarkerInfoCoroutine = null;
        }

        internal static Entry GetClosestEntry(Vector3 position)
        {
            Entry closestEntry = null;
            float closestDistanceSq = float.MaxValue;

            foreach (var entry in fightLocations.Locations.Concat(sessionLocations.Locations))
            {
                var entryPosition = new Vector3(entry.Position.x, entry.Position.y, entry.Position.z);
                float distanceSq = (entryPosition - position).sqrMagnitude;
                if (distanceSq < closestDistanceSq)
                {
                    closestDistanceSq = distanceSq;
                    closestEntry = entry;
                }
            }

            return closestEntry;
        }

        public static MethodInfo GetDisplayMessageNotificationMethod() => methodCache["DisplayMessageNotification"];
    }
}
