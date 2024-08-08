using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Donuts.Models;
using EFT;
using EFT.Communications;
using HarmonyLib;
using SPT.Reflection.Utils;
using UnityEngine;
using static Donuts.DonutComponent;
using static Donuts.DonutsBotPrep;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal static class DonutInitialization
    {
        internal static ManualLogSource Logger
        {
            get; private set;
        }
        internal static void InitializeComponent()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(DonutInitialization));
            methodCache = new Dictionary<string, MethodInfo>();
            gizmos = new Gizmos();

            var displayMessageNotification = PatchConstants.EftTypes.Single(x => x.GetMethod("DisplayMessageNotification") != null).GetMethod("DisplayMessageNotification");
            if (displayMessageNotification != null)
            {
                methodCache["DisplayMessageNotification"] = displayMessageNotification;
            }

            var methodInfo = AccessTools.Method(typeof(BotSpawner), "method_9");
            if (methodInfo != null)
            {
                methodCache[methodInfo.Name] = methodInfo;
            }

            var methodInfo2 = AccessTools.Method(typeof(BotSpawner), "method_10");
            if (methodInfo2 != null)
            {
                methodCache[methodInfo2.Name] = methodInfo2;
            }

            if (gameWorld.RegisteredPlayers.Count > 0)
            {
                foreach (var player in gameWorld.AllPlayersEverExisted)
                {
                    if (!player.IsAI)
                    {
                        playerList.Add(player);
                    }
                }
            }
        }

        internal static void SetupGame()
        {
            Logger.LogDebug("Starting SetupGame.");

            DonutComponent.fileLoaded = false;

            // Ensure gameWorld is initialized
            if (gameWorld == null)
            {
                Logger.LogError("GameWorld is null in SetupGame.");
                return;
            }

            DonutComponent.mainplayer = gameWorld.MainPlayer;

            if (DonutComponent.mainplayer == null)
            {
                Logger.LogError("MainPlayer is null in SetupGame.");
                return;
            }

            isInBattle = false;

            Logger.LogDebug("Setup maplocation: " + DonutsBotPrep.maplocation);

            LoadScenarioSelection();

            botWavesConfig = botWavesConfig.GetBotWavesConfig(selectionName, mapName);

            if (botWavesConfig == null)
            {
                Logger.LogError("BotWaveConfig is null in SetupGame.");
                return;
            }

            if (botWavesConfig.Maps == null)
            {
                Logger.LogError("BotWaveConfig.Maps is null in SetupGame.");
                return;
            }

            if (!botWavesConfig.Maps.ContainsKey(DonutsBotPrep.maplocation))
            {
                Logger.LogError($"Map location '{DonutsBotPrep.maplocation}' not found in BotWaveConfig.Maps.");
                return;
            }

            DonutComponent.botWaves = botWavesConfig.Maps[DonutsBotPrep.maplocation];

            if (botWaves == null)
            {
                Logger.LogError($"BotWaves for map location '{DonutsBotPrep.maplocation}' is null.");
                return;
            }

            // Reset variables for each raid
            hasSpawnedStartingBots = false;
            maxRespawnReachedPMC = false;
            maxRespawnReachedSCAV = false;
            currentMaxPMC = 0;
            currentMaxSCAV = 0;

            Logger.LogDebug("Setup PMC Bot limit: " + PMCBotLimit);
            Logger.LogDebug("Setup SCAV Bot limit: " + SCAVBotLimit);

            // Ensure spawnCheckTimer is not null
            if (DonutComponent.spawnCheckTimer == null)
            {
                Logger.LogError("SpawnCheckTimer is null in SetupGame.");
                return;
            }

            DonutComponent.spawnCheckTimer.Start();

            Logger.LogDebug("Completed SetupGame.");

            //do check for current Bots for starting. log them

        }

        internal static void SetupBotLimitSync(string folderName)
        {
            Folder raidFolderSelected = DonutsPlugin.GrabDonutsFolder(folderName);
            switch (DonutsBotPrep.maplocation)
            {
                case "factory4_day":
                case "factory4_night":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.FactoryBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.FactoryBotLimit;
                    break;
                case "bigmap":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.CustomsBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.CustomsBotLimit;
                    break;
                case "interchange":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.InterchangeBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.InterchangeBotLimit;
                    break;
                case "rezervbase":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.ReserveBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.ReserveBotLimit;
                    break;
                case "laboratory":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.LaboratoryBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.LaboratoryBotLimit;
                    break;
                case "lighthouse":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.LighthouseBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.LighthouseBotLimit;
                    break;
                case "shoreline":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.ShorelineBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.ShorelineBotLimit;
                    break;
                case "woods":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.WoodsBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.WoodsBotLimit;
                    break;
                case "tarkovstreets":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.TarkovStreetsBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.TarkovStreetsBotLimit;
                    break;
                case "sandbox":
                case "sandbox_high":
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.GroundZeroBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.GroundZeroBotLimit;
                    break;
                default:
                    PMCBotLimit = 8;
                    SCAVBotLimit = 5;
                    break;
            }
        }

        internal static void LoadScenarioSelection()
        {

            if (!fileLoaded)
            {
                methodCache.TryGetValue("DisplayMessageNotification", out MethodInfo displayMessageNotificationMethod);

                string dllPath = Assembly.GetExecutingAssembly().Location;
                string directoryPath = Path.GetDirectoryName(dllPath);
                string jsonFolderPath = Path.Combine(directoryPath, "patterns");

                if (DonutsBotPrep.selectionName == null)
                {
                    var txt = "Donuts Plugin: No valid Scenario Selection found for map";
                    Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod?.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    return;
                }

                string patternFolderPath = Path.Combine(jsonFolderPath, DonutsBotPrep.selectionName);

                if (!Directory.Exists(patternFolderPath))
                {
                    var txt = $"Donuts Plugin: Folder from ScenarioConfig.json does not actually exist: {patternFolderPath}\nDisabling the donuts plugin for this raid.";
                    Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod?.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                string[] jsonFiles = Directory.GetFiles(patternFolderPath, "*.json");

                if (jsonFiles.Length == 0)
                {
                    var txt = $"Donuts Plugin: No JSON Pattern files found in folder: {patternFolderPath}\nDisabling the donuts plugin for this raid.";
                    Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod?.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                fileLoaded = true;

                // Display selected preset
                DonutsPlugin.LogSelectedPreset(DonutsBotPrep.selectionName);
            }
        }



    }



}
