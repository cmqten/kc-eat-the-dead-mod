/*
For patching meat drop on death feature.

Author: cmjten10
*/
using Assets;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Zat.Shared.InterModComm;
using Zat.Shared.ModMenu.API;
using Zat.Shared.ModMenu.Interactive;

namespace EatTheDead
{
    public static class MeatDrop
    {
        private static System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
        private static MeatDropSettings settings = null;

        // =====================================================================
        // Patches
        // =====================================================================

        // Player::DestroyPerson patch for dropping meat on death.
        [HarmonyPatch(typeof(Player), "DestroyPerson")]
        public static class DeadVillagerMeatPatch
        {
            public static void Prefix(Villager p, bool leaveBehindBody)
            {
                if (settings.enabled.Value && EatTheDead.ModInit.settings.enabled.Value)
                {
                    int dropAmount = (int)settings.dropAmount.Value;
                    if (leaveBehindBody && dropAmount > 0)
                    {
                        int amount = dropAmount;

                        if (settings.randomDrop.Value)
                        {
                            amount = random.Next(0, dropAmount + 1);
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

        // =====================================================================
        // Settings
        // =====================================================================

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
        }

        public static void SetupSettings(MeatDropSettings _settings)
        {
            if (settings == null)
            {
                settings = _settings;
                settings.dropAmount.OnUpdate.AddListener((setting) =>
                {
                    settings.dropAmount.Label = ((int)setting.slider.value).ToString();
                });
            }
        }
    }
}
