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
        public static ConfigEntry<int> BotMin;
        public static ConfigEntry<int> BotMax;
        public static ConfigEntry<float> SpawnTimer;

        public static ConfigEntry<float> factoryMinDistance;
        public static ConfigEntry<float> factoryMaxDistance;
        public static ConfigEntry<float> interchangeMinDistance;
        public static ConfigEntry<float> interchangeMaxDistance;
        public static ConfigEntry<float> laboratoryMinDistance;
        public static ConfigEntry<float> laboratoryMaxDistance;
        public static ConfigEntry<float> lighthouseMinDistance;
        public static ConfigEntry<float> lighthouseMaxDistance;
        public static ConfigEntry<float> reserveMinDistance;
        public static ConfigEntry<float> reserveMaxDistance;
        public static ConfigEntry<float> shorelineMinDistance;
        public static ConfigEntry<float> shorelineMaxDistance;
        public static ConfigEntry<float> woodsMinDistance;
        public static ConfigEntry<float> woodsMaxDistance;
        public static ConfigEntry<float> customsMinDistance;
        public static ConfigEntry<float> customsMaxDistance;
        public static ConfigEntry<float> tarkovstreetsMinDistance;
        public static ConfigEntry<float> tarkovstreetsMaxDistance;
        private void Awake()
        {
            PluginEnabled = Config.Bind(
                "1.Main Settings",
                "Plugin on/off",
                true,
                "");

            BotMin = Config.Bind(
                "1.Main Settings",
                "Bot Min Limit (At Distance)",
                2,
                "Min Num of Bots Spawned");

            BotMax = Config.Bind(
                "1.Main Settings",
                "Bot Max Limit (At Distance)",
                5,
                "Max Num of Bots Spawned");

            SpawnTimer = Config.Bind(
                "1.Main Settings",
                "Time it spawns bots",
                300f,
                "In seconds, it will choose +/- rand(100) seconds randomly from the given value");

            factoryMinDistance = Config.Bind(
                "Factory",
                "Factory Min Distance",
                30.0f,
                "Min Distance which bots are spawned.");

            factoryMaxDistance = Config.Bind(
                "Factory",
                "Factory Max Distance",
                80.0f,
                "Max Distance which bots are spawned.");

            customsMinDistance = Config.Bind(
                "Customs",
                "Customs Min Distance",
                35.0f,
                "Min Distance which bots are spawned.");

            customsMaxDistance = Config.Bind(
                "Customs",
                "Customs Max Distance",
                100.0f,
                "Max Distance which bots are spawned.");

            interchangeMinDistance = Config.Bind(
                "Interchange",
                "Interchange Min Distance",
                70.0f,
                "Min Distance which bots are spawned.");

            interchangeMaxDistance = Config.Bind(
                "Interchange",
                "Interchange Max Distance",
                150.0f,
                "Max Distance which bots are spawned.");

            laboratoryMinDistance = Config.Bind(
                "Labs",
                "Labs Min Distance",
                80.0f,
                "Min Distance which bots are spawned.");

            laboratoryMaxDistance = Config.Bind(
                "Labs",
                "Labs Max Distance",
                150.0f,
                "Max Distance which bots are spawned.");

            lighthouseMinDistance = Config.Bind(
                "Lighthouse",
                "Lighthouse Min Distance",
                100.0f,
                "Min Distance which bots are spawned.");

            lighthouseMaxDistance = Config.Bind(
                "Lighthouse",
                "Lighthouse Min Distance",
                200.0f,
                "Max Distance which bots are spawned.");

            reserveMinDistance = Config.Bind(
                "Reserve",
                "Reserve Min Distance",
                75.0f,
                "Min Distance which bots are spawned.");

            reserveMaxDistance = Config.Bind(
                "Reserve",
                "Reserve Max Distance",
                150.0f,
                "Max Distance which bots are spawned.");

            shorelineMinDistance = Config.Bind(
                "Shoreline",
                "Shoreline Min Distance",
                100.0f,
                "Min Distance which bots are spawned.");

            shorelineMaxDistance = Config.Bind(
                "Shoreline",
                "Shoreline Max Distance",
                200.0f,
                "Max Distance which bots are spawned.");

            woodsMinDistance = Config.Bind(
                "Woods",
                "Woods Min Distance",
                70.0f,
                "Min Distance which bots are spawned.");

            woodsMaxDistance = Config.Bind(
                "Woods",
                "Woods Max Distance",
                200.0f,
                "Max Distance which bots are spawned.");

            tarkovstreetsMinDistance = Config.Bind(
                "Streets",
                "Streets Min Distance",
                80.0f,
                "Min Distance which bots are spawned.");

            tarkovstreetsMaxDistance = Config.Bind(
                "Streets",
                "Streets Max Distance",
                80.0f,
                "Max Distance which bots are spawned.");


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
