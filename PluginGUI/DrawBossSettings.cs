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
            "Cultist Priest", "Death Knight", "Glukhar", "Kaban", "Killa", "Kollontay",
            "Raider", "Reshala", "Rogue", "Sanitar", "Shturman", "Tagilla", "Zryachiy"
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
                GUIStyle currentStyle = subTabButtonStyle;
                if (selectedBossIndex == i)
                {
                    currentStyle = subTabButtonActiveStyle;
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

            // Use Global Spawn Chance toggle
            DefaultPluginVars.BossUseGlobalSpawnChance[bossName].Value = Toggle(
                DefaultPluginVars.BossUseGlobalSpawnChance[bossName].Name,
                DefaultPluginVars.BossUseGlobalSpawnChance[bossName].ToolTipText,
                DefaultPluginVars.BossUseGlobalSpawnChance[bossName].Value);

            GUILayout.Space(20);
            GUILayout.Label("Spawn Chances Per Map", labelStyle);

            foreach (string mapName in mapNames)
            {
                DefaultPluginVars.BossSpawnChances[bossName][mapName].Value = (int)Slider(
                    DefaultPluginVars.BossSpawnChances[bossName][mapName].Name,
                    DefaultPluginVars.BossSpawnChances[bossName][mapName].ToolTipText,
                    DefaultPluginVars.BossSpawnChances[bossName][mapName].Value,
                    DefaultPluginVars.BossSpawnChances[bossName][mapName].MinValue,
                    DefaultPluginVars.BossSpawnChances[bossName][mapName].MaxValue);
            }
        }
    }
}