using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Donuts;
using HarmonyLib;
using Aki.Reflection.Patching;

namespace dvize.Donuts.Patches
{
    internal class PlayerFireControlPatchGetter : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var playerType = typeof(EFT.Player.FirearmController);
            return AccessTools.PropertyGetter(playerType, "IsTriggerPressed");
        }

        [PatchPrefix]
        public static bool PrefixGet(ref bool __result)
        {
            if (DefaultPluginVars.showGUI)
            {
                __result = false;
                return false;
            }
            return true; // Continue with the original getter
        }
    }

    internal class PlayerFireControlPatchSetter : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var playerType = typeof(EFT.Player.FirearmController);
            return AccessTools.PropertySetter(playerType, "IsTriggerPressed");
        }

        [PatchPrefix]
        public static bool PrefixSet(ref bool value)
        {
            if (DefaultPluginVars.showGUI)
            {
                value = false;
                return false;
            }
            return true; // Continue with the original setter
        }
    }
}
