using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Aki.Reflection.Patching;
using EFT;

namespace Donuts.Patches
{
    public class BotOwnerBrainActivatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotOwner).GetMethod("method_10", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static void PatchPrefix(BotOwner __instance)
        {
            if (__instance.Profile.Info.Settings.Role == WildSpawnType.assaultGroup)
            {
                TryConvertSpawnType(__instance);
            }
        }
        private static bool TryConvertSpawnType(BotOwner __instance)
        {
            
            WildSpawnType? originalSpawnType = DonutsBotPrep.GetOriginalSpawnTypeForBot(__instance);
            
            string currentRoleName = __instance.Profile.Info.Settings.Role.ToString();

            __instance.Profile.Info.Settings.Role = originalSpawnType.Value;
            string actualRoleName = __instance.Profile.Info.Settings.Role.ToString();

            Logger.LogDebug("Converted spawn type for bot " + __instance.Profile.Nickname + " from " + currentRoleName + " to " + actualRoleName);

            return true;
        }

        

    }
}
