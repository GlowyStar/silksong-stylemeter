using System.Text;
using Stylemeter.Core;
using Stylemeter.Game;
using UnityEngine;

namespace Stylemeter.Dev
{
    // Plain-text view of the meter for development: rank, fill, height, the last source's thread and the feed.
    internal static class Overlay
    {
        private const int BarCells = 24;
        private static readonly StringBuilder Text = new StringBuilder();
        private static GUIStyle style;

        public static void Draw(StyleEngine engine)
        {
            if (engine == null || (!engine.Meter.InCombo && engine.Feed.Lines.Count == 0))
                return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight };
            style.fontSize = Mathf.Max(14, Screen.height / 40);

            Text.Length = 0;
            var meter = engine.Meter;
            if (meter.InCombo)
            {
                Text.AppendLine(Tuning.Ranks[meter.Rank].Name);
                Text.AppendLine(Bar(meter.Fill / meter.Size));
                Text.Append('x').Append(engine.Height.Current(engine.Now).ToString("0.00"));
                var source = engine.Threads.LastSource;
                if (source != null)
                {
                    var state = ThreadSet.StateOf(engine.Threads.Tension(source)).ToString().ToUpperInvariant();
                    Text.Append("   ").Append(state).Append(' ').Append(source);
                }
                Text.AppendLine();
            }
            foreach (var line in engine.Feed.Lines)
            {
                Text.Append("+ ").Append(line.Name);
                if (line.Count > 1)
                    Text.Append(" x").Append(line.Count);
                Text.AppendLine();
            }

            var width = Screen.width * 0.32f;
            var rect = new Rect(Screen.width * 0.98f - width, Screen.height * 0.12f, width, Screen.height * 0.7f);
            var text = Text.ToString();
            var color = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
        }

        private static string Bar(float fill)
        {
            var full = Mathf.Clamp(Mathf.RoundToInt(fill * BarCells), 0, BarCells);
            return "[" + new string('|', full) + new string('.', BarCells - full) + "]";
        }
    }
}
