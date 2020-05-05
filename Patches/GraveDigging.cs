/*
For patching grave digging feature.

Author: cmjten10
*/
using Assets;
using Harmony;
using System;
using UnityEngine;
using Zat.Shared.ModMenu.Interactive;

namespace EatTheDead
{
    public static class GraveDigging
    {
        private static System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
        
        // =====================================================================
        // Patches
        // =====================================================================

        // Cemetery::BuryPerson patch for placing meat under cemetery.
        [HarmonyPatch(typeof(Cemetery), "BuryPerson")]
        public static class PlaceMeatUnderCemeteryPatch
        {
            public static void Postfix(Cemetery __instance, bool __result)
            {
                bool enabled = ModMain.settings.enabled.Value && ModMain.settings.graveDiggingSettings.enabled.Value;

                if (__result && enabled)
                {
                    int meatChance = (int)ModMain.settings.graveDiggingSettings.spawnMeatOnBurialChance.Value;
                    bool burialHasMeat = random.Next(0, 100) < meatChance;

                    if (burialHasMeat)
                    {
                        // -0.05f hides the meat under the cemetery, but still accessible by villagers.
                        Vector3 position = __instance.B.Center().xz() + new Vector3(0f, -0.05f, 0f);
                        PlaceResourceAt(FreeResourceType.Pork, position);
                    }
                }
            }
        }

        // Villager::PickupResource patch for removing grave on meat pickup.
        [HarmonyPatch(typeof(Villager), "PickupResource")]
        [HarmonyPatch(new Type[] { typeof(FreeResource), typeof(bool) })]
        public static class RemoveGraveOnMeatPickupPatch
        {
            public static void Prefix(Villager __instance, FreeResource resource)
            {
                bool enabled = ModMain.settings.enabled.Value && ModMain.settings.graveDiggingSettings.enabled.Value;
                bool removeGraveAfterDigging = ModMain.settings.graveDiggingSettings.removeGraveAfterDigging.Value;

                if (resource.type == FreeResourceType.Pork && enabled && removeGraveAfterDigging)
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

        // =====================================================================
        // Utility Functions
        // =====================================================================

        private static void PlaceResourceAt(FreeResourceType resourceType, Vector3 position)
        {
            // Refer to Player::DestroyPerson.
            FreeResource resource = FreeResourceManager.inst.GetPrefabFor(resourceType).CreateResource(position, -1);
            resource.Holder = null;
        }

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
    }

    public class GraveDiggingSettings
    {
        [Setting("Enabled", "Dig meat from cemeteries")]
        [Toggle(true, "")]
        public InteractiveToggleSetting enabled { get; private set; }

        [Setting("Remove Grave After Digging", "Remove a random grave after digging meat")]
        [Toggle(true, "Enabled")]
        public InteractiveToggleSetting removeGraveAfterDigging { get; private set; }

        [Setting("Spawn Meat on Burial Chance", "Chance of burials spawning diggable meat")]
        [Slider(0, 100, 100, "100", true)]
        public InteractiveSliderSetting spawnMeatOnBurialChance { get; private set; }

        public static void Setup(GraveDiggingSettings settings)
        {
            if (settings != null)
            {
                settings.spawnMeatOnBurialChance.OnUpdate.AddListener((setting) =>
                {
                    settings.spawnMeatOnBurialChance.Label = ((int)setting.slider.value).ToString();
                });
            }
        }
    }
}
