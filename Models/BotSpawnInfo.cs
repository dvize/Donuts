using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using HarmonyLib;
using UnityEngine;

namespace Donuts.Models
{
    public class BotSpawnInfo
    {
        public bool HasSpawned { get; set; } = false;

        public int GroupNumber { get; private set; }
        public GClass513 Data { get; private set; }
        public List<BotOwner> Owners { get; set; } = new List<BotOwner>();

        // The key should be Profile.Id for each bot that's generated
        private Dictionary<string, WildSpawnType> OriginalBotSpawnTypes = new Dictionary<string, WildSpawnType>();

        public BotSpawnInfo(int groupNum, GClass513 data)
        {
            GroupNumber = groupNum;
            Data = data;
        }

        public WildSpawnType? GetOriginalSpawnTypeForBot(BotOwner bot)
        {
            if (!OriginalBotSpawnTypes.ContainsKey(bot.Profile.Id))
            {
                return null;
            }

            return OriginalBotSpawnTypes[bot.Profile.Id];
        }

        public void UpdateOriginalSpawnTypes()
        {
            foreach (Profile profile in Data.Profiles)
            {
                OriginalBotSpawnTypes.Add(profile.Id, profile.Info.Settings.Role);
            }
        }
    }
}
