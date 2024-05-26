using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Donuts.Models
{
    public class BotDetails
    {
        public int MinCount { get; set; }
        public int MaxCount { get; set; }
        public int MaxGroupSize { get; set; }
        public List<string> Zones { get; set; }
    }

    public class MapBotConfig
    {
        public BotDetails PMC { get; set; }
        public BotDetails SCAV { get; set; }
    }

    public class StartingBotConfig
    {
        public string Name { get; set; }
        public Dictionary<string, MapBotConfig> Maps { get; set; }
    }

    public class BotManager
    {
        public List<StartingBotConfig> StartingBotsData { get; set; }

        public static BotManager LoadFromJson(string filePath)
        {
            var jsonString = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<BotManager>(jsonString);
        }
    }
}
