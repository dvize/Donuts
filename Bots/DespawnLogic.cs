using System;
using System.Threading;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using Systems.Effects;
using UnityEngine;
using static Donuts.DonutComponent;

namespace Donuts
{
    internal class DonutDespawnLogic : MonoBehaviour
    {
        internal static ManualLogSource Logger
        {
            get; private set;
        }

        public DonutDespawnLogic()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(DonutDespawnLogic));
        }
        internal static async UniTask DespawnFurthestBot(string bottype, CancellationToken cancellationToken)
        {
            float despawnCooldown = bottype == "pmc" ? PMCdespawnCooldown : SCAVdespawnCooldown;
            float despawnCooldownDuration = bottype == "pmc" ? PMCdespawnCooldownDuration : SCAVdespawnCooldownDuration;

            float currentTime = Time.time;
            float timeSinceLastDespawn = currentTime - despawnCooldown;

            if (timeSinceLastDespawn < despawnCooldownDuration)
            {
                return;
            }

            if (!await ShouldConsiderDespawning(bottype, cancellationToken))
            {
                return;
            }

            Player furthestBot = await UpdateDistancesAndFindFurthestBot(bottype);

            if (furthestBot != null)
            {
                DespawnBot(furthestBot, bottype);
            }
            else
            {
                Logger.LogWarning($"No {bottype} bot found to despawn.");
            }
        }

        private static void DespawnBot(Player furthestBot, string bottype)
        {
            if (furthestBot == null)
            {
                Logger.LogError("Attempted to despawn a null bot.");
                return;
            }

            BotOwner botOwner = furthestBot.AIData.BotOwner;
            if (botOwner == null)
            {
                Logger.LogError("BotOwner is null for the furthest bot.");
                return;
            }

#if DEBUG
            Logger.LogDebug($"Despawning bot: {furthestBot.Profile.Info.Nickname} ({furthestBot.name})");
#endif

            gameWorld.RegisteredPlayers.Remove(botOwner);
            gameWorld.AllAlivePlayersList.Remove(botOwner.GetPlayer);

            var botgame = Singleton<IBotGame>.Instance;
            Singleton<Effects>.Instance.EffectsCommutator.StopBleedingForPlayer(botOwner.GetPlayer);
            botOwner.Deactivate();
            botOwner.Dispose();
            botgame.BotsController.BotDied(botOwner);
            botgame.BotsController.DestroyInfo(botOwner.GetPlayer);
            DestroyImmediate(botOwner.gameObject);
            Destroy(botOwner);


            // Update the cooldown
            if (bottype == "pmc")
            {
                PMCdespawnCooldown = Time.time;
            }
            else if (bottype == "scav")
            {
                SCAVdespawnCooldown = Time.time;
            }
        }

        private static async UniTask<bool> ShouldConsiderDespawning(string botType, CancellationToken cancellationToken)
        {
            int botLimit = botType == "pmc" ? DonutComponent.PMCBotLimit : DonutComponent.SCAVBotLimit;
            int activeBotCount = await BotCountManager.GetAlivePlayers(botType, cancellationToken);

            return activeBotCount > botLimit; // Only consider despawning if the number of active bots of the type exceeds the limit
        }

        private static UniTask<Player> UpdateDistancesAndFindFurthestBot(string bottype)
        {
            return UniTask.Create(async () =>
            {
                float maxDistance = float.MinValue;
                Player furthestBot = null;

                foreach (var bot in gameWorld.AllAlivePlayersList)
                {
                    if (!bot.IsYourPlayer && bot.AIData.BotOwner != null && IsBotType(bot, bottype))
                    {
                        // Get distance of bot to player using squared distance
                        float distance = (mainplayer.Transform.position - bot.Transform.position).sqrMagnitude;

                        // Check if this is the furthest distance
                        if (distance > maxDistance)
                        {
                            maxDistance = distance;
                            furthestBot = bot;
                        }
                    }
                }

                if (furthestBot == null)
                {
                    Logger.LogWarning("Furthest bot is null. No bots found in the list.");
                }
                else
                {
                    Logger.LogDebug($"Furthest bot found: {furthestBot.Profile.Info.Nickname} at distance {Mathf.Sqrt(maxDistance)}");
                }

                return furthestBot;
            });
        }
        private static bool IsBotType(Player bot, string bottype)
        {
            switch (bottype)
            {
                case "scav":
                    return BotCountManager.IsSCAV(bot.Profile.Info.Settings.Role);
                case "pmc":
                    return BotCountManager.IsPMC(bot.Profile.Info.Settings.Role);
                default:
                    throw new ArgumentException("Invalid bot type", nameof(bottype));
            }
        }
    }
}
