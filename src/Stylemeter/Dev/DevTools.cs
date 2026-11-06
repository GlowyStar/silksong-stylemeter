using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stylemeter.Dev
{
    // Everything the dev build adds, started from the plugin in one place: the sandbox and its command bridge, the
    // research and event logs, the inspector keys, the text overlay. The release build compiles none of it.
    internal static class DevTools
    {
        internal static ConfigEntry<bool> TextOverlay;
        internal static ConfigEntry<bool> LogEvents;
        internal static ConfigEntry<bool> LogResearch;
        internal static ConfigEntry<bool> BridgeEnabled;
        internal static ConfigEntry<int> SandboxSlot;

        public static void Start(BaseUnityPlugin plugin)
        {
            var config = plugin.Config;
            TextOverlay = config.Bind("Dev", "TextOverlay", false,
                "Also show the plain-text meter: fill, height and thread as text.");
            LogEvents = config.Bind("Dev", "LogEvents", true,
                "Write every hit, kill, clash, hit taken and tool use to the BepInEx log.");
            LogResearch = config.Bind("Dev", "LogResearch", true,
                "Write game events, enemy and tool FSM states, boss titles and scene changes to the BepInEx log.");
            BridgeEnabled = config.Bind("Dev", "Bridge", false,
                "Automated tests only: take commands from BepInEx\\Stylemeter\\bridge\\cmd.txt. They act only in the "
                + "sandbox save slot, where saving is blocked. Leave off for normal play.");
            SandboxSlot = config.Bind("Dev", "SandboxSlot", 3,
                "The save slot the test commands may change: a copy of a real save, deleted after the run.");

            Sandbox.Slot = SandboxSlot.Value;
            Bridge.Init(Paths.BepInExRootPath, plugin, BridgeEnabled.Value);
            KeymapApply.Run(Paths.BepInExRootPath);
            LangDump.Init(Paths.BepInExRootPath);
            SceneManager.activeSceneChanged += ResearchLog.OnSceneChanged;
            Inspector.WatchKeyboard();
        }

        public static void Update()
        {
            Inspector.Tick();
            LangDump.Tick();
            Bridge.Tick();
        }

        public static void LateUpdate()
        {
            if (!Bridge.Enabled)
                return;
            Dummies.Tick();
            Arena.Tick();
        }

        public static void OnGUI()
        {
            if (Plugin.MeterEnabled.Value && TextOverlay.Value)
                Overlay.Draw(Game.EngineHost.Engine);
            if (Sandbox.InSandbox && Sandbox.IsLabelShown)
                GUI.Label(new Rect(20f, 20f, 600f, 30f), "STYLEMETER SANDBOX: test slot, saving is off");
        }

        public static void OnLog(string line) => Bridge.OnLog(line);
    }
}
