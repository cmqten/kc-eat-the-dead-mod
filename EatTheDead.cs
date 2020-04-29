/*
No pork? No problem! Dead villagers now provide meat, which can be your temporary source early in the game, or 
permanently if you're a damn savage.

Author: cmjten10
Mod Version: 1.1
Target K&C Version: 117r5s-mods
Date: 2020-04-30
*/
using Assets;
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

        // Meat drop on death
        private static int meatDrop = 2;
        private static bool randomDrop = false;

        // Grave digging
        private static bool graveDiggingEnabled = true;
        private static bool removeGraveAfterDigging = true;
        private static Dictionary<Cemetery, int> gravesToRemove = new Dictionary<Cemetery, int>();

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
                    .AddSlider("Eat The Dead/Meat Drop", "Amount of meat dropped by the dead", 
                        "2", 0, 50, true, meatDrop)
                    .AddToggle("Eat The Dead/Random Drop", "Drop a random amount between 0 and Meat Drop", 
                        "Enabled", randomDrop)
                    .AddToggle("Eat The Dead/Grave Digging", "Obtain meat from cemeteries", 
                        "Enabled", graveDiggingEnabled)
                    .AddToggle("Eat The Dead/Remove Grave After Digging", "", 
                        "Enabled", removeGraveAfterDigging)
                    .Build();
                ModSettingsBootstrapper.Register(config, OnProxyRegistered, OnProxyRegisterError);
            }
        }

        // =====================================================================
        // Mod Menu Functions
        // =====================================================================

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
                proxy.AddSettingsChangedListener("Eat The Dead/Random Drop", (setting) =>
                {
                    randomDrop = setting.toggle.value;
                    proxy.UpdateSetting(setting, null, null);
                });
                proxy.AddSettingsChangedListener("Eat The Dead/Grave Digging", (setting) =>
                {
                    graveDiggingEnabled = setting.toggle.value;
                    proxy.UpdateSetting(setting, null, null);
                });
                proxy.AddSettingsChangedListener("Eat The Dead/Remove Grave After Digging", (setting) =>
                {
                    removeGraveAfterDigging = setting.toggle.value;
                    ResetGraveDigging();
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

        // =====================================================================
        // Meat Drop on Death Utility Functions
        // =====================================================================

        private static void PlaceResourceAt(FreeResourceType resourceType, Vector3 position)
        {
            // Refer to Player::DestroyPerson
            FreeResource resource = FreeResourceManager.inst.GetPrefabFor(resourceType).CreateResource(position, -1);
            resource.Holder = null;
        }

        private static void PlaceResourceStackAt(FreeResourceType resourceType, int amount, Vector3 position)
        {
            // Refer to Swineherd::OnDemolished
            GameObject resource = FreeResourceManager.inst.GetAutoStackFor(resourceType, amount);
            resource.transform.position = position;
        }

        // =====================================================================
        // Grave Digging Utility Functions
        // =====================================================================

        private static void ResetGraveDigging()
        {
            gravesToRemove.Clear();
        }

        // =====================================================================
        // Patches
        // =====================================================================

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
                    // Refer to Swineherd::OnDemolished
                    Vector3 position = p.transform.position.xz() + new Vector3(0f, 0.05f, 0f);
                    PlaceResourceStackAt(FreeResourceType.Pork, amount, position);
                }
            }
        }

        // Cemetery::BuryPerson patch for placing meat under cemetery.
        [HarmonyPatch(typeof(Cemetery))]
        [HarmonyPatch("BuryPerson")]
        public static class PlaceMeatUnderCemeteryPatch
        {
            public static void Postfix(Cemetery __instance, bool __result)
            {
                if (__result && graveDiggingEnabled)
                {
                    // -0.05f hides the meat under the cemetery, but still accessible by villagers.
                    Vector3 position = __instance.B.Center().xz() + new Vector3(0f, -0.05f, 0f);
                    PlaceResourceAt(FreeResourceType.Pork, position);
                }
            }
        }

        // Villager::PickupResource patch for notifying Cemetery::Tick patch to remove a grave on pickup.
        [HarmonyPatch(typeof(Villager))]
        [HarmonyPatch("PickupResource")]
        [HarmonyPatch(new Type[] { typeof(FreeResource), typeof(bool) })]
        public static class SignalRemoveGraveOnMeatPickupPatch
        {
            public static void Prefix(Villager __instance, FreeResource resource)
            {
                if (resource.type == FreeResourceType.Pork && graveDiggingEnabled && removeGraveAfterDigging)
                {
                    Vector3 position = resource.transform.position;
                    int x = (int)position.x;
                    int z = (int)position.z;
                    if (Cemetery.IsCemetery(x, z))
                    {
                        // Refer to Cemetery::IsCemetery
                        Building building = World.inst.GetCellData(x, z).StructureFindByCategory(World.cemeteryHash);
                        Cemetery cemetery = building.GetComponentInParent<Cemetery>();

                        if (gravesToRemove.ContainsKey(cemetery))
                        {
                            gravesToRemove[cemetery]++;
                        }
                        else 
                        {
                            gravesToRemove.Add(cemetery, 1);
                        }
                    }
                }
            }
        }

        // Cemetery::Tick patch for removing a grave if meat was picked up.
        [HarmonyPatch(typeof(Cemetery))]
        [HarmonyPatch("Tick")]
        public static class RemoveGraveOnMeatPickupPatch
        {
            public static void Postfix(Cemetery __instance, GraveData[] ___gravesData, 
                RenderInstance[] ___gravesRenderInstance)
            {
                if (gravesToRemove.ContainsKey(__instance) && gravesToRemove[__instance] > 0 && 
                    graveDiggingEnabled && removeGraveAfterDigging)
                {
                    gravesToRemove[__instance]--;

                    // Select random grave to remove
                    int totalGraves = ___gravesData.Length;
                    int graveIndex = random.Next(0, totalGraves);
                    if (!___gravesData[graveIndex].occupied)
                    {
                        for (graveIndex = 0; graveIndex < totalGraves; graveIndex++)
                        {
                            if (___gravesData[graveIndex].occupied)
                            {
                                break;
                            }
                        }
                    }

                    // If graveIndex is equal to totalGraves, there are no free graves.
                    if (graveIndex < totalGraves)
                    {
                        // Refer to Cemetery::Tick
                        ___gravesRenderInstance[graveIndex].Visible = false;
                        ___gravesData[graveIndex].occupied = false;
                        __instance.UpdateOpenSlotStatus();
                    }
                }
            }
        }

        // Player::Reset patch for resetting mod state when loading a different game.
        [HarmonyPatch(typeof(Player))]
        [HarmonyPatch("Reset")]
        public static class ResetEatTheDead
        {
            static void Postfix(Player __instance) 
            {
                ResetGraveDigging();
            }
        }
    }
}
