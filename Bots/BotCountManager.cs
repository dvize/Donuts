public static class BotCountManager
{
    public static int CurrentInitialPMCs { get; private set; } = 0;
    public static int CurrentInitialSCAVs { get; private set; } = 0;

    public static void IncrementPMC()
    {
        if (CurrentInitialPMCs < PMCBotLimit)
            CurrentInitialPMCs++;
    }

    public static void IncrementSCAV()
    {
        if (CurrentInitialSCAVs < SCAVBotLimit)
            CurrentInitialSCAVs++;
    }

    public static void DecrementPMC()
    {
        if (CurrentInitialPMCs > 0)
            CurrentInitialPMCs--;
    }

    public static void DecrementSCAV()
    {
        if (CurrentInitialSCAVs > 0)
            CurrentInitialSCAVs--;
    }

    public static void ResetCounts()
    {
        CurrentInitialPMCs = 0;
        CurrentInitialSCAVs = 0;
    }
}
