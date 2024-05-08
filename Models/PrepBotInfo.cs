using System.Collections.Generic;
using EFT;
using BotCacheClass = GClass591;

namespace Donuts.Models
{
    public class PrepBotInfo
    {
        public WildSpawnType SpawnType
        {
            get; set;
        }
        public BotDifficulty Difficulty
        {
            get; set;
        }
        public EPlayerSide Side
        {
            get; set;
        }
        public BotCacheClass Bots //may be one or many bots based on Bots.Profiles.Count
        { 
            get; set; 
        }
        public bool IsGroup
        {
            get; set;
        }
        public int GroupSize
        {
            get; set;
        }

        public PrepBotInfo(WildSpawnType spawnType, BotDifficulty difficulty, EPlayerSide side, bool isGroup = false, int groupSize = 1)
        {
            SpawnType = spawnType;
            Difficulty = difficulty;
            Side = side;
            IsGroup = isGroup;
            GroupSize = groupSize;
        }
    }

}
