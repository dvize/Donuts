using System.Collections.Generic;
using System.Reflection;
using Aki.Reflection.Patching;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace Donuts.Patches
{
    internal class CoverPointMasterNullRef : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {

            return AccessTools.Method(typeof(CoverPointMaster), nameof(CoverPointMaster.GetClosePoints));
        }

        [PatchPrefix]
        public static bool Prefix(Vector3 pos, BotOwner bot, float dist, ref List<CustomNavigationPoint> __result)
        {
            if (bot == null || bot.Covers == null)
            {
                __result = new List<CustomNavigationPoint>(); // Return an empty list or handle as needed
                return false;
            }
            return true; 
        }
    }
}
