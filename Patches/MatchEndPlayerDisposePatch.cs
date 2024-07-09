using System;
using System.Collections.Generic;
using System.Reflection;
using SPT.Reflection.Patching;
using EFT;
using EFT.AssetsManager;
using HarmonyLib;
using UnityEngine;

namespace Donuts.Patches
{
    internal class MatchEndPlayerDisposePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Method used by SPT for finding BaseLocalGame
            return AccessTools.Method(typeof(BaseLocalGame<EftGamePlayerOwner>), nameof(BaseLocalGame<EftGamePlayerOwner>.smethod_4));
        }

        [PatchPrefix]
        private static bool PatchPrefix(IDictionary<string, Player> players)
        {
            foreach (Player player in players.Values)
            {
                if (player != null)
                {
                    try
                    {
                        player.Dispose();
                        AssetPoolObject.ReturnToPool(player.gameObject, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
            players.Clear();

            return false;
        }
    }
}
