using Donuts;
using Donuts.Models;
using EFT;
using System.Collections.Generic;

internal class BossSpawn
{
    public int BossChance
    {
        get; set;
    }
    public string BossName
    {
        get; set;
    }
    public List<string> Zones
    {
        get; set;
    }
    public List<Support> Supports
    {
        get; set;
    }
    public int TimeDelay
    {
        get; set;
    }
    public int MaxTriggersBeforeCooldown
    {
        get; set;
    }
    public bool IgnoreTimerFirstSpawn
    {
        get; set;
    }

    public string TriggerID
    {
        get; set;
    }

    public bool HasBeenTriggeredBySwitch { get; set; } = false;

    // Cooldown
    public bool InCooldown
    {
        get; set;
    }
    public float CooldownTimer
    {
        get; set;
    }
    public int TimesSpawned
    {
        get; set;
    }

    // Stop Spamming Timers
    public bool IsSpawnPending
    {
        get; set;
    }

    public BossSpawn()
    {
        this.InCooldown = false;
        this.CooldownTimer = 0f;
        this.TimesSpawned = 0;
        this.IgnoreTimerFirstSpawn = false;
        this.IsSpawnPending = false;
    }

    public void UpdateCooldown(float deltaTime, float cooldownDuration)
    {
        if (InCooldown)
        {
            CooldownTimer += deltaTime;

            if (CooldownTimer >= cooldownDuration)
            {
                InCooldown = false;
                CooldownTimer = 0f;
                TimesSpawned = 0;
            }
        }
    }

    public bool ShouldSpawn()
    {
        if (InCooldown || IsSpawnPending)
        {
            return false;
        }

        if (IgnoreTimerFirstSpawn && TimesSpawned == 0)
        {
            return true; 
        }

        return TimesSpawned < MaxTriggersBeforeCooldown;
    }

    public void TriggerCooldown()
    {
        InCooldown = true;
        CooldownTimer = 0f;
        IsSpawnPending = false; 
    }
}



public class Support
{
    public int BossEscortAmount { get; set; }
    public string BossEscortType { get; set; }
}