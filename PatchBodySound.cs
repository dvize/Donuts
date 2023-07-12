using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Aki.Reflection.Patching;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace dvize.Donuts
{
    internal class PatchBodySound : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotOwner), "method_5");
        }

        [PatchPrefix]
        private static bool Prefix(BotOwner __instance, Vector3 obj)
        {
            try
            {
                if (__instance.Memory.IsPeace && (obj - __instance.Transform.position).sqrMagnitude < __instance.Settings.FileSettings.Hearing.DEAD_BODY_SOUND_RAD)
                {
                    __instance.BotsGroup.AddPointToSearch(obj, 80f, __instance, true);
                }
            }
            catch (Exception ex)
            {   }

            return false;
        }
    }
}
