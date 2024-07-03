﻿using System.IO;
using System.Reflection;
using UnityEngine;

namespace Donuts
{
    public class PluginGUIHelper : MonoBehaviour
    {
        internal static Rect windowRect = new Rect(20, 20, 1664, 936);
        private bool isDragging = false;
        private Vector2 dragOffset;
        private Vector2 scrollPosition = Vector2.zero;

        private const float ResizeHandleSize = 30f;
        private bool isResizing = false;
        private Vector2 resizeStartPos;

        internal static GUIStyle windowStyle;
        internal static GUIStyle labelStyle;
        internal static GUIStyle buttonStyle;
        internal static GUIStyle activeButtonStyle;
        internal static GUIStyle subTabButtonStyle;
        internal static GUIStyle subTabButtonActiveStyle;
        internal static GUIStyle textFieldStyle;
        internal static GUIStyle horizontalSliderStyle;
        internal static GUIStyle horizontalSliderThumbStyle;
        internal static GUIStyle toggleButtonStyle;
        internal static GUIStyle tooltipStyle;
        private static bool stylesInitialized = false;

        private void Start()
        {
            LoadWindowSettings();
        }

        private void OnGUI()
        {
            if (DefaultPluginVars.showGUI)
            {
                if (!stylesInitialized)
                {
                    InitializeStyles();
                    stylesInitialized = true;
                }

                // Save the current GUI skin
                GUISkin originalSkin = GUI.skin;

                windowRect = GUI.Window(123, windowRect, MainWindowFunc, "Donuts Configuration", windowStyle);
                GUI.FocusWindow(123);

                if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseDrag)
                {
                    if (windowRect.Contains(Event.current.mousePosition))
                    {
                        Event.current.Use();
                    }
                }

                // Restore the original GUI skin
                GUI.skin = originalSkin;
            }
        }

        public static void InitializeStyles()
        {
            var windowBackgroundTex = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.1f, 1f));
            var buttonNormalTex = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 1f));
            var buttonHoverTex = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.3f, 1f));
            var buttonActiveTex = MakeTex(1, 1, new Color(0.4f, 0.4f, 0.4f, 1f));
            var subTabNormalTex = MakeTex(1, 1, new Color(0.15f, 0.15f, 0.15f, 1f));
            var subTabHoverTex = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.5f, 1f));
            var subTabActiveTex = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.7f, 1f));
            var toggleEnabledTex = MakeTex(1, 1, Color.red);
            var toggleDisabledTex = MakeTex(1, 1, Color.gray);

            windowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = windowBackgroundTex, textColor = Color.white },
                focused = { background = windowBackgroundTex, textColor = Color.white },
                active = { background = windowBackgroundTex, textColor = Color.white },
                hover = { background = windowBackgroundTex, textColor = Color.white },
                onNormal = { background = windowBackgroundTex, textColor = Color.white },
                onFocused = { background = windowBackgroundTex, textColor = Color.white },
                onActive = { background = windowBackgroundTex, textColor = Color.white },
                onHover = { background = windowBackgroundTex, textColor = Color.white },
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                fontSize = 20,
                fontStyle = FontStyle.Bold,
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = buttonNormalTex, textColor = Color.white },
                hover = { background = buttonHoverTex, textColor = Color.white },
                active = { background = buttonActiveTex, textColor = Color.white },
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            activeButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { background = buttonActiveTex, textColor = Color.yellow },
                hover = { background = buttonHoverTex, textColor = Color.yellow },
                active = { background = buttonActiveTex, textColor = Color.yellow },
            };

            subTabButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { background = subTabNormalTex, textColor = Color.white },
                hover = { background = subTabHoverTex, textColor = Color.white },
                active = { background = subTabActiveTex, textColor = Color.white },
            };

            subTabButtonActiveStyle = new GUIStyle(subTabButtonStyle)
            {
                normal = { background = subTabActiveTex, textColor = Color.yellow },
                hover = { background = subTabHoverTex, textColor = Color.yellow },
                active = { background = subTabActiveTex, textColor = Color.yellow },
            };

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 18,
                normal = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 1f)) }
            };

            horizontalSliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            horizontalSliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                normal = { background = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.7f, 1f)) }
            };

            toggleButtonStyle = new GUIStyle(GUI.skin.toggle)
            {
                normal = { background = toggleDisabledTex, textColor = Color.white },
                onNormal = { background = toggleEnabledTex, textColor = Color.white },
                hover = { background = toggleDisabledTex, textColor = Color.white },
                onHover = { background = toggleEnabledTex, textColor = Color.white },
                active = { background = toggleDisabledTex, textColor = Color.white },
                onActive = { background = toggleEnabledTex, textColor = Color.white },
                focused = { background = toggleDisabledTex, textColor = Color.white },
                onFocused = { background = toggleEnabledTex, textColor = Color.white },
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 0, 0)
            };

            tooltipStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                wordWrap = true,
                normal = { background = MakeTex(1, 1, new Color(0.0f, 0.5f, 1.0f)), textColor = Color.white },
                fontStyle = FontStyle.Bold
            };
        }

        private void Update()
        {
            if (DefaultPluginVars.showGUI)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (Input.anyKey)
                {
                    Input.ResetInputAxes();
                }
            }
        }

        private void MainWindowFunc(int windowID)
        {
            GUILayout.BeginVertical();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            DrawMainTabs();
            DrawSelectedTabContent();
            GUILayout.EndScrollView();

            DrawFooter();

            GUILayout.EndVertical();

            HandleWindowDragging();
            HandleWindowResizing();

            GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));

            SaveWindowSettings();
        }

        private void DrawMainTabs()
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < DefaultPluginVars.tabNames.Length; i++)
            {
                GUIStyle currentStyle = PluginGUIHelper.buttonStyle;
                if (DefaultPluginVars.selectedTabIndex == i)
                {
                    currentStyle = PluginGUIHelper.activeButtonStyle;
                }

                if (GUILayout.Button(DefaultPluginVars.tabNames[i], currentStyle))
                {
                    DefaultPluginVars.selectedTabIndex = i;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSelectedTabContent()
        {
            switch (DefaultPluginVars.selectedTabIndex)
            {
                case 0:
                    DrawMainSettings.Enable();
                    break;
                case 1:
                    DrawSpawnSettings.Enable();
                    break;
                case 2:
                    DrawAdvancedSettings.Enable();
                    break;
                case 3:
                    DrawSpawnPointMaker.Enable();
                    break;
                case 4:
                    DrawDebugging.Enable();
                    break;
            }
        }

        private void DrawFooter()
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUIStyle greenButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { background = MakeTex(1, 1, new Color(0.0f, 0.5f, 0.0f)), textColor = Color.white },
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            if (GUILayout.Button("Save All Changes", greenButtonStyle, GUILayout.Width(250), GUILayout.Height(50)))
            {
                ExportConfig();
                DonutsPlugin.Logger.LogWarning("All changes saved.");
            }

            GUILayout.Space(ResizeHandleSize);

            GUILayout.EndHorizontal();
        }

        private void HandleWindowDragging()
        {
            if (Event.current.type == EventType.MouseDown && new Rect(0, 0, windowRect.width, 20).Contains(Event.current.mousePosition))
            {
                isDragging = true;
                dragOffset = Event.current.mousePosition;
            }

            if (isDragging)
            {
                if (Event.current.type == EventType.MouseUp)
                {
                    isDragging = false;
                }
                else if (Event.current.type == EventType.MouseDrag)
                {
                    windowRect.position += (Vector2)Event.current.mousePosition - dragOffset;
                }
            }
        }

        private void HandleWindowResizing()
        {
            Rect resizeHandleRect = new Rect(windowRect.width - ResizeHandleSize, windowRect.height - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
            GUI.DrawTexture(resizeHandleRect, Texture2D.whiteTexture);

            if (Event.current.type == EventType.MouseDown && resizeHandleRect.Contains(Event.current.mousePosition))
            {
                isResizing = true;
                resizeStartPos = Event.current.mousePosition;
                Event.current.Use();
            }

            if (isResizing)
            {
                if (Event.current.type == EventType.MouseUp)
                {
                    isResizing = false;
                }
                else if (Event.current.type == EventType.MouseDrag)
                {
                    Vector2 delta = Event.current.mousePosition - resizeStartPos;
                    windowRect.width = Mathf.Max(300, windowRect.width + delta.x);
                    windowRect.height = Mathf.Max(200, windowRect.height + delta.y);
                    resizeStartPos = Event.current.mousePosition;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseMove)
                {
                    Event.current.Use();
                }
            }
        }

        private void SaveWindowSettings()
        {
            DefaultPluginVars.windowRect = windowRect;
            ExportConfig();
        }

        private void LoadWindowSettings()
        {
            var dllPath = Assembly.GetExecutingAssembly().Location;
            var configDirectory = Path.Combine(Path.GetDirectoryName(dllPath), "Config");
            var configFilePath = Path.Combine(configDirectory, "DefaultPluginVars.json");

            if (File.Exists(configFilePath))
            {
                var json = File.ReadAllText(configFilePath);
                DefaultPluginVars.ImportFromJson(json);
                windowRect = DefaultPluginVars.windowRect;
            }
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

        public static void ExportConfig()
        {
            var dllPath = Assembly.GetExecutingAssembly().Location;
            var configDirectory = Path.Combine(Path.GetDirectoryName(dllPath), "Config");
            var configFilePath = Path.Combine(configDirectory, "DefaultPluginVars.json");

            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            string json = DefaultPluginVars.ExportToJson();
            File.WriteAllText(configFilePath, json);
        }
    }
}
