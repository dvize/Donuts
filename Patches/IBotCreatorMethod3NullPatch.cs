using System;
using System.Reflection;
using UnityEngine;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace dvize.Donuts.Patches
{
    internal class IBotCreatorMethod3NullPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GClass814), nameof(GClass814.method_3));
        }

        [PatchPrefix]
        public static bool Prefix(GClass814 __instance, BotZone zone, BotOwner bot, Action<BotOwner> callback, Func<BotOwner, BotZone, BotsGroup> groupAction, IBotGame ___ibotGame_0)
        {
            try
            {
                if (bot == null)
                {
                    Debug.LogError("method_3: BotOwner is null.");
                    return false; // Skip the method if bot is null
                }

                Debug.LogWarning("botOwner is : " + bot.name);

                Player getPlayer = bot.GetPlayer;
                if (getPlayer != null)
                {
                    Debug.LogWarning("Player is not null");
                    if (getPlayer.CharacterController != null)
                    {
                        getPlayer.CharacterController.isEnabled = true;
                    }
                    else
                    {
                        Debug.LogError("method_3: CharacterController is null.");
                    }

                    if (getPlayer.MovementContext != null)
                    {
                        getPlayer.MovementContext.ResetFlying();
                    }
                    else
                    {
                        Debug.LogError("method_3: MovementContext is null.");
                    }
                }
                else
                {
                    Debug.LogError("method_3: Player is null.");
                }

                if (zone == null)
                {
                    Debug.LogError("method_3: BotZone is null.");
                    return false; // Skip the method if zone is null
                }

                Debug.LogWarning("Zone: " + zone.name);

                if (groupAction == null)
                {
                    Debug.LogError("method_3: GroupAction is null.");
                    return false; // Skip the method if groupAction is null
                }

                Debug.LogWarning("Started coversData");
                AICoversData coversData = ___ibotGame_0.BotsController.CoversData;

                // Check coversData is not null
                if (coversData == null)
                {
                    Debug.LogError("method_3: CoversData is null.");
                    return false;
                }

                Debug.LogWarning("Started PreActivate");
                bot.PreActivate(zone, ___ibotGame_0.GameDateTime, ___ibotGame_0.BotsController.BotSpawner.GetGroupAndSetEnemies(bot, zone), coversData, true);


                Debug.LogWarning("Started method_5");
                __instance.method_5(bot, true);

                Debug.LogWarning("Started callback");

                //callback.Invoke(bot); 
            }
            catch (Exception ex)
            {
                Debug.LogError($"method_3: Exception occurred - {ex.Message} {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.LogError($"method_3: Inner exception - {ex.InnerException.Message} {ex.InnerException.StackTrace}");
                }
            }

            return false; // Skip original method to avoid double execution
        }
    }
}
