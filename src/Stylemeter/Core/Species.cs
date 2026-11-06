using System.Collections.Generic;

namespace Stylemeter.Core
{
    // Enemies the bonuses name, by their Hunter's Journal record. The key is the record name in capitals with
    // underscores, which is also its journal text key without "NAME_" (Choir Elder = "Pilgrim Stomper Song" =
    // PILGRIM_STOMPER_SONG). English names come from the game's own journal sheet.
    internal static class Species
    {
        public static string Key(string name) =>
            string.IsNullOrEmpty(name) || name == "-" ? null : name.Trim().ToUpperInvariant().Replace(' ', '_');

        // BIG GAME: elites and mini-bosses (Skarrgard, Craggler, Choir Clapper, Last Claw, Gargant Gloom, Huge Flea).
        public static readonly HashSet<string> BigGame = new HashSet<string>
        {
            "BONE_HUNTER_THROW", "ROOF_CRAB", "SONG_HEAVY_SENTRY", "BONE_HUNTER_CHIEF", "GLOOM_BEAST", "GIANT_FLEA",
        };

        // WHITE PALACE: Wingmould.
        public const string Wingmould = "WHITE_PALACE_FLY";

        // STOP, THIEF!: Snitchfly.
        public const string Snitchfly = "ROSARY_THIEF";

        // CAUGHT OFF GUARD: Underworker, Undercrank and Pilgrim Hiker hide in a shell.
        public static readonly HashSet<string> Shelled = new HashSet<string>
        {
            "UNDERSTORE_SMALL", "UNDERSTORE_HEAVY", "PILGRIM_HIKER",
        };

        // PEEKABOO: Gromling.
        public const string Gromling = "CRYPT_WORM";

        // PARRY THIS!: enemies that parry themselves (Memoria, Minister, Silk Snipper, Skarr Stalker); a clash with
        // any enemy's parry counter (an attack with "Parry" in its name, like the Reed's) counts too.
        public static readonly HashSet<string> Parriers = new HashSet<string>
        {
            "ARBORIUM_KEEPER", "SONG_ADMINISTRATOR", "FARMER_SCISSORS", "BONE_HUNTER",
        };

        // DOMINO EFFECT: enemies that blow up (Miremite, Swamp Squit, Flintbeetle).
        public static readonly HashSet<string> Explosive = new HashSet<string>
        {
            "SWAMP_GOOMBA", "SWAMP_MOSQUITO", "ROCK_ROLLER",
        };
    }
}
