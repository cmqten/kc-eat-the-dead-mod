/*
For patching meat drop on death feature.

Author: cmjten10
*/
using Harmony;
using System;
using UnityEngine;
using Zat.Shared.ModMenu.Interactive;

namespace EatTheDead
{
    public static class MeatDrop
    {
        private static System.Random random = new System.Random(Guid.NewGuid().GetHashCode());

        // =====================================================================
        // Patches
        // =====================================================================

        // Player::DestroyPerson patch for dropping meat on death.
        [HarmonyPatch(typeof(Player), "DestroyPerson")]
        public static class DeadVillagerMeatPatch
        {
            public static void Prefix(Villager p, bool leaveBehindBody)
            {
                bool enabled = ModMain.settings.enabled.Value && ModMain.settings.meatDropSettings.enabled.Value;
                if (enabled)
                {
                    int amount = (int)ModMain.settings.meatDropSettings.dropAmount.Value;
                    if (leaveBehindBody && amount > 0)
                    {
                        bool randomDrop = ModMain.settings.meatDropSettings.randomDrop.Value;
                        if (randomDrop)
                        {
                            amount = random.Next(0, amount + 1);
                        }
                        // Refer to Swineherd::OnDemolished.
                        Vector3 position = p.transform.position.xz() + new Vector3(0f, 0.05f, 0f);
                        PlaceResourceStackAt(FreeResourceType.Pork, amount, position);
                    }
                }
            }
        }

        // =====================================================================
        // Utility Functions
        // =====================================================================

        private static void PlaceResourceStackAt(FreeResourceType resourceType, int amount, Vector3 position)
        {
            // Refer to Swineherd::OnDemolished.
            GameObject resource = FreeResourceManager.inst.GetAutoStackFor(resourceType, amount);
            resource.transform.position = position;
        }
    }

    public class MeatDropSettings
    {
        [Setting("Enabled", "Drop meat on villager death")]
        [Toggle(true, "")]
        public InteractiveToggleSetting enabled { get; private set; }

        [Setting("Drop Amount", "Amount of meat dropped by the dead")]
        [Slider(0, 50, 2, "2", true)]
        public InteractiveSliderSetting dropAmount { get; private set; }

        [Setting("Random Drop", "Drop a random amount between 0 and \"Drop Amount\"")]
        [Toggle(false, "Enabled")]
        public InteractiveToggleSetting randomDrop { get; private set; }

        public static void Setup(MeatDropSettings settings)
        {
            if (settings != null)
            {
                settings.dropAmount.OnUpdate.AddListener((setting) =>
                {
                    settings.dropAmount.Label = ((int)setting.slider.value).ToString();
                });
            }
        }
    }
}
