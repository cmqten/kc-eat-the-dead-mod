/*
No pork? No problem! Dead villagers now provide meat, which can be your temporary source early in the game, or 
permanently if you're a damn savage.

Author: cmjten10
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
using Zat.Shared.ModMenu.API;

namespace EatTheDead 
{
    public class EatTheDeadMod : MonoBehaviour 
    {
        public static KCModHelper helper;
        public static ModSettingsProxy settingsProxy;

        private static System.Random random = new System.Random();
        private static int meatDrop = 2;
        private static bool randomDrop = false;

        void PreScriptLoad(KCModHelper __helper) 
        {
            helper = __helper;
            var harmony = HarmonyInstance.Create("harmony");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        void OnScriptLoad(KCModHelper __helper)
        {
            if (!settingsProxy)
            {
                ModConfig config = ModConfigBuilder
                    .Create("Eat The Dead", "v1", "cmjten10")
                    .AddSlider("Eat The Dead/Meat Drop", "Amount of meat dropped by the dead", "2", 0, 50, true, 2)
                    .AddToggle("Eat The Dead/Random", "Drop a random amount between 0 and Meat Drop", "Enabled", false)
                    .Build();
                ModSettingsBootstrapper.Register(config, OnProxyRegistered, OnProxyRegisterError);
            }
        }

        private void OnProxyRegistered(ModSettingsProxy proxy, SettingsEntry[] saved)
        {
            try
            {
                settingsProxy = proxy;
                helper.Log("SUCCESS: Registered proxy for Eat The Dead Mod Config");
                proxy.AddSettingsChangedListener("Eat The Dead/Meat Drop", (setting) =>
                {
                    meatDrop = (int)setting.slider.value;
                    setting.slider.label = meatDrop.ToString();
                    proxy.UpdateSetting(setting, null, null);
                });
                proxy.AddSettingsChangedListener("Eat The Dead/Random", (setting) =>
                {
                    randomDrop = setting.toggle.value;
                    proxy.UpdateSetting(setting, null, null);
                });

                // Apply saved values.
                foreach (var setting in saved)
                {
                    var own = proxy.Config[setting.path];
                    if (own != null)
                    {
                        own.CopyFrom(setting);
                        proxy.UpdateSetting(own, null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                helper.Log($"ERROR: Failed to register proxy for Eat The Dead Mod config: {ex.Message}");
                helper.Log(ex.StackTrace);
            }
        }

        private void OnProxyRegisterError(Exception ex)
        {
            helper.Log($"ERROR: Failed to register proxy for Eat The Dead Mod config: {ex.Message}");
            helper.Log($"{ex.StackTrace}");
        }

        // Player::DestroyPerson patch for dropping meat on death.
        [HarmonyPatch(typeof(Player))]
        [HarmonyPatch("DestroyPerson")]
        public static class DeadVillagerMeatPatch
        {
            public static void Prefix(Villager p, bool leaveBehindBody)
            {
                if (leaveBehindBody && meatDrop > 0)
                {
                    int amount = meatDrop;
                    if (randomDrop)
                    {
                        amount = random.Next(0, meatDrop + 1);
                    }
                    GameObject meat = FreeResourceManager.inst.GetAutoStackFor(FreeResourceType.Pork, amount);
                    meat.transform.position = p.transform.position.xz() + new Vector3(0f, 0.05f, 0f);
                }
            }
        }
    }
}
