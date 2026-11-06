using System;
using System.IO;
using System.Linq;
using System.Text;
using TeamCherry.Localization;
using TeamCherry.SharedUtils;
using UnityEngine;

namespace Stylemeter.Dev
{
    // Research only: writes the English language sheets as the game decrypts them, every tool with its name key and
    // every journal record with its name key, so boss title keys and tool names can be read offline. The files stay in
    // the game folder (BepInEx/Stylemeter/research), never in the mod. Runs once, while the research log is on.
    internal static class LangDump
    {
        private static string dir;
        private static bool sheetsDone;
        private static bool toolsDone;
        private static bool journalDone;

        public static void Init(string bepInExRoot)
        {
            dir = Path.Combine(bepInExRoot, "Stylemeter", "research");
            sheetsDone = File.Exists(Path.Combine(dir, "EN_sheets.txt"));
            toolsDone = File.Exists(Path.Combine(dir, "tools.tsv"));
            journalDone = File.Exists(Path.Combine(dir, "journal.tsv"));
        }

        public static void Tick()
        {
            if (dir == null || !DevTools.LogResearch.Value || (sheetsDone && toolsDone && journalDone))
                return;
            try
            {
                Directory.CreateDirectory(dir);
                if (!sheetsDone)
                    sheetsDone = DumpSheets();
                if (!toolsDone)
                    toolsDone = DumpTools();
                if (!journalDone)
                    journalDone = DumpJournal();
            }
            catch (Exception e)
            {
                sheetsDone = toolsDone = journalDone = true;
                Plugin.Log.LogWarning($"Language dump failed: {e}");
            }
        }

        private static bool DumpSheets()
        {
            var sheets = Language.GetSheets()?.ToList();
            if (sheets == null || sheets.Count == 0)
                return false;
            var index = new StringBuilder();
            foreach (var sheet in sheets)
            {
                var asset = Resources.Load<TextAsset>("Languages/EN_" + sheet);
                if (!asset)
                    continue;
                File.WriteAllText(Path.Combine(dir, "EN_" + sheet + ".xml"), Encryption.Decrypt(asset.text),
                    Encoding.UTF8);
                index.AppendLine(sheet);
            }
            File.WriteAllText(Path.Combine(dir, "EN_sheets.txt"), index.ToString(), Encoding.UTF8);
            Plugin.Out($"LANGDUMP sheets={sheets.Count}");
            return true;
        }

        private static bool DumpTools()
        {
            var tools = ToolItemManager.GetAllTools()?.Where(t => t).ToList();
            if (tools == null || tools.Count == 0)
                return false;
            var text = new StringBuilder();
            foreach (var tool in tools)
            {
                var name = tool.DisplayName;
                text.AppendLine($"{tool.name}\t{tool.Type}\t{name.Sheet}\t{name.Key}");
            }
            File.WriteAllText(Path.Combine(dir, "tools.tsv"), text.ToString(), Encoding.UTF8);
            Plugin.Out($"LANGDUMP tools={tools.Count}");
            return true;
        }

        private static bool DumpJournal()
        {
            var records = Resources.FindObjectsOfTypeAll<EnemyJournalRecord>();
            if (records == null || records.Length == 0)
                return false;
            var text = new StringBuilder();
            foreach (var record in records.Where(r => r).OrderBy(r => r.name))
            {
                var name = record.DisplayName;
                text.AppendLine($"{record.name}\t{name.Sheet}\t{name.Key}");
            }
            File.WriteAllText(Path.Combine(dir, "journal.tsv"), text.ToString(), Encoding.UTF8);
            Plugin.Out($"LANGDUMP journal={records.Length}");
            return true;
        }
    }
}
