using UnityEngine;
using static Donuts.PluginGUIHelper;
using static Donuts.DefaultPluginVars;

namespace Donuts
{
    internal class DrawDebugging
    {
        internal static void Enable()
        {
            GUILayout.Space(30);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            // Add toggles for DebugGizmos and gizmoRealSize
            DebugGizmos.Value = ImGUIToolkit.Toggle(DebugGizmos.Name, DebugGizmos.ToolTipText, DebugGizmos.Value);
            GUILayout.Space(10);

            gizmoRealSize.Value = ImGUIToolkit.Toggle(gizmoRealSize.Name, gizmoRealSize.ToolTipText, gizmoRealSize.Value);
            GUILayout.Space(10);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }

}
