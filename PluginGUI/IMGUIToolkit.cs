using UnityEngine;
using System.Collections.Generic;
using Donuts.Models;

namespace Donuts
{
    public class ImGUIToolkit
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

        internal static int Dropdown<T>(Setting<T> setting, int selectedIndex)
        {
            // Check if the Options list is properly initialized and log error if needed
            if (setting.LogErrorOnceIfOptionsInvalid())
            {
                return selectedIndex;
            }

            int dropdownId = GUIUtility.GetControlID(FocusType.Passive);

            if (!dropdownStates.ContainsKey(dropdownId))
            {
                dropdownStates[dropdownId] = false;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(setting.Name, GUILayout.Width(150)); 

            if (GUILayout.Button(setting.Options[selectedIndex]?.ToString(), dropdownStyle, GUILayout.Width(200)))
            {
                dropdownStates[dropdownId] = !dropdownStates[dropdownId];
            }

            GUILayout.EndHorizontal();

            if (dropdownStates[dropdownId])
            {
                for (int i = 0; i < setting.Options.Length; i++)
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
        

        public static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150));
            GUILayout.Space(10); // Add space
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(200));

            string valueStr = value.ToString("F2");
            valueStr = GUILayout.TextField(valueStr, GUILayout.Width(50));

            if (float.TryParse(valueStr, out float parsedValue))
            {
                value = Mathf.Clamp(parsedValue, min, max);
            }

            GUILayout.EndHorizontal();

            return value;
        }

        public static int Slider(string label, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150)); 
            GUILayout.Space(10); // Add space
            value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(200)));

            string valueStr = value.ToString();
            valueStr = GUILayout.TextField(valueStr, GUILayout.Width(50));

            if (int.TryParse(valueStr, out int parsedValue))
            {
                value = Mathf.Clamp(parsedValue, min, max);
            }

            GUILayout.EndHorizontal();

            return value;
        }

        public static string TextField(string label, string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150));
            GUILayout.Space(10); // Add space
            text = GUILayout.TextField(text, GUILayout.Width(250));
            GUILayout.EndHorizontal();

            return text;
        }

        public static bool Toggle(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150)); 
            GUILayout.Space(10); // Add space

            bool newValue = GUILayout.Toggle(value, value ? "YES" : "NO", toggleStyle, GUILayout.Width(100), GUILayout.Height(25));

            GUILayout.EndHorizontal();

            return newValue;
        }

        public static bool Button(string label, GUIStyle style = null)
        {
            if (style == null)
            {
                style = GUI.skin.button;
            }
            return GUILayout.Button(label, style, GUILayout.Width(150)); 
        }
    }
}
