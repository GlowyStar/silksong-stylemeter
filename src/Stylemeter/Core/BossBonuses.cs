using System.Collections.Generic;
using System.Linq;

namespace Stylemeter.Core
{
    // The named win of each boss (BONUSES.md §12), by the key of its title card (the game's Titles sheet). Two fights
    // share a title, so the game side adds a suffix: LACE_1 / LACE_2 by whether Lace was beaten before,
    // MOSSBONE_MOTHER_2 for the second Moss Mother fight (two at once in Weavenest Atla).
    internal static class BossBonuses
    {
        private static readonly Dictionary<string, BonusDef> ByTitle = new Dictionary<string, BonusDef>
        {
            ["MOSSBONE_MOTHER"] = Win("ROLLING STONE"),
            ["MOSSBONE_MOTHER_2"] = Win("DOUBLE TROUBLE", 600),
            ["BELLBEAST"] = Win("FOR WHOM THE BELL TOLLS"),
            ["LACE_1"] = Win("UNLACED"),
            ["LACE_2"] = Win("TOUCHÉ", 600),
            ["SONG_GOLEM"] = Win("LAST VERSE"),
            ["GIANT_BONE_FLYER"] = Win("SWATTED"),
            ["SKULL_KING"] = Win("SKULLDUGGERY"),
            ["VAMPIRE_GNAT"] = Win("GROUNDED"),
            ["SPLINTER_QUEEN"] = Win("SPLINTER CELL"),
            ["SPINNER"] = Win("WIDOWMAKER"),
            ["DRILLERS"] = Win("SHELL GAME"),
            ["LAST_JUDGE"] = Win("OBJECTION!"),
            ["PHANTOM"] = Win("WHO YOU GONNA CALL?"),
            ["COGWORK_DANCERS"] = Win("LAST DANCE"),
            ["TROBBIO"] = Win("SHOW'S OVER"),
            ["SILK"] = Win("CUT THE CORD", 1000),
            ["FIRST_WEAVER"] = Win("ABSOLVED"),
            ["CHEF"] = Win("KITCHEN NIGHTMARES"),
            ["WICKER_BUG"] = Win("LIGHTS OUT"),
            ["SWAMP_SHAMAN"] = Win("SWALLOWED PRIDE"),
            ["DOCK_GUARD_DUO"] = Win("BROTHERS IN ARMS"),
            ["DOCK_GUARD_SOLO"] = Win("BROTHERS IN ARMS"),
            ["DOCK_GUARD_THROWER"] = Win("BROTHERS IN ARMS"),
            ["WARD_BOSS"] = Win("LOOSE ENDS"),
            ["DRILLER_SOLO"] = Win("ANGER MANAGEMENT"),
            ["ZAPNEST_BOSS"] = Win("SHORT CIRCUIT"),
            ["BROODMOTHER"] = Win("EMPTY NEST"),
            ["SONG_KNIGHT"] = Win("CHANGING OF THE GUARD"),
            ["GARMOND"] = Win("GOOD SPORT", 300),
            ["MAPPER"] = Win("MAPPED OUT", 300),
            ["BIG_CENTIPEDE"] = Win("DINNER BELL"),
            ["WHITE_CLOVERSTAG"] = Win("OH DEER"),
            ["CLOVER_DANCERS"] = Win("LUCK OF THE DRAW"),
            ["SETH"] = Win("SHIELD BREAKER"),
            ["SETH_BC"] = Win("SHIELD BREAKER"),
            ["FLOWER_QUEEN"] = Win("UPROOTED"),
            ["HUNTER_QUEEN"] = Win("DIVA DOWN", 600),
            ["HUNTER_QUEEN_BC"] = Win("DIVA DOWN", 600),
            ["CORAL_KING"] = Win("DETHRONED", 600),
            ["CORAL_KING_BC"] = Win("DETHRONED", 600),
            ["PINSTRESS"] = Win("PINNED DOWN"),
            ["HUNTER_TRAPPER"] = Win("TRAPPER TRAPPED"),
            ["TORMENTED_TROBBIO"] = Win("ENCORE", 600),
            ["BLUE_ASSISTANT"] = Win("SIDE EFFECTS"),
            ["GARMOND_BT"] = Win("LAID TO REST"),
            ["CROWFATHER"] = Win("NEVERMORE", 600),
            ["GREY_CORAL_WARRIOR"] = Win("OVER THE EDGE"),
            ["LOST_LACE"] = Win("FINAL CUT", 1000),
            ["ABYSS_MASS"] = Win("NO SAVIOUR"),
            [VoidMossMother] = Win("INTO THE VOID", 600),
        };

        // The named win, or FINALE for a boss without a name of its own.
        public static BonusDef WinFor(string titleKey) =>
            titleKey != null && ByTitle.TryGetValue(titleKey, out var bonus) ? bonus : Bonuses.Finale;

        public static bool IsLace(string titleKey) => titleKey != null && titleKey.StartsWith("LACE");

        public const string Pinstress = "PINSTRESS";
        public const string Widow = "SPINNER";
        public const string FirstSinner = "FIRST_WEAVER";
        // Void Moss Mother has no title of her own; the game side tells her by the black thread (WorldHooks.BossKey).
        public const string VoidMossMother = "MOSSBONE_MOTHER_VOID";

        // Every boss win, once per name (three Dock Guard fights share BROTHERS IN ARMS).
        public static IEnumerable<BonusDef> All => ByTitle.Values.GroupBy(b => b.Name).Select(g => g.First());

        // Flat: the result comes after the fight, and with the boss x2 the final bosses (1000) would pay more than a
        // whole SILKSONG meter.
        private static BonusDef Win(string name, float points = 500) =>
            new BonusDef(name, points, Scaling.Flat, BonusGroup.Boss);
    }
}
