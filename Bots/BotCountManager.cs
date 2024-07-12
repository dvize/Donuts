using System;
using SPT.PrePatch;
using Cysharp.Threading.Tasks;
using EFT;
using static Donuts.DonutComponent;
using System.Threading;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    public static class BotCountManager
    {
        public static UniTask<int> GetAlivePlayers(string spawnType, CancellationToken cancellationToken)
        {
            return UniTask.Create(async () =>
            {
                int count = 0;
                foreach (Player bot in gameWorld.AllAlivePlayersList)
                {
                    if (!bot.IsYourPlayer)
                    {
                        switch (spawnType)
                        {
                            case "scav":
                                if (IsSCAV(bot.Profile.Info.Settings.Role))
                                {
                                    count++;
                                }
                                break;

                            case "pmc":
                                if (IsPMC(bot.Profile.Info.Settings.Role))
                                {
                                    count++;
                                }
                                break;

                            default:
                                throw new ArgumentException("Invalid spawnType", nameof(spawnType));
                        }
                    }
                }

                return count;
            });
        }

        private static bool IsPMC(WildSpawnType role)
        {
            return role == WildSpawnType.pmcUSEC || role == WildSpawnType.pmcBEAR;
        }

        private static bool IsSCAV(WildSpawnType role)
        {
            return role == WildSpawnType.assault;
        }
    }
}
