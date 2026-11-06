using System;
using System.Collections.Generic;

namespace Stylemeter.Game
{
    // Every Harmony prefix and postfix does its work through here: code running inside the game's own methods must
    // never throw into them. The first error of a check is logged with its stack; a check that keeps failing (a game
    // update changed what it reads) is off for the session, so only its bonus is lost.
    internal static class HookGuard
    {
        private const int ErrorsBeforeOff = 10;

        private static readonly Dictionary<string, int> Errors = new Dictionary<string, int>();

        public static void Run(string name, Action action)
        {
            if (IsOff(name))
                return;
            try
            {
                action();
            }
            catch (Exception e)
            {
                Report(name, e);
            }
        }

        public static T Get<T>(string name, Func<T> read, T fallback)
        {
            if (IsOff(name))
                return fallback;
            try
            {
                return read();
            }
            catch (Exception e)
            {
                Report(name, e);
                return fallback;
            }
        }

        private static bool IsOff(string name) => Errors.TryGetValue(name, out var count) && count >= ErrorsBeforeOff;

        private static void Report(string name, Exception e)
        {
            Errors.TryGetValue(name, out var count);
            Errors[name] = ++count;
            if (count == 1)
                Plugin.Log.LogWarning($"Stylemeter: {name} failed: {e}");
            else if (count == ErrorsBeforeOff)
                Plugin.Log.LogWarning($"Stylemeter: {name} is off for this session after {count} errors");
        }
    }
}
