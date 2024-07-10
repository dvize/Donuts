using System.Reflection;
using Aki.Reflection.Patching;
using EFT;

namespace Donuts.Patches
{
    internal class BotProfilePreparationHook : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(BotsController).GetMethod(nameof(BotsController.AddActivePLayer));

        [PatchPrefix]
        public static void PatchPrefix() => DonutsBotPrep.Enable();
    }
}
