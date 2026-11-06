using System;
using System.IO;
using UnityEngine;

namespace Stylemeter.Dev
{
    // One-shot: applies BepInEx\Stylemeter\keymap.txt ("PlayerPrefsKey=Value" per line, an empty value deletes the
    // key) through the game's own PlayerPrefs, then renames the file. Runs from Awake, before the game loads its key
    // bindings. Tools started from a packaged (MSIX) app write to a virtualized registry the game never sees, so the
    // game has to write these itself.
    internal static class KeymapApply
    {
        public static void Run(string bepInExRoot)
        {
            var file = Path.Combine(bepInExRoot, "Stylemeter", "keymap.txt");
            if (!File.Exists(file))
                return;
            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.Trim();
                var eq = line.IndexOf('=');
                if (line.StartsWith("#") || eq <= 0)
                    continue;
                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();
                if (value.Length == 0)
                    PlayerPrefs.DeleteKey(key);
                else
                    PlayerPrefs.SetString(key, value);
                Plugin.Out($"KEYMAP {key}={(value.Length == 0 ? "<deleted>" : value)}");
            }
            PlayerPrefs.Save();
            File.Move(file, Path.Combine(Path.GetDirectoryName(file),
                $"keymap-applied-{DateTime.Now:yyyyMMdd-HHmmss}.txt"));
        }
    }
}
