using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Donuts.Models;
using EFT;
using HarmonyLib;
using UnityEngine;
using static Donuts.DefaultPluginVars;
using static Donuts.DonutComponent;
using IProfileData = GClass592;
using Random = System.Random;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class BotSpawn
    {
        private const string PmcSpawnTypes = "pmc,pmcUSEC,pmcBEAR";
        private const string ScavSpawnType = "assault";

        internal static AICorePoint GetClosestCorePoint(Vector3 position)
        {
            var botGame = Singleton<IBotGame>.Instance;
            var coversData = botGame.BotsController.CoversData;
            var groupPoint = coversData.GetClosest(position);
            return groupPoint.CorePointInGame;
        }

        internal static async UniTask SpawnBotsFromInfo(List<BotSpawnInfo> botSpawnInfos, CancellationToken cancellationToken)
        {
            foreach (var botSpawnInfo in botSpawnInfos)
            {
                await SpawnStartingBots(botSpawnInfo, cancellationToken);
            }
        }

        internal static async UniTask SpawnStartingBots(BotSpawnInfo botSpawnInfo, CancellationToken cancellationToken)
        {
            var wildSpawnType = botSpawnInfo.BotType;
            var side = botSpawnInfo.Faction;
            var groupSize = botSpawnInfo.GroupSize;
            var coordinates = botSpawnInfo.Coordinates;
            var botDifficulty = botSpawnInfo.Difficulty;
            var zone = botSpawnInfo.Zone;

            var cachedBotGroup = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, groupSize);

            if (cachedBotGroup == null)
            {
                DonutComponent.Logger.LogDebug("No starting bots found in cache for this spawn, need to generate data on the fly, this may take some time.");
                var botInfo = new PrepBotInfo(wildSpawnType, botDifficulty, side, groupSize > 1, groupSize);
                await DonutsBotPrep.CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize, cancellationToken);
                DonutsBotPrep.BotInfos.Add(botInfo);
                cachedBotGroup = botInfo.Bots;
            }

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();

            foreach (var coordinate in coordinates)
            {
                Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coordinate, maxSpawnTriesPerBot.Value, cancellationToken);
                if (!spawnPosition.HasValue)
                {
                    DonutComponent.Logger.LogDebug("No valid spawn position found - skipping this spawn");
                    return;
                }

                await ActivateStartingBots(cachedBotGroup, wildSpawnType, side, botCreator, botSpawnerClass, coordinate, botDifficulty, groupSize, zone, cancellationToken);
            }
        }

        internal static async UniTask ActivateStartingBots(BotCreationDataClass botCacheElement, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 spawnPosition, BotDifficulty botDifficulty, int maxCount, string zone, CancellationToken cancellationToken)
        {
            if (botCacheElement != null)
            {
                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out _);
                var closestCorePoint = GetClosestCorePoint(spawnPosition);
                botCacheElement.AddPosition(spawnPosition, closestCorePoint.Id);

#if DEBUG
                DonutComponent.Logger.LogWarning($"Spawning bots at distance to player of: {Vector3.Distance(spawnPosition, gameWorld.MainPlayer.Position)} " +
                                  $"of side: {botCacheElement.Side} and difficulty: {botDifficulty} in spawn zone: {zone}");
#endif

                var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
            }
            else
            {
                DonutComponent.Logger.LogError("Attempted to spawn a group bot but the botCacheElement was null.");
            }
        }

        internal static async UniTask SpawnBots(BotWave botWave, string zone, Vector3 coordinate, string wildSpawnType, CancellationToken cancellationToken)
        {
            WildSpawnType actualWildSpawnType = DetermineWildSpawnType(wildSpawnType);
            int maxCount = DetermineMaxBotCount(wildSpawnType, botWave.MinGroupSize, botWave.MaxGroupSize);

            // Check if hard cap is enabled and adjust maxCount based on active bot counts and limits
            if (HardCapEnabled.Value)
            {
                maxCount = await AdjustMaxCountForHardCap(wildSpawnType, maxCount, cancellationToken);
            }

            // Check respawn limits and adjust accordingly
            if (maxRespawnsPMC.Value != 0 || maxRespawnsSCAV.Value != 0)
            {
                maxCount = AdjustMaxCountForRespawnLimits(wildSpawnType, maxCount);
            }

            if (maxCount == 0)
            {
#if DEBUG
                DonutComponent.Logger.LogDebug($"Max bot count is 0, skipping spawn");
#endif
                return;
            }

            bool isGroup = maxCount > 1;
            await SetupSpawn(botWave, maxCount, isGroup, actualWildSpawnType, coordinate, zone, cancellationToken);
        }

        private static async UniTask<int> AdjustMaxCountForHardCap(string wildSpawnType, int maxCount, CancellationToken cancellationToken)
        {
            int activePMCs = await BotCountManager.GetAlivePlayers("pmc", cancellationToken);
            int activeSCAVs = await BotCountManager.GetAlivePlayers("scav", cancellationToken);

            if (wildSpawnType == "pmc")
            {
                if (activePMCs + maxCount > Initialization.PMCBotLimit)
                {
                    maxCount = Initialization.PMCBotLimit - activePMCs;
                }
            }
            else if (wildSpawnType == "scav")
            {
                if (activeSCAVs + maxCount > Initialization.SCAVBotLimit)
                {
                    maxCount = Initialization.SCAVBotLimit - activeSCAVs;
                }
            }

            return maxCount;
        }

        private static int AdjustMaxCountForRespawnLimits(string wildSpawnType, int maxCount)
        {
            if (wildSpawnType == "pmc" && !maxRespawnReachedPMC)
            {
                if (currentMaxPMC + maxCount >= maxRespawnsPMC.Value)
                {
#if DEBUG
                    DonutComponent.Logger.LogDebug($"Max PMC respawn limit reached: {maxRespawnsPMC.Value}. Current PMCs respawns this raid: {currentMaxPMC + maxCount}");
#endif
                    if (currentMaxPMC < maxRespawnsPMC.Value)
                    {
                        maxCount = maxRespawnsPMC.Value - currentMaxPMC;
                        maxRespawnReachedPMC = true;
                    }
                    else
                    {
                        maxRespawnReachedPMC = true;
                        return 0;
                    }
                    maxRespawnReachedPMC = true;
                }
                currentMaxPMC += maxCount;
                return maxCount;
            }

            if (wildSpawnType == "scav" && !maxRespawnReachedSCAV)
            {
                if (currentMaxSCAV + maxCount >= maxRespawnsSCAV.Value)
                {
#if DEBUG
                    DonutComponent.Logger.LogDebug($"Max SCAV respawn limit reached: {maxRespawnsSCAV.Value}. Current SCAVs respawns this raid: {currentMaxPMC + maxCount}");
#endif
                    if (currentMaxSCAV < maxRespawnsSCAV.Value)
                    {
                        maxCount = maxRespawnsSCAV.Value - currentMaxSCAV;
                        maxRespawnReachedSCAV = true;
                    }
                    else
                    {
                        maxRespawnReachedSCAV = true;
                        return 0;
                    }
                    maxRespawnReachedSCAV = true;
                }
                currentMaxSCAV += maxCount;
                return maxCount;
            }

            return maxCount;
        }

        public static int DetermineMaxBotCount(string spawnType, int defaultMinCount, int defaultMaxCount)
        {
            string groupChance = spawnType == "scav" ? scavGroupChance.Value : pmcGroupChance.Value;
            return getActualBotCount(groupChance, defaultMinCount, defaultMaxCount);
        }

        private static async UniTask SetupSpawn(BotWave botWave, int maxCount, bool isGroup, WildSpawnType wildSpawnType, Vector3 coordinate, string zone, CancellationToken cancellationToken)
        {
            DonutComponent.Logger.LogDebug($"Attempting to spawn {(isGroup ? "group" : "solo")} with bot count {maxCount} in spawn zone {zone}");
            if (isGroup)
            {
                await SpawnGroupBots(botWave, maxCount, wildSpawnType, coordinate, zone, cancellationToken);
            }
            else
            {
                await SpawnSingleBot(botWave, wildSpawnType, coordinate, zone, cancellationToken);
            }
        }

        private static async UniTask SpawnGroupBots(BotWave botWave, int count, WildSpawnType wildSpawnType, Vector3 coordinate, string zone, CancellationToken cancellationToken)
        {
#if DEBUG
            DonutComponent.Logger.LogDebug($"Spawning a group of {count} bots.");
#endif
            EPlayerSide side = GetSideForWildSpawnType(wildSpawnType, WildSpawnType.pmcUSEC, WildSpawnType.pmcBEAR);
            var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
            BotDifficulty botDifficulty = GetBotDifficulty(wildSpawnType, WildSpawnType.pmcUSEC, WildSpawnType.pmcBEAR);

            var cachedBotGroup = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, count);
            if (cachedBotGroup == null)
            {
#if DEBUG
                DonutComponent.Logger.LogWarning($"No cached bots found for this spawn, generating on the fly for {count} bots - this may take some time.");
#endif
                var botInfo = new PrepBotInfo(wildSpawnType, botDifficulty, side, true, count);
                await DonutsBotPrep.CreateBot(botInfo, true, count, cancellationToken);
                DonutsBotPrep.BotInfos.Add(botInfo);
                cachedBotGroup = botInfo.Bots;
            }
            else
            {
                DonutComponent.Logger.LogWarning("Found grouped cached bots, spawning them.");
            }

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();
            Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coordinate, maxSpawnTriesPerBot.Value, cancellationToken);
            if (!spawnPosition.HasValue)
            {
                DonutComponent.Logger.LogDebug("No valid spawn position found - skipping this spawn");
                return;
            }

            await SpawnBotForGroup(cachedBotGroup, wildSpawnType, side, botCreator, botSpawnerClass, spawnPosition.Value, cancellationTokenSource, botDifficulty, count, botWave, zone, cancellationToken);
        }

        private static async UniTask SpawnSingleBot(BotWave botWave, WildSpawnType wildSpawnType, Vector3 coordinate, string zone, CancellationToken cancellationToken)
        {
            EPlayerSide side = GetSideForWildSpawnType(wildSpawnType, WildSpawnType.pmcUSEC, WildSpawnType.pmcBEAR);
            var cancellationTokenSource = AccessTools.Field(typeof(BotSpawner), "_cancellationTokenSource").GetValue(botSpawnerClass) as CancellationTokenSource;
            BotDifficulty botDifficulty = GetBotDifficulty(wildSpawnType, WildSpawnType.pmcUSEC, WildSpawnType.pmcBEAR);
            var BotCacheDataList = DonutsBotPrep.GetWildSpawnData(wildSpawnType, botDifficulty);

            var minSpawnDistFromPlayer = SpawnChecks.GetMinDistanceFromPlayer();
            Vector3? spawnPosition = await SpawnChecks.GetValidSpawnPosition(minSpawnDistFromPlayer, 1, 1, coordinate, maxSpawnTriesPerBot.Value, cancellationToken);

            if (!spawnPosition.HasValue)
            {
                DonutComponent.Logger.LogDebug("No valid spawn position found - skipping this spawn");
                return;
            }

            await SpawnBotFromCacheOrCreateNew(BotCacheDataList, wildSpawnType, side, botCreator, botSpawnerClass, spawnPosition.Value, cancellationTokenSource, botDifficulty, botWave, zone, cancellationToken);
        }
        private static WildSpawnType DetermineWildSpawnType(string spawnType)
        {
            WildSpawnType determinedSpawnType = GetWildSpawnType(
                forceAllBotType.Value == "PMC" ? "pmc" :
                forceAllBotType.Value == "SCAV" ? "assault" :
                spawnType, WildSpawnType.pmcUSEC, WildSpawnType.pmcBEAR);

            return determinedSpawnType == GetWildSpawnType("pmc", WildSpawnType.pmcUSEC, WildSpawnType.pmcBEAR) ? DeterminePMCFactionBasedOnRatio(WildSpawnType.pmcUSEC, WildSpawnType.pmcBEAR) : determinedSpawnType;
        }

        public static WildSpawnType DeterminePMCFactionBasedOnRatio(WildSpawnType sptUsec, WildSpawnType sptBear)
        {
            int factionRatio = pmcFactionRatio.Value;
            Random rand = new Random();
            return rand.Next(100) < factionRatio ? WildSpawnType.pmcUSEC : WildSpawnType.pmcBEAR;
        }

        #region botHelperMethods

        #region botDifficulty
        internal static BotDifficulty GetBotDifficulty(WildSpawnType wildSpawnType, WildSpawnType sptUsec, WildSpawnType sptBear)
        {
            if (wildSpawnType == WildSpawnType.assault)
            {
                return grabSCAVDifficulty();
            }
            else if (wildSpawnType == WildSpawnType.pmcUSEC || wildSpawnType == WildSpawnType.pmcBEAR || wildSpawnType == WildSpawnType.pmcBot)
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

        internal static async UniTask SpawnBotFromCacheOrCreateNew(List<BotCreationDataClass> botCacheList, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 spawnPosition, CancellationTokenSource cancellationTokenSource, BotDifficulty botDifficulty, BotWave botWave, string zone, CancellationToken cancellationToken)
        {
#if DEBUG
            DonutComponent.Logger.LogDebug("Finding Cached Bot");
#endif
            var botCacheElement = DonutsBotPrep.FindCachedBots(wildSpawnType, botDifficulty, 1);

            if (botCacheElement != null)
            {
                await ActivateBotFromCache(botCacheElement, spawnPosition, cancellationTokenSource, botWave, zone, cancellationToken);
            }
            else
            {
#if DEBUG
                DonutComponent.Logger.LogDebug("Bot Cache is empty for solo bot. Creating a new bot.");
#endif
                await CreateNewBot(wildSpawnType, side, ibotCreator, botSpawnerClass, spawnPosition, cancellationTokenSource, zone, cancellationToken);
            }
        }

        private static async UniTask ActivateBotFromCache(BotCreationDataClass botCacheElement, Vector3 spawnPosition, CancellationTokenSource cancellationTokenSource, BotWave botWave, string zone, CancellationToken cancellationToken)
        {
            var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
            var closestCorePoint = GetClosestCorePoint(spawnPosition);
            botCacheElement.AddPosition(spawnPosition, closestCorePoint.Id);

#if DEBUG
            DonutComponent.Logger.LogWarning($"Spawning bot at distance to player of: {Vector3.Distance(spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                             $"of side: {botCacheElement.Side} in spawn zone {zone}");
#endif
            await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
        }

        internal static async UniTask SpawnBotForGroup(BotCreationDataClass botCacheElement, WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator,
            BotSpawner botSpawnerClass, Vector3 spawnPosition, CancellationTokenSource cancellationTokenSource, BotDifficulty botDifficulty, int maxCount, BotWave botWave, string zone, CancellationToken cancellationToken)
        {
            if (botCacheElement != null)
            {
                var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
                var closestCorePoint = GetClosestCorePoint(spawnPosition);
                botCacheElement.AddPosition(spawnPosition, closestCorePoint.Id);

#if DEBUG
                DonutComponent.Logger.LogWarning($"Spawning grouped bots at distance to player of: {Vector3.Distance(spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                                 $"of side: {botCacheElement.Side} and difficulty: {botDifficulty} in spawn zone {zone}");
#endif

                await ActivateBot(closestBotZone, botCacheElement, cancellationTokenSource, cancellationToken);
            }
            else
            {
                DonutComponent.Logger.LogError("Attempted to spawn a group bot but the botCacheElement was null.");
            }
        }

        internal static async UniTask CreateNewBot(WildSpawnType wildSpawnType, EPlayerSide side, IBotCreator ibotCreator, BotSpawner botSpawnerClass, Vector3 spawnPosition, CancellationTokenSource cancellationTokenSource, string zone, CancellationToken cancellationToken)
        {
            BotDifficulty botdifficulty = GetBotDifficulty(wildSpawnType, WildSpawnType.pmcUSEC, WildSpawnType.pmcBEAR);

            IProfileData botData = new IProfileData(side, wildSpawnType, botdifficulty, 0f, null);
            BotCreationDataClass bot = await BotCreationDataClass.Create(botData, ibotCreator, 1, botSpawnerClass);
            var closestCorePoint = GetClosestCorePoint(spawnPosition);
            bot.AddPosition(spawnPosition, closestCorePoint.Id);

            var closestBotZone = botSpawnerClass.GetClosestZone(spawnPosition, out float dist);
#if DEBUG
            DonutComponent.Logger.LogWarning($"Spawning bot at distance to player of: {Vector3.Distance(spawnPosition, DonutComponent.gameWorld.MainPlayer.Position)} " +
                                             $"of side: {bot.Side} and difficulty: {botdifficulty} in spawn zone {zone}");
#endif

            await ActivateBot(closestBotZone, bot, cancellationTokenSource, cancellationToken);
        }

        internal static async UniTask ActivateBot(BotZone botZone, BotCreationDataClass botData, CancellationTokenSource cancellationTokenSource, CancellationToken cancellationToken)
        {
            CreateBotCallbackWrapper createBotCallbackWrapper = new CreateBotCallbackWrapper
            {
                botData = botData
            };

            GetGroupWrapper getGroupWrapper = new GetGroupWrapper();

            botCreator.ActivateBot(botData, botZone, false, new Func<BotOwner, BotZone, BotsGroup>(getGroupWrapper.GetGroupAndSetEnemies), new Action<BotOwner>(createBotCallbackWrapper.CreateBotCallback), cancellationTokenSource.Token);
            await ClearBotCacheAfterActivation(botData);
        }

        internal static UniTask ClearBotCacheAfterActivation(BotCreationDataClass botData)
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

            return UniTask.CompletedTask;
        }

        internal static bool IsWithinBotActivationDistance(BotWave botWave, Vector3 position)
        {
            try
            {
                foreach (var player in playerList)
                {
                    if (player?.HealthController == null || !player.HealthController.IsAlive) continue;

                    float distanceSquared = (player.Position - position).sqrMagnitude;
                    float activationDistanceSquared = botWave.TriggerDistance * botWave.TriggerDistance;

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
                case "pmcUSEC":
                    return WildSpawnType.pmcUSEC;
                case "bear":
                case "pmcBEAR":
                    return WildSpawnType.pmcBEAR;
                case "followerbigpipe":
                    return WildSpawnType.followerBigPipe;
                case "followerbirdeye":
                    return WildSpawnType.followerBirdEye;
                case "bossknight":
                    return WildSpawnType.bossKnight;
                case "pmc":
                    return UnityEngine.Random.Range(0, 2) == 0 ? WildSpawnType.pmcUSEC : WildSpawnType.pmcBEAR;
                default:
                    return WildSpawnType.assault;
            }
        }

        internal static EPlayerSide GetSideForWildSpawnType(WildSpawnType spawnType, WildSpawnType sptUsec, WildSpawnType sptBear)
        {
            if (spawnType == WildSpawnType.pmcBot || spawnType == WildSpawnType.pmcUSEC)
            {
                return EPlayerSide.Usec;
            }
            else if (spawnType == WildSpawnType.pmcBEAR)
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

        internal static int getActualBotCount(string pluginGroupChance, int minGroupSize, int maxGroupSize)
        {
            InitializeGroupChanceWeights();

            if (pluginGroupChance == "Random")
            {
                string[] groupChances = { "None", "Low", "Default", "High", "Max" };
                pluginGroupChance = groupChances[UnityEngine.Random.Range(0, groupChances.Length)];
            }

            return pluginGroupChance switch
            {
                "None" => minGroupSize,
                "Max" => maxGroupSize,
                _ => getGroupChance(pluginGroupChance, minGroupSize, maxGroupSize)
            };
        }

        internal static int getGroupChance(string pmcGroupChance, int minGroupSize, int maxGroupSize)
        {
            double[] probabilities = GetProbabilityArray(pmcGroupChance) ?? GetDefaultProbabilityArray(pmcGroupChance);
            System.Random random = new System.Random();
            return getOutcomeWithProbability(random, probabilities, minGroupSize, maxGroupSize) + minGroupSize;
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

        internal static int getOutcomeWithProbability(System.Random random, double[] probabilities, int minGroupSize, int maxGroupSize)
        {
            double probabilitySum = probabilities.Sum();
            if (Math.Abs(probabilitySum - 1.0) > 0.0001)
            {
                throw new InvalidOperationException("Probabilities should sum up to 1.");
            }

            double probabilityThreshold = random.NextDouble();
            double cumulative = 0.0;
            int adjustedMaxCount = maxGroupSize - minGroupSize;
            for (int i = 0; i <= adjustedMaxCount; i++)
            {
                cumulative += probabilities[i];
                if (probabilityThreshold < cumulative)
                {
                    return i;
                }
            }
            return adjustedMaxCount;
        }

        internal static void InitializeGroupChanceWeights()
        {
            int[] defaultWeights = ParseGroupWeightDistro(groupWeightDistroDefault.Value);
            int[] lowWeights = ParseGroupWeightDistro(groupWeightDistroLow.Value);
            int[] highWeights = ParseGroupWeightDistro(groupWeightDistroHigh.Value);

            groupChanceWeights["Default"] = defaultWeights;
            groupChanceWeights["Low"] = lowWeights;
            groupChanceWeights["High"] = highWeights;
        }

        internal static int[] ParseGroupWeightDistro(string weightsString)
        {
            // Use the Split(char[]) method and manually remove empty entries
            return weightsString.Split(new char[] { ',' })
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(int.Parse)
                                .ToArray();
        }

        #endregion
    }
}
