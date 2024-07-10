using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SPT.PrePatch;
using EFT;
using EFT.Communications;
using Newtonsoft.Json;
using UnityEngine;
using Donuts.Models;
using Cysharp.Threading.Tasks;
using static Donuts.DonutComponent;
using static Donuts.DefaultPluginVars;
using static Donuts.Gizmos;
using System.Threading;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class Initialization
    {

        internal static int PMCBotLimit;
        internal static int SCAVBotLimit;

        internal static void InitializeStaticVariables()
        {
            fightLocations = new FightLocations()
            {
                Locations = new List<Entry>()
            };

            sessionLocations = new FightLocations()
            {
                Locations = new List<Entry>()
            };

            fileLoaded = false;
            groupedHotspotTimers = new Dictionary<int, List<HotspotTimer>>();
            groupedFightLocations = new List<List<Entry>>();
            hotspotTimers = new List<HotspotTimer>();
            currentInitialPMCs = 0;
            currentInitialSCAVs = 0;
        }

        internal static void SetupBotLimit(string folderName)
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
                    PMCBotLimit = raidFolderSelected.PMCBotLimitPresets.GroundZeroBotLimit;
                    SCAVBotLimit = raidFolderSelected.SCAVBotLimitPresets.GroundZeroBotLimit;
                    break;
                default:
                    PMCBotLimit = 8;
                    SCAVBotLimit = 5;
                    break;
            }
        }

        internal static async UniTask LoadFightLocations(CancellationToken cancellationToken)
        {
            // Check if the operation has been cancelled
            cancellationToken.ThrowIfCancellationRequested();

            if (!fileLoaded)
            {
                methodCache.TryGetValue("DisplayMessageNotification", out MethodInfo displayMessageNotificationMethod);

                string dllPath = Assembly.GetExecutingAssembly().Location;
                string directoryPath = Path.GetDirectoryName(dllPath);
                string jsonFolderPath = Path.Combine(directoryPath, "patterns");

                

                if (DonutsBotPrep.selectionName == null)
                {
                    var txt = "Donuts Plugin: No valid Scenario Selection found for map";
                    DonutComponent.Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod?.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    return;
                }

                string patternFolderPath = Path.Combine(jsonFolderPath, DonutsBotPrep.selectionName);

                if (!Directory.Exists(patternFolderPath))
                {
                    var txt = $"Donuts Plugin: Folder from ScenarioConfig.json does not actually exist: {patternFolderPath}\nDisabling the donuts plugin for this raid.";
                    DonutComponent.Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod?.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                string[] jsonFiles = Directory.GetFiles(patternFolderPath, "*.json");

                if (jsonFiles.Length == 0)
                {
                    var txt = $"Donuts Plugin: No JSON Pattern files found in folder: {patternFolderPath}\nDisabling the donuts plugin for this raid.";
                    DonutComponent.Logger.LogError(txt);
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
