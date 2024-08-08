using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Donuts.Models
{
    internal class Coordinate
    {
        [JsonProperty("x")]
        internal float X
        {
            get; set;
        }

        [JsonProperty("y")]
        internal float Y
        {
            get; set;
        }

        [JsonProperty("z")]
        internal float Z
        {
            get; set;
        }
    }

    internal class MapZoneConfig
    {
        public string MapName
        {
            get; set;
        }
        public Dictionary<string, List<Coordinate>> Zones
        {
            get; set;
        }
    }

    internal class AllMapsZoneConfig
    {
        internal Dictionary<string, MapZoneConfig> Maps { get; set; } = new Dictionary<string, MapZoneConfig>();

        internal static AllMapsZoneConfig LoadFromDirectory(string directoryPath)
        {
            var allMapsConfig = new AllMapsZoneConfig();
            var files = Directory.GetFiles(directoryPath, "*.json");

            Debug.Log($"{nameof(AllMapsZoneConfig)}.{nameof(LoadFromDirectory)}: Loading files from directory: {directoryPath}");
            Debug.Log($"{nameof(AllMapsZoneConfig)}.{nameof(LoadFromDirectory)}: Found {files.Length} files.");

            foreach (var file in files)
            {
                try
                {
                    var jsonString = File.ReadAllText(file);

                    if (string.IsNullOrEmpty(jsonString))
                    {
                        Debug.LogError($"{nameof(AllMapsZoneConfig)}.{nameof(LoadFromDirectory)}: File {file} is empty or null.");
                        continue;
                    }

                    // Deserialize with error handling
                    var mapConfig = JsonConvert.DeserializeObject<MapZoneConfig>(jsonString, new JsonSerializerSettings
                    {
                        Error = (sender, args) =>
                        {
                            Debug.LogError($"{nameof(AllMapsZoneConfig)}.{nameof(LoadFromDirectory)}: Error deserializing file {file}: {args.ErrorContext.Error.Message}");
                            args.ErrorContext.Handled = true;
                        }
                    });

                    if (mapConfig == null)
                    {
                        Debug.LogError($"{nameof(AllMapsZoneConfig)}.{nameof(LoadFromDirectory)}: Failed to deserialize JSON from file: {file}");
                        continue;
                    }

                    if (string.IsNullOrEmpty(mapConfig.MapName))
                    {
                        Debug.LogError($"{nameof(AllMapsZoneConfig)}.{nameof(LoadFromDirectory)}: MapName is null or empty in file: {file}");
                        continue;
                    }

                    Debug.Log($"{nameof(AllMapsZoneConfig)}.{nameof(LoadFromDirectory)}: Loaded map: {mapConfig.MapName}");

                    if (!allMapsConfig.Maps.ContainsKey(mapConfig.MapName))
                    {
                        allMapsConfig.Maps[mapConfig.MapName] = new MapZoneConfig
                        {
                            MapName = mapConfig.MapName,
                            Zones = new Dictionary<string, List<Coordinate>>()
                        };
                    }

                    foreach (var zone in mapConfig.Zones)
                    {
                        if (string.IsNullOrEmpty(zone.Key))
                        {
                            Debug.LogError($"{nameof(AllMapsZoneConfig)}.{nameof(LoadFromDirectory)}: Zone key is null or empty in map: {mapConfig.MapName} from file: {file}");
                            continue;
                        }

                        if (!allMapsConfig.Maps[mapConfig.MapName].Zones.ContainsKey(zone.Key))
                        {
                            allMapsConfig.Maps[mapConfig.MapName].Zones[zone.Key] = new List<Coordinate>();
                        }

                        allMapsConfig.Maps[mapConfig.MapName].Zones[zone.Key].AddRange(zone.Value);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{nameof(AllMapsZoneConfig)}.{nameof(LoadFromDirectory)}: Exception while processing file {file}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            return allMapsConfig;
        }
    }
}
