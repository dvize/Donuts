using System;
using EFT;
using UnityEngine;

using static Donuts.DonutComponent;

namespace Donuts.Models
{
    // Custom GetGroupAndSetEnemies wrapper that handles grouping bots into multiple groups within the same botzone
    internal class GetGroupWrapper
    {
        private BotsGroup group = null;

        public BotsGroup GetGroupAndSetEnemies(BotOwner bot, BotZone zone)
        {
            if (bot == null)
            {
                Debug.LogError("GetGroupAndSetEnemies: BotOwner is null.");
                return null;
            }

            if (zone == null)
            {
                Debug.LogError("GetGroupAndSetEnemies: BotZone is null.");
                return null;
            }

            if (group == null)
            {
                try
                {
                    group = botSpawnerClass.GetGroupAndSetEnemies(bot, zone);
                    if (group == null)
                    {
                        Debug.LogError("GetGroupAndSetEnemies: Failed to create or retrieve BotsGroup.");
                        return null;
                    }
                    group.Lock();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"GetGroupAndSetEnemies: Exception occurred while creating BotsGroup - {ex.Message}");
                    return null;
                }
            }

            return group;
        }
    }


}
