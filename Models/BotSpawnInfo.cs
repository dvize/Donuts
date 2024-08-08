using System.Collections.Generic;
using EFT;
using EFT.Bots;
using UnityEngine;

public class BotSpawnInfo
{
    public WildSpawnType BotType { get; set; }
    public int GroupSize { get; set; }
    public List<Vector3> Coordinates { get; set; }
    public BotDifficulty Difficulty { get; set; }
    public EPlayerSide Faction { get; set; }
    public string Zone { get; set; }

    public BotSpawnInfo(WildSpawnType botType, int groupSize, List<Vector3> coordinates, BotDifficulty difficulty, EPlayerSide faction, string zone)
    {
        BotType = botType;
        GroupSize = groupSize;
        Coordinates = coordinates;
        Difficulty = difficulty;
        Faction = faction;
        Zone = zone;
    }
}
