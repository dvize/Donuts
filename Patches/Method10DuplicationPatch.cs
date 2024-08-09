using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using EFT;
using HarmonyLib;
using UnityEngine;
using SPT.Reflection.Patching;

namespace Donuts.Patches
{
    internal class BotSpawnerMethod10Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotSpawner), "method_10");
        }

        [PatchPrefix]
        public static bool Prefix(
            ref BotOwner bot,
            ref BotCreationDataClass data,
            ref Action<BotOwner> callback,
            ref bool shallBeGroup,
            ref Stopwatch stopWatch,
            BotSpawner __instance,
            BotsClass ____bots,
            int ____followersBotsCount,
            int ____bossBotsCount,
            int ____allBotsCount,
            int ____inSpawnProcess,
            Action<BotOwner> ___OnBotCreated)
        {
            if (!data.SpawnStopped)
            {
                bot.SpawnProfileData = data._profileData;

                // Comment out the bot addition to the list
                ____bots.Add(bot);

                if (bot.Profile.Info.Settings.IsFollower())
                {
                    ____followersBotsCount++;
                }
                else if (bot.Profile.Info.Settings.IsBoss())
                {
                    ____bossBotsCount++;
                }

                ____allBotsCount++;
                Action<BotOwner> onBotCreated = ___OnBotCreated;
                if (onBotCreated != null)
                {
                    onBotCreated(bot);
                }

                // Call the original method_11 using __instance
                __instance.method_11(bot);

                if (callback != null)
                {
                    callback(bot);
                }

                ____inSpawnProcess--;
                stopWatch.Stop();

                if (shallBeGroup && !data.SpawnParams.ShallBeGroup.IsBossSetted)
                {
                    data.SpawnParams.ShallBeGroup.IsBossSetted = true;
                    bot.Boss.SetBoss(data.SpawnParams.ShallBeGroup.StartCount);
                }

                return false; // Skip the original method
            }

            if (bot != null)
            {
                ____allBotsCount++;
                ____inSpawnProcess--;
                UnityEngine.Debug.LogError("Remove from map");
                bot.LeaveData.RemoveFromMap();
                return false; // Skip the original method
            }

            ____inSpawnProcess--;
            return false; // Skip the original method
        }
    }
}
