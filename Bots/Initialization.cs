using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Aki.PrePatch;
using EFT;
using EFT.Communications;
using Newtonsoft.Json;
using UnityEngine;
using Donuts.Models;
using Cysharp.Threading.Tasks;
using static Donuts.DonutComponent;
using static Donuts.DefaultPluginVars;
using static Donuts.Gizmos;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class Initialization
    {
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
            PMCBotLimit = 0;
            SCAVBotLimit = 0;
            currentInitialPMCs = 0;
            currentInitialSCAVs = 0;

            Gizmos.drawnCoordinates = new HashSet<Vector3>();
            gizmoSpheres = new List<GameObject>();

            sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;
        }

        internal static void SetupBotLimit(string folderName)
        {
            Folder raidFolderSelected = DonutsPlugin.GrabDonutsFolder(folderName);
            switch (maplocation)
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

        internal static void InitializeHotspotTimers()
        {
            // Group the fight locations by groupNum
            foreach (var listHotspots in groupedFightLocations)
            {
                foreach (var hotspot in listHotspots)
                {
                    var hotspotTimer = new HotspotTimer(hotspot);

                    int groupNum = hotspot.GroupNum;

                    if (!groupedHotspotTimers.ContainsKey(groupNum))
                    {
                        groupedHotspotTimers[groupNum] = new List<HotspotTimer>();
                    }

                    groupedHotspotTimers[groupNum].Add(hotspotTimer);
                }
            }

            // Assign the groupedHotspotTimers dictionary back to hotspotTimers
            hotspotTimers = groupedHotspotTimers.SelectMany(kv => kv.Value).ToList();
        }

        internal static async UniTask LoadFightLocations()
        {
            if (!fileLoaded)
            {
                MethodInfo displayMessageNotificationMethod;
                methodCache.TryGetValue("DisplayMessageNotification", out displayMessageNotificationMethod);

                string dllPath = Assembly.GetExecutingAssembly().Location;
                string directoryPath = Path.GetDirectoryName(dllPath);

                string jsonFolderPath = Path.Combine(directoryPath, "patterns");

                var selectionName = DonutsPlugin.RunWeightedScenarioSelection();

                SetupBotLimit(selectionName);

                if (selectionName == null)
                {
                    var txt = "Donuts Plugin: No valid Scenario Selection found for map";
                    DonutComponent.Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod?.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    return;
                }

                string PatternFolderPath = Path.Combine(jsonFolderPath, selectionName);

                if (!Directory.Exists(PatternFolderPath))
                {
                    var txt = "Donuts Plugin: Folder from ScenarioConfig.json does not actually exist: " + PatternFolderPath + "\nDisabling the donuts plugin for this raid.";
                    DonutComponent.Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod?.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                string[] jsonFiles = Directory.GetFiles(PatternFolderPath, "*.json");

                if (jsonFiles.Length == 0)
                {
                    var txt = "Donuts Plugin: No JSON Pattern files found in folder: " + PatternFolderPath + "\nDisabling the donuts plugin for this raid.";
                    DonutComponent.Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod?.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                List<Entry> combinedLocations = new List<Entry>();

                foreach (string file in jsonFiles)
                {
                    string fileContent = await UniTask.Create(async () => File.ReadAllText(file));
                    FightLocations fightfile = JsonConvert.DeserializeObject<FightLocations>(fileContent);
                    combinedLocations.AddRange(fightfile.Locations);
                }

                if (combinedLocations.Count == 0)
                {
                    var txt = "Donuts Plugin: No Entries found in JSON files, disabling plugin for raid.";
                    DonutComponent.Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod?.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                DonutComponent.Logger.LogDebug("Loaded " + combinedLocations.Count + " Bot Fight Entries");

                fightLocations = new FightLocations { Locations = combinedLocations };

                fightLocations.Locations.RemoveAll(x => x.MapName != maplocation);

                if (fightLocations.Locations.Count == 0)
                {
                    var txt = "Donuts Plugin: There are no valid Spawn Marker Entries for the current map. Disabling the plugin for this raid.";
                    DonutComponent.Logger.LogError(txt);
                    EFT.UI.ConsoleScreen.LogError(txt);
                    displayMessageNotificationMethod?.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Alert, Color.yellow });
                    fileLoaded = false;
                    return;
                }

                DonutComponent.Logger.LogDebug("Valid Bot Fight Entries For Current Map: " + fightLocations.Locations.Count);

                fileLoaded = true;
            }

            foreach (Entry entry in fightLocations.Locations)
            {
                bool groupExists = false;
                foreach (List<Entry> group in groupedFightLocations)
                {
                    if (group.Count > 0 && group.First().GroupNum == entry.GroupNum)
                    {
                        group.Add(entry);
                        groupExists = true;
                        break;
                    }
                }

                if (!groupExists)
                {
                    groupedFightLocations.Add(new List<Entry> { entry });
                }
            }
        }
    }
}
