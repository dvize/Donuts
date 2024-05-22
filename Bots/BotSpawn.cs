using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Aki.PrePatch;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using HarmonyLib;
using UnityEngine;
using static Donuts.DefaultPluginVars;
using static Donuts.DonutComponent;
using BotCacheClass = GClass591;
using IProfileData = GClass592;
using Random = System.Random;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class BotSpawn
    {
        private const string PmcSpawnTypes = "pmc,sptusec,sptbear";
        private const string ScavSpawnType = "assault";

        internal static AICorePoint GetClosestCorePoint(Vector3 position)
        {
            var botGame = Singleton<IBotGame>.Instance;
            var coversData = botGame.BotsController.CoversData;
            var groupPoint = coversData.GetClosest(position);
            return groupPoint.CorePointInGame;
        }

        internal static async UniTask SpawnBots(HotspotTimer hotspotTimer, Vector3 coordinate)
        {
            string hotspotSpawnType = hotspotTimer.Hotspot.WildSpawnType;
            WildSpawnType wildSpawnType = DetermineWildSpawnType(hotspotTimer, hotspotSpawnType);

            if (!IsRaidTimeRemaining(hotspotSpawnType))
            {
                DonutComponent.Logger.LogDebug("Spawn not allowed due to raid time conditions - skipping this spawn");
                return;
            }

            int maxCount = DetermineMaxBotCount(hotspotSpawnType, hotspotTimer.Hotspot.MaxRandomNumBots);
            if (hotspotTimer.Hotspot.BotTimerTrigger > 9999)
            {
                maxCount = BotCountManager.AllocateBots(hotspotSpawnType, maxCount);
                if (maxCount == 0)
                {
                    DonutComponent.Logger.LogDebug("Starting bot cap reached - no bots can be spawned");
                    return;
                }
            }

            if (HardCapEnabled.Value)
            {
                maxCount = BotCountManager.HandleHardCap(hotspotSpawnType, maxCount);
                if (maxCount == 0)
                {
                    DonutComponent.Logger.LogDebug("Hard cap exceeded - no bots can be spawned");
                    return;
                }
            }

            bool isGroup = maxCount > 1;
            await SetupSpawn(hotspotTimer, maxCount, isGroup, wildSpawnType, coordinate);
        }

        private static bool IsRaidTimeRemaining(string hotspotSpawnType)
        {
            int hardStopTime = GetHardStopTime(hotspotSpawnType); // Time or percent remaining return
            int raidTimeLeftTime = (int)Aki.SinglePlayer.Utils.InRaid.RaidTimeUtil.GetRemainingRaidSeconds(); // Time left
            int raidTimeLeftPercent = (int)(Aki.SinglePlayer.Utils.InRaid.RaidTimeUtil.GetRaidTimeRemainingFraction() * 100f); // Percent left
            return useTimeBasedHardStop.Value ? raidTimeLeftTime >= hardStopTime : raidTimeLeftPercent >= hardStopTime;
        }

        private static int GetHardStopTime(string hotspotSpawnType)
        {
            if ((PmcSpawnTypes.Contains(hotspotSpawnType) && hardStopOptionPMC.Value) ||
                (hotspotSpawnType == ScavSpawnType && hardStopOptionSCAV.Value))
            {
                // Time based hard stop
                if (useTimeBasedHardStop.Value)
                {
                    return hotspotSpawnType == ScavSpawnType ? hardStopTimeSCAV.Value : hardStopTimePMC.Value;
                }
                // Percentage based hard stop
                return hotspotSpawnType == ScavSpawnType ? hardStopPercentSCAV.Value : hardStopPercentPMC.Value;
            }
            return int.MaxValue;
        }

        private static int DetermineMaxBotCount(string spawnType, int defaultMaxCount)
        {
            string groupChance = spawnType == "assault" ? scavGroupChance.Value : pmcGroupChance.Value;
            return getActualBotCount(groupChance, defaultMaxCount);
        }

        private static async UniTask SetupSpawn(HotspotTimer hotspotTimer, int maxCount, bool isGroup, WildSpawnType wildSpawnType, Vector3 coordinate)
        {
            DonutComponent.Logger.LogDebug($"Attempting to spawn {(isGroup ? "group" : "solo")} with bot count {maxCount}");
            if (isGroup)
            {
                await SpawnGroupBots(hotspotTimer, maxCount, wildSpawnType, coordinate);
            }
            else
            {
                await SpawnSingleBot(hotspotTimer, wildSpawnType, coordinate);
            }
        }

        private static async UniTask SpawnGroupBots(HotspotTimer hotspotTimer, int count, WildSpawnType wildSpawnType, Vector3 coordinate)
        {
#if DEBUG
            DonutComponent.Logger.LogDebug($"Spawning a group of {count} bots.");
#endif
            WildSpawnType sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            WildSpawnType sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;
            EPlayerSide side = GetSideForWildSpawnType(wildSpawnType, sptUsec, sptBear);
            var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
            BotDifficulty botDifficulty = GetBotDifficulty(wildSpawnType, sptUsec, sptBear);

            var cachedBotGroup = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, count);
            if (cachedBotGroup == null)
            {
#if DEBUG
                DonutComponent.Logger.LogWarning($"No grouped cached bots found, generating on the fly for: {hotspotTimer.Hotspot.Name} for {count} grouped number of bots.");
#endif
                var botInfo = new PrepBotInfo(wildSpawnType, botDifficulty, side, true, count);
                await DonutsBotPrep.CreateBot(botInfo, true, count);
                DonutsBotPrep.BotInfos.Add(botInfo);
                cachedBotGroup = botInfo.Bots;
            }
            else
            {
                DonutComponent.Logger.LogWarning("Found grouped cached bots, spawning them.");
            }

            Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(hotspotTimer.Hotspot, coordinate, maxSpawnTriesPerBot.Value);
            if (!spawnPosition.HasValue)
            {
                DonutComponent.Logger.LogDebug("No valid spawn position found - skipping this spawn");
                return;
            }

            await SpawnBotForGroup(cachedBotGroup, wildSpawnType, side, botCreator, botSpawnerClass, spawnPosition.Value, cancellationTokenSource, botDifficulty, count, hotspotTimer);
        }

        private static async UniTask SpawnSingleBot(HotspotTimer hotspotTimer, WildSpawnType wildSpawnType, Vector3 coordinate)
        {
            DonutComponent.Logger.LogDebug($"Spawning a single bot.");
            WildSpawnType sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            WildSpawnType sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;
            EPlayerSide side = GetSideForWildSpawnType(wildSpawnType, sptUsec, sptBear);
            var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
            BotDifficulty botDifficulty = GetBotDifficulty(wildSpawnType, sptUsec, sptBear);
            var BotCacheDataList = DonutsBotPrep.GetWildSpawnData(wildSpawnType, botDifficulty);

            Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(hotspotTimer.Hotspot, coordinate, maxSpawnTriesPerBot.Value);
            if (!spawnPosition.HasValue)
            {
                DonutComponent.Logger.LogDebug("No valid spawn position found - skipping this spawn");
                return;
            }

            await SpawnBotFromCacheOrCreateNew(BotCacheDataList, wildSpawnType, side, botCreator, botSpawnerClass, spawnPosition.Value, cancellationTokenSource, botDifficulty, hotspotTimer);
        }

        private static WildSpawnType DetermineWildSpawnType(HotspotTimer hotspotTimer, string hotspotSpawnType)
        {
            WildSpawnType sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            WildSpawnType sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;
            WildSpawnType wildSpawnType = GetWildSpawnType(
                forceAllBotType.Value == "PMC" ? "pmc" :
                forceAllBotType.Value == "SCAV" ? "assault" :
                hotspotTimer.Hotspot.WildSpawnType, sptUsec, sptBear);

            return wildSpawnType == GetWildSpawnType("pmc", sptUsec, sptBear) ? DeterminePMCFactionBasedOnRatio(sptUsec, sptBear) : wildSpawnType;
        }

        private static WildSpawnType DeterminePMCFactionBasedOnRatio(WildSpawnType sptUsec, WildSpawnType sptBear)
        {
            int factionRatio = pmcFactionRatio.Value;
            Random rand = new Random();
            return rand.Next(100) < factionRatio ? sptUsec : sptBear;
        }

        private static int GetBotLimit(string spawnType)
        {
            return spawnType.Contains("pmc") ? PMCBotLimit : spawnType.Contains("assault") ? SCAVBotLimit : 0;
        }

        #region botHelperMethods

        #region botDifficulty
        internal static BotDifficulty GetBotDifficulty(WildSpawnType wildSpawnType, WildSpawnType sptUsec, WildSpawnType sptBear)
        {
            if (wildSpawnType == WildSpawnType.assault)
            {
                return grabSCAVDifficulty();
            }
            else if (wildSpawnType == sptUsec || wildSpawnType == sptBear || wildSpawnType == WildSpawnType.pmcBot)
            {
                return grabPMCDifficulty();
            }
            else
            {
                return grabOtherDifficulty();
            }
        }

        internal static BotDifficulty grabPMCDifficulty()
        {
            switch (botDifficultiesPMC.Value.ToLower())
            {
                case "asonline":
                    BotDifficulty[] randomDifficulty = { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard };
                    return randomDifficulty[UnityEngine.Random.Range(0, 3)];
                case "easy":
                    return BotDifficulty.easy;
                case "normal":
                    return BotDifficulty.normal;
                case "hard":
                    return BotDifficulty.hard;
                case "impossible":
                    return BotDifficulty.impossible;
                default:
                    return BotDifficulty.normal;
            }
        }

        internal static BotDifficulty grabSCAVDifficulty()
        {
            switch (botDifficultiesSCAV.Value.ToLower())
            {
                case "asonline":
                    BotDifficulty[] randomDifficulty = { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard };
                    return randomDifficulty[UnityEngine.Random.Range(0, 3)];
                case "easy":
                    return BotDifficulty.easy;
                case "normal":
                    return BotDifficulty.normal;
                case "hard":
                    return BotDifficulty.hard;
                case "impossible":
                    return BotDifficulty.impossible;
                default:
                    return BotDifficulty.normal;
            }
        }

        internal static BotDifficulty grabOtherDifficulty()
        {
            switch (botDifficultiesOther.Value.ToLower())
            {
                case "asonline":
                    BotDifficulty[] randomDifficulty = { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard };
                    return randomDifficulty[UnityEngine.Random.Range(0, 3)];
                case "easy":
                    return BotDifficulty.easy;
                case "normal":
                    return BotDifficulty.normal;
                case "hard":
                    return BotDifficulty.hard;
                case "impossible":
                    return BotDifficulty.impossible;
                default:
                    return BotDifficulty.normal;
            }
        }

        #endregion

        internal static async UniTask SpawnBotFromCacheOrCreateNew(List<BotCacheClass> botCacheList, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 spawnPosition, CancellationTokenSource cancellationTokenSource, BotDifficulty botDifficulty, HotspotTimer hotspotTimer)
        {
#if DEBUG
            DonutComponent.Logger.LogDebug("Finding Cached Bot");
#endif
            var botCacheElement = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, 1);

            if (botCacheElement != null)
            {
                await ActivateBotFromCache(botCacheElement, spawnPosition, cancellationTokenSource, hotspotTimer);
            }
            else
            {
#if DEBUG
                DonutComponent.Logger.LogDebug("Bot Cache is empty for solo bot. Creating a new bot.");
#endif
                await CreateNewBot(wildSpawnType, side, ibotCreator, botSpawnerClass, spawnPosition, cancellationTokenSource);
            }
        }

        private static async UniTask ActivateBotFromCache(BotCacheClass botCacheElement, Vector3 spawnPosition, CancellationTokenSource cancellationTokenSource, HotspotTimer hotspotTimer)
        {
            var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
            var closestCorePoint = GetClosestCorePoint(spawnPosition);
            botCacheElement.AddPosition(spawnPosition, closestCorePoint.Id);

#if DEBUG
            DonutComponent.Logger.LogWarning($"Spawning bot at distance to player of: {Vector3.Distance(spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                             $"of side: {botCacheElement.Side} for hotspot {hotspotTimer.Hotspot.Name} ");
#endif
            await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource);
        }

        internal static async UniTask SpawnBotForGroup(BotCacheClass botCacheElement, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 spawnPosition, CancellationTokenSource cancellationTokenSource, BotDifficulty botDifficulty, int maxCount, HotspotTimer hotspotTimer)
        {
            if (botCacheElement != null)
            {
                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
                var closestCorePoint = GetClosestCorePoint(spawnPosition);
                botCacheElement.AddPosition(spawnPosition, closestCorePoint.Id);

#if DEBUG
                DonutComponent.Logger.LogWarning($"Spawning grouped bots at distance to player of: {Vector3.Distance(spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                                 $"of side: {botCacheElement.Side} and difficulty: {botDifficulty} at hotspot: {hotspotTimer.Hotspot.Name}");
#endif

                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource);
            }
            else
            {
                DonutComponent.Logger.LogError("Attempted to spawn a group bot but the botCacheElement was null.");
            }
        }

        internal static async UniTask CreateNewBot(WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator, BotSpawner botSpawnerClass, Vector3 spawnPosition, CancellationTokenSource cancellationTokenSource)
        {
            BotDifficulty botdifficulty = GetBotDifficulty(wildSpawnType, (WildSpawnType)AkiBotsPrePatcher.sptUsecValue, (WildSpawnType)AkiBotsPrePatcher.sptBearValue);

            IProfileData botData = new IProfileData(side, wildSpawnType, botdifficulty, 0f, null);
            BotCacheClass bot = await BotCacheClass.Create(botData, ibotCreator, 1, botSpawnerClass);
            var closestCorePoint = GetClosestCorePoint(spawnPosition);
            bot.AddPosition(spawnPosition, closestCorePoint.Id);

            var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
#if DEBUG
            DonutComponent.Logger.LogWarning($"Spawning bot at distance to player of: {Vector3.Distance(spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                             $"of side: {bot.Side} and difficulty: {botdifficulty}");
#endif

            await ActivateBot(closestBotZone, bot, cancellationTokenSource);
        }

        internal static async UniTask ActivateBot(BotZone botZone, BotCacheClass botData, CancellationTokenSource cancellationTokenSource)
        {
            CreateBotCallbackWrapper createBotCallbackWrapper = new CreateBotCallbackWrapper
            {
                botData = botData
            };

            GetGroupWrapper getGroupWrapper = new GetGroupWrapper();

            botCreator.ActivateBot(botData, botZone, false, new Func<BotOwner, BotZone, BotsGroup>(getGroupWrapper.GetGroupAndSetEnemies), new Action<BotOwner>(createBotCallbackWrapper.CreateBotCallback), cancellationTokenSource.Token);
            ClearBotCacheAfterActivation(botData);
        }

        internal static void ClearBotCacheAfterActivation(BotCacheClass botData)
        {
            var botInfo = DonutsBotPrep.BotInfos.FirstOrDefault(b => b.Bots == botData);
            if (botInfo != null)
            {
                DonutsBotPrep.timeSinceLastReplenish = 0f;
                botInfo.Bots = null;
#if DEBUG
                DonutComponent.Logger.LogDebug($"Cleared cached bot info for bot type: {botInfo.SpawnType}");
#endif
            }
        }

        internal static bool IsWithinBotActivationDistance(Entry hotspot, Vector3 position)
        {
            try
            {
                foreach (var player in playerList)
                {
                    if (player?.HealthController == null || !player.HealthController.IsAlive) continue;

                    float distanceSquared = (player.Position - position).sqrMagnitude;
                    float activationDistanceSquared = hotspot.BotTriggerDistance * hotspot.BotTriggerDistance;

                    if (distanceSquared <= activationDistanceSquared)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        internal static WildSpawnType GetWildSpawnType(string spawnType, WildSpawnType sptUsec, WildSpawnType sptBear)
        {
            switch (spawnType.ToLower())
            {
                case "arenafighterevent":
                    return WildSpawnType.arenaFighterEvent;
                case "assault":
                    return WildSpawnType.assault;
                case "assaultgroup":
                    return WildSpawnType.assaultGroup;
                case "bossboar":
                    return WildSpawnType.bossBoar;
                case "bossboarsniper":
                    return WildSpawnType.bossBoarSniper;
                case "bossbully":
                    return WildSpawnType.bossBully;
                case "bossgluhar":
                    return WildSpawnType.bossGluhar;
                case "bosskilla":
                    return WildSpawnType.bossKilla;
                case "bosskojaniy":
                    return WildSpawnType.bossKojaniy;
                case "bosssanitar":
                    return WildSpawnType.bossSanitar;
                case "bosstagilla":
                    return WildSpawnType.bossTagilla;
                case "bosszryachiy":
                    return WildSpawnType.bossZryachiy;
                case "crazyassaultevent":
                    return WildSpawnType.crazyAssaultEvent;
                case "cursedassault":
                    return WildSpawnType.cursedAssault;
                case "exusec":
                    return WildSpawnType.exUsec;
                case "followerboar":
                    return WildSpawnType.followerBoar;
                case "followerbully":
                    return WildSpawnType.followerBully;
                case "followergluharassault":
                    return WildSpawnType.followerGluharAssault;
                case "followergluharscout":
                    return WildSpawnType.followerGluharScout;
                case "followergluharsecurity":
                    return WildSpawnType.followerGluharSecurity;
                case "followergluharsnipe":
                    return WildSpawnType.followerGluharSnipe;
                case "followerkojaniy":
                    return WildSpawnType.followerKojaniy;
                case "followersanitar":
                    return WildSpawnType.followerSanitar;
                case "followertagilla":
                    return WildSpawnType.followerTagilla;
                case "followerzryachiy":
                    return WildSpawnType.followerZryachiy;
                case "gifter":
                    return WildSpawnType.gifter;
                case "marksman":
                    return WildSpawnType.marksman;
                case "raiders":
                    return WildSpawnType.pmcBot;
                case "sectantpriest":
                    return WildSpawnType.sectantPriest;
                case "sectantwarrior":
                    return WildSpawnType.sectantWarrior;
                case "usec":
                case "sptusec":
                    return sptUsec;
                case "bear":
                case "sptbear":
                    return sptBear;
                case "followerbigpipe":
                    return WildSpawnType.followerBigPipe;
                case "followerbirdeye":
                    return WildSpawnType.followerBirdEye;
                case "bossknight":
                    return WildSpawnType.bossKnight;
                case "pmc":
                    return UnityEngine.Random.Range(0, 2) == 0 ? sptUsec : sptBear;
                default:
                    return WildSpawnType.assault;
            }
        }

        internal static EPlayerSide GetSideForWildSpawnType(WildSpawnType spawnType, WildSpawnType sptUsec, WildSpawnType sptBear)
        {
            if (spawnType == WildSpawnType.pmcBot || spawnType == sptUsec)
            {
                return EPlayerSide.Usec;
            }
            else if (spawnType == sptBear)
            {
                return EPlayerSide.Bear;
            }
            else
            {
                return EPlayerSide.Savage;
            }
        }

        #endregion

        #region botGroups

        internal static int getActualBotCount(string pluginGroupChance, int count)
        {
            if (pluginGroupChance == "None")
            {
                return 1;
            }
            if (pluginGroupChance == "Max")
            {
                return count;
            }
            if (pluginGroupChance == "Random")
            {
                string[] groupChances = { "None", "Low", "Default", "High", "Max" };
                pluginGroupChance = groupChances[UnityEngine.Random.Range(0, groupChances.Length)];
            }
            else
            {
                int actualGroupChance = getGroupChance(pluginGroupChance, count);
                return actualGroupChance;
            }

            return getActualBotCount(pluginGroupChance, count);
        }

        internal static int getGroupChance(string pmcGroupChance, int maxCount)
        {
            double[] probabilities = GetProbabilityArray(pmcGroupChance) ?? GetDefaultProbabilityArray(pmcGroupChance);
            System.Random random = new System.Random();
            return getOutcomeWithProbability(random, probabilities, maxCount) + 1;
        }

        internal static double[] GetProbabilityArray(string pmcGroupChance)
        {
            if (groupChanceWeights.TryGetValue(pmcGroupChance, out var relativeWeights))
            {
                double totalWeight = relativeWeights.Sum();
                return relativeWeights.Select(weight => weight / totalWeight).ToArray();
            }

            throw new ArgumentException($"Invalid pmcGroupChance: {pmcGroupChance}");
        }

        internal static double[] GetDefaultProbabilityArray(string pmcGroupChance)
        {
            if (groupChanceWeights.TryGetValue(pmcGroupChance, out var relativeWeights))
            {
                double totalWeight = relativeWeights.Sum();
                return relativeWeights.Select(weight => weight / totalWeight).ToArray();
            }

            throw new ArgumentException($"Invalid pmcGroupChance: {pmcGroupChance}");
        }

        internal static int getOutcomeWithProbability(System.Random random, double[] probabilities, int maxCount)
        {
            double probabilitySum = probabilities.Sum();
            if (Math.Abs(probabilitySum - 1.0) > 0.0001)
            {
                throw new InvalidOperationException("Probabilities should sum up to 1.");
            }

            double probabilityThreshold = random.NextDouble();
            double cumulative = 0.0;
            for (int i = 0; i < maxCount; i++)
            {
                cumulative += probabilities[i];
                if (probabilityThreshold < cumulative)
                {
                    return i;
                }
            }
            return maxCount - 1;
        }

        #endregion
    }
}
