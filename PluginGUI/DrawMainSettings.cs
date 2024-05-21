using System.Collections.Generic;
using UnityEngine;
using Donuts.Models;
using static Donuts.PluginGUIHelper;

namespace Donuts
{
    internal class DrawMainSettings
    {
        private static int selectedMainSettingsIndex = 0;
        private static string[] mainSettingsSubTabs = { "General", "Spawn Frequency", "Bot Attributes" };

        //for dropdowns
        internal static int botDifficultiesPMCIndex = 0;
        internal static int botDifficultiesSCAVIndex = 0;
        internal static int botDifficultiesOtherIndex = 0;
        internal static int pmcGroupChanceIndex = 0;
        internal static int scavGroupChanceIndex = 0;
        internal static int pmcFactionIndex = 0;
        internal static int forceAllBotTypeIndex = 0;

        //need this for dropdowns to show loaded values
        static DrawMainSettings()
        {
            InitializeDropdownIndices();
        }
        private static void InitializeDropdownIndices()
        {
            botDifficultiesPMCIndex = FindIndex(DefaultPluginVars.botDifficultiesPMC);
            botDifficultiesSCAVIndex = FindIndex(DefaultPluginVars.botDifficultiesSCAV);
            botDifficultiesOtherIndex = FindIndex(DefaultPluginVars.botDifficultiesOther);

            pmcGroupChanceIndex = FindIndex(DefaultPluginVars.pmcGroupChance);
            scavGroupChanceIndex = FindIndex(DefaultPluginVars.scavGroupChance);

            pmcFactionIndex = FindIndex(DefaultPluginVars.pmcFaction);
            forceAllBotTypeIndex = FindIndex(DefaultPluginVars.forceAllBotType);
        }
        private static int FindIndex<T>(Setting<T> setting)
        {
            for (int i = 0; i < setting.Options.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(setting.Options[i], setting.Value))
                {
                    return i;
                }
            }
            return 0;
        }

        internal static void Enable()
        {
            // Apply the cached styles to ensure consistency
            PluginGUIHelper.ApplyCachedStyles();

            // Initialize the custom styles for the dropdown
            ImGUIToolkit.InitializeStyles();
            GUILayout.Space(30);
            GUILayout.BeginHorizontal();

            // Left-hand navigation menu for sub-tabs
            GUILayout.BeginVertical(GUILayout.Width(150));

            GUILayout.Space(20);
            DrawSubTabs();
            GUILayout.EndVertical();

            //space between menu and subtab pages
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
        }

        private static void DrawSubTabs()
        {
            for (int i = 0; i < mainSettingsSubTabs.Length; i++)
            {
                GUIStyle currentStyle = cachedSubTabButtonStyle;
                if (selectedMainSettingsIndex == i)
                {
                    currentStyle = cachedSubTabButtonActiveStyle;
                }

                // Set background color explicitly for each button
                PluginGUIHelper.ApplyCachedStyles();

                if (GUILayout.Button(mainSettingsSubTabs[i], currentStyle))
                {
                    selectedMainSettingsIndex = i;
                }
            }
        }

        internal static void DrawMainSettingsGeneral()
        {
            // Draw general spawn settings
            GUILayout.Label("Main Settings: General", cachedSubTabLabelStyle);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            DefaultPluginVars.PluginEnabled.Value = ImGUIToolkit.Toggle(DefaultPluginVars.PluginEnabled.Name, DefaultPluginVars.PluginEnabled.Value);
            DefaultPluginVars.DespawnEnabledPMC.Value = ImGUIToolkit.Toggle(DefaultPluginVars.DespawnEnabledPMC.Name, DefaultPluginVars.DespawnEnabledPMC.Value);
            DefaultPluginVars.DespawnEnabledSCAV.Value = ImGUIToolkit.Toggle(DefaultPluginVars.DespawnEnabledSCAV.Name, DefaultPluginVars.DespawnEnabledSCAV.Value);
            DefaultPluginVars.ShowRandomFolderChoice.Value = ImGUIToolkit.Toggle(DefaultPluginVars.ShowRandomFolderChoice.Name, DefaultPluginVars.ShowRandomFolderChoice.Value);
            DefaultPluginVars.battleStateCoolDown.Value = ImGUIToolkit.Slider(DefaultPluginVars.battleStateCoolDown.Name, DefaultPluginVars.battleStateCoolDown.Value, 0f, 1000f);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        internal static void DrawMainSettingsSpawnFrequency()
        {
            // Draw advanced spawn settings
            GUILayout.Label("Main Settings: Spawn Frequency Settings", cachedSubTabLabelStyle);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            DefaultPluginVars.HardCapEnabled.Value = ImGUIToolkit.Toggle(DefaultPluginVars.HardCapEnabled.Name, DefaultPluginVars.HardCapEnabled.Value);
            DefaultPluginVars.hardStopOptionPMC.Value = ImGUIToolkit.Toggle(DefaultPluginVars.hardStopOptionPMC.Name, DefaultPluginVars.hardStopOptionPMC.Value);
            DefaultPluginVars.hardStopTimePMC.Value = ImGUIToolkit.Slider(DefaultPluginVars.hardStopTimePMC.Name, DefaultPluginVars.hardStopTimePMC.Value, 0, 10000);
            DefaultPluginVars.hardStopOptionSCAV.Value = ImGUIToolkit.Toggle(DefaultPluginVars.hardStopOptionSCAV.Name, DefaultPluginVars.hardStopOptionSCAV.Value);
            DefaultPluginVars.hardStopTimeSCAV.Value = ImGUIToolkit.Slider(DefaultPluginVars.hardStopTimeSCAV.Name, DefaultPluginVars.hardStopTimeSCAV.Value, 0, 10000);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            DefaultPluginVars.coolDownTimer.Value = ImGUIToolkit.Slider(DefaultPluginVars.coolDownTimer.Name, DefaultPluginVars.coolDownTimer.Value, 0f, 1000f);
            DefaultPluginVars.hotspotBoostPMC.Value = ImGUIToolkit.Toggle(DefaultPluginVars.hotspotBoostPMC.Name, DefaultPluginVars.hotspotBoostPMC.Value);
            DefaultPluginVars.hotspotBoostSCAV.Value = ImGUIToolkit.Toggle(DefaultPluginVars.hotspotBoostSCAV.Name, DefaultPluginVars.hotspotBoostSCAV.Value);
            DefaultPluginVars.hotspotIgnoreHardCapPMC.Value = ImGUIToolkit.Toggle(DefaultPluginVars.hotspotIgnoreHardCapPMC.Name, DefaultPluginVars.hotspotIgnoreHardCapPMC.Value);
            DefaultPluginVars.hotspotIgnoreHardCapSCAV.Value = ImGUIToolkit.Toggle(DefaultPluginVars.hotspotIgnoreHardCapSCAV.Name, DefaultPluginVars.hotspotIgnoreHardCapSCAV.Value);
        
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            pmcGroupChanceIndex = ImGUIToolkit.Dropdown(DefaultPluginVars.pmcGroupChance, pmcGroupChanceIndex);
            DefaultPluginVars.pmcGroupChance.Value = DefaultPluginVars.pmcGroupChance.Options[pmcGroupChanceIndex];

            scavGroupChanceIndex = ImGUIToolkit.Dropdown(DefaultPluginVars.scavGroupChance, scavGroupChanceIndex);
            DefaultPluginVars.scavGroupChance.Value = DefaultPluginVars.scavGroupChance.Options[scavGroupChanceIndex];

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        internal static void DrawMainSettingsBotAttributes()
        {
            // Draw other spawn settings
            GUILayout.Label("Main Settings: Bot Attributes", cachedSubTabLabelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            // Draw the dropdowns with proper handling of selected indices
            botDifficultiesPMCIndex = ImGUIToolkit.Dropdown(DefaultPluginVars.botDifficultiesPMC, botDifficultiesPMCIndex);
            DefaultPluginVars.botDifficultiesPMC.Value = DefaultPluginVars.botDifficultiesPMC.Options[botDifficultiesPMCIndex];

            botDifficultiesSCAVIndex = ImGUIToolkit.Dropdown(DefaultPluginVars.botDifficultiesSCAV, botDifficultiesSCAVIndex);
            DefaultPluginVars.botDifficultiesSCAV.Value = DefaultPluginVars.botDifficultiesSCAV.Options[botDifficultiesSCAVIndex];

            botDifficultiesOtherIndex = ImGUIToolkit.Dropdown(DefaultPluginVars.botDifficultiesOther, botDifficultiesOtherIndex);
            DefaultPluginVars.botDifficultiesOther.Value = DefaultPluginVars.botDifficultiesOther.Options[botDifficultiesOtherIndex];

            pmcFactionIndex = ImGUIToolkit.Dropdown(DefaultPluginVars.pmcFaction, pmcFactionIndex);
            DefaultPluginVars.pmcFaction.Value = DefaultPluginVars.pmcFaction.Options[pmcFactionIndex];

            forceAllBotTypeIndex = ImGUIToolkit.Dropdown(DefaultPluginVars.forceAllBotType, forceAllBotTypeIndex);
            DefaultPluginVars.forceAllBotType.Value = DefaultPluginVars.forceAllBotType.Options[forceAllBotTypeIndex];

            DefaultPluginVars.pmcFactionRatio.Value = ImGUIToolkit.Slider(DefaultPluginVars.pmcFactionRatio.Name, DefaultPluginVars.pmcFactionRatio.Value, 0, 100);
            
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }

      
        
    
}
