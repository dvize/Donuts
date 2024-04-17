public static class BotCountManager
{
    public static int PMCBotLimit { get; set; } = 10;
    public static int SCAVBotLimit { get; set; } = 10;
    public static int CurrentInitialPMCs { get; private set; } = 0;
    public static int CurrentInitialSCAVs { get; private set; } = 0;

    public static int HandleHardCap(string spawnType, int requestedCount)
    {
        int currentBotsAlive = GetRegisteredPlayers(spawnType);
        int botLimit = GetBotLimit(spawnType);
        if (currentBotsAlive + requestedCount > botLimit)
        {
            requestedCount = botLimit - currentBotsAlive;
            LogDebug($"{spawnType} hard cap exceeded. Current: {currentBotsAlive}, Limit: {botLimit}, Adjusted count: {requestedCount}");
            return Math.Max(0, requestedCount);
        }
        return requestedCount;
    }

    public static int AllocateBots(string spawnType, int requestedCount)
    {
        int currentCount = GetCurrentBotCount(spawnType);
        int maxLimit = GetBotLimit(spawnType);
        if (currentCount + requestedCount > maxLimit)
        {
            int adjustedCount = maxLimit - currentCount;
            SetCurrentBotCount(spawnType, currentCount + adjustedCount);
            LogDebug($"{spawnType} preset bot cap exceeded. Current: {currentCount}, Limit: {maxLimit}, Adjusted count: {adjustedCount}");
            return adjustedCount;
        }
        SetCurrentBotCount(spawnType, currentCount + requestedCount);
        return requestedCount;
    }

    private static int GetCurrentBotCount(string spawnType)
    {
        if (spawnType.Contains("pmc"))
            return CurrentInitialPMCs;
        else if (spawnType.Contains("assault"))
            return CurrentInitialSCAVs;
        return 0;
    }

    private static void SetCurrentBotCount(string spawnType, int newCount)
    {
        if (spawnType.Contains("pmc"))
            CurrentInitialPMCs = newCount;
        else if (spawnType.Contains("assault"))
            CurrentInitialSCAVs = newCount;
    }

    private static int GetBotLimit(string spawnType)
    {
        return spawnType.Contains("pmc") ? PMCBotLimit : SCAVBotLimit;
    }

    private static int GetRegisteredPlayers(string spawnType)
    {
        int count = 0;
        foreach (Player bot in gameWorld.RegisteredPlayers)
        {
            if (!bot.IsYourPlayer && ((spawnType == "assault" && IsSCAV(bot.Profile.Info.Settings.Role)) ||
                                      (spawnType != "assault" && IsPMC(bot.Profile.Info.Settings.Role))))
            {
                count++;
            }
        }
        return count;
    }

    private static bool IsPMC(WildSpawnType role)
    {
        return role == WildSpawnType.pmc || role == WildSpawnType.sptUsec || role == WildSpawnType.sptBear;
    }

    private static bool IsSCAV(WildSpawnType role)
    {
        return role == WildSpawnType.assault;
    }
}
