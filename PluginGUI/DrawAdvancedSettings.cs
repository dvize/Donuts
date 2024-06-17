using UnityEngine;
using static Donuts.PluginGUIHelper;
using static Donuts.DefaultPluginVars;
using Donuts.Models;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace Donuts
{
    internal class DrawAdvancedSettings
    {
        internal static void Enable()
        {

            GUILayout.Space(30);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            // Slider for replenishInterval
            replenishInterval.Value = ImGUIToolkit.Slider(
                replenishInterval.Name,
                replenishInterval.ToolTipText,
                replenishInterval.Value,
                replenishInterval.MinValue,
                replenishInterval.MaxValue
            );

            // Slider for maxSpawnTriesPerBot
            maxSpawnTriesPerBot.Value = ImGUIToolkit.Slider(
                maxSpawnTriesPerBot.Name,
                maxSpawnTriesPerBot.ToolTipText,
                maxSpawnTriesPerBot.Value,
                maxSpawnTriesPerBot.MinValue,
                maxSpawnTriesPerBot.MaxValue
            );

            // Slider for despawnInterval
            despawnInterval.Value = ImGUIToolkit.Slider(
                despawnInterval.Name,
                despawnInterval.ToolTipText,
                despawnInterval.Value,
                despawnInterval.MinValue,
                despawnInterval.MaxValue
            );

            GUILayout.Space(150);

            // Reset to Default Values button
            GUIStyle redButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { background = MakeTex(1, 1, new Color(0.5f, 0.0f, 0.0f)), textColor = Color.white },
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            if (GUILayout.Button("Reset to Default Values", redButtonStyle, GUILayout.Width(250), GUILayout.Height(50)))
            {
                ResetToDefaults();
                DonutsPlugin.Logger.LogWarning("All settings have been reset to default values.");
                RestartPluginGUIHelper();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            
        }

        public static void ResetToDefaults()
        {
            foreach (var field in typeof(DefaultPluginVars).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Setting<>))
                {
                    var settingValue = field.GetValue(null);
                    var valueProperty = settingValue.GetType().GetProperty("Value");
                    var defaultValueProperty = settingValue.GetType().GetProperty("DefaultValue");

                    var defaultValue = defaultValueProperty.GetValue(settingValue);
                    valueProperty.SetValue(settingValue, defaultValue);
                }
            }

            // Reset dropdown indices
            DrawMainSettings.InitializeDropdownIndices();

            // Reset dropdown indices for spawn point maker settings
            DrawSpawnPointMaker.InitializeDropdownIndices();
        }

        private static void RestartPluginGUIHelper()
        {
            if (DonutsPlugin.pluginGUIHelper != null)
            {
                DonutsPlugin.pluginGUIHelper.enabled = false;
                DonutsPlugin.pluginGUIHelper.enabled = true;
            }
        }
    }
}
