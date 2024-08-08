using EFT;
using EFT.Bots;

namespace Donuts.Models
{
    public class PrepBotInfo
    {
        public WildSpawnType SpawnType { get; set; }
        public BotDifficulty Difficulty { get; set; }
        public EPlayerSide Side { get; set; }
        public BotCreationDataClass Bots { get; set; }
        public bool IsGroup { get; set; }
        public int GroupSize { get; set; }

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
