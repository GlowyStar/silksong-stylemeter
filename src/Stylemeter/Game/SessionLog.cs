using System;
using System.IO;
using System.Linq;

namespace Stylemeter.Game
{
    // BepInEx overwrites LogOutput.log on every launch, so the style lines also go to one file per session:
    // BepInEx\Stylemeter\logs\session-<time>.log. The newest 10 stay there and older ones move to logs\old, which
    // keeps 200, so a few short launches cannot push out a long recorded session.
    internal static class SessionLog
    {
        private const int Keep = 10;
        private const int KeepOld = 200;
        private static StreamWriter writer;

        public static string Open(string bepInExRoot)
        {
            try
            {
                var dir = Path.Combine(bepInExRoot, "Stylemeter", "logs");
                var oldDir = Path.Combine(dir, "old");
                Directory.CreateDirectory(oldDir);
                foreach (var old in Directory.GetFiles(dir, "session-*.log").OrderByDescending(f => f).Skip(Keep - 1))
                {
                    var to = Path.Combine(oldDir, Path.GetFileName(old));
                    if (File.Exists(to))
                        File.Delete(old);
                    else
                        File.Move(old, to);
                }
                foreach (var gone in Directory.GetFiles(oldDir, "session-*.log")
                    .OrderByDescending(f => f).Skip(KeepOld))
                    File.Delete(gone);
                var path = Path.Combine(dir, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                writer = new StreamWriter(path) { AutoFlush = true };
                return path;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Session log disabled: {e}");
                return null;
            }
        }

        public static void Write(string line)
        {
            writer?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
        }
    }
}
