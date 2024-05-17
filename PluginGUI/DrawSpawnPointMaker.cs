using UnityEngine;
using static Donuts.PluginGUIHelper;

namespace Donuts
{
    internal class DrawSpawnPointMaker
    {
        internal static void Enable()
        {
            // Draw content for SpawnPoint Maker
            GUILayout.Label("SpawnPoint Maker", cachedLabelStyle);
            // Add more settings here
        }
    }
}
