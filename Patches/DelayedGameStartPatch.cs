using System;
using System.Collections;
using System.Reflection;
using Aki.Reflection.Patching;
using Donuts;
using UnityEngine;

namespace Donuts.Patches
{
    public class DelayedGameStartPatch : ModulePatch
    {
        private static object localGameObj = null;
        protected override MethodBase GetTargetMethod()
        {
            Type localGameType = Aki.Reflection.Utils.PatchConstants.LocalGameType;
            return localGameType.GetMethod("method_18", BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        private static void PatchPostfix(ref IEnumerator __result, object __instance, float startDelay)
        {
            localGameObj = __instance;
            __result = addIterationsToWaitForBotGenerators(__result);  //thanks danW
        }
        private static IEnumerator addIterationsToWaitForBotGenerators(IEnumerator originalTask)
        {
            // Now also wait for all bots to be fully initialized
            Logger.LogWarning("Donuts is waiting for bot preparation to complete...");
            float lastLogTime = Time.time;

            while (!DonutsBotPrep.IsBotPreparationComplete)
            {
                yield return new WaitForSeconds(0.1f); // Check every 100ms

                if (Time.time - lastLogTime >= 1.0f)
                {
                    lastLogTime = Time.time; // Update the last log time
                    Logger.LogWarning("Donuts still waiting...");
                }
            }

            // Continue with the original task
            Logger.LogWarning("Donuts bot preparation is complete...");
            yield return originalTask;
        }

        
    }
}
