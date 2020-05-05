using UnityEngine;
using Zat.Shared.ModMenu.Interactive;

namespace EatTheDead
{
    [Mod("Eat The Dead", "v1.1.1", "cmjten10")]
    public class EatTheDeadSettings
    {
        [Setting("Meat Drop", "Amount of meat dropped by the dead")]
        [Slider(0, 50, 2, "2", true)]
        public InteractiveSliderSetting meatDrop { get; private set; }

        [Setting("Random Drop", "Drop a random amount between 0 and \"Meat Drop\"")]
        [Toggle(false, "Enabled")]
        public InteractiveToggleSetting randomDrop { get; private set; }

        [Setting("Grave Digging", "Obtain meat from cemeteries")]
        [Toggle(true, "Enabled")]
        public InteractiveToggleSetting graveDiggingEnabled { get; private set; }

        [Setting("Remove Grave After Digging", "Remove a random grave after digging meat")]
        [Toggle(true, "Enabled")]
        public InteractiveToggleSetting removeGraveAfterDigging { get; private set; }

        [Setting("Spawn Meat on Burial Chance", "Chance of burials spawning diggable meat")]
        [Slider(0, 100, 100, "100", true)]
        public InteractiveSliderSetting spawnMeatOnBurialChance { get; private set; }
    }
}