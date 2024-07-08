﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SPT.Reflection.Patching;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Donuts.Models;
using Donuts.Patches;
using EFT;
using EFT.Communications;
using Newtonsoft.Json;
using UnityEngine;

//disable the ide0007 warning for the entire file
#pragma warning disable IDE0007

namespace Donuts
{

    [BepInPlugin("com.dvize.Donuts", "dvize.Donuts", "1.5.1")]
    [BepInDependency("com.SPT.core", "3.9.0")]
    [BepInDependency("xyz.drakia.waypoints")]
    [BepInDependency("com.Arys.UnityToolkit")]
    public class DonutsPlugin : BaseUnityPlugin
    {
        internal static PluginGUIHelper pluginGUIHelper;
        internal static ConfigEntry<KeyboardShortcut> toggleGUIKey;
        internal static KeyCode escapeKey;
        internal static new ManualLogSource Logger;
        DonutsPlugin()
        {
            Logger ??= BepInEx.Logging.Logger.CreateLogSource(nameof(DonutsPlugin));
        }
        private async void Awake()
        {
            // Run dependency checker
            if (!DependencyChecker.ValidateDependencies(Logger, Info, this.GetType(), Config))
            {
                throw new Exception($"Missing Dependencies");
            }

            pluginGUIHelper = gameObject.AddComponent<PluginGUIHelper>();

            toggleGUIKey = Config.Bind(
                "Config Settings",
                "Key To Enable/Disable Config Interface",
                new KeyboardShortcut(KeyCode.F8),
                "Key to Enable/Disable Donuts Configuration Menu");

            escapeKey = KeyCode.Escape;

            // Patches
            new NewGameDonutsPatch().Enable();
            new BotGroupAddEnemyPatch().Enable();
            new BotMemoryAddEnemyPatch().Enable();
            new MatchEndPlayerDisposePatch().Enable();
            new PatchStandbyTeleport().Enable();
            new BotProfilePreparationHook().Enable();
            new AddEnemyPatch().Enable();
            new ShootDataNullRefPatch().Enable();
            new CoverPointMasterNullRef().Enable();
            new DelayedGameStartPatch().Enable();

            await SetupScenariosUI();
            ImportConfig();
        }

        private async Task SetupScenariosUI()
        {
            await LoadDonutsFoldersAsync();

            var scenarioValuesList = DefaultPluginVars.pmcScenarioCombinedArray?.ToList() ?? new List<string>();
            var scavScenarioValuesList = DefaultPluginVars.scavScenarioCombinedArray?.ToList() ?? new List<string>();

            await AddScenarioNamesToListAsync(DefaultPluginVars.pmcScenarios, scenarioValuesList, folder => folder.Name);
            await AddScenarioNamesToListAsync(DefaultPluginVars.pmcRandomScenarios, scenarioValuesList, folder => folder.RandomScenarioConfig);

            await AddScenarioNamesToListAsync(DefaultPluginVars.scavScenarios, scavScenarioValuesList, folder => folder.Name);
            await AddScenarioNamesToListAsync(DefaultPluginVars.randomScavScenarios, scavScenarioValuesList, folder => folder.RandomScenarioConfig);

            DefaultPluginVars.pmcScenarioCombinedArray = scenarioValuesList.ToArray();
            DefaultPluginVars.scavScenarioCombinedArray = scavScenarioValuesList.ToArray();

            // Dynamically initialize the scenario settings
            DefaultPluginVars.pmcScenarioSelection = new Setting<string>(
                "PMC Raid Spawn Preset Selection",
                "Select a preset to use when spawning as PMC",
                DefaultPluginVars.pmcScenarioSelection?.Value ?? "live-like",
                "live-like",
                null,
                null,
                DefaultPluginVars.pmcScenarioCombinedArray
            );

            DefaultPluginVars.scavScenarioSelection = new Setting<string>(
                "SCAV Raid Spawn Preset Selection",
                "Select a preset to use when spawning as SCAV",
                DefaultPluginVars.scavScenarioSelection?.Value ?? "live-like",
                "live-like",
                null,
                null,
                DefaultPluginVars.scavScenarioCombinedArray
            );

#if DEBUG
            Logger.LogWarning($"Loaded PMC Scenarios: {string.Join(", ", DefaultPluginVars.pmcScenarioCombinedArray)}");
            Logger.LogWarning($"Loaded Scav Scenarios: {string.Join(", ", DefaultPluginVars.scavScenarioCombinedArray)}");
#endif

            // Call InitializeDropdownIndices to ensure scenarios are loaded and indices are set
            DrawMainSettings.InitializeDropdownIndices();
        }


        private void Update()
        {
            if (ImGUIToolkit.IsSettingKeybind())
            {
                // If setting a keybind, do not trigger functionality
                return;
            }

            if (IsKeyPressed(toggleGUIKey.Value) || IsKeyPressed(escapeKey))
            {
                if (IsKeyPressed(escapeKey))
                {
                    //check if the config window is open    
                    if (DefaultPluginVars.showGUI)
                    {
                        DefaultPluginVars.showGUI = false;
                    }
                }
                else
                {
                    DefaultPluginVars.showGUI = !DefaultPluginVars.showGUI;
                }
            }

            if (IsKeyPressed(DefaultPluginVars.CreateSpawnMarkerKey.Value))
            {
                EditorFunctions.CreateSpawnMarker();
            }
            if (IsKeyPressed(DefaultPluginVars.WriteToFileKey.Value))
            {
                EditorFunctions.WriteToJsonFile();
            }
            if (IsKeyPressed(DefaultPluginVars.DeleteSpawnMarkerKey.Value))
            {
                EditorFunctions.DeleteSpawnMarker();
            }
        }
        public static void ImportConfig()
        {
            // Get the path of the currently executing assembly
            var dllPath = Assembly.GetExecutingAssembly().Location;
            var configDirectory = Path.Combine(Path.GetDirectoryName(dllPath), "Config");
            var configFilePath = Path.Combine(configDirectory, "DefaultPluginVars.json");

            if (!File.Exists(configFilePath))
            {
                Logger.LogError($"Config file not found: {configFilePath}, creating a new one");
                PluginGUIHelper.ExportConfig();
                return;

            }

            string json = File.ReadAllText(configFilePath);
            DefaultPluginVars.ImportFromJson(json);
        }

        #region Donuts Non-Raid Related Methods
        private async Task LoadDonutsFoldersAsync()
        {
            var dllPath = Assembly.GetExecutingAssembly().Location;
            var directoryPath = Path.GetDirectoryName(dllPath);

            DefaultPluginVars.pmcScenarios = await LoadFoldersAsync(Path.Combine(directoryPath, "ScenarioConfig.json"));
            DefaultPluginVars.pmcRandomScenarios = await LoadFoldersAsync(Path.Combine(directoryPath, "RandomScenarioConfig.json"));
            DefaultPluginVars.scavScenarios = await LoadFoldersAsync(Path.Combine(directoryPath, "ScavScenarioConfig.json"));
            DefaultPluginVars.randomScavScenarios = await LoadFoldersAsync(Path.Combine(directoryPath, "RandomScavScenarioConfig.json"));

            await PopulateScenarioValuesAsync();
            DrawMainSettings.InitializeDropdownIndices();  // Re-initialize dropdown indices after loading scenarios
        }

        private async Task PopulateScenarioValuesAsync()
        {
            DefaultPluginVars.pmcScenarioCombinedArray = await GenerateScenarioValuesAsync(DefaultPluginVars.pmcScenarios, DefaultPluginVars.pmcRandomScenarios);
            DefaultPluginVars.scavScenarioCombinedArray = await GenerateScenarioValuesAsync(DefaultPluginVars.scavScenarios, DefaultPluginVars.randomScavScenarios);
        }

        private async Task<string[]> GenerateScenarioValuesAsync(List<Folder> scenarios, List<Folder> randomScenarios)
        {
            var valuesList = new List<string>();

            await AddScenarioNamesToListAsync(scenarios, valuesList, folder => folder.Name);
            await AddScenarioNamesToListAsync(randomScenarios, valuesList, folder => folder.RandomScenarioConfig);

            return valuesList.ToArray();
        }

        private async Task AddScenarioNamesToListAsync(IEnumerable<Folder> folders, List<string> valuesList, Func<Folder, string> getNameFunc)
        {
            if (folders != null)
            {
                foreach (var folder in folders)
                {
                    var name = getNameFunc(folder);
                    Logger.LogWarning($"Adding scenario: {name}");
                    valuesList.Add(name);
                }
            }
        }

        private static async Task<List<Folder>> LoadFoldersAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.LogWarning($"File not found: {filePath}");
                return new List<Folder>();
            }

            var fileContent = await Task.Run(() => File.ReadAllText(filePath));
            var folders = JsonConvert.DeserializeObject<List<Folder>>(fileContent);

            if (folders == null || folders.Count == 0)
            {
                Logger.LogError("No Donuts Folders found in Scenario Config file, disabling plugin");
                Debug.Break();
                return new List<Folder>();
            }

            Logger.LogWarning($"Loaded {folders.Count} Donuts Scenario Folders");
            return folders;
        }


        #endregion

        #region Donuts Raid Related Scenario Selection Methods

        internal static Folder GrabDonutsFolder(string folderName)
        {
            return DefaultPluginVars.pmcScenarios.FirstOrDefault(folder => folder.Name == folderName);
        }

        internal static string RunWeightedScenarioSelection()
        {
            try
            {
                var scenarioSelection = DefaultPluginVars.pmcScenarioSelection.Value;

                if (SPT.SinglePlayer.Utils.InRaid.RaidChangesUtil.IsScavRaid)
                {
                    Logger.LogWarning("This is a SCAV raid, using SCAV raid preset selector");
                    scenarioSelection = DefaultPluginVars.scavScenarioSelection.Value;
                }

                var selectedFolder = DefaultPluginVars.pmcScenarios.FirstOrDefault(folder => folder.Name == scenarioSelection)
                                     ?? DefaultPluginVars.pmcRandomScenarios.FirstOrDefault(folder => folder.RandomScenarioConfig == scenarioSelection);

                if (selectedFolder != null)
                {
                    return SelectPreset(selectedFolder);
                }

                return null;
            }
            catch (Exception e)
            {
                Logger.LogError("Error in RunWeightedScenarioSelection: " + e);
                return null;
            }
        }

        private static string SelectPreset(Folder folder)
        {
            if (folder.presets == null || folder.presets.Count == 0) return folder.Name;

            var totalWeight = folder.presets.Sum(preset => preset.Weight);
            var randomWeight = UnityEngine.Random.Range(0, totalWeight);

            var selectedPreset = folder.presets
                .Aggregate((currentPreset, nextPreset) =>
                    randomWeight < (currentPreset.Weight += nextPreset.Weight) ? currentPreset : nextPreset);

            LogSelectedPreset(selectedPreset.Name);

            return selectedPreset.Name;
        }
        private static void LogSelectedPreset(string selectedPreset)
        {
            Console.WriteLine($"Donuts: Random Selected Preset: {selectedPreset}");

            if (DefaultPluginVars.ShowRandomFolderChoice.Value && DonutComponent.methodCache.TryGetValue("DisplayMessageNotification", out var displayMessageNotificationMethod))
            {
                var txt = $"Donuts Random Selected Preset: {selectedPreset}";
                EFT.UI.ConsoleScreen.Log(txt);
                displayMessageNotificationMethod.Invoke(null, new object[] { txt, ENotificationDurationType.Long, ENotificationIconType.Default, Color.yellow });
            }
        }

        #endregion

        internal static bool IsKeyPressed(KeyboardShortcut key)
        {
            if (!UnityInput.Current.GetKeyDown(key.MainKey)) return false;

            return key.Modifiers.All(modifier => UnityInput.Current.GetKey(modifier));
        }

        internal static bool IsKeyPressed(KeyCode key)
        {
            if (!UnityInput.Current.GetKeyDown(key)) return false;

            return true;
        }

    }

    //re-initializes each new game
    internal class NewGameDonutsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));

        [PatchPrefix]
        public static void PatchPrefix() => DonutComponent.Enable();
    }











}
