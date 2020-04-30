/*
No pork? No problem! Dead villagers now provide meat, which can be your temporary source early in the game, or 
permanently if you're a damn savage.

Author: cmjten10
Mod Version: 1.1.1
Target K&C Version: 117r5s-mods
Date: 2020-05-01
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

        private static System.Random random = new System.Random(Guid.NewGuid().GetHashCode());

        // Meat drop on death
        private static int meatDrop = 2;
        private static bool randomDrop = false;

        // Grave digging
        private static bool graveDiggingEnabled = true;
        private static int chanceOfMeatOnBurial = 100;
        private static bool removeGraveAfterDigging = true;

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
                    .Create("Eat The Dead", "v1.1.1", "cmjten10")
                    .AddSlider("Eat The Dead/Meat Drop", "Amount of meat dropped by the dead", 
                        "2", 0, 50, true, meatDrop)
                    .AddToggle("Eat The Dead/Random", "Drop a random amount between 0 and \"Meat Drop\"", 
                        "Enabled", randomDrop)
                    .AddToggle("Eat The Dead/Grave Digging", "Obtain meat from cemeteries", 
                        "Enabled", graveDiggingEnabled)
                    .AddToggle("Eat The Dead/Remove Grave After Digging", "Remove a random grave after digging meat", 
                        "Enabled", removeGraveAfterDigging)
                    .AddSlider("Eat The Dead/Spawn Meat on Burial Chance", "Chance of burials spawning diggable meat", 
                        "100", 0, 100, true, chanceOfMeatOnBurial)
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
                proxy.AddSettingsChangedListener("Eat The Dead/Random", (setting) =>
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
                    proxy.UpdateSetting(setting, null, null);
                });
                proxy.AddSettingsChangedListener("Eat The Dead/Spawn Meat on Burial Chance", (setting) =>
                {
                    chanceOfMeatOnBurial = (int)setting.slider.value;
                    setting.slider.label = chanceOfMeatOnBurial.ToString();
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
            // Refer to Player::DestroyPerson.
            FreeResource resource = FreeResourceManager.inst.GetPrefabFor(resourceType).CreateResource(position, -1);
            resource.Holder = null;
        }

        private static void PlaceResourceStackAt(FreeResourceType resourceType, int amount, Vector3 position)
        {
            // Refer to Swineherd::OnDemolished.
            GameObject resource = FreeResourceManager.inst.GetAutoStackFor(resourceType, amount);
            resource.transform.position = position;
        }

        // =====================================================================
        // Grave Digging Utility Functions
        // =====================================================================

        private static void RemoveRandomGraveFromCemetery(Cemetery cemetery)
        {
            // Harmony Traverse for accessing private fields.
            // For reference:
            // https://harmony.pardeike.net/articles/utilities.html
            // https://github.com/pardeike/Harmony/issues/289
            Traverse gravesDataTraverse = Traverse.Create(cemetery).Field("gravesData");
            Traverse gravesRenderInstanceTraverse = Traverse.Create(cemetery).Field("gravesRenderInstance");
            GraveData[] gravesData = gravesDataTraverse.GetValue<GraveData[]>();
            RenderInstance[] gravesRenderInstance = gravesRenderInstanceTraverse.GetValue<RenderInstance[]>();

            // Select random grave to remove.
            int totalGraves = gravesData.Length;
            int graveIndex = random.Next(0, totalGraves);
            if (!gravesData[graveIndex].occupied)
            {
                for (graveIndex = 0; graveIndex < totalGraves; graveIndex++)
                {
                    if (gravesData[graveIndex].occupied)
                    {
                        break;
                    }
                }
            }

            // If graveIndex is equal to totalGraves, there are no free graves.
            if (graveIndex < totalGraves)
            {
                // Refer to Cemetery::Tick.
                gravesRenderInstance[graveIndex].Visible = false;
                gravesData[graveIndex].occupied = false;
                cemetery.UpdateOpenSlotStatus();
            }
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
                    // Refer to Swineherd::OnDemolished.
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
                    bool burialHasMeat = random.Next(0, 100) < chanceOfMeatOnBurial;
                    if (burialHasMeat)
                    {
                        // -0.05f hides the meat under the cemetery, but still accessible by villagers.
                        Vector3 position = __instance.B.Center().xz() + new Vector3(0f, -0.05f, 0f);
                        PlaceResourceAt(FreeResourceType.Pork, position);
                    }
                }
            }
        }

        // Villager::PickupResource patch for notifying Cemetery::Tick patch to remove a grave on pickup.
        [HarmonyPatch(typeof(Villager))]
        [HarmonyPatch("PickupResource")]
        [HarmonyPatch(new Type[] { typeof(FreeResource), typeof(bool) })]
        public static class RemoveGraveOnMeatPickupPatch
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
                        // Refer to Cemetery::IsCemetery.
                        Building building = World.inst.GetCellData(x, z).StructureFindByCategory(World.cemeteryHash);
                        Cemetery cemetery = building.GetComponentInParent<Cemetery>();
                        RemoveRandomGraveFromCemetery(cemetery);
                    }
                }
            }
        }
    }
}
