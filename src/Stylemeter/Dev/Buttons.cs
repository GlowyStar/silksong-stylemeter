using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using InControl;
using UnityEngine;

namespace Stylemeter.Dev
{
    // Virtual button presses for the tester. A held action gets the value 1 in InControl's own update, before the real
    // bindings are read (PlayerAction.UpdateBindings), so the game sees an ordinary press: WasPressed, IsPressed and
    // WasReleased all come from the normal state change.
    internal static class Buttons
    {
        private static readonly Dictionary<string, float> HeldUntil = new Dictionary<string, float>();

        public static void Hold(string names, float seconds)
        {
            foreach (var name in names.Split('+'))
                HeldUntil[name.ToLowerInvariant()] = Time.unscaledTime + seconds;
        }

        public static void Release(string names)
        {
            if (names == "all")
            {
                HeldUntil.Clear();
                return;
            }
            foreach (var name in names.Split('+'))
                HeldUntil.Remove(name.ToLowerInvariant());
        }

        public static void ReleaseAll() => HeldUntil.Clear();

        // press a+b 0.1: hold for that long, then let go for at least a frame before reporting.
        public static IEnumerator Press(string names, float seconds)
        {
            Hold(names, seconds);
            yield return new WaitForSecondsRealtime(seconds);
            Release(names);
            yield return null;
            yield return null;
            yield return "released " + names;
        }

        // seq 0:attack:0.1 1:up+attack:0.1 ... (start in s from now, buttons, how long; all in real time).
        public static IEnumerator Sequence(string[] steps)
        {
            var parsed = steps.Select(s => s.Split(':'))
                .Select(p => (Start: Bridge.F(p[0]), Names: p[1], Length: Bridge.F(p[2])))
                .OrderBy(p => p.Start)
                .ToList();
            var start = Time.unscaledTime;
            foreach (var step in parsed)
            {
                while (Time.unscaledTime - start < step.Start)
                    yield return null;
                Hold(step.Names, step.Length);
            }
            var end = parsed.Max(p => p.Start + p.Length);
            while (Time.unscaledTime - start < end)
                yield return null;
            yield return null;
            yield return $"sequence of {parsed.Count} done";
        }

        private static bool Held(PlayerAction action)
        {
            if (HeldUntil.Count == 0)
                return false;
            var actions = InputHandler.Instance ? InputHandler.Instance.inputActions : null;
            if (actions == null || action.Owner != actions)
                return false;
            var now = Time.unscaledTime;
            foreach (var pair in HeldUntil)
            {
                if (now < pair.Value && ActionOf(actions, pair.Key) == action)
                    return true;
            }
            return false;
        }

        private static PlayerAction ActionOf(HeroActions a, string name)
        {
            switch (name)
            {
                case "attack": return a.Attack;
                case "jump": return a.Jump;
                case "dash": return a.Dash;
                case "superdash": return a.SuperDash;
                case "bind": return a.Cast;
                case "tool": return a.QuickCast;
                case "taunt": return a.Taunt;
                case "needolin": return a.DreamNail;
                case "left": return a.Left;
                case "right": return a.Right;
                case "up": return a.Up;
                case "down": return a.Down;
                default: return null;
            }
        }

        [HarmonyPatch(typeof(PlayerAction), "UpdateBindings")]
        private static class Inject
        {
            private static void Prefix(PlayerAction __instance, ulong updateTick, float deltaTime)
            {
                if (Held(__instance))
                    __instance.UpdateWithValue(1f, updateTick, deltaTime);
            }
        }
    }
}
