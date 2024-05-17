using UnityEngine;
using static Donuts.PluginGUIHelper;

namespace Donuts
{
    internal class DrawAdvancedSettings
    {
        internal static void Enable()
        {
            // Draw content for Advanced Settings
            GUILayout.Label("Advanced Settings", cachedLabelStyle);
            // Add more settings here
        }
    }
}
