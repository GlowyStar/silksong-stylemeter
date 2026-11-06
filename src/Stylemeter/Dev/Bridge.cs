using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Stylemeter.Dev
{
    // A command channel for automated tests, off by default ([Dev] Bridge). The tester appends "<id> <verb> <args>"
    // lines to bridge/cmd.txt; every result goes to bridge/out.txt as "<id> OK|ERR|DONE <text>". Commands that change
    // the game run only in the sandbox save slot, where saving is blocked (Sandbox).
    internal static class Bridge
    {
        private static string dir;
        private static string cmdPath;
        private static string outPath;
        private static long seenLength = -1;
        private static DateTime seenWrite;
        private static int lastId;
        private static MonoBehaviour host;
        private static readonly Dictionary<string, Func<string[], int, string>> Verbs =
            new Dictionary<string, Func<string[], int, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SafeVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ping", "state", "census", "load", "awards", "mark", "shot", "quit",
            "hud", "hudglyph", "hudchars", "hudatlas", "ui",
        };

        // when <text> <verb ...>: armed commands run once, on the frame after a log line containing the text.
        private static readonly List<(int Id, string Text, string[] Words)> Armed = new List<(int, string, string[])>();
        private static readonly Queue<(int Id, string[] Words)> Fired = new Queue<(int, string[])>();

        public static bool Enabled { get; private set; }

        public static void Init(string bepInExRoot, MonoBehaviour owner, bool enabled)
        {
            Enabled = enabled;
            if (!enabled)
                return;
            host = owner;
            dir = Path.Combine(bepInExRoot, "Stylemeter", "bridge");
            Directory.CreateDirectory(dir);
            cmdPath = Path.Combine(dir, "cmd.txt");
            outPath = Path.Combine(dir, "out.txt");
            // Commands left from an earlier run are history, not orders.
            lastId = ReadCommands().Select(c => c.Id).DefaultIfEmpty(0).Max();
            Register();
            Write(0, "READY", $"{Plugin.Name} {PluginInfo.Version} last={lastId}");
        }

        public static void Tick()
        {
            if (!Enabled)
                return;
            // Tests run while the window is in the background: neither Unity nor InControl may pause then.
            Application.runInBackground = true;
            InControl.InputManager.SuspendInBackground = false;
            Sandbox.Tick();
            while (Fired.Count > 0)
            {
                var fired = Fired.Dequeue();
                Run(fired.Id, fired.Words);
            }
            var info = new FileInfo(cmdPath);
            if (!info.Exists || (info.Length == seenLength && info.LastWriteTimeUtc == seenWrite))
                return;
            seenLength = info.Length;
            seenWrite = info.LastWriteTimeUtc;
            foreach (var command in ReadCommands().Where(c => c.Id > lastId).ToList())
            {
                lastId = command.Id;
                Run(command.Id, command.Words);
            }
        }

        private static void Run(int id, string[] words)
        {
            if (words.Length == 0)
                return;
            try
            {
                if (!Verbs.TryGetValue(words[0], out var verb))
                {
                    Write(id, "ERR", "unknown verb " + words[0]);
                    return;
                }
                if (!SafeVerbs.Contains(words[0]) && !Sandbox.InSandbox)
                {
                    Write(id, "ERR", $"not in the sandbox slot {Sandbox.Slot} (profile {Sandbox.CurrentProfile})");
                    return;
                }
                var result = verb(words, id);
                if (result != null)
                    Write(id, "OK", result);
            }
            catch (Exception e)
            {
                Write(id, "ERR", e.GetType().Name + ": " + e.Message);
            }
        }

        // Every log line of the mod passes here; an armed command whose text it contains fires on the next frame.
        public static void OnLog(string line)
        {
            if (!Enabled || Armed.Count == 0 || line.StartsWith("BRIDGE "))
                return;
            for (var i = Armed.Count - 1; i >= 0; i--)
            {
                if (!line.Contains(Armed[i].Text))
                    continue;
                Fired.Enqueue((Armed[i].Id, Armed[i].Words));
                Armed.RemoveAt(i);
            }
        }

        // Long commands finish later: they report DONE themselves.
        public static void Later(int id, IEnumerator routine) => host.StartCoroutine(Wrap(id, routine));

        private static IEnumerator Wrap(int id, IEnumerator routine)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext())
                        break;
                    current = routine.Current;
                }
                catch (Exception e)
                {
                    Write(id, "ERR", e.GetType().Name + ": " + e.Message);
                    yield break;
                }
                if (current is string text)
                {
                    Write(id, "DONE", text);
                    yield break;
                }
                yield return current;
            }
            Write(id, "DONE", "");
        }

        public static void Write(int id, string status, string text)
        {
            var line = $"{id} {status} {text}";
            Plugin.Out("BRIDGE " + line);
            try
            {
                File.AppendAllText(outPath, line.Replace("\n", " | ") + "\n", Encoding.UTF8);
            }
            catch (IOException e)
            {
                Plugin.Log.LogWarning($"Stylemeter: bridge reply not written to out.txt: {e}");
            }
        }

        private static List<(int Id, string[] Words)> ReadCommands()
        {
            var list = new List<(int, string[])>();
            if (!File.Exists(cmdPath))
                return list;
            string[] lines;
            try
            {
                using (var stream = new FileStream(cmdPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                    lines = reader.ReadToEnd().Split('\n');
            }
            catch (IOException e)
            {
                Plugin.Log.LogWarning($"Stylemeter: bridge could not read cmd.txt: {e}");
                return list;
            }
            foreach (var raw in lines)
            {
                var words = raw.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 2 && int.TryParse(words[0], out var id))
                    list.Add((id, words.Skip(1).ToArray()));
            }
            return list;
        }

        internal static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        private static IEnumerator WaitFor(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield return "waited";
        }

        internal static string V(Vector3 v) => FormattableString.Invariant($"({v.x:0.00},{v.y:0.00})");

        private static void Register()
        {
            Verbs["ping"] = (w, id) => $"pong scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} "
                + $"profile={Sandbox.CurrentProfile} sandbox={Sandbox.InSandbox} "
                + FormattableString.Invariant($"t={Time.time:0.00}");
            Verbs["state"] = (w, id) => Probe.State();
            Verbs["census"] = (w, id) => Probe.Census();
            Verbs["mark"] = (w, id) => Probe.Mark();
            Verbs["awards"] = (w, id) => Probe.Awards();
            Verbs["shot"] = (w, id) =>
            {
                var path = Path.Combine(dir, (w.Length > 1 ? w[1] : "shot") + ".png");
                ScreenCapture.CaptureScreenshot(path);
                return path;
            };
            Verbs["quit"] = (w, id) =>
            {
                Application.Quit();
                return "quitting";
            };
            Verbs["load"] = (w, id) => Sandbox.Load(w.Length > 1 ? int.Parse(w[1]) : Sandbox.Slot);
            Verbs["tp"] = (w, id) => Puppet.Teleport(F(w[1]), F(w[2]));
            Verbs["face"] = (w, id) => Puppet.Face(w[1]);
            Verbs["freeze"] = (w, id) => Dummies.Freeze(w.Length > 1 ? w[1] : "on");
            Verbs["enemy"] = (w, id) => Dummies.Move(w[1], F(w[2]), F(w[3]));
            Verbs["nail"] = (w, id) => Sandbox.Nail(w[1]);
            Verbs["silk"] = (w, id) => Puppet.Silk(int.Parse(w[1]));
            Verbs["health"] = (w, id) => Puppet.Health(int.Parse(w[1]));
            Verbs["hold"] = (w, id) =>
            {
                Buttons.Hold(w[1], w.Length > 2 ? F(w[2]) : 3600f);
                return "holding " + w[1];
            };
            Verbs["release"] = (w, id) =>
            {
                Buttons.Release(w.Length > 1 ? w[1] : "all");
                return "released";
            };
            Verbs["press"] = (w, id) =>
            {
                Later(id, Buttons.Press(w[1], w.Length > 2 ? F(w[2]) : 0.1f));
                return null;
            };
            Verbs["seq"] = (w, id) =>
            {
                Later(id, Buttons.Sequence(w.Skip(1).ToArray()));
                return null;
            };
            Verbs["scene"] = (w, id) =>
            {
                Later(id, Sandbox.Scene(w[1], w.Length > 2 ? w[2] : ""));
                return null;
            };
            Verbs["gates"] = (w, id) => Sandbox.Gates(w[1]);
            Verbs["usetool"] = (w, id) => Puppet.UseTool(w.Length > 1 ? w[1] : "neutral");
            Verbs["hp"] = (w, id) =>
            {
                // A training dummy: lots of health, so normal hits count and nothing dies.
                var hm = Dummies.Find(w[1]);
                if (!hm)
                    return "no such enemy " + w[1];
                hm.hp = int.Parse(w[2]);
                return $"{hm.name} hp={hm.hp}";
            };
            Verbs["autopogo"] = (w, id) =>
            {
                Later(id, Puppet.AutoPogo(w[1], F(w[2])));
                return null;
            };
            Verbs["menu"] = (w, id) =>
            {
                // Back to the title without saving; loading the sandbox again brings every enemy back.
                GameManager.instance.ReturnToMainMenuNoSave();
                return "to menu";
            };
            Verbs["wake"] = (w, id) => Dummies.Wake(w[1], w.Length > 2 && w[2] == "free");
            Verbs["hazards"] = (w, id) => Dummies.Hazards();
            Verbs["bool"] = (w, id) =>
            {
                PlayerData.instance.SetBool(w[1], w[2] == "true");
                return $"{w[1]} = {PlayerData.instance.GetBool(w[1])}";
            };
            Verbs["loadout"] = (w, id) => Loadout.Show();
            Verbs["equip"] = (w, id) => Loadout.Equip(w[1], w.Length > 2 ? int.Parse(w[2]) : -1);
            Verbs["crest"] = (w, id) => Loadout.Crest(w[1]);
            Verbs["wait"] = (w, id) =>
            {
                Later(id, WaitFor(F(w[1])));
                return null;
            };
            Verbs["when"] = (w, id) =>
            {
                if (w.Length < 3)
                    return "usage: when <text> <verb ...>";
                if (w[1] == "clear")
                {
                    Armed.Clear();
                    return "disarmed";
                }
                var text = w[1].Replace('_', ' ');
                Armed.Add((id, text, w.Skip(2).ToArray()));
                return $"armed on \"{text}\": {string.Join(" ", w.Skip(2))}";
            };
            Verbs["glide"] = (w, id) => Puppet.Glide(w.Length > 1 ? F(w[1]) : 1f);
            Verbs["field"] = (w, id) => Probe.Field(w[1]);
            Verbs["dmgscan"] = (w, id) => Probe.EnemyDamage();
            Verbs["invuln"] = (w, id) =>
            {
                Sandbox.Invuln = w[1] == "off" ? CheatManager.InvincibilityStates.Off
                    : w[1] == "prevent" ? CheatManager.InvincibilityStates.PreventDeath
                    : CheatManager.InvincibilityStates.FullInvincible;
                return "invuln " + Sandbox.Invuln;
            };
            Verbs["setstatic"] = (w, id) => Probe.SetStatic(w[1], w[2], w[3]);
            Verbs["fsm"] = (w, id) => Probe.Fsms(w[1]);
            // For looking at the display: meter <rank 0-7, -1 = none> <fill 0-1>; drain <scale> (0 holds the meter).
            Verbs["meter"] = (w, id) =>
            {
                var engine = Game.EngineHost.Engine;
                engine.ForceMeter(int.Parse(w[1]), w.Length > 2 ? F(w[2]) : 0.5f);
                return FormattableString.Invariant(
                    $"meter rank={engine.Meter.Rank} fill={engine.Meter.Fill:0}/{engine.Meter.Size:0}");
            };
            Verbs["drain"] = (w, id) =>
            {
                Core.Tuning.DrainScale = F(w[1]);
                return FormattableString.Invariant($"drain x{Core.Tuning.DrainScale}");
            };
            Verbs["hud"] = (w, id) => Game.Hud.MeterHud.Diagnose(Game.EngineHost.Engine);
            Verbs["hudglyph"] = (w, id) => Game.Hud.MeterHud.Glyph(w.Length > 1 ? w[1][0] : 'W');
            Verbs["hudset"] = (w, id) => Game.Hud.MeterHud.Set(w);
            Verbs["ui"] = (w, id) => UiProbe.Run(w);
            Verbs["hudchars"] = (w, id) => Game.Hud.MeterHud.Chars(w.Length > 1 ? w[1] : "rest");
            Verbs["hudatlas"] = (w, id) => Game.Hud.MeterHud.Atlas(w[1], w[2][0], dir);
            Verbs["feed"] = (w, id) =>
            {
                // feed <NAME> [count] [weight 0-4]: weight is the colour (plain, gold, rose, red, silk).
                Game.EngineHost.Engine.ForceFeed(w[1].Replace('_', ' '), w.Length > 2 ? int.Parse(w[2]) : 1,
                    w.Length > 3 ? int.Parse(w[3]) : 0);
                return "feed " + w[1];
            };
        }
    }
}
