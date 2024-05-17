using UnityEngine;
using static Donuts.PluginGUIHelper;

namespace Donuts
{
    internal class DrawSpawnSettings
    {
        private static int selectedSpawnSettingsIndex = 0;
        private static string[] spawnSettingsSubTabs = { "General", "Advanced", "Other" };

        internal static void Enable()
        {
            // Apply the cached styles to ensure consistency
            PluginGUIHelper.ApplyCachedStyles();

            GUILayout.Space(30);
            GUILayout.BeginHorizontal();

            // Left-hand navigation menu for sub-tabs
            GUILayout.BeginVertical(GUILayout.Width(150));

            GUILayout.Space(20);
            DrawSubTabs();
            GUILayout.EndVertical();

            // Right-hand content area for selected sub-tab
            GUILayout.BeginVertical();

            switch (selectedSpawnSettingsIndex)
            {
                case 0:
                    DrawSpawnSettingsGeneral();
                    break;
                case 1:
                    DrawSpawnSettingsAdvanced();
                    break;
                case 2:
                    DrawSpawnSettingsOther();
                    break;
            }

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private static void DrawSubTabs()
        {
            for (int i = 0; i < spawnSettingsSubTabs.Length; i++)
            {
                GUIStyle currentStyle = cachedSubTabButtonStyle;
                if (selectedSpawnSettingsIndex == i)
                {
                    currentStyle = cachedSubTabButtonActiveStyle;
                }

                // Set background color explicitly for each button
                PluginGUIHelper.ApplyCachedStyles();

                if (GUILayout.Button(spawnSettingsSubTabs[i], currentStyle))
                {
                    selectedSpawnSettingsIndex = i;
                }
            }
        }

        internal static void DrawSpawnSettingsGeneral()
        {
            // Draw general spawn settings
            GUILayout.Label("General Spawn Settings", cachedSubTabLabelStyle);
            // Add more settings here
        }

        internal static void DrawSpawnSettingsAdvanced()
        {
            // Draw advanced spawn settings
            GUILayout.Label("Advanced Spawn Settings", cachedSubTabLabelStyle);
            // Add more settings here
        }

        internal static void DrawSpawnSettingsOther()
        {
            // Draw other spawn settings
            GUILayout.Label("Other Spawn Settings", cachedSubTabLabelStyle);
            // Add more settings here
        }
    }
}
