using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace Stylemeter
{
    // The composition root: binds the config, starts the engine, the meter on screen and the mod menu, and patches the
    // game. The dev build adds the sandbox tools through Dev.DevTools.
    [BepInPlugin(Guid, Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.glowystar.stylemeter";
        public const string Name = "Stylemeter";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> MeterEnabled;
        internal static ConfigEntry<bool> ShowOverlay;
        internal static ConfigEntry<bool> SilkReward;
        internal static ConfigEntry<float> PointsMultiplier;
        internal static ConfigEntry<string> DisabledBonuses;
        internal static ConfigEntry<bool> LogStyle;

        private static bool isHudBroken;

        private void Awake()
        {
            Log = Logger;
            BindConfig();
            var sessionLog = Game.SessionLog.Open(Paths.BepInExRootPath);
#if STYLEMETER_DEV
            Dev.DevTools.Start(this);
#endif
            Game.EngineHost.Init();
            SceneManager.activeSceneChanged += Game.EngineHost.OnSceneChanged;
            PatchAll();
            Out($"{Name} {PluginInfo.Version} loaded, session log: {sessionLog ?? "-"}");
        }

        private void BindConfig()
        {
            MeterEnabled = Config.Bind("Meter", "Enabled", true, "Count style points.");
            ShowOverlay = Config.Bind("Meter", "ShowOverlay", true,
                "Show the style meter in the top right corner: the rank, the needle and the feed of bonuses.");
            SilkReward = Config.Bind("Meter", "SilkReward", true,
                "Holding the top rank, SILKSONG, gives Hornet one silk for every 5 seconds held.");
            PointsMultiplier = Config.Bind("Meter", "PointsMultiplier", 1f, new ConfigDescription(
                "Every style point is multiplied by this (also in the mod menu: Options > Stylemeter).",
                new AcceptableValueRange<float>(Core.StyleSettings.MinMultiplier, Core.StyleSettings.MaxMultiplier)));
            DisabledBonuses = Config.Bind("Bonuses", "Disabled", "",
                "Bonuses switched off, by name, separated by | (easier in the mod menu: Options > Stylemeter).");
            LogStyle = Config.Bind("Debug", "LogStyle", true,
                "Write every style bonus and rank change to the session log in BepInEx\\Stylemeter\\logs.");
        }

        private void Update()
        {
#if STYLEMETER_DEV
            Dev.DevTools.Update();
#endif
            Game.EngineHost.Update();
            UpdateHud();
        }

        // The meter on screen must never break the game: one error turns it off for the session.
        private static void UpdateHud()
        {
            if (isHudBroken)
                return;
            try
            {
                Game.Hud.MeterHud.Tick(Game.EngineHost.Engine);
            }
            catch (Exception e)
            {
                isHudBroken = true;
                Log.LogError($"Stylemeter: the meter display is off after an error: {e}");
            }
        }

#if STYLEMETER_DEV
        private void LateUpdate() => Dev.DevTools.LateUpdate();

        private void OnGUI() => Dev.DevTools.OnGUI();
#endif

        internal static void Out(string line)
        {
            Log.LogInfo(line);
            Game.SessionLog.Write(line);
#if STYLEMETER_DEV
            Dev.DevTools.OnLog(line);
#endif
        }

        // One hook group at a time, so a game update that renames one method only disables that group.
        private static void PatchAll()
        {
            var harmony = new Harmony(Guid);
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Log.LogError($"Hooks in {type.Name} are off: {e}");
                }
            }
        }
    }
}
