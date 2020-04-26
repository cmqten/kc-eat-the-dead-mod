/*
No pork? No problem! Dead villagers now provide meat, which can be your temporary source early in the game, or 
permanently if you're a damn savage.

Author: https://steamcommunity.com/id/cmjten10/
Mod Version: 1
Target K&C Version: 117r5s-mods
Date: 2020-04-26
*/
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace EatTheDeadMod 
{
    public class ModInit : MonoBehaviour 
    {
        public static KCModHelper helper;

        void Preload(KCModHelper __helper) 
        {
            helper = __helper;
            var harmony = HarmonyInstance.Create("harmony");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
    
    // Player::DestroyPerson patch for dropping meat on death.
    [HarmonyPatch(typeof(Player))]
    [HarmonyPatch("DestroyPerson")]
    public static class DeadVillagerMeatPatch
    {
        public static int meatDrop = 2;

        public static void Prefix(Villager p, bool leaveBehindBody)
        {
            if (leaveBehindBody)
            {
                GameObject meatStack = FreeResourceManager.inst.GetAutoStackFor(FreeResourceType.Pork, meatDrop);
                meatStack.transform.position = p.transform.position.xz() + new Vector3(0f, 0.05f, 0f);
            }
        }
    }
}
