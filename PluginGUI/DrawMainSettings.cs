using EFT.Visual;
using UnityEngine;
using static Donuts.ImGUIToolkit;
using static Donuts.PluginGUIHelper;

#pragma warning disable IDE0007, IDE0044

namespace Donuts
{
    internal class DrawMainSettings
    {
        private static int selectedMainSettingsIndex = 0;
        private static string[] mainSettingsSubTabs = { "General", "Spawn Frequency", "Bot Attributes" };

        // For dropdowns
        internal static int botDifficultiesPMCIndex = 0;
        internal static int botDifficultiesSCAVIndex = 0;
        internal static int botDifficultiesOtherIndex = 0;
        internal static int pmcGroupChanceIndex = 0;
        internal static int scavGroupChanceIndex = 0;
        internal static int pmcFactionIndex = 0;
        internal static int forceAllBotTypeIndex = 0;
        internal static int pmcScenarioSelectionIndex = 0;
        internal static int scavScenarioSelectionIndex = 0;

        // Flag to check if scenarios are loaded
        internal static bool scenariosLoaded = false;

        // Need this for dropdowns to show loaded values and also reset to default
        static DrawMainSettings()
        {
            InitializeDropdownIndices();
        }

        internal static void InitializeDropdownIndices()
        {
            botDifficultiesPMCIndex = FindIndex(DefaultPluginVars.botDifficultiesPMC);
            botDifficultiesSCAVIndex = FindIndex(DefaultPluginVars.botDifficultiesSCAV);
            botDifficultiesOtherIndex = FindIndex(DefaultPluginVars.botDifficultiesOther);

            pmcGroupChanceIndex = FindIndex(DefaultPluginVars.pmcGroupChance);
            scavGroupChanceIndex = FindIndex(DefaultPluginVars.scavGroupChance);

            pmcFactionIndex = FindIndex(DefaultPluginVars.pmcFaction);
            forceAllBotTypeIndex = FindIndex(DefaultPluginVars.forceAllBotType);

            if (DefaultPluginVars.pmcScenarioSelection?.Options != null && DefaultPluginVars.pmcScenarioSelection.Options.Length > 0)
            {
                pmcScenarioSelectionIndex = FindIndex(DefaultPluginVars.pmcScenarioSelection);
            }

            if (DefaultPluginVars.scavScenarioSelection?.Options != null && DefaultPluginVars.scavScenarioSelection.Options.Length > 0)
            {
                scavScenarioSelectionIndex = FindIndex(DefaultPluginVars.scavScenarioSelection);
            }

            scenariosLoaded = (DefaultPluginVars.pmcScenarioSelection?.Options != null && DefaultPluginVars.pmcScenarioSelection.Options.Length > 0) &&
                              (DefaultPluginVars.scavScenarioSelection?.Options != null && DefaultPluginVars.scavScenarioSelection.Options.Length > 0);

#if DEBUG
            //DonutsPlugin.Logger.LogError("scenariosLoaded:" + scenariosLoaded);
#endif
        }

        internal static void Enable()
        {
            // Apply the custom skin to ensure consistency
            PluginGUIHelper.ApplyCustomSkin(() =>
            {
                // Initialize the custom styles for the dropdown
                GUILayout.Space(30);
                GUILayout.BeginHorizontal();

                // Left-hand navigation menu for sub-tabs
                GUILayout.BeginVertical(GUILayout.Width(150));

                GUILayout.Space(20);
                DrawSubTabs();
                GUILayout.EndVertical();

                // Space between menu and subtab pages
                GUILayout.Space(40);

                // Right-hand content area for selected sub-tab
                GUILayout.BeginVertical();

                switch (selectedMainSettingsIndex)
                {
                    case 0:
                        DrawMainSettingsGeneral();
                        break;
                    case 1:
                        DrawMainSettingsSpawnFrequency();
                        break;
                    case 2:
                        DrawMainSettingsBotAttributes();
                        break;
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            });
        }

        private static void DrawSubTabs()
        {
            for (int i = 0; i < mainSettingsSubTabs.Length; i++)
            {
                GUIStyle currentStyle = customSkin.customStyles[2]; // subTabButtonStyle
                if (selectedMainSettingsIndex == i)
                {
                    currentStyle = customSkin.customStyles[3]; // subTabButtonActiveStyle
                }

                if (GUILayout.Button(mainSettingsSubTabs[i], currentStyle))
                {
                    selectedMainSettingsIndex = i;
                }
            }
        }

        internal static void DrawMainSettingsGeneral()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            DefaultPluginVars.PluginEnabled.Value = Toggle(DefaultPluginVars.PluginEnabled.Name, DefaultPluginVars.PluginEnabled.ToolTipText, DefaultPluginVars.PluginEnabled.Value);
            GUILayout.Space(10);

            DefaultPluginVars.DespawnEnabledPMC.Value = Toggle(DefaultPluginVars.DespawnEnabledPMC.Name, DefaultPluginVars.DespawnEnabledPMC.ToolTipText, DefaultPluginVars.DespawnEnabledPMC.Value);
            GUILayout.Space(10);

            DefaultPluginVars.DespawnEnabledSCAV.Value = Toggle(DefaultPluginVars.DespawnEnabledSCAV.Name, DefaultPluginVars.DespawnEnabledSCAV.ToolTipText, DefaultPluginVars.DespawnEnabledSCAV.Value);
            GUILayout.Space(10);

            DefaultPluginVars.despawnInterval.Value = Slider(DefaultPluginVars.despawnInterval.Name, DefaultPluginVars.despawnInterval.ToolTipText, DefaultPluginVars.despawnInterval.Value, 0f, 1000f);
            GUILayout.Space(10);

            DefaultPluginVars.ShowRandomFolderChoice.Value = Toggle(DefaultPluginVars.ShowRandomFolderChoice.Name, DefaultPluginVars.ShowRandomFolderChoice.ToolTipText, DefaultPluginVars.ShowRandomFolderChoice.Value);
            GUILayout.Space(10);

            DefaultPluginVars.battleStateCoolDown.Value = Slider(DefaultPluginVars.battleStateCoolDown.Name, DefaultPluginVars.battleStateCoolDown.ToolTipText, DefaultPluginVars.battleStateCoolDown.Value, 0f, 1000f);
            GUILayout.Space(10);

            if (scenariosLoaded)
            {
                pmcScenarioSelectionIndex = Dropdown(DefaultPluginVars.pmcScenarioSelection, pmcScenarioSelectionIndex);
                DefaultPluginVars.pmcScenarioSelection.Value = DefaultPluginVars.pmcScenarioSelection.Options[pmcScenarioSelectionIndex];
                GUILayout.Space(10);

                scavScenarioSelectionIndex = Dropdown(DefaultPluginVars.scavScenarioSelection, scavScenarioSelectionIndex);
                DefaultPluginVars.scavScenarioSelection.Value = DefaultPluginVars.scavScenarioSelection.Options[scavScenarioSelectionIndex];
            }
            else
            {
                GUILayout.Label("Loading PMC scenarios...");
                GUILayout.Label("Loading SCAV scenarios...");
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        internal static void DrawMainSettingsSpawnFrequency()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            DefaultPluginVars.HardCapEnabled.Value = Toggle(DefaultPluginVars.HardCapEnabled.Name, DefaultPluginVars.HardCapEnabled.ToolTipText, DefaultPluginVars.HardCapEnabled.Value);
            GUILayout.Space(10);

            DefaultPluginVars.useTimeBasedHardStop.Value = Toggle(DefaultPluginVars.useTimeBasedHardStop.Name, DefaultPluginVars.useTimeBasedHardStop.ToolTipText, DefaultPluginVars.useTimeBasedHardStop.Value);
            GUILayout.Space(10);

            DefaultPluginVars.hardStopOptionPMC.Value = Toggle(DefaultPluginVars.hardStopOptionPMC.Name, DefaultPluginVars.hardStopOptionPMC.ToolTipText, DefaultPluginVars.hardStopOptionPMC.Value);
            GUILayout.Space(10);

            if (DefaultPluginVars.useTimeBasedHardStop.Value)
            {
                DefaultPluginVars.hardStopTimePMC.Value = Slider(DefaultPluginVars.hardStopTimePMC.Name, DefaultPluginVars.hardStopTimePMC.ToolTipText, DefaultPluginVars.hardStopTimePMC.Value, 0, 10000);
            }
            else
            {
                DefaultPluginVars.hardStopPercentPMC.Value = Slider(DefaultPluginVars.hardStopPercentPMC.Name, DefaultPluginVars.hardStopPercentPMC.ToolTipText, DefaultPluginVars.hardStopPercentPMC.Value, 0, 100);
            }
            GUILayout.Space(10);

            DefaultPluginVars.hardStopOptionSCAV.Value = Toggle(DefaultPluginVars.hardStopOptionSCAV.Name, DefaultPluginVars.hardStopOptionSCAV.ToolTipText, DefaultPluginVars.hardStopOptionSCAV.Value);
            GUILayout.Space(10);

            if (DefaultPluginVars.useTimeBasedHardStop.Value)
            {
                DefaultPluginVars.hardStopTimeSCAV.Value = Slider(DefaultPluginVars.hardStopTimeSCAV.Name, DefaultPluginVars.hardStopTimeSCAV.ToolTipText, DefaultPluginVars.hardStopTimeSCAV.Value, 0, 10000);
            }
            else
            {
                DefaultPluginVars.hardStopPercentSCAV.Value = Slider(DefaultPluginVars.hardStopPercentSCAV.Name, DefaultPluginVars.hardStopPercentSCAV.ToolTipText, DefaultPluginVars.hardStopPercentSCAV.Value, 0, 100);
            }
            GUILayout.Space(10);

            GUILayout.EndVertical();
            GUILayout.BeginVertical();

            DefaultPluginVars.coolDownTimer.Value = Slider(DefaultPluginVars.coolDownTimer.Name, DefaultPluginVars.coolDownTimer.ToolTipText, DefaultPluginVars.coolDownTimer.Value, 0f, 1000f);
            GUILayout.Space(10);

            DefaultPluginVars.hotspotBoostPMC.Value = Toggle(DefaultPluginVars.hotspotBoostPMC.Name, DefaultPluginVars.hotspotBoostPMC.ToolTipText, DefaultPluginVars.hotspotBoostPMC.Value);
            GUILayout.Space(10);

            DefaultPluginVars.hotspotBoostSCAV.Value = Toggle(DefaultPluginVars.hotspotBoostSCAV.Name, DefaultPluginVars.hotspotBoostSCAV.ToolTipText, DefaultPluginVars.hotspotBoostSCAV.Value);
            GUILayout.Space(10);

            DefaultPluginVars.hotspotIgnoreHardCapPMC.Value = Toggle(DefaultPluginVars.hotspotIgnoreHardCapPMC.Name, DefaultPluginVars.hotspotIgnoreHardCapPMC.ToolTipText, DefaultPluginVars.hotspotIgnoreHardCapPMC.Value);
            GUILayout.Space(10);

            DefaultPluginVars.hotspotIgnoreHardCapSCAV.Value = Toggle(DefaultPluginVars.hotspotIgnoreHardCapSCAV.Name, DefaultPluginVars.hotspotIgnoreHardCapSCAV.ToolTipText, DefaultPluginVars.hotspotIgnoreHardCapSCAV.Value);
            GUILayout.Space(10);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            pmcGroupChanceIndex = Dropdown(DefaultPluginVars.pmcGroupChance, pmcGroupChanceIndex);
            DefaultPluginVars.pmcGroupChance.Value = DefaultPluginVars.pmcGroupChance.Options[pmcGroupChanceIndex];
            GUILayout.Space(10);

            scavGroupChanceIndex = Dropdown(DefaultPluginVars.scavGroupChance, scavGroupChanceIndex);
            DefaultPluginVars.scavGroupChance.Value = DefaultPluginVars.scavGroupChance.Options[scavGroupChanceIndex];

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        internal static void DrawMainSettingsBotAttributes()
        {
            // Draw other spawn settings 
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            // Add spacing before each control to ensure proper vertical alignment
            botDifficultiesPMCIndex = Dropdown(DefaultPluginVars.botDifficultiesPMC, botDifficultiesPMCIndex);
            DefaultPluginVars.botDifficultiesPMC.Value = DefaultPluginVars.botDifficultiesPMC.Options[botDifficultiesPMCIndex];

            GUILayout.Space(10); // Add vertical space

            botDifficultiesSCAVIndex = Dropdown(DefaultPluginVars.botDifficultiesSCAV, botDifficultiesSCAVIndex);
            DefaultPluginVars.botDifficultiesSCAV.Value = DefaultPluginVars.botDifficultiesSCAV.Options[botDifficultiesSCAVIndex];

            GUILayout.Space(10); // Add vertical space

            botDifficultiesOtherIndex = Dropdown(DefaultPluginVars.botDifficultiesOther, botDifficultiesOtherIndex);
            DefaultPluginVars.botDifficultiesOther.Value = DefaultPluginVars.botDifficultiesOther.Options[botDifficultiesOtherIndex];

            GUILayout.Space(10); // Add vertical space

            pmcFactionIndex = Dropdown(DefaultPluginVars.pmcFaction, pmcFactionIndex);
            DefaultPluginVars.pmcFaction.Value = DefaultPluginVars.pmcFaction.Options[pmcFactionIndex];

            GUILayout.Space(10); // Add vertical space

            forceAllBotTypeIndex = Dropdown(DefaultPluginVars.forceAllBotType, forceAllBotTypeIndex);
            DefaultPluginVars.forceAllBotType.Value = DefaultPluginVars.forceAllBotType.Options[forceAllBotTypeIndex];

            GUILayout.Space(10); // Add vertical space

            DefaultPluginVars.pmcFactionRatio.Value = Slider(DefaultPluginVars.pmcFactionRatio.Name,
                DefaultPluginVars.pmcFactionRatio.ToolTipText, DefaultPluginVars.pmcFactionRatio.Value, 0, 100);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }




}
