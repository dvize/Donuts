using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Aki.Reflection.Patching;
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

    [BepInPlugin("com.dvize.Donuts", "dvize.Donuts", "1.5.0")]
    [BepInDependency("com.spt-aki.core", "3.8.0")]
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
        private void Awake()
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

            //Patches
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

            SetupScenariosUI();
            ImportConfig();
        }

        private void SetupScenariosUI()
        {
            LoadDonutsFolders();

            var scenarioValuesList = DefaultPluginVars.scenarioValues?.ToList() ?? new List<string>();
            var scavScenarioValuesList = DefaultPluginVars.scavScenarioValues?.ToList() ?? new List<string>();

            AddScenarioNamesToValuesList(DefaultPluginVars.scenarios, ref scenarioValuesList, folder => folder.Name);
            AddScenarioNamesToValuesList(DefaultPluginVars.randomScenarios, ref scenarioValuesList, folder => folder.RandomScenarioConfig);

            AddScenarioNamesToValuesList(DefaultPluginVars.scavScenarios, ref scavScenarioValuesList, folder => folder.Name);
            AddScenarioNamesToValuesList(DefaultPluginVars.randomScavScenarios, ref scavScenarioValuesList, folder => folder.RandomScenarioConfig);

            DefaultPluginVars.scenarioValues = scenarioValuesList.ToArray();
            DefaultPluginVars.scavScenarioValues = scavScenarioValuesList.ToArray();

            Logger.LogWarning($"Loaded PMC Scenarios: {DefaultPluginVars.scenarioValues.ToString()} ");
            Logger.LogWarning($"Loaded Scav Scenarios: {DefaultPluginVars.scavScenarioValues.ToString()} ");

            //if there is no default value selected from our loaded DefaultPluginVars, set the value to the first index
            if (DefaultPluginVars.pmcScenarioSelection.Value == null || DefaultPluginVars.pmcScenarioSelection.Value == "")
            {
                DefaultPluginVars.pmcScenarioSelection.Value = DefaultPluginVars.scenarioValues[0];
            }
            if (DefaultPluginVars.scavScenarioSelection.Value == null || DefaultPluginVars.scavScenarioSelection.Value == "")
            {
                DefaultPluginVars.scavScenarioSelection.Value = DefaultPluginVars.scavScenarioValues[0];
            }
        }

        private void Update()
        {
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
        internal void LoadDonutsFolders()
        {
            var dllPath = Assembly.GetExecutingAssembly().Location;
            var directoryPath = Path.GetDirectoryName(dllPath);

            DefaultPluginVars.scenarios = LoadFolders(Path.Combine(directoryPath, "ScenarioConfig.json"));
            DefaultPluginVars.randomScenarios = LoadFolders(Path.Combine(directoryPath, "RandomScenarioConfig.json"));
            DefaultPluginVars.scavScenarios = LoadFolders(Path.Combine(directoryPath, "ScavScenarioConfig.json"));
            DefaultPluginVars.randomScavScenarios = LoadFolders(Path.Combine(directoryPath, "RandomScavScenarioConfig.json"));

            PopulateScenarioValues();
            DrawMainSettings.InitializeDropdownIndices();  // Re-initialize dropdown indices after loading scenarios
            DrawMainSettings.scenariosLoaded = DefaultPluginVars.pmcScenarioSelection.Options.Length > 0 &&
                                               DefaultPluginVars.scavScenarioSelection.Options.Length > 0;
        }

        private void PopulateScenarioValues()
        {
            DefaultPluginVars.scenarioValues = GenerateScenarioValues(DefaultPluginVars.scenarios, DefaultPluginVars.randomScenarios);
            DefaultPluginVars.scavScenarioValues = GenerateScenarioValues(DefaultPluginVars.scavScenarios, DefaultPluginVars.randomScavScenarios);
        }

        private static string[] GenerateScenarioValues(List<Folder> scenarios, List<Folder> randomScenarios)
        {
            var valuesList = new List<string>();

            AddScenarioNamesToValuesList(scenarios, ref valuesList, folder => folder.Name);
            AddScenarioNamesToValuesList(randomScenarios, ref valuesList, folder => folder.RandomScenarioConfig);

            // Log the contents of the final valuesList
           /* Logger.LogInfo("Final scenario values list:");
            foreach (var value in valuesList)
            {
                Logger.LogInfo(value);
            }*/

            return valuesList.ToArray();
        }
        private static void AddScenarioNamesToValuesList(IEnumerable<Folder> folders, ref List<string> valuesList, Func<Folder, string> getNameFunc)
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
        private static List<Folder> LoadFolders(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.LogWarning($"File not found: {filePath}");
                return new List<Folder>();
            }

            var fileContent = File.ReadAllText(filePath);
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
            return DefaultPluginVars.scenarios.FirstOrDefault(folder => folder.Name == folderName);
        }

        internal static string RunWeightedScenarioSelection()
        {
            try
            {
                var scenarioSelection = DefaultPluginVars.pmcScenarioSelection.Value;

                if (Aki.SinglePlayer.Utils.InRaid.RaidChangesUtil.IsScavRaid)
                {
                    Logger.LogWarning("This is a SCAV raid, using SCAV raid preset selector");
                    scenarioSelection = DefaultPluginVars.scavScenarioSelection.Value;
                }

                var selectedFolder = DefaultPluginVars.scenarios.FirstOrDefault(folder => folder.Name == scenarioSelection)
                                     ?? DefaultPluginVars.randomScenarios.FirstOrDefault(folder => folder.RandomScenarioConfig == scenarioSelection);

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
