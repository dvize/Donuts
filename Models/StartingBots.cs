using System.Collections.Generic;

namespace Donuts.Models
{
    public class BotConfig
    {
        public int MinCount { get; set; }
        public int MaxCount { get; set; }
        public int MinGroupSize { get; set; }
        public int MaxGroupSize { get; set; }
        public List<string> Zones { get; set; }
    }

    public class MapBotConfig
    {
        public BotConfig PMC { get; set; }
        public BotConfig SCAV { get; set; }
    }

    public class StartingBotConfig
    {
        public Dictionary<string, MapBotConfig> Maps { get; set; }
    }
}
