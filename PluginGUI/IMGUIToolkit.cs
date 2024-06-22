using System.Collections.Generic;
using System.Reflection;
using Donuts.Models;
using EFT.Visual;
using HarmonyLib;
using UnityEngine;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    public class ImGUIToolkit
    {
        private static Dictionary<int, bool> dropdownStates = new Dictionary<int, bool>();
        private static Dictionary<int, bool> accordionStates = new Dictionary<int, bool>();
        private static Dictionary<int, KeyCode> newKeybinds = new Dictionary<int, KeyCode>();
        private static Dictionary<int, bool> keybindStates = new Dictionary<int, bool>();
        private static bool isSettingKeybind = false; // Flag to indicate if setting a keybind

        private static bool stylesInitialized = false;

        private static void EnsureStylesInitialized()
        {
            if (!stylesInitialized)
            {
                PluginGUIHelper.InitializeStyles();
                stylesInitialized = true;
            }
        }

        internal static int Dropdown<T>(Setting<T> setting, int selectedIndex)
        {
            EnsureStylesInitialized();

            if (setting.LogErrorOnceIfOptionsInvalid())
            {
                return selectedIndex;
            }

            if (selectedIndex >= setting.Options.Length)
            {
                selectedIndex = 0;
            }

            int dropdownId = GUIUtility.GetControlID(FocusType.Passive);

            if (!dropdownStates.ContainsKey(dropdownId))
            {
                dropdownStates[dropdownId] = false;
            }

            GUILayout.BeginHorizontal();

            GUIContent labelContent = new GUIContent(setting.Name, setting.ToolTipText);
            GUILayout.Label(labelContent, PluginGUIHelper.labelStyle, GUILayout.Width(200));

            GUIStyle currentDropdownStyle = dropdownStates[dropdownId] ? PluginGUIHelper.subTabButtonActiveStyle : PluginGUIHelper.subTabButtonStyle;

            GUIContent buttonContent = new GUIContent(setting.Options[selectedIndex]?.ToString(), setting.ToolTipText);
            if (GUILayout.Button(buttonContent, currentDropdownStyle, GUILayout.Width(300)))
            {
                dropdownStates[dropdownId] = !dropdownStates[dropdownId];
            }

            GUILayout.EndHorizontal();

            if (dropdownStates[dropdownId])
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(209);

                GUILayout.BeginVertical();

                for (int i = 0; i < setting.Options.Length; i++)
                {
                    GUIContent optionContent = new GUIContent(setting.Options[i]?.ToString(), setting.ToolTipText);
                    if (GUILayout.Button(optionContent, PluginGUIHelper.subTabButtonStyle, GUILayout.Width(300)))
                    {
                        selectedIndex = i;
                        setting.Value = setting.Options[i];
                        dropdownStates[dropdownId] = false;
                    }
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            ShowTooltip();

            return selectedIndex;
        }

        public static float Slider(string label, string toolTip, float value, float min, float max)
        {
            EnsureStylesInitialized();

            GUILayout.BeginHorizontal();

            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, PluginGUIHelper.labelStyle, GUILayout.Width(200));

            value = GUILayout.HorizontalSlider(value, min, max, PluginGUIHelper.horizontalSliderStyle, PluginGUIHelper.horizontalSliderThumbStyle, GUILayout.Width(300));

            string textFieldValue = GUILayout.TextField(value.ToString("F2"), PluginGUIHelper.textFieldStyle, GUILayout.Width(100));
            if (float.TryParse(textFieldValue, out float newValue))
            {
                value = Mathf.Clamp(newValue, min, max);
            }

            GUILayout.EndHorizontal();
            ShowTooltip();

            return value;
        }

        public static int Slider(string label, string toolTip, int value, int min, int max)
        {
            EnsureStylesInitialized();

            GUILayout.BeginHorizontal();

            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, PluginGUIHelper.labelStyle, GUILayout.Width(200));

            value = (int)GUILayout.HorizontalSlider(value, min, max, PluginGUIHelper.horizontalSliderStyle, PluginGUIHelper.horizontalSliderThumbStyle, GUILayout.Width(300));

            string textFieldValue = GUILayout.TextField(value.ToString(), PluginGUIHelper.textFieldStyle, GUILayout.Width(100));
            if (int.TryParse(textFieldValue, out int newValue))
            {
                value = Mathf.Clamp(newValue, min, max);
            }

            GUILayout.EndHorizontal();
            ShowTooltip();

            return value;
        }

        public static string TextField(string label, string toolTip, string text, int maxLength = 50)
        {
            // Create GUIContent for the label with tooltip
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelContent, PluginGUIHelper.labelStyle, GUILayout.Width(200));
            string newText = GUILayout.TextField(text, maxLength, PluginGUIHelper.textFieldStyle, GUILayout.Width(300));

            // Ensure the text does not exceed the maximum length
            if (newText.Length > maxLength)
            {
                newText = newText.Substring(0, maxLength);
            }

            GUILayout.EndHorizontal();

            return newText;
        }

        public static bool Toggle(string label, string toolTip, bool value)
        {
            EnsureStylesInitialized();

            GUILayout.BeginHorizontal();
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, PluginGUIHelper.labelStyle, GUILayout.Width(200));
            GUILayout.Space(10);

            GUIContent toggleContent = new GUIContent(value ? "YES" : "NO", toolTip);
            bool newValue = GUILayout.Toggle(value, toggleContent, PluginGUIHelper.toggleButtonStyle, GUILayout.Width(150), GUILayout.Height(35));

            GUILayout.EndHorizontal();

            ShowTooltip();

            return newValue;
        }

        public static bool Button(string label, string toolTip, GUIStyle style = null)
        {
            EnsureStylesInitialized();

            style ??= PluginGUIHelper.buttonStyle;

            GUIContent buttonContent = new GUIContent(label, toolTip);
            bool result = GUILayout.Button(buttonContent, style, GUILayout.Width(200));

            ShowTooltip();

            return result;
        }

        public static void Accordion(string label, string toolTip, System.Action drawContents)
        {
            EnsureStylesInitialized();

            int accordionId = GUIUtility.GetControlID(FocusType.Passive);

            if (!accordionStates.ContainsKey(accordionId))
            {
                accordionStates[accordionId] = false;
            }

            GUIContent buttonContent = new GUIContent(label, toolTip);
            if (GUILayout.Button(buttonContent, PluginGUIHelper.buttonStyle))
            {
                accordionStates[accordionId] = !accordionStates[accordionId];
            }

            if (accordionStates[accordionId])
            {
                GUILayout.BeginVertical(GUI.skin.box);
                drawContents();
                GUILayout.EndVertical();
            }

            ShowTooltip();
        }

        public static KeyCode KeybindField(string label, string toolTip, KeyCode currentKey)
        {
            EnsureStylesInitialized();

            int keybindId = GUIUtility.GetControlID(FocusType.Passive);

            if (!keybindStates.ContainsKey(keybindId))
            {
                keybindStates[keybindId] = false;
                newKeybinds[keybindId] = currentKey;
            }

            GUILayout.BeginHorizontal();
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, PluginGUIHelper.labelStyle, GUILayout.Width(200));
            GUILayout.Space(10);

            if (keybindStates[keybindId])
            {
                GUIContent waitingContent = new GUIContent("Press any key...", toolTip);
                GUILayout.Button(waitingContent, PluginGUIHelper.buttonStyle, GUILayout.Width(200));
                isSettingKeybind = true;
            }
            else
            {
                GUIContent keyContent = new GUIContent(currentKey.ToString(), toolTip);
                if (GUILayout.Button(keyContent, PluginGUIHelper.buttonStyle, GUILayout.Width(200)))
                {
                    keybindStates[keybindId] = true;
                    isSettingKeybind = true;
                }
            }

            if (GUILayout.Button("Clear", GUILayout.Width(90)))
            {
                currentKey = KeyCode.None;
            }

            GUILayout.EndHorizontal();

            if (keybindStates[keybindId])
            {
                Event e = Event.current;
                if (e.isKey)
                {
                    newKeybinds[keybindId] = e.keyCode;
                    keybindStates[keybindId] = false;
                    currentKey = e.keyCode;
                    System.Threading.Tasks.Task.Delay(1000).ContinueWith(t => isSettingKeybind = false);
                }
            }

            ShowTooltip();

            return currentKey;
        }

        public static bool IsSettingKeybind()
        {
            return isSettingKeybind;
        }

        private static void ShowTooltip()
        {
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                Vector2 mousePosition = Event.current.mousePosition;
                Vector2 size = PluginGUIHelper.tooltipStyle.CalcSize(new GUIContent(GUI.tooltip));
                size.y = PluginGUIHelper.tooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), size.x);
                Rect tooltipRect = new Rect(mousePosition.x, mousePosition.y - size.y, size.x, size.y);
                GUI.Box(tooltipRect, GUI.tooltip, PluginGUIHelper.tooltipStyle);
            }
        }

        internal static int FindIndex<T>(Setting<T> setting)
        {
            if (setting == null)
            {
                DonutsPlugin.Logger.LogError("Setting is null.");
                return -1;
            }

            for (int i = 0; i < setting.Options.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(setting.Options[i], setting.Value))
                {
                    return i;
                }
            }
            DonutsPlugin.Logger.LogError($"Value '{setting.Value}' not found in Options for setting '{setting.Name}'");
            return -1;
        }
    }
}
