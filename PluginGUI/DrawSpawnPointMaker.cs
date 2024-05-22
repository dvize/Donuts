using UnityEngine;
using static Donuts.PluginGUIHelper;
using static Donuts.ImGUIToolkit;
using static Donuts.DefaultPluginVars;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class DrawSpawnPointMaker
    {
        //For tab
        private static int selectedSpawnMakerSettingsIndex = 0;
        private static string[] spawnMakerSettingsSubTabs = { "Keybinds", "Spawn Setup" };

        // For dropdowns
        internal static int wildSpawnsIndex = 0;

        // Need this for dropdowns to show loaded values and also reset to default
        static DrawSpawnPointMaker()
        {
            InitializeDropdownIndices();
        }

        internal static void InitializeDropdownIndices()
        {
            wildSpawnsIndex = FindIndex(wildSpawns);
        }

        internal static void Enable()
        {

            // Apply the cached styles to ensure consistency
            PluginGUIHelper.ApplyCachedStyles();

            // Initialize the custom styles for the dropdown
            InitializeStyles();
            GUILayout.Space(30);
            GUILayout.BeginHorizontal();

            // Left-hand navigation menu for sub-tabs
            GUILayout.BeginVertical(GUILayout.Width(150));

            GUILayout.Space(20);
            DrawSubTabs();
            GUILayout.EndVertical();

            // Space between menu and subtab pages
            GUILayout.Space(40);

            // Right-hand content area for selected sub-tab
            GUILayout.BeginVertical();

            switch (selectedSpawnMakerSettingsIndex)
            {
                case 0:
                    DrawKeybindsTab();
                    break;
                case 1:
                    DrawSpawnSetupTab();
                    break;
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void DrawSubTabs()
        {
            for (int i = 0; i < spawnMakerSettingsSubTabs.Length; i++)
            {
                GUIStyle currentStyle = cachedSubTabButtonStyle;
                if (selectedSpawnMakerSettingsIndex == i)
                {
                    currentStyle = cachedSubTabButtonActiveStyle;
                }

                // Set background color explicitly for each button
                PluginGUIHelper.ApplyCachedStyles();

                if (GUILayout.Button(spawnMakerSettingsSubTabs[i], currentStyle))
                {
                    selectedSpawnMakerSettingsIndex = i;
                }
            }
        }

        internal static void DrawKeybindsTab()
        {
            // Draw general spawn settings

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            // Draw Keybind settings
            CreateSpawnMarkerKey.Value = KeybindField(CreateSpawnMarkerKey.Name, CreateSpawnMarkerKey.ToolTipText, CreateSpawnMarkerKey.Value);
            DeleteSpawnMarkerKey.Value = KeybindField(DeleteSpawnMarkerKey.Name, DeleteSpawnMarkerKey.ToolTipText, DeleteSpawnMarkerKey.Value);
            WriteToFileKey.Value = KeybindField(WriteToFileKey.Name, WriteToFileKey.ToolTipText, WriteToFileKey.Value);

            // Draw Toggle setting
            saveNewFileOnly.Value = Toggle(saveNewFileOnly.Name, saveNewFileOnly.ToolTipText, saveNewFileOnly.Value);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        internal static void DrawSpawnSetupTab()
        {
            // Draw advanced spawn settings

            GUILayout.BeginHorizontal();

            // First column
            GUILayout.BeginVertical();

            spawnName.Value = TextField(spawnName.Name, spawnName.ToolTipText, spawnName.Value);
            GUILayout.Space(10); 

            groupNum.Value = Slider(groupNum.Name, groupNum.ToolTipText, groupNum.Value, groupNum.MinValue, groupNum.MaxValue);
            GUILayout.Space(10);

            // Dropdown for wildSpawns
            wildSpawnsIndex = Dropdown(wildSpawns, wildSpawnsIndex);
            wildSpawns.Value = wildSpawns.Options[wildSpawnsIndex];
            GUILayout.Space(10);

            minSpawnDist.Value = Slider(minSpawnDist.Name, minSpawnDist.ToolTipText, minSpawnDist.Value, minSpawnDist.MinValue, minSpawnDist.MaxValue);
            GUILayout.Space(10);

            maxSpawnDist.Value = Slider(maxSpawnDist.Name, maxSpawnDist.ToolTipText, maxSpawnDist.Value, maxSpawnDist.MinValue, maxSpawnDist.MaxValue);
            GUILayout.Space(10); 

            botTriggerDistance.Value = Slider(botTriggerDistance.Name, botTriggerDistance.ToolTipText, botTriggerDistance.Value, botTriggerDistance.MinValue, botTriggerDistance.MaxValue);
            GUILayout.Space(10); 

            GUILayout.EndVertical();

            // Second column
            GUILayout.BeginVertical();

            botTimerTrigger.Value = Slider(botTimerTrigger.Name, botTimerTrigger.ToolTipText, botTimerTrigger.Value, botTimerTrigger.MinValue, botTimerTrigger.MaxValue);
            GUILayout.Space(10); 

            maxRandNumBots.Value = Slider(maxRandNumBots.Name, maxRandNumBots.ToolTipText, maxRandNumBots.Value, maxRandNumBots.MinValue, maxRandNumBots.MaxValue);
            GUILayout.Space(10); 

            spawnChance.Value = Slider(spawnChance.Name, spawnChance.ToolTipText, spawnChance.Value, spawnChance.MinValue, spawnChance.MaxValue);
            GUILayout.Space(10); 

            maxSpawnsBeforeCooldown.Value = Slider(maxSpawnsBeforeCooldown.Name, maxSpawnsBeforeCooldown.ToolTipText, maxSpawnsBeforeCooldown.Value, maxSpawnsBeforeCooldown.MinValue, maxSpawnsBeforeCooldown.MaxValue);
            GUILayout.Space(10); 

            ignoreTimerFirstSpawn.Value = Toggle(ignoreTimerFirstSpawn.Name, ignoreTimerFirstSpawn.ToolTipText, ignoreTimerFirstSpawn.Value);
            GUILayout.Space(10); 

            minSpawnDistanceFromPlayer.Value = Slider(minSpawnDistanceFromPlayer.Name, minSpawnDistanceFromPlayer.ToolTipText, minSpawnDistanceFromPlayer.Value, minSpawnDistanceFromPlayer.MinValue, minSpawnDistanceFromPlayer.MaxValue);
            GUILayout.Space(10); 

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

        }

    }
}

