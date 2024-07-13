using System.Collections.Generic;
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace dvize.Donuts.Patches
{
    internal class BotSettingsRepoAbstractClassPatch : ModulePatch
    {
        //Patch so that bots notice eachother again  - Thanks DanW
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotSettingsRepoAbstractClass), nameof(BotSettingsRepoAbstractClass.Init));
        }

        [PatchPostfix]
        public static void Postfix(BotSettingsRepoAbstractClass __instance, Dictionary<WildSpawnType, GClass696> ___dictionary_0)
        {
            // Modify the entries for pmcBEAR and pmcBot
            if (___dictionary_0 != null)
            {
                ___dictionary_0[WildSpawnType.pmcBEAR] = new GClass696(false, false, false, "ScavRole/PmcBot", (ETagStatus)0);
                ___dictionary_0[WildSpawnType.pmcBot] = new GClass696(false, false, false, "ScavRole/PmcBot", (ETagStatus)0);
            }
        }
    }
}
