/*
No pork? No problem! Dead villagers now provide meat, which can be your temporary source early in the game, or 
permanently if you're a damn savage.

Author: cmjten10
Mod Version: 1.2
Target K&C Version: 117r5s-mods
Date: 2020-05-06
*/
using Harmony;
using System;
using System.Reflection;
using UnityEngine;
using Zat.Shared.ModMenu.API;
using Zat.Shared.ModMenu.Interactive;

namespace EatTheDead
{
    public class ModMain : MonoBehaviour 
    {
        public const string authorName = "cmjten10";
        public const string modName = "Eat The Dead";
        public const string modNameNoSpace = "EatTheDead";
        public const string version = "v1.2";

        public static KCModHelper helper;
        public static ModSettingsProxy proxy;
        public static EatTheDeadSettings settings;

        void Preload(KCModHelper __helper) 
        {
            helper = __helper;
            var harmony = HarmonyInstance.Create($"{authorName}.{modNameNoSpace}");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        void SceneLoaded(KCModHelper __helper)
        {
            if (!proxy)
            {
                var config = new InteractiveConfiguration<EatTheDeadSettings>();
                settings = config.Settings;
                ModSettingsBootstrapper.Register(config.ModConfig, (_proxy, saved) =>
                {
                    config.Install(_proxy, saved);
                    proxy = _proxy;
                    MeatDropSettings.Setup(settings.meatDropSettings);
                    GraveDiggingSettings.Setup(settings.graveDiggingSettings);
                }, (ex) =>
                {
                    helper.Log($"ERROR: Failed to register proxy for {modName} Mod config: {ex.Message}");
                    helper.Log(ex.StackTrace);
                });
            }
        }
    }

    [Mod(ModMain.modName, ModMain.version, ModMain.authorName)]
    public class EatTheDeadSettings
    {
        [Setting("Eat The Dead Enabled", "Enable or disable mod. If disabled, the rest of the settings do not apply.")]
        [Toggle(true, "")]
        public InteractiveToggleSetting enabled { get; private set; }

        [Category("Grave Digging")]
        public GraveDiggingSettings graveDiggingSettings { get; private set; }

        [Category("Meat Drop")]
        public MeatDropSettings meatDropSettings { get; private set; }
    }
}
