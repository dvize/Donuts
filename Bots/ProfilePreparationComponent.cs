using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aki.PrePatch;
using BepInEx.Logging;
using Comfort.Common;
using Donuts.Models;
using EFT;
using HarmonyLib;
using UnityEngine;
using Cysharp.Threading.Tasks;

using BotCacheClass = GClass591;
using IProfileData = GClass592;

#pragma warning disable IDE0007, CS4014

namespace Donuts
{
    internal class DonutsBotPrep : MonoBehaviour
    {
        private static GameWorld gameWorld;
        private static IBotCreator botCreator;
        private static BotSpawner botSpawnerClass;
        private static Player mainplayer;

        internal static Dictionary<string, WildSpawnType> OriginalBotSpawnTypes;

        private static WildSpawnType sptUsec;
        private static WildSpawnType sptBear;

        public static List<PrepBotInfo> BotInfos
        {
            get; set;
        }

        internal static float timeSinceLastReplenish = 0f;

        private bool isReplenishing = false;
        public static bool IsBotPreparationComplete { get; private set; } = false;

        private readonly Dictionary<WildSpawnType, EPlayerSide> spawnTypeToSideMapping = new Dictionary<WildSpawnType, EPlayerSide>
        {
            { WildSpawnType.arenaFighterEvent, EPlayerSide.Savage },
            { WildSpawnType.assault, EPlayerSide.Savage },
            { WildSpawnType.assaultGroup, EPlayerSide.Savage },
            { WildSpawnType.bossBoar, EPlayerSide.Savage },
            { WildSpawnType.bossBoarSniper, EPlayerSide.Savage },
            { WildSpawnType.bossBully, EPlayerSide.Savage },
            { WildSpawnType.bossGluhar, EPlayerSide.Savage },
            { WildSpawnType.bossKilla, EPlayerSide.Savage },
            { WildSpawnType.bossKojaniy, EPlayerSide.Savage },
            { WildSpawnType.bossSanitar, EPlayerSide.Savage },
            { WildSpawnType.bossTagilla, EPlayerSide.Savage },
            { WildSpawnType.bossZryachiy, EPlayerSide.Savage },
            { WildSpawnType.crazyAssaultEvent, EPlayerSide.Savage },
            { WildSpawnType.cursedAssault, EPlayerSide.Savage },
            { WildSpawnType.exUsec, EPlayerSide.Savage },
            { WildSpawnType.followerBoar, EPlayerSide.Savage },
            { WildSpawnType.followerBully, EPlayerSide.Savage },
            { WildSpawnType.followerGluharAssault, EPlayerSide.Savage },
            { WildSpawnType.followerGluharScout, EPlayerSide.Savage },
            { WildSpawnType.followerGluharSecurity, EPlayerSide.Savage },
            { WildSpawnType.followerGluharSnipe, EPlayerSide.Savage },
            { WildSpawnType.followerKojaniy, EPlayerSide.Savage },
            { WildSpawnType.followerSanitar, EPlayerSide.Savage },
            { WildSpawnType.followerTagilla, EPlayerSide.Savage },
            { WildSpawnType.followerZryachiy, EPlayerSide.Savage },
            { WildSpawnType.marksman, EPlayerSide.Savage },
            { WildSpawnType.pmcBot, EPlayerSide.Savage },
            { WildSpawnType.sectantPriest, EPlayerSide.Savage },
            { WildSpawnType.sectantWarrior, EPlayerSide.Savage },
            { WildSpawnType.followerBigPipe, EPlayerSide.Savage },
            { WildSpawnType.followerBirdEye, EPlayerSide.Savage },
            { WildSpawnType.bossKnight, EPlayerSide.Savage },
        };

        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public DonutsBotPrep()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(DonutsBotPrep));
        }

        public static void Enable()
        {
            gameWorld = Singleton<GameWorld>.Instance;
            gameWorld.GetOrAddComponent<DonutsBotPrep>();

            Logger.LogDebug("DonutBotPrep Enabled");
        }

        public async void Awake()
        {
            botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            botCreator = AccessTools.Field(typeof(BotSpawner), "_botCreator").GetValue(botSpawnerClass) as IBotCreator;
            mainplayer = gameWorld.MainPlayer;
            OriginalBotSpawnTypes = new Dictionary<string, WildSpawnType>();
            BotInfos = new List<PrepBotInfo>();
            timeSinceLastReplenish = 0;
            IsBotPreparationComplete = false;

            sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;

            botSpawnerClass.OnBotRemoved += (BotOwner bot) =>
            {
                OriginalBotSpawnTypes.Remove(bot.Profile.Id);
            };

            botSpawnerClass.OnBotCreated += (BotOwner bot) =>
            {
                bot.Memory.OnGoalEnemyChanged += Memory_OnGoalEnemyChanged;
            };

            if (mainplayer != null)
            {
                Logger.LogDebug("Mainplayer is not null, attaching event handlers");
                mainplayer.BeingHitAction += Mainplayer_BeingHitAction;
            }

            await InitializeAllBotInfos();
            IsBotPreparationComplete = true;
        }

        private void Memory_OnGoalEnemyChanged(BotOwner owner)
        {
            if (owner != null && owner.Memory != null && owner.Memory.GoalEnemy != null && owner.Memory.HaveEnemy)
            {
                if (owner.Memory.GoalEnemy.HaveSeenPersonal && owner.Memory.GoalEnemy.IsVisible)
                {
                    timeSinceLastReplenish = 0f;
                }
            }
        }

        private void Mainplayer_BeingHitAction(DamageInfo arg1, EBodyPart arg2, float arg3)
        {
            switch (arg1.DamageType)
            {
                case EDamageType.Btr:
                case EDamageType.Melee:
                case EDamageType.Bullet:
                case EDamageType.Explosion:
                case EDamageType.GrenadeFragment:
                case EDamageType.Sniper:
                    timeSinceLastReplenish = 0f;
                    break;
                default:
                    break;
            }
        }

        private async UniTask InitializeAllBotInfos()
        {
            await UniTask.WhenAll(InitializeBotInfos(), InitializeScavBotInfos());
        }

        private async UniTask InitializeBotInfos()
        {
            string difficultySetting = DefaultPluginVars.botDifficultiesPMC.Value.ToLower();
            string pmcGroupChance = DefaultPluginVars.pmcGroupChance.Value;

            List<BotDifficulty> difficultiesForSetting = GetDifficultiesForSetting(difficultySetting);
            int[] groupSizes = DetermineGroupSizes(pmcGroupChance, "PMC");

            foreach (var difficulty in difficultiesForSetting)
            {
                if (groupSizes.Length == 0)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var botInfoUsec = new PrepBotInfo(sptUsec, difficulty, EPlayerSide.Usec, false, 1);
                        await CreateBot(botInfoUsec, botInfoUsec.IsGroup, botInfoUsec.GroupSize);
                        BotInfos.Add(botInfoUsec);

                        var botInfoBear = new PrepBotInfo(sptBear, difficulty, EPlayerSide.Bear, false, 1);
                        await CreateBot(botInfoBear, botInfoBear.IsGroup, botInfoBear.GroupSize);
                        BotInfos.Add(botInfoBear);
                    }
                }

                foreach (int groupSize in groupSizes)
                {
                    var botInfoUsecGroup = new PrepBotInfo(sptUsec, difficulty, EPlayerSide.Usec, true, groupSize);
                    await CreateBot(botInfoUsecGroup, botInfoUsecGroup.IsGroup, botInfoUsecGroup.GroupSize);
                    BotInfos.Add(botInfoUsecGroup);

                    var botInfoBearGroup = new PrepBotInfo(sptBear, difficulty, EPlayerSide.Bear, true, groupSize);
                    await CreateBot(botInfoBearGroup, botInfoBearGroup.IsGroup, botInfoBearGroup.GroupSize);
                    BotInfos.Add(botInfoBearGroup);
                }
            }
        }

        private async UniTask InitializeScavBotInfos()
        {
            string difficultySetting = DefaultPluginVars.botDifficultiesSCAV.Value.ToLower();
            string scavGroupChance = DefaultPluginVars.scavGroupChance.Value;

            List<BotDifficulty> difficultiesForSetting = GetDifficultiesForSetting(difficultySetting);
            int[] groupSizes = DetermineGroupSizes(scavGroupChance, "SCAV");

            foreach (var difficulty in difficultiesForSetting)
            {
                if (groupSizes.Length == 0)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var botInfo = new PrepBotInfo(WildSpawnType.assault, difficulty, EPlayerSide.Savage, false, 1);
                        await CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize);
                        BotInfos.Add(botInfo);
                    }
                }

                foreach (int groupSize in groupSizes)
                {
                    var botInfo = new PrepBotInfo(WildSpawnType.assault, difficulty, EPlayerSide.Savage, true, groupSize);
                    await CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize);
                    BotInfos.Add(botInfo);
                }
            }
        }

        private List<BotDifficulty> GetDifficultiesForSetting(string difficultySetting)
        {
            switch (difficultySetting)
            {
                case "asonline":
                    return new List<BotDifficulty> { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard };
                case "easy":
                    return new List<BotDifficulty> { BotDifficulty.easy };
                case "normal":
                    return new List<BotDifficulty> { BotDifficulty.normal };
                case "hard":
                    return new List<BotDifficulty> { BotDifficulty.hard };
                case "impossible":
                    return new List<BotDifficulty> { BotDifficulty.impossible };
                default:
                    Logger.LogError("Unsupported difficulty setting: " + difficultySetting);
                    return new List<BotDifficulty>();
            }
        }

        private int[] DetermineGroupSizes(string groupChance, string botType)
        {
            switch (botType.ToLower())
            {
                case "pmc":
                    return groupChance.ToLower() switch
                    {
                        "none" => Array.Empty<int>(),
                        "low" => new int[] { 1, 1, 2 },
                        "max" => new int[] { 5, 5 },
                        "high" => new int[] { 3, 4, 5 },
                        _ => new int[] { 1, 2, 3 },
                    };
                case "scav":
                    return groupChance.ToLower() switch
                    {
                        "none" => Array.Empty<int>(),
                        "low" => new int[] { 1, 2 },
                        "max" => new int[] { 3, 4 },
                        "high" => new int[] { 2, 3 },
                        _ => new int[] { 1, 1, 2 },
                    };
                default:
                    throw new ArgumentException("Invalid bot type provided.");
            }
        }

        private void Update()
        {
            timeSinceLastReplenish += Time.deltaTime;
            if (timeSinceLastReplenish >= DefaultPluginVars.replenishInterval.Value && !isReplenishing)
            {
                timeSinceLastReplenish = 0f;
                ReplenishAllBots().Forget();
            }
        }

        private async UniTask ReplenishAllBots()
        {
            isReplenishing = true;
            int singleBotsCount = 0;
            int groupBotsCount = 0;

            var safeBotInfos = new List<PrepBotInfo>(BotInfos);

            foreach (var botInfo in safeBotInfos)
            {
                if (NeedReplenishment(botInfo))
                {
                    if (botInfo.IsGroup && groupBotsCount < 1)
                    {
#if DEBUG
                        Logger.LogWarning($"Replenishing group bot: {botInfo.SpawnType} {botInfo.Difficulty} {botInfo.Side} Count: {botInfo.GroupSize}");
#endif
                        await CreateBot(botInfo, true, botInfo.GroupSize);
                        groupBotsCount++;
                    }
                    else if (!botInfo.IsGroup && singleBotsCount < 3)
                    {
#if DEBUG
                        Logger.LogWarning($"Replenishing single bot: {botInfo.SpawnType} {botInfo.Difficulty} {botInfo.Side} Count: 1");
#endif
                        await CreateBot(botInfo, false, 1);
                        singleBotsCount++;
                    }

                    if (singleBotsCount >= 3 && groupBotsCount >= 1)
                        break;
                }
            }

            isReplenishing = false;
        }

        private static bool NeedReplenishment(PrepBotInfo botInfo)
        {
            return botInfo.Bots == null || botInfo.Bots.Profiles.Count == 0;
        }

        internal static async UniTask CreateBot(PrepBotInfo botInfo, bool isGroup, int groupSize)
        {
            var botData = new IProfileData(botInfo.Side, botInfo.SpawnType, botInfo.Difficulty, 0f, null);
#if DEBUG
            Logger.LogDebug($"Creating bot: Type={botInfo.SpawnType}, Difficulty={botInfo.Difficulty}, Side={botInfo.Side}, GroupSize={groupSize}");
#endif
            BotCacheClass bot = await BotCacheClass.Create(botData, botCreator, groupSize, botSpawnerClass);
            if (bot == null || bot.Profiles == null || !bot.Profiles.Any())
            {
#if DEBUG
                Logger.LogError($"Failed to create or properly initialize bot for {botInfo.SpawnType}");
#endif
                return;
            }

            botInfo.Bots = bot;
#if DEBUG
            Logger.LogDebug($"Bot created and assigned successfully: {bot.Profiles.Count} profiles loaded.");
#endif
        }

        public static BotCacheClass FindCachedBots(WildSpawnType spawnType, BotDifficulty difficulty, int targetCount)
        {
            if (DonutsBotPrep.BotInfos == null)
            {
                DonutComponent.Logger.LogError("BotInfos is null");
                return null;
            }

            try
            {
                // Find the bot info that matches the spawn type and difficulty
                var botInfo = DonutsBotPrep.BotInfos.FirstOrDefault(b => b.SpawnType == spawnType && b.Difficulty == difficulty && b.Bots != null && b.Bots.Profiles.Count == targetCount);

                if (botInfo != null)
                {
                    return botInfo.Bots;
                }

                DonutComponent.Logger.LogWarning($"No cached bots found for spawn type {spawnType}, difficulty {difficulty}, and target count {targetCount}");
                return null;
            }
            catch (Exception ex)
            {
                DonutComponent.Logger.LogError($"Exception in FindCachedBots: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        public static List<BotCacheClass> GetWildSpawnData(WildSpawnType spawnType, BotDifficulty botDifficulty)
        {
            return BotInfos
                .Where(b => b.SpawnType == spawnType && b.Difficulty == botDifficulty)
                .Select(b => b.Bots)
                .ToList();
        }

        internal static WildSpawnType? GetOriginalSpawnTypeForBot(BotOwner bot)
        {
            var originalProfile = OriginalBotSpawnTypes.First(profile => profile.Key == bot.Profile.Id);

            if (originalProfile.Key != null)
            {
#if DEBUG
                Logger.LogWarning("Found original profile for bot " + bot.Profile.Nickname + " as " + originalProfile.Value.ToString());
#endif
                return originalProfile.Value;
            }
            else
            {
#if DEBUG
                Logger.LogWarning("Could not find original profile for bot " + bot.Profile.Nickname);
#endif
                return null;
            }
        }

        private void OnDestroy()
        {
            if (botSpawnerClass != null)
            {
                botSpawnerClass.OnBotRemoved -= (BotOwner bot) =>
                {
                    OriginalBotSpawnTypes.Remove(bot.Profile.Id);
                };
            }

            botSpawnerClass.OnBotCreated -= (BotOwner bot) =>
            {
                bot.Memory.OnGoalEnemyChanged -= Memory_OnGoalEnemyChanged;
            };

            if (mainplayer != null)
            {
                mainplayer.BeingHitAction -= Mainplayer_BeingHitAction;
            }

            isReplenishing = false;
            timeSinceLastReplenish = 0;
            IsBotPreparationComplete = false;

            gameWorld = null;
            botCreator = null;
            botSpawnerClass = null;
            mainplayer = null;
            OriginalBotSpawnTypes = null;
            BotInfos = null;

#if DEBUG
            Logger.LogWarning("DonutsBotPrep component cleaned up and disabled.");
#endif
        }
    }
}
