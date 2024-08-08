using Donuts;
using Donuts.Models;
using EFT;
using System.Collections.Generic;

internal class BossSpawn
{
    public int BossChance { get; set; }
    public string BossName { get; set; }
    public List<string> Zones { get; set; }
    public List<Support> Supports { get; set; } 
    public int Time { get; set; }
}

public class Support
{
    public int BossEscortAmount { get; set; }
    public string BossEscortType { get; set; }
}