using System.Reflection;
using SPT.Reflection.Patching;
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
