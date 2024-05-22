using System.IO;
using System.Reflection;
using UnityEngine;

#pragma warning disable IDE0007

namespace Donuts
{
    public class PluginGUIHelper : MonoBehaviour
    {
        private Rect windowRect = new Rect(20, 20, 1664, 936);
        private bool isDragging = false;
        private Vector2 dragOffset;
        private Vector2 scrollPosition = Vector2.zero;

        private const float ResizeHandleSize = 30f;
        private bool isResizing = false;
        private Vector2 resizeStartPos;


        internal static GUIStyle cachedWindowStyle;
        internal static GUIStyle cachedLabelStyle;
        internal static GUIStyle cachedButtonStyle;
        internal static GUIStyle cachedButtonHoverStyle;
        internal static GUIStyle cachedButtonActiveStyle;
        internal static GUIStyle cachedSubTabButtonStyle;
        internal static GUIStyle cachedSubTabButtonHoverStyle;
        internal static GUIStyle cachedSubTabButtonActiveStyle;
        internal static GUIStyle cachedSubTabLabelStyle;

        private void OnGUI()
        {
            if (DefaultPluginVars.showGUI)
            {
                if (cachedWindowStyle == null)
                {
                    CacheGUIStyles();
                }

                ApplyCachedStyles();

                // Make the window resizable
                windowRect = GUI.Window(1, windowRect, MainWindowFunc, "Donuts Configuration", cachedWindowStyle);
                GUI.FocusWindow(1);
            }
        }

        private void Update()
        {
            if (DefaultPluginVars.showGUI)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void MainWindowFunc(int windowID)
        {
            GUILayout.BeginVertical();

            // Main content area with scroll view
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            DrawMainTabs();
            DrawSelectedTabContent();

            GUILayout.EndScrollView();

            // Footer section
            DrawFooter();

            GUILayout.EndVertical();

            HandleWindowDragging();
            HandleWindowResizing();

            GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));
        }


        private void DrawMainTabs()
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < DefaultPluginVars.tabNames.Length; i++)
            {
                GUIStyle currentStyle = cachedButtonStyle;
                if (DefaultPluginVars.selectedTabIndex == i)
                {
                    currentStyle = cachedButtonActiveStyle;
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

            GUIStyle greenButtonStyle = new GUIStyle(GUI.skin.button)
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

            // Check for resizing start
            if (Event.current.type == EventType.MouseDown && resizeHandleRect.Contains(Event.current.mousePosition))
            {
                isResizing = true;
                resizeStartPos = Event.current.mousePosition;
                Event.current.Use(); // Consume the event so other controls don't use it
            }

            // Handle resizing
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
                    Event.current.Use(); // Consume the event so other controls don't use it
                }
                else if (Event.current.type == EventType.MouseMove)
                {
                    Event.current.Use(); // Consume the event to keep resizing
                }
            }
        }

        internal Texture2D MakeTex(int width, int height, Color col)
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

        private void CacheGUIStyles()
        {
            var windowBackgroundTex = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.1f, 1f));
            var buttonNormalTex = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 1f));
            var buttonHoverTex = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.3f, 1f));
            var buttonActiveTex = MakeTex(1, 1, new Color(0.4f, 0.4f, 0.4f, 1f));
            var subTabNormalTex = MakeTex(1, 1, new Color(0.15f, 0.15f, 0.15f, 1f));
            var subTabHoverTex = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.5f, 1f));
            var subTabActiveTex = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.7f, 1f));

            cachedWindowStyle = new GUIStyle(GUI.skin.window)
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

            cachedLabelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                fontSize = 20,
                fontStyle = FontStyle.Bold,
            };

            cachedButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = buttonNormalTex, textColor = Color.white },
                hover = { background = buttonHoverTex, textColor = Color.white },
                active = { background = buttonActiveTex, textColor = Color.white },
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            cachedButtonHoverStyle = new GUIStyle(cachedButtonStyle)
            {
                hover = { background = buttonHoverTex, textColor = Color.white }
            };

            cachedButtonActiveStyle = new GUIStyle(cachedButtonStyle)
            {
                normal = { background = buttonActiveTex, textColor = Color.yellow },
                hover = { background = buttonHoverTex, textColor = Color.yellow },
                active = { background = buttonActiveTex, textColor = Color.yellow },
            };

            cachedSubTabButtonStyle = new GUIStyle(cachedButtonStyle)
            {
                normal = { background = subTabNormalTex, textColor = Color.white },
                hover = { background = subTabHoverTex, textColor = Color.white },
                active = { background = subTabActiveTex, textColor = Color.white },
            };

            cachedSubTabButtonHoverStyle = new GUIStyle(cachedSubTabButtonStyle)
            {
                hover = { background = subTabHoverTex, textColor = Color.white }
            };

            cachedSubTabButtonActiveStyle = new GUIStyle(cachedSubTabButtonStyle)
            {
                normal = { background = subTabActiveTex, textColor = Color.yellow },
                hover = { background = subTabHoverTex, textColor = Color.yellow },
                active = { background = subTabActiveTex, textColor = Color.yellow },
            };

            cachedSubTabLabelStyle = new GUIStyle(cachedLabelStyle)
            {
                normal = { textColor = new Color(0.5f, 0.5f, 1f, 1f) },
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
        }

        internal static void ApplyCachedStyles()
        {
            GUI.skin.window = cachedWindowStyle;
            GUI.skin.label = cachedLabelStyle;
            GUI.skin.button = cachedButtonStyle;
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
