using System;
using System.Collections.Generic;
using Donuts.Models;
using UnityEngine;
using static Donuts.DefaultPluginVars;

namespace Donuts
{
    internal class DrawSpawnSettings
    {
        internal static void Enable()
        {
            // Apply the custom skin to ensure consistency
            PluginGUIHelper.ApplyCustomSkin(() =>
            {

                GUILayout.Space(30);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();

                ImGUIToolkit.Accordion("Global Min Distance To Player Settings", "Click to expand/collapse", () =>
                {
                    // Toggle for globalMinSpawnDistanceFromPlayerBool
                    globalMinSpawnDistanceFromPlayerBool.Value = ImGUIToolkit.Toggle(
                        globalMinSpawnDistanceFromPlayerBool.Name,
                        globalMinSpawnDistanceFromPlayerBool.ToolTipText,
                        globalMinSpawnDistanceFromPlayerBool.Value
                    );

                    // List of float settings
                    var floatSettings = new List<Setting<float>>
                    {
                    globalMinSpawnDistanceFromPlayerFactory,
                    globalMinSpawnDistanceFromPlayerCustoms,
                    globalMinSpawnDistanceFromPlayerReserve,
                    globalMinSpawnDistanceFromPlayerStreets,
                    globalMinSpawnDistanceFromPlayerWoods,
                    globalMinSpawnDistanceFromPlayerLaboratory,
                    globalMinSpawnDistanceFromPlayerShoreline,
                    globalMinSpawnDistanceFromPlayerGroundZero,
                    globalMinSpawnDistanceFromPlayerInterchange,
                    globalMinSpawnDistanceFromPlayerLighthouse
                    };

                    // Sort the settings by name in ascending order
                    floatSettings.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

                    // Create sliders for the sorted settings
                    foreach (var setting in floatSettings)
                    {
                        setting.Value = ImGUIToolkit.Slider(
                            setting.Name,
                            setting.ToolTipText,
                            setting.Value,
                            0f,
                            1000f
                        );
                    }
                });

            });

            ImGUIToolkit.Accordion("Global Min Distance To Other Bots Settings", "Click to expand/collapse", () =>
            {
                // Toggle for globalMinSpawnDistanceFromOtherBotsBool
                globalMinSpawnDistanceFromOtherBotsBool.Value = ImGUIToolkit.Toggle(
                    globalMinSpawnDistanceFromOtherBotsBool.Name,
                    globalMinSpawnDistanceFromOtherBotsBool.ToolTipText,
                    globalMinSpawnDistanceFromOtherBotsBool.Value
                );

                // List of float settings for other bots
                var otherBotsFloatSettings = new List<Setting<float>>
                {
                    globalMinSpawnDistanceFromOtherBotsFactory,
                    globalMinSpawnDistanceFromOtherBotsCustoms,
                    globalMinSpawnDistanceFromOtherBotsReserve,
                    globalMinSpawnDistanceFromOtherBotsStreets,
                    globalMinSpawnDistanceFromOtherBotsWoods,
                    globalMinSpawnDistanceFromOtherBotsLaboratory,
                    globalMinSpawnDistanceFromOtherBotsShoreline,
                    globalMinSpawnDistanceFromOtherBotsGroundZero,
                    globalMinSpawnDistanceFromOtherBotsInterchange,
                    globalMinSpawnDistanceFromOtherBotsLighthouse
                };

                // Sort the settings by name in ascending order
                otherBotsFloatSettings.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

                // Create sliders for the sorted settings
                foreach (var setting in otherBotsFloatSettings)
                {
                    setting.Value = ImGUIToolkit.Slider(
                        setting.Name,
                        setting.ToolTipText,
                        setting.Value,
                        0f,
                        1000f
                    );
                }
            });

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }




    }
}
