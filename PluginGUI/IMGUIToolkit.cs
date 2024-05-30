using System.Collections.Generic;
using Donuts.Models;
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

        internal static int Dropdown<T>(Setting<T> setting, int selectedIndex)
        {
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
            GUILayout.Label(labelContent, GUILayout.Width(200));

            GUIStyle currentDropdownStyle = dropdownStates[dropdownId] ? PluginGUIHelper.customSkin.customStyles[5] : PluginGUIHelper.customSkin.customStyles[6];

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
                    if (GUILayout.Button(optionContent, PluginGUIHelper.customSkin.customStyles[5], GUILayout.Width(300)))
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
            GUILayout.BeginHorizontal();

            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));

            value = GUILayout.HorizontalSlider(value, min, max, PluginGUIHelper.customSkin.customStyles[10], PluginGUIHelper.customSkin.customStyles[11], GUILayout.Width(300));

            string valueStr = GUILayout.TextField(value.ToString("F2"), PluginGUIHelper.customSkin.customStyles[9], GUILayout.Width(100));

            if (float.TryParse(valueStr, out float parsedValue))
            {
                value = Mathf.Clamp(parsedValue, min, max);
            }

            GUILayout.EndHorizontal();
            ShowTooltip();

            return value;
        }

        public static int Slider(string label, string toolTip, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();

            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));

            value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, PluginGUIHelper.customSkin.customStyles[10], PluginGUIHelper.customSkin.customStyles[11], GUILayout.Width(300)));

            string valueStr = GUILayout.TextField(value.ToString(), PluginGUIHelper.customSkin.customStyles[9], GUILayout.Width(100));

            if (int.TryParse(valueStr, out int parsedValue))
            {
                value = Mathf.Clamp(parsedValue, min, max);
            }

            GUILayout.EndHorizontal();
            ShowTooltip();

            return value;
        }

        public static string TextField(string label, string toolTip, string text)
        {
            GUILayout.BeginHorizontal();
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            text = GUILayout.TextField(text, PluginGUIHelper.customSkin.customStyles[9], GUILayout.Width(300));
            GUILayout.EndHorizontal();

            ShowTooltip();

            return text;
        }

        public static bool Toggle(string label, string toolTip, bool value)
        {
            GUILayout.BeginHorizontal();
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));
            GUILayout.Space(10);

            GUIContent toggleContent = new GUIContent(value ? "YES" : "NO", toolTip);
            bool newValue = GUILayout.Toggle(value, toggleContent, PluginGUIHelper.customSkin.customStyles[7], GUILayout.Width(150), GUILayout.Height(35));

            GUILayout.EndHorizontal();

            ShowTooltip();

            return newValue;
        }

        public static bool Button(string label, string toolTip, GUIStyle style = null)
        {
            style ??= PluginGUIHelper.customSkin.button;

            GUIContent buttonContent = new GUIContent(label, toolTip);
            bool result = GUILayout.Button(buttonContent, style, GUILayout.Width(200));

            ShowTooltip();

            return result;
        }

        public static void Accordion(string label, string toolTip, System.Action drawContents)
        {
            int accordionId = GUIUtility.GetControlID(FocusType.Passive);

            if (!accordionStates.ContainsKey(accordionId))
            {
                accordionStates[accordionId] = false;
            }

            GUIContent buttonContent = new GUIContent(label, toolTip);
            if (GUILayout.Button(buttonContent, PluginGUIHelper.customSkin.customStyles[8]))
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
            int keybindId = GUIUtility.GetControlID(FocusType.Passive);

            if (!keybindStates.ContainsKey(keybindId))
            {
                keybindStates[keybindId] = false;
                newKeybinds[keybindId] = currentKey;
            }

            GUILayout.BeginHorizontal();
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));
            GUILayout.Space(10);

            if (keybindStates[keybindId])
            {
                GUIContent waitingContent = new GUIContent("Press any key...", toolTip);
                GUILayout.Button(waitingContent, PluginGUIHelper.customSkin.customStyles[8], GUILayout.Width(200));
                isSettingKeybind = true;
            }
            else
            {
                GUIContent keyContent = new GUIContent(currentKey.ToString(), toolTip);
                if (GUILayout.Button(keyContent, PluginGUIHelper.customSkin.customStyles[8], GUILayout.Width(200)))
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
                Vector2 size = PluginGUIHelper.customSkin.customStyles[4].CalcSize(new GUIContent(GUI.tooltip));
                size.y = PluginGUIHelper.customSkin.customStyles[4].CalcHeight(new GUIContent(GUI.tooltip), size.x);
                Rect tooltipRect = new Rect(mousePosition.x, mousePosition.y - size.y, size.x, size.y);
                GUI.Box(tooltipRect, GUI.tooltip, PluginGUIHelper.customSkin.customStyles[4]);
            }
        }

        internal static int FindIndex<T>(Setting<T> setting)
        {
            for (int i = 0; i < setting.Options.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(setting.Options[i], setting.Value))
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
