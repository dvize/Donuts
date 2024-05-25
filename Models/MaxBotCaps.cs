using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Donuts.Models
{
    public class MaxBotCap
    {
        public int PMC { get; set; }
        public int SCAV { get; set; }
    }

    public class MapConfig
    {
        public Dictionary<string, MaxBotCap> MaxBotCaps { get; set; }

        public static MapConfig LoadFromJson(string filePath)
        {
            var jsonString = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<MapConfig>(jsonString);
        }
    }
}
