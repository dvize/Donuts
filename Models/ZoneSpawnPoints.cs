using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Donuts.Models
{
    public class Coordinate
    {
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
    }

    public class Zones
    {
        public Dictionary<string, List<Coordinate>> Locations { get; set; }
    }

    public class MapZoneConfig
    {
        public string MapName { get; set; }
        public Dictionary<string, Dictionary<string, List<Coordinate>>> Zones { get; set; }
    }

    public class AllMapsZoneConfig
    {
        public List<MapZoneConfig> Maps { get; set; } = new List<MapZoneConfig>();

        public static AllMapsZoneConfig LoadFromDirectory(string directoryPath)
        {
            var allMapsConfig = new AllMapsZoneConfig();
            var files = Directory.GetFiles(directoryPath, "*.json");

            foreach (var file in files)
            {
                var jsonString = File.ReadAllText(file);
                var mapConfig = JsonSerializer.Deserialize<MapZoneConfig>(jsonString);
                allMapsConfig.Maps.Add(mapConfig);
            }

            return allMapsConfig;
        }
    }
}
