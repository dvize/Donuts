using System;
using System.Collections.Generic;
using Donuts.Models;
using UnityEngine;
using static Donuts.DefaultPluginVars;
using static Donuts.ImGUIToolkit;

namespace Donuts
{
    internal class DrawBossSettings
    {
        private static int selectedBossIndex = 0;
        private static string[] bossNames = {
            "Cultists", "Goons", "Glukhar", "Kaban", "Killa", "Kollontay",
            "Raiders", "Reshala", "Rogues", "Sanitar", "Shturman", "Tagilla", "Zryachiy"
        };

        private static string[] mapNames = {
            "Factory", "Customs", "Reserve", "Streets", "Woods", "Laboratory",
            "Shoreline", "Ground Zero", "Interchange", "Lighthouse"
        };

        internal static void Enable()
        {
            GUILayout.BeginHorizontal();

            // Left-hand navigation menu for boss tabs
            GUILayout.BeginVertical(GUILayout.Width(150));
            GUILayout.Space(20);
            DrawBossTabs();
            GUILayout.EndVertical();

            // Space between menu and content
            GUILayout.Space(40);

            // Right-hand content area for selected boss
            GUILayout.BeginVertical();
            DrawSelectedBossSettings();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private static void DrawBossTabs()
        {
            for (int i = 0; i < bossNames.Length; i++)
            {
                GUIStyle currentStyle = PluginGUIHelper.subTabButtonStyle;
                if (selectedBossIndex == i)
                {
                    currentStyle = PluginGUIHelper.subTabButtonActiveStyle;
                }

                if (GUILayout.Button(bossNames[i], currentStyle))
                {
                    selectedBossIndex = i;
                }
            }
        }

        private static void DrawSelectedBossSettings()
        {
            string bossName = bossNames[selectedBossIndex];
            
            // Safely access BossUseGlobalSpawnChance
            if (DefaultPluginVars.BossUseGlobalSpawnChance.TryGetValue(bossName, out var useGlobalChanceSetting))
            {
                useGlobalChanceSetting.Value = Toggle(
                    useGlobalChanceSetting.Name,
                    useGlobalChanceSetting.ToolTipText,
                    useGlobalChanceSetting.Value);
            }
            else
            {
                Debug.LogWarning($"BossUseGlobalSpawnChance setting not found for boss: {bossName}");
            }

            GUILayout.Space(20);
            GUILayout.Label("Spawn Chances Per Map", PluginGUIHelper.labelStyle);

            if (DefaultPluginVars.BossSpawnChances.TryGetValue(bossName, out var bossSpawnChances))
            {
                foreach (string mapName in mapNames)
                {
                    if (bossSpawnChances.TryGetValue(mapName, out var spawnChanceSetting))
                    {
                        spawnChanceSetting.Value = (int)Slider(
                            spawnChanceSetting.Name,
                            spawnChanceSetting.ToolTipText,
                            spawnChanceSetting.Value,
                            spawnChanceSetting.MinValue,
                            spawnChanceSetting.MaxValue);
                    }
                    else
                    {
                        Debug.LogWarning($"Spawn chance setting not found for boss {bossName} on map {mapName}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"BossSpawnChances not found for boss: {bossName}");
            }
        }
    }
}
