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
        private static Dictionary<int, bool> keybindStates = new Dictionary<int, bool>();
        private static Dictionary<int, KeyCode> newKeybinds = new Dictionary<int, KeyCode>();
        private static bool isSettingKeybind = false; // Flag to indicate if setting a keybind

        private static GUIStyle dropdownStyle;
        private static GUIStyle dropdownButtonStyle;
        private static GUIStyle toggleStyle;
        private static GUIStyle accordionButtonStyle;
        private static GUIStyle tooltipStyle;
        private static GUIStyle textFieldStyle;
        private static GUIStyle expandedDropdownStyle;
        private static GUIStyle keybindFieldStyle;

        private static GUIStyle sliderThumbStyle;
        private static GUIStyle sliderStyle;

        public static void InitializeStyles()
        {
            dropdownStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 25,
                fontSize = 18
            };

            dropdownButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 25,
                fontSize = 18
            };

            toggleStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            accordionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 30,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 18,
                normal = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 1f)) }
            };

            tooltipStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                wordWrap = true,

                normal = { background = MakeTex(1, 1, new Color(0.0f, 0.5f, 1.0f)), textColor = Color.white }, // vibrant blue background with white text
                fontStyle = FontStyle.Bold

            };

            expandedDropdownStyle = new GUIStyle(dropdownStyle)
            {
                normal = { background = MakeTex(1, 1, Color.blue) }
            };

            keybindFieldStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 25,
                fontSize = 18,
                normal = { textColor = Color.white }
            };

            sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                normal = { background = MakeTex(1, 1, Color.blue) }

            };

            // Create textures for the toggle button states
            CreateToggleButtonTextures();
        }

        internal static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private static void CreateToggleButtonTextures()
        {
            toggleStyle.normal.background = MakeTex(1, 1, Color.gray);
            toggleStyle.hover.background = MakeTex(1, 1, Color.gray);
            toggleStyle.active.background = MakeTex(1, 1, Color.gray);

            toggleStyle.onNormal.background = MakeTex(1, 1, Color.red);
            toggleStyle.onHover.background = MakeTex(1, 1, Color.red);
            toggleStyle.onActive.background = MakeTex(1, 1, Color.red);
        }

        internal static int Dropdown<T>(Setting<T> setting, int selectedIndex)
        {
            // Check if the Options list is properly initialized and log error if needed
            if (setting.LogErrorOnceIfOptionsInvalid())
            {
                return selectedIndex; // Return the current index without drawing the button
            }

            // Ensure selectedIndex is within bounds
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

            // Draw label with tooltip
            GUIContent labelContent = new GUIContent(setting.Name, setting.ToolTipText);
            GUILayout.Label(labelContent, GUILayout.Width(200));

            // Choose style based on dropdown state
            GUIStyle currentDropdownStyle = dropdownStates[dropdownId] ? expandedDropdownStyle : dropdownStyle;

            // Draw button with tooltip
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
                    if (GUILayout.Button(optionContent, dropdownButtonStyle, GUILayout.Width(300)))
                    {
                        selectedIndex = i;
                        setting.Value = setting.Options[i];
                        dropdownStates[dropdownId] = false;
                    }
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            // Use the centralized ShowTooltip method
            ShowTooltip();

            return selectedIndex;
        }
        public static float Slider(string label, string toolTip, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();

            // Draw the label
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));

            value = GUILayout.HorizontalSlider(value, min, max, sliderStyle, sliderThumbStyle, GUILayout.Width(300));

            string valueStr = GUILayout.TextField(value.ToString("F2"), textFieldStyle, GUILayout.Width(100));


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

            // Draw the label
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));

            value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, sliderStyle, sliderThumbStyle, GUILayout.Width(300)));

            // Draw the textbox next to the slider without extra vertical space
            string valueStr = GUILayout.TextField(value.ToString(), textFieldStyle, GUILayout.Width(100));


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
            text = GUILayout.TextField(text, textFieldStyle, GUILayout.Width(300));
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

            // Apply the custom toggle style
            GUIContent toggleContent = new GUIContent(value ? "YES" : "NO", toolTip);
            bool newValue = GUILayout.Toggle(value, toggleContent, toggleStyle, GUILayout.Width(150), GUILayout.Height(35));

            GUILayout.EndHorizontal();

            ShowTooltip();

            return newValue;
        }

        public static bool Button(string label, string toolTip, GUIStyle style = null)
        {
            style ??= GUI.skin.button;

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
            if (GUILayout.Button(buttonContent, accordionButtonStyle))
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
                GUILayout.Button(waitingContent, keybindFieldStyle, GUILayout.Width(200));
                isSettingKeybind = true; // Indicate that we are setting a keybind
            }
            else
            {
                GUIContent keyContent = new GUIContent(currentKey.ToString(), toolTip);
                if (GUILayout.Button(keyContent, keybindFieldStyle, GUILayout.Width(200)))
                {
                    keybindStates[keybindId] = true;
                    isSettingKeybind = true; // Indicate that we are setting a keybind
                }
            }

            // Add a "Clear" button
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
                    currentKey = e.keyCode; // Update the current key

                    //delay 1 second non blocking
                    System.Threading.Tasks.Task.Delay(1000).ContinueWith(t => isSettingKeybind = false);
                }
            }

            ShowTooltip();

            return currentKey; // Return the updated keybind
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
                Vector2 size = tooltipStyle.CalcSize(new GUIContent(GUI.tooltip));
                size.y = tooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), size.x);
                Rect tooltipRect = new Rect(mousePosition.x, mousePosition.y - size.y, size.x, size.y);
                GUI.Box(tooltipRect, GUI.tooltip, tooltipStyle);
            }
        }

        //used for finding the correct selection on dropdowns
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
