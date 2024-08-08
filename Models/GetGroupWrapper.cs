using Donuts;
using EFT;
using System.Collections.Generic;
using System;
using HarmonyLib;
using Comfort.Common;
using System.Linq;
using UnityEngine;
using BepInEx.Logging;

public class GetGroupWrapper
{
    private BotSpawner _botSpawnerClass;
    private GameWorld _gameWorld;
    private bool _freeForAll;
    private const float SupportDistanceThreshold = 100f; // 100 meters
    private BotZoneGroupsDictionary botZoneGroupsDict;
    internal static ManualLogSource Logger
    {
        get; private set;
    }
    public GetGroupWrapper(BotSpawner botSpawnerClass, GameWorld gameWorld)
    {
        _botSpawnerClass = botSpawnerClass;
        _gameWorld = gameWorld;
        _freeForAll = true;
        Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(GetGroupWrapper));
    }

    public BotsGroup GetGroupAndSetEnemies(BotOwner bot, BotZone zone)
    {
        try
        {
            // Check if the bot is a boss, follower, or support
            bool isBossOrFollower = bot.Profile.Info.Settings.Role.IsBoss() ||
                                    bot.Profile.Info.Settings.Role.IsFollower() ||
                                    BotSupportTracker.botSourceTypeMap.ContainsKey(bot.Id.ToString());
            EPlayerSide side = bot.Profile.Info.Side;
            WildSpawnType role = bot.Profile.Info.Settings.Role;

            Logger.LogInfo($"Processing bot {bot.Id}, Role: {role}, Side: {side}, IsBossOrFollower: {isBossOrFollower}");

            //set dict if null
            if (botZoneGroupsDict == null)
            {
                //grab private field _groups from botSpawnerClass with accesstools
                botZoneGroupsDict = AccessTools.Field(typeof(BotSpawner), "_groups").GetValue(_botSpawnerClass) as BotZoneGroupsDictionary;
            }

            // Retrieve eligible bots for grouping
            IEnumerable<BotOwner> eligibleBots = GetEligibleBots(bot);
            if (eligibleBots == null)
            {
                Logger.LogError("No eligible bots found for grouping.");
                return null;
            }

            if (isBossOrFollower)
            {
                Logger.LogInfo("Checking for existing group...");

                //look at botZoneGroupsDict for any boss that exists in same zone and must be same side and isBossOrFollower and not full
                bool existingGroup = false;

                //try get value from zone in dict
                if(botZoneGroupsDict.Keys.Contains(zone))
                {
                    var groups = botZoneGroupsDict[zone].GetGroups(true);
                    foreach(var group in groups)
                    {
                        if(group.Side == bot.Side && !group.IsFull)
                        {
                            existingGroup = true;
                            Logger.LogInfo($"Using existing group for bot {bot.Id}.");
                            return group;
                        }
                    } 
                   
                }

                Logger.LogInfo("No existing group found, creating new group...");

                // Use AccessTools to get deadBodiesController and allPlayers
                var deadBodiesController = AccessTools.Field(typeof(BotSpawner), "_deadBodiesController").GetValue(_botSpawnerClass) as DeadBodiesController;
                var allPlayers = AccessTools.Field(typeof(BotSpawner), "_allPlayers").GetValue(_botSpawnerClass) as List<Player>;

                if (deadBodiesController == null || allPlayers == null)
                {
                    Logger.LogError("Failed to retrieve necessary components for group creation.");
                    return null;
                }

                // Convert IEnumerable<BotOwner> to List<BotOwner>
                List<BotOwner> eligibleBotsList = eligibleBots.ToList();
                Logger.LogInfo($"Eligible bots count: {eligibleBotsList.Count}");

                var newGroup = new BotsGroup(zone, Singleton<IBotGame>.Instance, bot, eligibleBotsList, deadBodiesController, allPlayers, true);
                AddSupportsToGroup(newGroup, bot);

                Logger.LogInfo($"New group created for bot {bot.Id} with {eligibleBotsList.Count} eligible bots.");

                _botSpawnerClass.Groups.Add(zone, side, newGroup, true);
                return newGroup;
            }
            else
            {
                Logger.LogInfo($"Delegating to original GetGroupAndSetEnemies for bot {bot.Id}.");
                // Use the existing logic for regular bots
                return _botSpawnerClass.GetGroupAndSetEnemies(bot, zone);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Exception occurred while creating or retrieving BotsGroup for bot {bot.Id}. Exception: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }
    private IEnumerable<BotOwner> GetEligibleBots(BotOwner owner)
    {
        //use accesstools to get Bots from botspawnerClass : // protected readonly BotsClass _bots;
        var bots = AccessTools.Field(typeof(BotSpawner), "_bots").GetValue(_botSpawnerClass) as BotsClass;

        if (_freeForAll)
        {
            return bots.BotOwners;
        }
        return bots.GetEnemiesBySettings(owner.Settings.FileSettings);
    }

    private void AddSupportsToGroup(BotsGroup group, BotOwner boss)
    {
        //use accesstools to get Bots from botspawnerClass : // protected readonly BotsClass _bots;
        var bots = AccessTools.Field(typeof(BotSpawner), "_bots").GetValue(_botSpawnerClass) as BotsClass;

        foreach (var botOwner in bots.BotOwners)
        {
            if (BotSupportTracker.GetBotSourceType(botOwner.Id.ToString()) == BotSourceType.Support &&
                Vector3.Distance(botOwner.Position, boss.Position) <= SupportDistanceThreshold)
            {
                group.AddMember(botOwner, true);
            }
        }
    }
}
