using UnityEngine;
using System.Collections.Generic;
using Donuts.Models;

namespace Donuts
{
    public class ImGuiToolkit
    {
        private static Dictionary<int, bool> dropdownStates = new Dictionary<int, bool>();
        private static GUIStyle dropdownStyle;
        private static GUIStyle dropdownButtonStyle;
        private static GUIStyle toggleStyle;

        public static void InitializeStyles()
        {
            dropdownStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 25
            };

            dropdownButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 25
            };

            toggleStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            // Create textures for the toggle button states
            CreateToggleButtonTextures();
        }

        private static Texture2D MakeTex(int width, int height, Color col)
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

        /// <summary>
        /// Creates a dropdown menu.
        /// </summary>
        /// <param name="label">Label for the dropdown.</param>
        /// <param name="options">List of options.</param>
        /// <param name="selectedIndex">Index of the currently selected option.</param>
        /// <returns>Index of the selected option.</returns>
        internal static int Dropdown<T>(Setting<T> setting, int selectedIndex)
        {
            int dropdownId = GUIUtility.GetControlID(FocusType.Passive);

            if (!dropdownStates.ContainsKey(dropdownId))
            {
                dropdownStates[dropdownId] = false;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(setting.Name, GUILayout.Width(100));

            if (GUILayout.Button(setting.Options[selectedIndex]?.ToString(), dropdownStyle, GUILayout.Width(200)))
            {
                dropdownStates[dropdownId] = !dropdownStates[dropdownId];
            }

            GUILayout.EndHorizontal();

            if (dropdownStates[dropdownId])
            {
                for (int i = 0; i < setting.Options.Count; i++)
                {
                    if (GUILayout.Button(setting.Options[i]?.ToString(), dropdownButtonStyle, GUILayout.Width(200)))
                    {
                        selectedIndex = i;
                        setting.Value = setting.Options[i];
                        dropdownStates[dropdownId] = false;
                    }
                }
            }

            return selectedIndex;
        }

        /// <summary>
        /// Creates a slider.
        /// </summary>
        /// <param name="label">Label for the slider.</param>
        /// <param name="value">Current value of the slider.</param>
        /// <param name="min">Minimum value.</param>
        /// <param name="max">Maximum value.</param>
        /// <returns>New value of the slider.</returns>
        public static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(200));
            GUILayout.Label(value.ToString("F2"), GUILayout.Width(50));
            GUILayout.EndHorizontal();

            return value;
        }

        /// <summary>
        /// Creates a slider for integer values.
        /// </summary>
        /// <param name="label">Label for the slider.</param>
        /// <param name="value">Current value of the slider.</param>
        /// <param name="min">Minimum value.</param>
        /// <param name="max">Maximum value.</param>
        /// <returns>New value of the slider.</returns>
        public static int Slider(string label, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));
            value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(200)));
            GUILayout.Label(value.ToString(), GUILayout.Width(50));
            GUILayout.EndHorizontal();

            return value;
        }

        /// <summary>
        /// Creates a text entry field.
        /// </summary>
        /// <param name="label">Label for the text entry field.</param>
        /// <param name="text">Current text.</param>
        /// <returns>New text.</returns>
        public static string TextField(string label, string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));
            text = GUILayout.TextField(text, GUILayout.Width(250));
            GUILayout.EndHorizontal();

            return text;
        }

        /// <summary>
        /// Creates a custom toggle.
        /// </summary>
        /// <param name="label">Label for the toggle.</param>
        /// <param name="value">Current value of the toggle.</param>
        /// <returns>New value of the toggle.</returns>
        public static bool Toggle(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));

            bool newValue = GUILayout.Toggle(value, value ? "YES" : "NO", toggleStyle, GUILayout.Width(100), GUILayout.Height(25));

            GUILayout.EndHorizontal();

            return newValue;
        }

        /// <summary>
        /// Creates a button.
        /// </summary>
        /// <param name="label">Label for the button.</param>
        /// <param name="style">Optional style for the button.</param>
        /// <returns>True if the button is clicked, false otherwise.</returns>
        public static bool Button(string label, GUIStyle style = null)
        {
            if (style == null)
            {
                style = GUI.skin.button;
            }
            return GUILayout.Button(label, style, GUILayout.Width(100));
        }
    }
}
