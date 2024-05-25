using System.Collections.Generic;

namespace Donuts.Models
{
    public class MaxBotCap
    {
        public int PMC { get; set; }
        public int SCAV { get; set; }
    }

    public class StartingBotConfig
    {
        public string Name { get; set; }
        public Dictionary<string, MaxBotCap> MaxBotCaps { get; set; }
    }

    public class BotManager
    {
        public List<StartingBotConfig> StartingBotsData { get; set; }

        public static BotManager LoadFromJson(string filePath)
        {
            var jsonString = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<BotManager>(jsonString);
        }
    }
}
