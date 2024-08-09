using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace dvize.Donuts.Patches
{
    internal class BotMethod10DuplicationPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotSpawner), nameof(BotSpawner.method_10));
        }

        [PatchPrefix]
        static bool Prefix(BotOwner bot, BotCreationDataClass data, Action<BotOwner> callback, bool shallBeGroup, Stopwatch stopWatch,ref List<BotOwner> ____bots)
        {
            // Check if bot already exists in the list
            if (____bots.Contains(bot))
            {
                UnityEngine.Debug.LogWarning($"Bot {bot.Profile.Info.Nickname} already exists, skipping duplicate addition.");
                return false; // Skip the original method to prevent duplicate
            }

            // Allow original method to run if no duplicates found
            return true;
        }
    }
}
