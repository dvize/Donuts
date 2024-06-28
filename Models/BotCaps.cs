using System.Collections.Generic;

namespace Donuts.Models
{
    public class BotCaps
    {
        public int PMC { get; set; }
        public int SCAV { get; set; }
    }

    public class MaxBotCaps
    {
        public Dictionary<string, BotCaps> MaxBotCapsConfig { get; set; }
    }
}
