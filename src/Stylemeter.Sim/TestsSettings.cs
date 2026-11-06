using System.Linq;
using Stylemeter.Core;

namespace Stylemeter.Sim
{
    // Checks for the mod menu's settings: bonuses switched off, the points multiplier, and the catalog the menu lists.
    internal static partial class Tests
    {
        private static void MenuSettings()
        {
            var e = NewEngine(out var awards);
            e.Settings.Toggle("KILL");
            e.Observe(0f, Ground());
            e.OnHit(Hit(0f, Enemy(150), AttackKind.NeedleSide, 30, -5, killed: true));
            Check(!Has(awards, "KILL") && e.Feed.Lines.All(l => l.Name != "KILL"),
                "a bonus switched off gives no points and no line");
            Check(Has(awards, "needle"), "the other points of the same hit still pay");

            var f = NewEngine(out var doubled);
            f.Settings.PointsMultiplier = 2f;
            f.Observe(0f, Ground());
            f.OnHit(Hit(0f, Enemy(151), AttackKind.NeedleSide, 30, -5, killed: true));
            Check(Near(doubled.First(a => a.Name == "KILL").Total, 90f), "the multiplier x2 doubles KILL (45 to 90)");
            f.Settings.PointsMultiplier = 99f;
            Check(Near(f.Settings.PointsMultiplier, StyleSettings.MaxMultiplier),
                "the multiplier stays within its range");

            var settings = new StyleSettings();
            settings.LoadDisabled("RUN, FORREST, RUN! | KILL||");
            Check(!settings.IsEnabled("RUN, FORREST, RUN!") && !settings.IsEnabled("KILL")
                && settings.DisabledCount == 2,
                "names with commas survive the config text");
            var copy = new StyleSettings();
            copy.LoadDisabled(settings.DisabledText);
            Check(copy.DisabledCount == 2 && !copy.IsEnabled("KILL"), "the config text reads back the same");

            var section = new StyleSettings();
            var changes = 0;
            section.Changed += () => changes++;
            var names = new[] { "KILL", "UPPERCUT", "SHORYUKEN" };
            section.Disable(names);
            Check(changes == 1 && names.All(name => !section.IsEnabled(name)),
                "switching a whole section off is one change");
            section.Disable(names);
            Check(changes == 1, "switching off what is already off changes nothing");
            section.Toggle("KILL");
            section.Enable(names);
            Check(changes == 3 && names.All(section.IsEnabled), "a toggle and a whole section on are a change each");

            var catalog = Bonuses.Catalog;
            Check(catalog.All(b => !b.Silent && b.Group != BonusGroup.None),
                "the menu lists only named bonuses with a group");
            Check(catalog.Select(b => b.Name).Distinct().Count() == catalog.Count, "every name is listed once");
            Check(catalog.All(b => b.Name != "SILK") && catalog.Any(b => b.Name == "RAMPAGE")
                && catalog.Any(b => b.Name == "CUT THE CORD"),
                "the silk notice is not listed; streaks and boss wins are");
        }
    }
}
