using UnityEngine;
using static Donuts.PluginGUIHelper;

namespace Donuts
{
    internal class DrawDebugging
    {
        internal static void Enable()
        {
            // Draw content for Debugging
            GUILayout.Label("Debugging", cachedLabelStyle);
            // Add more settings here
        }
    }
}
