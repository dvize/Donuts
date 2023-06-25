using System.Reflection;
using Aki.Reflection.Patching;
using BepInEx;
using BepInEx.Configuration;
using EFT;

namespace Donuts
{
    [BepInPlugin("com.dvize.Donuts", "dvize.Donuts", "1.0.0")]
    public class DonutsPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> PluginEnabled;
        public static ConfigEntry<float> SpawnTimer;
        public static ConfigEntry<float> botSpawnDistance;
        private void Awake()
        {
            PluginEnabled = Config.Bind(
                "1.Main Settings",
                "Plugin on/off",
                true,
                "");

            botSpawnDistance = Config.Bind(
                "1.Main Settings",
                "Bot Spawn Distance",
                100f,
                "Distance in which the player is away from the fight location point that it triggers bot spawn");

            SpawnTimer = Config.Bind(
                "1.Main Settings",
                "Bot Spawn Timer",
                300f,
                "In seconds before it spawns next wave while player in the fight zone area");


            new NewGamePatch().Enable();
        }

    }

    //re-initializes each new game
    internal class NewGamePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));

        [PatchPrefix]
        public static void PatchPrefix()
        {
            DonutComponent.Enable();
        }
    }
}
