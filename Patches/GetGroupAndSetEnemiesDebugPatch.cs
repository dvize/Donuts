using System.Collections.Generic;
using System.Reflection;
using SPT.Reflection.Patching;
using EFT;
using HarmonyLib;
using UnityEngine;
using System;

namespace Donuts.Patches
{
    internal class GetGroupAndSetEnemiesDebugPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Specify the target method to patch
            return AccessTools.Method(typeof(BotSpawner), "GetGroupAndSetEnemies");
        }

        [PatchPrefix]
        public static bool Prefix(BotOwner bot, BotZone zone, ref BotsGroup __result, BotSpawner __instance)
        {
            Debug.Log($"GetGroupAndSetEnemies called with bot: {bot?.Profile?.Info?.Nickname ?? "null"}, zone: {zone?.name ?? "null"}");

            // Check if bot or zone is null
            if (bot == null)
            {
                Debug.LogError("GetGroupAndSetEnemies: BotOwner is null.");
                return false;
            }

            if (zone == null)
            {
                Debug.LogError("GetGroupAndSetEnemies: BotZone is null.");
                return false;
            }

            try
            {
                Logger.LogWarning("Trying to access the private fields of BotSpawner GetGroupAndSetEnemiesDebugPatch.");
                // Access private fields using reflection
                var groupsField = AccessTools.Field(typeof(BotSpawner), "_groups");
                var gameField = AccessTools.Field(typeof(BotSpawner), "_game");
                var deadBodiesControllerField = AccessTools.Field(typeof(BotSpawner), "_deadBodiesController");
                var allPlayersField = AccessTools.Field(typeof(BotSpawner), "_allPlayers");

                var groups = (BotZoneGroupsDictionary)groupsField.GetValue(__instance);
                var game = (IBotGame)gameField.GetValue(__instance);
                var deadBodiesController = (DeadBodiesController)deadBodiesControllerField.GetValue(__instance);
                var allPlayers = (List<Player>)allPlayersField.GetValue(__instance);


                // Check for null values of private fields
                if (groups == null)
                {
                    Debug.LogError("GetGroupAndSetEnemies: _groups is null.");
                    return false;
                }

                if (game == null)
                {
                    Debug.LogError("GetGroupAndSetEnemies: _game is null.");
                    return false;
                }

                if (deadBodiesController == null)
                {
                    Debug.LogError("GetGroupAndSetEnemies: _deadBodiesController is null.");
                    return false;
                }

                if (allPlayers == null)
                {
                    Debug.LogError("GetGroupAndSetEnemies: _allPlayers is null.");
                    return false;
                }

                // Extract necessary bot information
                bool isBossOrFollower = bot.Profile.Info.Settings.Role.IsBoss() || bot.Profile.Info.Settings.Role.IsFollower();
                EPlayerSide side = bot.Profile.Info.Side;
                WildSpawnType role = bot.Profile.Info.Settings.Role;

                Debug.Log($"Role: {role}, Side: {side}, IsBossOrFollower: {isBossOrFollower}");

                List<BotOwner> botList = new List<BotOwner>();
                BotsGroup botsGroup;

                // Log before trying to retrieve or create a group
                Debug.Log("Attempting to retrieve existing group or create new group.");

                if(bot.SpawnProfileData != null)
                {
                    Debug.Log($"Bot SpawnProfileData: {bot.SpawnProfileData}");
                }

                if(bot.SpawnProfileData.SpawnParams != null)
                {
                    Debug.Log($"Bot SpawnParams: {bot.SpawnProfileData.SpawnParams}");
                }   

                if(bot.SpawnProfileData.SpawnParams.ShallBeGroup != null)
                {
                    Debug.Log($"Bot ShallBeGroup: {bot.SpawnProfileData.SpawnParams.ShallBeGroup}");
                }


                // Check if a group already exists
                if (groups.TryGetValue(zone, side, role, out botsGroup, isBossOrFollower) &&
                    (bot.SpawnProfileData == null || bot.SpawnProfileData?.SpawnParams?.ShallBeGroup == null ||
                     (!bot.Boss.IamBoss && !botsGroup.IsFull)))
                {
                    Debug.Log("Returning existing group.");
                    __result = botsGroup;
                    return false;
                }

                // Log before calling method_4
                Debug.Log("Calling method_4 to add bots to the list.");

                foreach (BotOwner botOwner in __instance.method_4(bot))
                {
                    if (botOwner == null)
                    {
                        Debug.LogError("GetGroupAndSetEnemies: A bot in method_4 result is null.");
                        continue;
                    }
                    botList.Add(botOwner);
                }

                // Log before creating a new group
                Debug.Log("Creating a new BotsGroup.");

                botsGroup = new BotsGroup(zone, game, bot, botList, deadBodiesController, allPlayers, isBossOrFollower);
                if (bot.SpawnProfileData?.SpawnParams?.ShallBeGroup != null)
                {
                    botsGroup.TargetMembersCount = bot.SpawnProfileData.SpawnParams.ShallBeGroup.StartCount;
                }

                // Log before adding the group
                Debug.Log("Adding new group to the groups dictionary.");

                groups.Add(zone, side, botsGroup, isBossOrFollower);

                Debug.Log("New group created and added.");

                __result = botsGroup;
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"GetGroupAndSetEnemies: Exception occurred - {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}
