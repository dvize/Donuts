using System.Collections.Generic;
using UnityEngine;
using Donuts.Models;
using static Donuts.PluginGUIHelper;

namespace Donuts
{
    internal class DrawMainSettings
    {
        private static Setting<string> setting1;
        private static Setting<string> setting2;
        private static Setting<float> sliderSetting;
        private static Setting<int> intSliderSetting;
        private static Setting<string> textFieldSetting;
        private static Setting<bool> toggleSetting;

        static DrawMainSettings()
        {
            setting1 = new Setting<string>("Select Option 1:", "Choose an option", "Option 1", "Option 1", options: new List<string> { "Option 1", "Option 2", "Option 3" });
            setting2 = new Setting<string>("Select Option 2:", "Choose an option", "Option 1", "Option 1", options: new List<string> { "Option 1", "Option 2", "Option 3" });
            sliderSetting = new Setting<float>("Adjust Value:", "Adjust the float value", 0.5f, 0.5f, 0f, 1f);
            intSliderSetting = new Setting<int>("Adjust Int Value:", "Adjust the int value", 5, 5, 0, 10);
            textFieldSetting = new Setting<string>("Enter Text:", "Enter some text", "Hello, World!", "Hello, World!");
            toggleSetting = new Setting<bool>("Enable Feature:", "Toggle the feature", true, true);
        }

        internal static void Enable()
        {
            // Apply the cached styles to ensure consistency
            PluginGUIHelper.ApplyCachedStyles();

            // Initialize the custom styles for the dropdown
            ImGuiToolkit.InitializeStyles();

            // Draw content for Main Settings
            GUILayout.Label("Main Settings", cachedSubTabLabelStyle);

            // Use the ImGuiToolkit for drawing controls
            setting1.Value = setting1.Options[ImGuiToolkit.Dropdown(setting1, setting1.Options.IndexOf(setting1.Value))];
            setting2.Value = setting2.Options[ImGuiToolkit.Dropdown(setting2, setting2.Options.IndexOf(setting2.Value))];
            sliderSetting.Value = ImGuiToolkit.Slider(sliderSetting.Name, sliderSetting.Value, sliderSetting.MinValue, sliderSetting.MaxValue);
            intSliderSetting.Value = ImGuiToolkit.Slider(intSliderSetting.Name, intSliderSetting.Value, intSliderSetting.MinValue, intSliderSetting.MaxValue);
            textFieldSetting.Value = ImGuiToolkit.TextField(textFieldSetting.Name, textFieldSetting.Value);
            toggleSetting.Value = ImGuiToolkit.Toggle(toggleSetting.Name, toggleSetting.Value);

            // Example of using a custom style for a button
            GUIStyle customButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { textColor = Color.green },
                fontStyle = FontStyle.Bold
            };

            if (ImGuiToolkit.Button("Apply", customButtonStyle))
            {
                Debug.Log("Settings Applied");
            }
        }
    }
}
