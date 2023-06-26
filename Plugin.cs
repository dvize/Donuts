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
        public static ConfigEntry<int> AbsMaxBotCount;
        public static ConfigEntry<bool> DespawnEnabled;
        private void Awake()
        {
            PluginEnabled = Config.Bind(
                "Main Settings",
                "1. Plugin on/off",
                true,
                "");

            botSpawnDistance = Config.Bind(
                "Main Settings",
                "2. Bot Spawn Distance",
                150f,
                "Distance in which the player is away from the fight location point that it triggers bot spawn");

            SpawnTimer = Config.Bind(
                "Main Settings",
                "3. Bot Spawn Timer",
                180f,
                "In seconds before it spawns next wave while player in the fight zone area");

            AbsMaxBotCount = Config.Bind(
                "Main Settings",
                "4. Absolute Max Bot Count",
                18,
                "It will stop spawning bots over your maxbotcap limit once it hits this.");

            DespawnEnabled = Config.Bind(
                "Main Settings",
                "5. Despawn Option",
                true,
                "When enabled, removes furthest bots from player for each new dynamic spawn bot");


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
