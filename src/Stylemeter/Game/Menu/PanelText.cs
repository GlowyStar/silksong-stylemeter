using Stylemeter.Core;

namespace Stylemeter.Game.Menu
{
    // The panel's words: English whatever language the game runs in, like the bonus names in the meter.
    internal static class PanelText
    {
        public const string Multiplier = "Points multiplier";
        public const string WholeSection = "Whole section";
        public const string On = "ON";
        public const string Off = "OFF";
        public const string Partly = "PARTIAL";
        public const string Hint = "Select: open or switch     Left, Right: change     Back: close";

        public static string Section(BonusGroup group) => group switch
        {
            BonusGroup.Needle => "Needle",
            BonusGroup.Air => "Air and movement",
            BonusGroup.Clashes => "Clashes and parries",
            BonusGroup.Silk => "Silk skills",
            BonusGroup.Crest => "Crests",
            BonusGroup.Tool => "Attack tools",
            BonusGroup.Support => "Support tools",
            BonusGroup.Needolin => "Needolin",
            BonusGroup.Health => "Healing and health",
            BonusGroup.World => "Elements and world",
            BonusGroup.Enemy => "Enemies",
            BonusGroup.Boss => "Bosses",
            BonusGroup.Combo => "Multikills and combos",
            BonusGroup.Streak => "Streaks and fight results",
            _ => group.ToString(),
        };
    }
}
