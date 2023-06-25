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
        private void Awake()
        {
            PluginEnabled = Config.Bind(
                "1.Main Settings",
                "Plugin on/off",
                true,
                "");

            SpawnTimer = Config.Bind(
                "1.Main Settings",
                "Time it spawns bots",
                300f,
                "In seconds, it will choose +/- rand(100) seconds randomly from the given value");


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
