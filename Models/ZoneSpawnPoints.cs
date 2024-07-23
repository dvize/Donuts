using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Donuts.Models
{
    public class Coordinate
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }

    public class MapZoneConfig
    {
        public string MapName { get; set; }
        public Dictionary<string, List<Coordinate>> Zones { get; set; }
    }

    public class AllMapsZoneConfig
    {
        public Dictionary<string, MapZoneConfig> Maps { get; set; } = new Dictionary<string, MapZoneConfig>();

        public static AllMapsZoneConfig LoadFromDirectory(string directoryPath)
        {
            var allMapsConfig = new AllMapsZoneConfig();
            var files = Directory.GetFiles(directoryPath, "*.json");

            foreach (var file in files)
            {
                var jsonString = File.ReadAllText(file);
                var mapConfig = JsonConvert.DeserializeObject<MapZoneConfig>(jsonString);

                if (mapConfig != null)
                {
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
                        if (!allMapsConfig.Maps[mapConfig.MapName].Zones.ContainsKey(zone.Key))
                        {
                            allMapsConfig.Maps[mapConfig.MapName].Zones[zone.Key] = new List<Coordinate>();
                        }

                        allMapsConfig.Maps[mapConfig.MapName].Zones[zone.Key].AddRange(zone.Value);
                    }
                }
            }

            return allMapsConfig;
        }
    }
}
