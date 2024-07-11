using EFT;
using System;
using System.Collections;
using System.Reflection;
using SPT.Reflection.Patching;
using Donuts;
using UnityEngine;
using Comfort.Common;

namespace Donuts.Patches
{
    public class DelayedGameStartPatch : ModulePatch
    {
        private static object localGameObj = null;
        protected override MethodBase GetTargetMethod()
        {
            Type baseGameType = typeof(BaseLocalGame<EftGamePlayerOwner>);
            return baseGameType.GetMethod("vmethod_4", BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        private static void PatchPostfix(ref IEnumerator __result, object __instance)
        {
            if(!Singleton<AbstractGame>.Instance.InRaid)
            {
                return;
            }

            localGameObj = __instance;

            if(DonutComponent.IsBotSpawningEnabled)
            {
                __result = addIterationsToWaitForBotGenerators(__result); // Thanks danW
            }
        }

        private static IEnumerator addIterationsToWaitForBotGenerators(IEnumerator originalTask)
        {
            // Now also wait for all bots to be fully initialized
            Logger.LogWarning("Donuts is waiting for bot preparation to complete...");
            float lastLogTime = Time.time;
            float startTime = Time.time;
            float maxWaitTime = DefaultPluginVars.maxRaidDelay.Value;

            while (!DonutsBotPrep.IsBotPreparationComplete)
            {
                yield return new WaitForEndOfFrame(); // Check every frame

                if (Time.time - lastLogTime >= 1.0f)
                {
                    lastLogTime = Time.time; // Update the last log time
                    Logger.LogWarning("Donuts still waiting...");
                }

                if (Time.time - startTime >= maxWaitTime)
                {
                    Logger.LogWarning("Max raid delay time reached. Proceeding with raid start, some bots might spawn late!");
                    break;
                }
            }

            // Continue with the original task
            Logger.LogWarning("Donuts bot preparation is complete...");
            yield return originalTask;
        }
    }
}
