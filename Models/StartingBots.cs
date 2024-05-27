using System;
using System.Collections.Generic;
using System.IO;
using Donuts.Models;
using Newtonsoft.Json;

namespace Donuts.Models
{
    public class BotConfig
    {
        public int MinCount { get; set; }
        public int MaxCount { get; set; }
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
        public string Name { get; set; }
        public Dictionary<string, MapBotConfig> Maps { get; set; }
    }

    public class StartingBotsManager
    {
        public List<StartingBotConfig> StartingBotsData { get; set; }
    }
}
