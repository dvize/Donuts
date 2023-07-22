using System.Reflection;
using Aki.Reflection.Patching;
using EFT;
using HarmonyLib;

namespace dvize.Donuts
{
    internal class PatchStandbyTeleport : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GClass351), "UpdateNode");
        }

        [PatchPrefix]
        public static bool Prefix(GClass351 __instance, BotStandByType ___botStandByType_0)
        {
            FieldInfo botOwnerField = AccessTools.Field(typeof(GClass294), "botOwner_0");

            if (botOwnerField != null)
            {
                BotOwner botOwner = botOwnerField.GetValue(__instance) as BotOwner;

                if (!botOwner.Settings.FileSettings.Mind.CAN_STAND_BY)
                {
                    return false;
                }
            }

            if (!__instance.CanDoStandBy)
            {
                return false;
            }

            if (___botStandByType_0 == BotStandByType.goToSave)
            {
                MethodInfo method1 = AccessTools.Method(typeof(GClass351), "method_1");
                if (method1 != null)
                {
                    method1.Invoke(__instance, new object[] { });
                }
            }

            return false;
        }
    }
}
