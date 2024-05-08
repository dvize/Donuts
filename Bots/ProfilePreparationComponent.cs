using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aki.PrePatch;
using BepInEx.Logging;
using Comfort.Common;
using Donuts.Models;
using EFT;
using HarmonyLib;
using UnityEngine;

//custom usings
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

        //use dictionary of profile.id and wildspawntype
        internal static Dictionary<string, WildSpawnType> OriginalBotSpawnTypes;

        private static WildSpawnType sptUsec;
        private static WildSpawnType sptBear;

        public static List<PrepBotInfo> BotInfos { get; set; }

        internal static float timeSinceLastReplenish = 0f;

        private bool isReplenishing = false;
        public static bool IsBotPreparationComplete { get; private set; } = false;

        private Dictionary<WildSpawnType, EPlayerSide> spawnTypeToSideMapping = new Dictionary<WildSpawnType, EPlayerSide>
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
            if (Logger == null)
            {
                Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(DonutsBotPrep));
            }
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
                // Remove bot from OriginalBotSpawnTypes dictionary
                OriginalBotSpawnTypes.Remove(bot.Profile.Id);
            };

            botSpawnerClass.OnBotCreated += (BotOwner bot) =>
            {
                //attach to see if goalenemy is mainplayer, meaning they see us and probably in combat
                bot.Memory.OnGoalEnemyChanged += Memory_OnGoalEnemyChanged;
            };

            //nullcheck mainplayer, might be too early
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
            // null checks for dying bots
            if (owner != null && owner.Memory != null && owner.Memory.GoalEnemy != null && owner.Memory.HaveEnemy)
            {
                if (owner.Memory.GoalEnemy.HaveSeenPersonal && owner.Memory.GoalEnemy.IsVisible)
                {
                    // Stop Replenishing bots when player in combat (when you can be shot at)
#if DEBUG
                    Logger.LogWarning("Bot set goal enemy as you, resetting replenishment timer.");
#endif
                    timeSinceLastReplenish = 0f;
                }
            }

        }

        private void Mainplayer_BeingHitAction(DamageInfo arg1, EBodyPart arg2, float arg3)
        {
            // Stop Replenishing bots when player in combat (when shot at)
            switch (arg1.DamageType)
            {
                case EDamageType.Btr:
                case EDamageType.Melee:
                case EDamageType.Bullet:
                case EDamageType.Explosion:
                case EDamageType.GrenadeFragment:
                case EDamageType.Sniper:
#if DEBUG
                    Logger.LogWarning("You were hit and in active combat, resetting replenishment timer.");
#endif
                    timeSinceLastReplenish = 0f;
                    break;
                default:
                    break;
            }
        }

        private async Task InitializeAllBotInfos()
        {
            await Task.WhenAll(InitializeBotInfos(), InitializeScavBotInfos());
        }
        private async Task InitializeBotInfos()
        {
            string difficultySetting = DonutsPlugin.botDifficultiesPMC.Value.ToLower();

            // Define difficulties that might be configured for each setting
            List<BotDifficulty> difficultiesForSetting;

            switch (difficultySetting)
            {
                case "asonline":
                    difficultiesForSetting = new List<BotDifficulty> { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard };
                    break;
                case "easy":
                    difficultiesForSetting = new List<BotDifficulty> { BotDifficulty.easy };
                    break;
                case "normal":
                    difficultiesForSetting = new List<BotDifficulty> { BotDifficulty.normal };
                    break;
                case "hard":
                    difficultiesForSetting = new List<BotDifficulty> { BotDifficulty.hard };
                    break;
                case "impossible":
                    difficultiesForSetting = new List<BotDifficulty> { BotDifficulty.impossible };
                    break;
                default:
                    Logger.LogError("Unsupported difficulty setting: " + difficultySetting);
                    return;
            }

            // Apply these difficulties to sptUsec and sptBear with both single and group bots
            foreach (var difficulty in difficultiesForSetting)
            {
                // Create three single bots
                for (int i = 0; i < 5; i++)
                {
                    var botInfoUsec = new PrepBotInfo(sptUsec, difficulty, EPlayerSide.Usec, false, 1);
                    await CreateBot(botInfoUsec, botInfoUsec.IsGroup, botInfoUsec.GroupSize);
                    BotInfos.Add(botInfoUsec);

                    var botInfoBear = new PrepBotInfo(sptBear, difficulty, EPlayerSide.Bear, false, 1);
                    await CreateBot(botInfoBear, botInfoBear.IsGroup, botInfoBear.GroupSize);
                    BotInfos.Add(botInfoBear);
                }

                // Create group bots of sizes 2, 3, and 4
                foreach (int groupSize in new int[] { 2, 2, 2, 3, 3, 4, 4, 5 })
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
        private async Task InitializeScavBotInfos()
        {
            string difficultySetting = DonutsPlugin.botDifficultiesSCAV.Value.ToLower();
            List<BotDifficulty> difficultiesForSetting;

            switch (difficultySetting)
            {
                case "asonline":
                    difficultiesForSetting = new List<BotDifficulty> { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard };
                    break;
                case "easy":
                    difficultiesForSetting = new List<BotDifficulty> { BotDifficulty.easy };
                    break;
                case "normal":
                    difficultiesForSetting = new List<BotDifficulty> { BotDifficulty.normal };
                    break;
                case "hard":
                    difficultiesForSetting = new List<BotDifficulty> { BotDifficulty.hard };
                    break;
                case "impossible":
                    difficultiesForSetting = new List<BotDifficulty> { BotDifficulty.impossible };
                    break;
                default:
                    Logger.LogWarning("Unsupported difficulty setting for SCAV bots: " + difficultySetting);
                    return;
            }

            // Adding SCAV bot info for the assault type with both single and group bots
            foreach (var difficulty in difficultiesForSetting)
            {
                // Create three single bots
                for (int i = 0; i < 3; i++)
                {
                    var botInfo = new PrepBotInfo(WildSpawnType.assault, difficulty, EPlayerSide.Savage, false, 1);
                    await CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize);
                    BotInfos.Add(botInfo);
                }

                // Create group bots of sizes 2, 3, and 4
                foreach (int groupSize in new int[] { 2, 3, 4 })
                {
                    var botInfo = new PrepBotInfo(WildSpawnType.assault, difficulty, EPlayerSide.Savage, true, groupSize);
                    await CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize);
                    BotInfos.Add(botInfo);
                }
            }
        }

        private void Update()
        {
            timeSinceLastReplenish += Time.deltaTime;
            if (timeSinceLastReplenish >= DonutsPlugin.replenishInterval.Value && !isReplenishing)
            {
                timeSinceLastReplenish = 0f;
                StartCoroutine(ReplenishAllBots());
            }
        }
        private IEnumerator ReplenishAllBots()
        {
            isReplenishing = true;
            int singleBotsCount = 0;
            int groupBotsCount = 0;

            // Create a copy of BotInfos for safe iteration
            var safeBotInfos = new List<PrepBotInfo>(BotInfos);

            foreach (var botInfo in safeBotInfos)
            {
                if (NeedReplenishment(botInfo))
                {
                    Task creationTask;

                    if (botInfo.IsGroup && groupBotsCount < 1)
                    {
#if DEBUG
                        Logger.LogWarning($"Replenishing group bot: {botInfo.SpawnType} {botInfo.Difficulty} {botInfo.Side} Count: {botInfo.GroupSize}");
#endif
                        creationTask = CreateBot(botInfo, true, botInfo.GroupSize);
                        groupBotsCount++;
                    }
                    else if (!botInfo.IsGroup && singleBotsCount < 3)
                    {
#if DEBUG
                        Logger.LogWarning($"Replenishing single bot: {botInfo.SpawnType} {botInfo.Difficulty} {botInfo.Side} Count: 1");
#endif
                        creationTask = CreateBot(botInfo, false, 1);
                        singleBotsCount++;
                    }
                    else
                    {
                        continue;
                    }

                    yield return new WaitUntil(() => creationTask.IsCompleted);

                    if (creationTask.Status == TaskStatus.Faulted)
                    {
                        Logger.LogError("Bot creation failed: " + creationTask.Exception.ToString());
                    }

                    if (singleBotsCount >= 3 && groupBotsCount >= 1)
                        break;
                }
            }

            isReplenishing = false;
        }

        private static bool NeedReplenishment(PrepBotInfo botInfo)
        {
            // Assuming that botInfo.Bots is null or its count is zero when it needs replenishment
            return botInfo.Bots == null || botInfo.Bots.Profiles.Count == 0;
        }

        private async void ReplenishBots(PrepBotInfo botInfo)
        {
            // Logic to create bots using await directly here
            await CreateBot(botInfo, botInfo.IsGroup, botInfo.GroupSize);
        }

        internal static async Task CreateBot(PrepBotInfo botInfo, bool isGroup, int groupSize)
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
#if DEBUG
            /*Logger.LogDebug("Dumping BotInfos contents:");
            foreach (var info in BotInfos)
            {
                Logger.LogDebug($"Type: {info.SpawnType}, Difficulty: {info.Difficulty}, Profiles Count: {info.Bots?.Profiles.Count ?? 0}");
            }*/
#endif

            // Find the bot info that matches the spawn type and difficulty
            var botInfo = BotInfos.FirstOrDefault(b => b.SpawnType == spawnType && b.Difficulty == difficulty && b.Bots.Profiles.Count == targetCount);

            if (botInfo != null)
            {
                return botInfo.Bots;
            }

            return null;
        }

        public static List<BotCacheClass> GetWildSpawnData(WildSpawnType spawnType, BotDifficulty botDifficulty)
        {
            // Filter the BotInfos list for entries matching the given spawn type and difficulty
            return BotInfos
                .Where(b => b.SpawnType == spawnType && b.Difficulty == botDifficulty)
                .Select(b => b.Bots) // Assuming Bots is a BotCacheClass instance.
                .ToList();
        }

        //return the original wildspawntype of a bot that was converted to a group
        internal static WildSpawnType? GetOriginalSpawnTypeForBot(BotOwner bot)
        {
            //search originalspawntype dictionary for the bot's profile.id
            var originalProfile = OriginalBotSpawnTypes.First(profile => profile.Key == bot.Profile.Id);

            //if we found the original profile, return the original role

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
        void OnDestroy()
        {
            // Unsubscribe from the OnBotRemoved event
            if (botSpawnerClass != null)
            {
                botSpawnerClass.OnBotRemoved -= (BotOwner bot) =>
                {
                    OriginalBotSpawnTypes.Remove(bot.Profile.Id);
                };
            }

            // Unsubscribe from the OnBotCreated event
            botSpawnerClass.OnBotCreated -= (BotOwner bot) =>
            {
                bot.Memory.OnGoalEnemyChanged -= Memory_OnGoalEnemyChanged;
            };

            // Detach event handler from main player
            if (mainplayer != null)
            {
                mainplayer.BeingHitAction -= Mainplayer_BeingHitAction;
            }

            StopAllCoroutines();

            // Nullify static references to ensure they can be garbage collected
            gameWorld = null;
            botCreator = null;
            botSpawnerClass = null;
            mainplayer = null;
            OriginalBotSpawnTypes = null;
            BotInfos = null;
            timeSinceLastReplenish = 0;
            IsBotPreparationComplete = false;
#if DEBUG
            Logger.LogWarning("DonutsBotPrep component cleaned up and disabled.");
#endif
        }
    }

}

