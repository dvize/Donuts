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
using static Donuts.DonutComponent;
using Cysharp.Threading.Tasks;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class Initialization
    {
        private static BotManager botManager;

        private static void LoadStartingBots()
        {
            string dllPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(dllPath);
            string jsonFilePath = Path.Combine(directoryPath, "StartingBots.json");

            if (File.Exists(jsonFilePath))
            {
                var jsonString = File.ReadAllText(jsonFilePath);
                botManager = JsonConvert.DeserializeObject<BotManager>(jsonString);
                DonutComponent.Logger.LogDebug("Loaded StartingBots.json successfully.");
            }
            else
            {
                DonutComponent.Logger.LogError("StartingBots.json file not found.");
            }
        }

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

            LoadStartingBots();

            sptUsec = (WildSpawnType)AkiBotsPrePatcher.sptUsecValue;
            sptBear = (WildSpawnType)AkiBotsPrePatcher.sptBearValue;
        }

        internal static void SetupBotLimit(string mapName)
        {
            if (botManager == null)
            {
                throw new InvalidOperationException("BotManager is not loaded.");
            }

            var selectedConfig = botManager.StartingBotsData.FirstOrDefault(config => config.Name == selectionName);
            if (selectedConfig != null && selectedConfig.Maps.TryGetValue(mapName, out var mapBotConfig))
            {
                PMCBotLimit = mapBotConfig.PMC.MaxCount;
                SCAVBotLimit = mapBotConfig.SCAV.MaxCount;
            }
            else
            {
                PMCBotLimit = 5; // Default value
                SCAVBotLimit = 5; // Default value
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

                string jsonFilePath = Path.Combine(directoryPath, "MaxBotCaps.json");

                // Load the bot configurations from the JSON file
                mapConfig = MapConfig.LoadFromJson(jsonFilePath);

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

                string PatternFolderPath = Path.Combine(jsonFilePath, selectionName);

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
                    string fileContent = await UniTask.Run(() => File.ReadAllText(file));
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
