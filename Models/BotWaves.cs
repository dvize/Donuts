using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Donuts.Models
{
    internal class BotWave
    {
        public int GroupNum
        {
            get; set;
        }
        public int TriggerTimer
        {
            get; set;
        }
        public int TriggerDistance
        {
            get; set;
        }
        public int SpawnChance
        {
            get; set;
        }
        public int MaxTriggersBeforeCooldown
        {
            get; set;
        }
        public bool IgnoreTimerFirstSpawn
        {
            get; set;
        }
        public int MinGroupSize
        {
            get; set;
        }
        public int MaxGroupSize
        {
            get; set;
        }
        public List<string> Zones
        {
            get; set;
        }

        // Timer properties
        public float timer;
        public bool InCooldown
        {
            get; set;
        }
        public int TimesSpawned
        {
            get; set;
        }
        public float cooldownTimer;

        public BotWave()
        {
            this.timer = 0f;
            this.InCooldown = false;
            this.TimesSpawned = 0;
            this.cooldownTimer = 0f;
        }

        public void UpdateTimer(float deltaTime, float coolDownDuration)
        {
            timer += deltaTime;
            if (InCooldown)
            {
                cooldownTimer += deltaTime;

                if (cooldownTimer >= coolDownDuration)
                {
                    InCooldown = false;
                    cooldownTimer = 0f;
                    TimesSpawned = 0;
                }
            }
        }

        public bool ShouldSpawn()
        {
            if (InCooldown)
            {
                return false;
            }

            if (IgnoreTimerFirstSpawn)
            {
                IgnoreTimerFirstSpawn = false; // Ensure this is only true for the first spawn
                return true;
            }

            return timer >= TriggerTimer;
        }

        public void ResetTimer()
        {
            timer = 0f;
        }

        public void TriggerCooldown()
        {
            InCooldown = true;
            cooldownTimer = 0f;
        }
    }

    internal class MapBotWaves
    {
        public List<BotWave> PMC { get; set; } = new List<BotWave>();
        public List<BotWave> SCAV { get; set; } = new List<BotWave>();
        public List<BossSpawn> BOSSES { get; set; } = new List<BossSpawn>();
    }

    internal class BotWavesConfig
    {
        public Dictionary<string, MapBotWaves> Maps { get; set; } = new Dictionary<string, MapBotWaves>();

        public BotWavesConfig GetBotWavesConfig(string selectionName, string mapName)
        {
            string dllPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(dllPath);
            string jsonFilePath = Path.Combine(directoryPath, "patterns", selectionName, $"{mapName}_waves.json");

            UnityEngine.Debug.LogWarning($"Attempting to load JSON file from: {jsonFilePath}");

            if (!File.Exists(jsonFilePath))
            {
                UnityEngine.Debug.LogError($"{mapName}_waves.json file not found at path: {jsonFilePath}");
                return null;
            }

            var jsonString = File.ReadAllText(jsonFilePath);
            //UnityEngine.Debug.LogWarning($"JSON file content: {jsonString}");

            try
            {
                var settings = new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore, // Ignore missing members in JSON
                    NullValueHandling = NullValueHandling.Include // Include null values if necessary
                };

                var botWavesData = JsonConvert.DeserializeObject<BotWavesConfig>(jsonString, settings);

                if (botWavesData != null && botWavesData.Maps != null && botWavesData.Maps.Count > 0)
                {
                    UnityEngine.Debug.LogWarning($"Successfully deserialized: {botWavesData.Maps.Count} map entries.");
                    foreach (var mapEntry in botWavesData.Maps)
                    {
                        UnityEngine.Debug.LogWarning($"Map: {mapEntry.Key}");
                        UnityEngine.Debug.LogWarning($"PMC count: {mapEntry.Value.PMC.Count}");
                        UnityEngine.Debug.LogWarning($"SCAV count: {mapEntry.Value.SCAV.Count}");
                        UnityEngine.Debug.LogWarning($"BOSSES count: {mapEntry.Value.BOSSES.Count}");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogError("Failed to deserialize BotWavesConfig or Maps is null/empty.");
                }

                EnsureUniqueGroupNumsForWave(ref botWavesData);
                return botWavesData;
            }
            catch (JsonSerializationException ex)
            {
                UnityEngine.Debug.LogError($"JSON deserialization error: {ex.Message}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Unexpected error during deserialization: {ex.Message}");
            }

            return null;
        }


        private void EnsureUniqueGroupNumsForWave(ref BotWavesConfig botWavesConfig)
        {
            if (botWavesConfig == null)
            {
                UnityEngine.Debug.LogError("EnsureUniqueGroupNumsForWave: BotWavesConfig is null.");
                return;
            }

            if (botWavesConfig.Maps == null || botWavesConfig.Maps.Values == null)
            {
                UnityEngine.Debug.LogError("EnsureUniqueGroupNumsForWave: BotWavesConfig.Maps.Values is null.");
                return;
            }

            foreach (var mapEntry in botWavesConfig.Maps)
            {
                if (mapEntry.Value == null)
                {
                    UnityEngine.Debug.LogError($"Map entry '{mapEntry.Key}' is null.");
                    continue;
                }

                UnityEngine.Debug.LogWarning($"Processing map: {mapEntry.Key}");

                if (mapEntry.Value.SCAV == null)
                {
                    UnityEngine.Debug.LogError($"SCAV waves for map '{mapEntry.Key}' are null.");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"Ensuring unique group numbers for SCAV waves on map '{mapEntry.Key}'.");
                    mapEntry.Value.SCAV = EnsureUniqueGroupNums(mapEntry.Value.SCAV);
                }

                if (mapEntry.Value.PMC == null)
                {
                    UnityEngine.Debug.LogError($"PMC waves for map '{mapEntry.Key}' are null.");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"Ensuring unique group numbers for PMC waves on map '{mapEntry.Key}'.");
                    mapEntry.Value.PMC = EnsureUniqueGroupNums(mapEntry.Value.PMC);
                }
            }
        }

        private List<BotWave> EnsureUniqueGroupNums(List<BotWave> botWaves)
        {
            if (botWaves == null)
            {
                UnityEngine.Debug.LogError("Bot waves list is null.");
                return new List<BotWave>(); // Return an empty list to prevent further errors
            }

            var uniqueWavesDict = new Dictionary<int, BotWave>();
            var groupedByGroupNum = botWaves.GroupBy(wave => wave.GroupNum);

            foreach (var group in groupedByGroupNum)
            {
                UnityEngine.Debug.LogWarning($"Processing group number: {group.Key} with {group.Count()} waves.");

                if (group.Count() > 1)
                {
                    var selectedWave = group.OrderBy(_ => UnityEngine.Random.value).First();
                    uniqueWavesDict[group.Key] = selectedWave;
                    UnityEngine.Debug.LogWarning($"Multiple waves found for group {group.Key}, selecting one randomly.");
                }
                else
                {
                    uniqueWavesDict[group.Key] = group.First();
                    UnityEngine.Debug.LogWarning($"Single wave found for group {group.Key}.");
                }
            }

            return uniqueWavesDict.Values.ToList();
        }
    }
}
