using System.Collections.Generic;
using System.Linq;
using static Stylemeter.Core.BonusGroup;

namespace Stylemeter.Core
{
    // How a bonus is multiplied (MECHANICS.md §1): actions take thread, height and boss; moves (clashes, binds, chains)
    // take height and boss but never wear a thread; flat bonuses (streaks, ranks, arenas) go as they are.
    internal enum Scaling
    {
        Action,
        Move,
        Flat,
    }

    // The catalog's sections (BONUSES.md §1-14), for the mod menu's list.
    // None: lines that are not bonuses to switch off.
    internal enum BonusGroup
    {
        None,
        Needle,
        Air,
        Clashes,
        Silk,
        Crest,
        Tool,
        Support,
        Needolin,
        Health,
        World,
        Enemy,
        Boss,
        Combo,
        Streak,
    }

    internal sealed class BonusDef
    {
        public readonly string Name;
        public readonly float Points;
        public readonly Scaling Scaling;
        public readonly BonusGroup Group;
        public readonly bool Silent;
        // The feed's colour: 0 plain, 1 gold, 2 rose, 3 red (by base points, Tuning.FeedWeight*), 4 the silk reward.
        public readonly int Weight;

        public BonusDef(string name, float points, Scaling scaling, BonusGroup group = BonusGroup.None,
            bool silent = false, int weight = -1)
        {
            Name = name;
            Points = points;
            Scaling = scaling;
            Group = group;
            Silent = silent;
            Weight = weight >= 0 ? weight
                : points >= Tuning.FeedWeightRed ? 3
                : points >= Tuning.FeedWeightRose ? 2
                : points >= Tuning.FeedWeightGold ? 1
                : 0;
        }
    }

    // The bonuses of BONUSES.md wired into the engine, with their base points.
    internal static class Bonuses
    {
        private static List<BonusDef> catalog;

        // Every bonus a player can switch off in the mod menu, by group and in the catalog's order: named, not silent,
        // in a group (the SILK line is a reward notice, not a bonus).
        public static IReadOnlyList<BonusDef> Catalog => catalog ?? (catalog = BuildCatalog());

        private static List<BonusDef> BuildCatalog()
        {
            var fields = typeof(Bonuses)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(BonusDef))
                .Select(f => (BonusDef)f.GetValue(null));
            return fields.Concat(BossBonuses.All)
                .Where(b => !b.Silent && b.Group != None)
                // Two ways to earn one name (KABOOM by a hit and by a blast) are one switch.
                .GroupBy(b => b.Name).Select(g => g.First())
                .Select((b, order) => (Bonus: b, Order: order))
                .OrderBy(p => p.Bonus.Group).ThenBy(p => p.Order)
                .Select(p => p.Bonus)
                .ToList();
        }

        // 1. Needle.
        // Silent hits pay this much because a real fight gives only about 0.7 needle hits a second (MECHANICS.md §2).
        public static readonly BonusDef NeedleGround = new BonusDef("needle", 10, Scaling.Action, Needle, silent: true);
        public static readonly BonusDef NeedleAir =
            new BonusDef("needle in air", 16, Scaling.Action, Needle, silent: true);
        public static readonly BonusDef Kill = new BonusDef("KILL", 30, Scaling.Action, Needle);
        public static readonly BonusDef Uppercut = new BonusDef("UPPERCUT", 50, Scaling.Action, Needle);
        public static readonly BonusDef Shoryuken = new BonusDef("SHORYUKEN", 80, Scaling.Action, Needle);
        public static readonly BonusDef Toasty = new BonusDef("TOASTY!", 50, Scaling.Action, Needle);
        public static readonly BonusDef PinDrop = new BonusDef("PIN DROP", 60, Scaling.Action, Needle);
        public static readonly BonusDef CleanCut = new BonusDef("CLEAN CUT", 40, Scaling.Action, Needle);
        public static readonly BonusDef Overstitched = new BonusDef("OVERSTITCHED", 40, Scaling.Action, Needle);
        public static readonly BonusDef TwoBirds = new BonusDef("TWO BIRDS", 30, Scaling.Action, Needle);
        public static readonly BonusDef CrowdControl = new BonusDef("CROWD CONTROL", 60, Scaling.Action, Needle);
        public static readonly BonusDef Needlework = new BonusDef("NEEDLEWORK", 40, Scaling.Move, Needle);
        public static readonly BonusDef CriticalHit = new BonusDef("CRITICAL HIT", 40, Scaling.Action, Needle);
        public static readonly BonusDef DashSlash = new BonusDef("DASH SLASH", 60, Scaling.Action, Needle);
        public static readonly BonusDef AnimationCancel = new BonusDef("ANIMATION CANCEL", 40, Scaling.Action, Needle);
        public static readonly BonusDef GreatSlash = new BonusDef("GREAT SLASH", 80, Scaling.Action, Needle);
        public static readonly BonusDef Suspended = new BonusDef("SUSPENDED", 80, Scaling.Action, Needle);
        public static readonly BonusDef Taunt = new BonusDef("TAUNT", 20, Scaling.Move, Needle);
        public static readonly BonusDef InsultToInjury = new BonusDef("INSULT TO INJURY", 80, Scaling.Action, Needle);
        public static readonly BonusDef BigKill = new BonusDef("BIG KILL", 100, Scaling.Action, Needle);
        public static readonly BonusDef Backstitch = new BonusDef("BACKSTITCH", 60, Scaling.Action, Needle);
        public static readonly BonusDef Patience = new BonusDef("PATIENCE", 100, Scaling.Action, Needle);
        public static readonly BonusDef GloryKill = new BonusDef("GLORY KILL", 120, Scaling.Action, Needle);

        // 2. Air and movement.
        public static readonly BonusDef Pogo = new BonusDef("pogo", 20, Scaling.Action, Air, silent: true);
        public static readonly BonusDef PogoChain = new BonusDef("POGO", 15, Scaling.Move, Air);
        public static readonly BonusDef OneUp = new BonusDef("1-UP", 200, Scaling.Move, Air);
        public static readonly BonusDef Stairway = new BonusDef("STAIRWAY", 100, Scaling.Move, Air);
        public static readonly BonusDef Airshot = new BonusDef("AIRSHOT", 30, Scaling.Action, Air);
        public static readonly BonusDef Wingclipped = new BonusDef("WINGCLIPPED", 40, Scaling.Action, Air);
        public static readonly BonusDef BedOfNails = new BonusDef("BED OF NAILS", 40, Scaling.Move, Air);
        public static readonly BonusDef SkyHigh = new BonusDef("SKY HIGH", 150, Scaling.Move, Air);
        public static readonly BonusDef HangTime = new BonusDef("HANG TIME", 100, Scaling.Flat, Air);
        public static readonly BonusDef TouchGrass = new BonusDef("TOUCH GRASS", 25, Scaling.Flat, Air);
        public static readonly BonusDef OffTheWall = new BonusDef("OFF THE WALL", 50, Scaling.Action, Air);
        public static readonly BonusDef SecondWind = new BonusDef("SECOND WIND", 40, Scaling.Action, Air);
        public static readonly BonusDef Drifter = new BonusDef("DRIFTER", 60, Scaling.Action, Air);
        public static readonly BonusDef DiveBomb = new BonusDef("DIVE BOMB", 60, Scaling.Action, Air);
        public static readonly BonusDef Springboard = new BonusDef("SPRINGBOARD", 50, Scaling.Action, Air);
        public static readonly BonusDef DeathFromAbove = new BonusDef("DEATH FROM ABOVE", 120, Scaling.Action, Air);
        public static readonly BonusDef Liftoff = new BonusDef("LIFTOFF", 80, Scaling.Action, Air);
        public static readonly BonusDef Hooked = new BonusDef("HOOKED", 20, Scaling.Action, Air);
        public static readonly BonusDef ClawlinePull =
            new BonusDef("clawline pull", 20, Scaling.Action, Air, silent: true);
        public static readonly BonusDef ReeledIn = new BonusDef("REELED IN", 60, Scaling.Action, Air);
        public static readonly BonusDef GetOverHere = new BonusDef("GET OVER HERE!", 100, Scaling.Action, Air);
        // A near miss: an enemy attack passed right by Hornet in a jump or a dash.
        public static readonly BonusDef Graze = new BonusDef("GRAZE", 25, Scaling.Move, Air);

        // 3. Clash and parry.
        public static readonly BonusDef Clash = new BonusDef("CLASH", 50, Scaling.Move, Clashes);
        public static readonly BonusDef Deflect = new BonusDef("DEFLECT", 50, Scaling.Move, Clashes);
        public static readonly BonusDef Counterpoint = new BonusDef("COUNTERPOINT", 80, Scaling.Move, Clashes);
        public static readonly BonusDef Riposte = new BonusDef("RIPOSTE", 150, Scaling.Action, Clashes);
        public static readonly BonusDef Parry = new BonusDef("PARRY", 100, Scaling.Move, Clashes);
        public static readonly BonusDef NeedleAndThread = new BonusDef("NEEDLE AND THREAD", 250, Scaling.Move, Clashes);
        public static readonly BonusDef WitchTime = new BonusDef("WITCH TIME", 80, Scaling.Move, Clashes);

        // 4. Silk skills: silent hits, the kill bonus of each skill and what one use of a skill did.
        public static readonly BonusDef SkillHit = new BonusDef("skill", 30, Scaling.Action, Silk, silent: true);
        public static readonly BonusDef Skewered = new BonusDef("SKEWERED", 60, Scaling.Action, Silk);
        public static readonly BonusDef ShishKebab = new BonusDef("SHISH KEBAB", 120, Scaling.Action, Silk);
        public static readonly BonusDef CycloneSlash = new BonusDef("CYCLONE SLASH", 60, Scaling.Action, Silk);
        public static readonly BonusDef EyeOfTheStorm = new BonusDef("EYE OF THE STORM", 120, Scaling.Action, Silk);
        public static readonly BonusDef CrossCounter = new BonusDef("CROSS COUNTER", 200, Scaling.Action, Silk);
        public static readonly BonusDef Darted = new BonusDef("DARTED", 60, Scaling.Action, Silk);
        public static readonly BonusDef Pinball = new BonusDef("PINBALL", 120, Scaling.Action, Silk);
        public static readonly BonusDef Runic = new BonusDef("RUNIC", 60, Scaling.Action, Silk);
        public static readonly BonusDef RageQuit = new BonusDef("RAGE QUIT", 150, Scaling.Action, Silk);
        public static readonly BonusDef PaleNailed = new BonusDef("PALE NAILED", 60, Scaling.Action, Silk);
        public static readonly BonusDef HatTrick = new BonusDef("HAT TRICK", 80, Scaling.Action, Silk);
        public static readonly BonusDef NailedIt = new BonusDef("NAILED IT", 150, Scaling.Action, Silk);
        public static readonly BonusDef SilkRoad = new BonusDef("SILK ROAD", 100, Scaling.Move, Silk);

        // 5. Crests.
        public static readonly BonusDef TheHuntIsOn = new BonusDef("THE HUNT IS ON", 40, Scaling.Move, Crest);
        public static readonly BonusDef ApexPredator = new BonusDef("APEX PREDATOR", 80, Scaling.Move, Crest);
        public static readonly BonusDef Trophy = new BonusDef("TROPHY", 80, Scaling.Action, Crest);
        public static readonly BonusDef Harvest = new BonusDef("HARVEST", 100, Scaling.Action, Crest);
        public static readonly BonusDef DontFearTheReaper =
            new BonusDef("DON'T FEAR THE REAPER", 120, Scaling.Action, Crest);
        public static readonly BonusDef WanderersLuck = new BonusDef("WANDERER'S LUCK", 50, Scaling.Action, Crest);
        public static readonly BonusDef LuckyStrike = new BonusDef("LUCKY STRIKE", 120, Scaling.Action, Crest);
        public static readonly BonusDef OldHabits = new BonusDef("OLD HABITS", 80, Scaling.Move, Crest);
        public static readonly BonusDef BeastMode = new BonusDef("BEAST MODE", 30, Scaling.Move, Crest);
        public static readonly BonusDef Bloodlust = new BonusDef("BLOODLUST", 60, Scaling.Action, Crest);
        public static readonly BonusDef RipAndTear = new BonusDef("RIP AND TEAR", 150, Scaling.Action, Crest);
        public static readonly BonusDef Whiplash = new BonusDef("WHIPLASH", 50, Scaling.Action, Crest);
        public static readonly BonusDef Hexed = new BonusDef("HEXED", 100, Scaling.Action, Crest);
        public static readonly BonusDef Rooted = new BonusDef("ROOTED", 150, Scaling.Action, Crest);
        public static readonly BonusDef DrillBit = new BonusDef("DRILL BIT", 60, Scaling.Action, Crest);
        public static readonly BonusDef PierceTheHeavens =
            new BonusDef("PIERCE THE HEAVENS", 150, Scaling.Action, Crest);
        public static readonly BonusDef VengefulSpirit = new BonusDef("VENGEFUL SPIRIT", 50, Scaling.Action, Crest);
        public static readonly BonusDef Spellbound = new BonusDef("SPELLBOUND", 60, Scaling.Action, Crest);
        public static readonly BonusDef CursedBlade = new BonusDef("CURSED BLADE", 50, Scaling.Action, Crest);
        public static readonly BonusDef AgainstAllOdds = new BonusDef("AGAINST ALL ODDS", 200, Scaling.Flat, Crest);
        public static readonly BonusDef NoCloakNoProblem =
            new BonusDef("NO CLOAK, NO PROBLEM", 50, Scaling.Action, Crest);
        public static readonly BonusDef Wanderlust = new BonusDef("WANDERLUST", 60, Scaling.Action, Crest);
        public static readonly BonusDef Unstoppable = new BonusDef("UNSTOPPABLE", 80, Scaling.Action, Crest);
        public static readonly BonusDef CatsCradle = new BonusDef("CAT'S CRADLE", 100, Scaling.Move, Crest);
        public static readonly BonusDef Blueprint = new BonusDef("BLUEPRINT", 80, Scaling.Action, Crest);
        public static readonly BonusDef Restock = new BonusDef("RESTOCK", 50, Scaling.Move, Crest);
        public static readonly BonusDef DesolateDive = new BonusDef("DESOLATE DIVE", 80, Scaling.Move, Crest);

        // 6. Attack tools: silent hits, the kill bonus of each tool and what one throw did.
        public static readonly BonusDef ToolHit = new BonusDef("tool", 20, Scaling.Action, Tool, silent: true);
        public static readonly BonusDef Pinned = new BonusDef("PINNED", 50, Scaling.Action, Tool);
        public static readonly BonusDef PinPoint = new BonusDef("PIN-POINT", 80, Scaling.Action, Tool);
        public static readonly BonusDef Forked = new BonusDef("FORKED", 50, Scaling.Action, Tool);
        public static readonly BonusDef Trifecta = new BonusDef("TRIFECTA", 100, Scaling.Action, Tool);
        public static readonly BonusDef Stung = new BonusDef("STUNG", 60, Scaling.Action, Tool);
        public static readonly BonusDef Caltropped = new BonusDef("CALTROPPED", 50, Scaling.Action, Tool);
        public static readonly BonusDef WatchYourStep = new BonusDef("WATCH YOUR STEP", 100, Scaling.Action, Tool);
        public static readonly BonusDef Longshot = new BonusDef("LONGSHOT", 60, Scaling.Action, Tool);
        public static readonly BonusDef ClayPigeon = new BonusDef("CLAY PIGEON", 80, Scaling.Action, Tool);
        public static readonly BonusDef Ringer = new BonusDef("RINGER", 50, Scaling.Action, Tool);
        public static readonly BonusDef Ricochet = new BonusDef("RICOCHET", 100, Scaling.Action, Tool);
        public static readonly BonusDef BombsAway = new BonusDef("BOMBS AWAY", 80, Scaling.Action, Tool);
        public static readonly BonusDef ShellShocked = new BonusDef("SHELL SHOCKED", 60, Scaling.Action, Tool);
        public static readonly BonusDef FullSpin = new BonusDef("FULL SPIN", 80, Scaling.Action, Tool);
        public static readonly BonusDef Slugger = new BonusDef("SLUGGER", 60, Scaling.Action, Tool);
        public static readonly BonusDef ThreeRoundBurst = new BonusDef("THREE-ROUND BURST", 80, Scaling.Action, Tool);
        public static readonly BonusDef Piercer = new BonusDef("PIERCER", 120, Scaling.Action, Tool);
        public static readonly BonusDef Screwed = new BonusDef("SCREWED", 80, Scaling.Action, Tool);
        public static readonly BonusDef Roadkill = new BonusDef("ROADKILL", 60, Scaling.Action, Tool);
        public static readonly BonusDef Wingman = new BonusDef("WINGMAN", 50, Scaling.Action, Tool);
        public static readonly BonusDef FistfulOfRosaries =
            new BonusDef("FISTFUL OF ROSARIES", 60, Scaling.Action, Tool);
        public static readonly BonusDef MakeItRain = new BonusDef("MAKE IT RAIN", 100, Scaling.Action, Tool);
        public static readonly BonusDef LightningRod = new BonusDef("LIGHTNING ROD", 80, Scaling.Action, Tool);
        public static readonly BonusDef ShesOnFire = new BonusDef("SHE'S ON FIRE!", 120, Scaling.Action, Tool);
        public static readonly BonusDef GottaGoFast = new BonusDef("GOTTA GO FAST", 100, Scaling.Action, Tool);
        public static readonly BonusDef Lifeblood = new BonusDef("LIFEBLOOD", 40, Scaling.Move, Tool);
        public static readonly BonusDef Snared = new BonusDef("SNARED", 60, Scaling.Action, Tool);
        public static readonly BonusDef TrapCard = new BonusDef("TRAP CARD", 150, Scaling.Action, Tool);
        public static readonly BonusDef Shrapnel = new BonusDef("SHRAPNEL", 80, Scaling.Action, Tool);
        public static readonly BonusDef ArmorPiercer = new BonusDef("ARMOR PIERCER", 80, Scaling.Action, Tool);
        public static readonly BonusDef Boomerang = new BonusDef("BOOMERANG", 60, Scaling.Action, Tool);
        public static readonly BonusDef TrickShot = new BonusDef("TRICK SHOT", 120, Scaling.Action, Tool);
        public static readonly BonusDef WallRide = new BonusDef("WALL RIDE", 100, Scaling.Action, Tool);

        // 7. Defensive and support tools.
        public static readonly BonusDef BuzzCut = new BonusDef("BUZZ CUT", 60, Scaling.Action, Support);
        public static readonly BonusDef MirrorMatch = new BonusDef("MIRROR MATCH", 80, Scaling.Action, Support);
        public static readonly BonusDef SubstitutionJutsu =
            new BonusDef("SUBSTITUTION JUTSU", 100, Scaling.Action, Support);
        public static readonly BonusDef WillOWisp = new BonusDef("WILL-O'-WISP", 50, Scaling.Action, Support);
        public static readonly BonusDef Conductor = new BonusDef("CONDUCTOR", 100, Scaling.Action, Support);
        public static readonly BonusDef LiveWire = new BonusDef("LIVE WIRE", 60, Scaling.Move, Support);
        public static readonly BonusDef GlassCannon = new BonusDef("GLASS CANNON", 150, Scaling.Flat, Support);
        public static readonly BonusDef Pickpocket = new BonusDef("PICKPOCKET", 30, Scaling.Action, Support);
        public static readonly BonusDef DoubleTap = new BonusDef("DOUBLE TAP", 60, Scaling.Action, Support);
        public static readonly BonusDef SavedByTheBell = new BonusDef("SAVED BY THE BELL", 100, Scaling.Move, Support);
        public static readonly BonusDef DoubleKnot = new BonusDef("DOUBLE KNOT", 80, Scaling.Move, Support);
        public static readonly BonusDef ReserveTank = new BonusDef("RESERVE TANK", 80, Scaling.Move, Support);
        public static readonly BonusDef DeathDefiance = new BonusDef("DEATH DEFIANCE", 200, Scaling.Move, Support);
        public static readonly BonusDef LoadedDice = new BonusDef("LOADED DICE", 150, Scaling.Move, Support);

        // 8. Needolin.
        public static readonly BonusDef SwanSong = new BonusDef("SWAN SONG", 100, Scaling.Action, Needolin);
        public static readonly BonusDef StandingOvation = new BonusDef("STANDING OVATION", 200, Scaling.Move, Needolin);
        public static readonly BonusDef Peekaboo = new BonusDef("PEEKABOO", 50, Scaling.Action, Needolin);

        // 9. Healing and health.
        public static readonly BonusDef Mended = new BonusDef("MENDED", 50, Scaling.Move, Health);
        public static readonly BonusDef MidairMend = new BonusDef("MIDAIR MEND", 80, Scaling.Move, Health);
        public static readonly BonusDef StitchInTime = new BonusDef("STITCH IN TIME", 150, Scaling.Move, Health);
        public static readonly BonusDef RiskyBusiness = new BonusDef("RISKY BUSINESS", 100, Scaling.Move, Health);
        public static readonly BonusDef WoundTight = new BonusDef("WOUND TIGHT", 30, Scaling.Flat, Health);
        public static readonly BonusDef NoCostTooGreat = new BonusDef("NO COST TOO GREAT", 150, Scaling.Flat, Health);
        public static readonly BonusDef LastStand = new BonusDef("LAST STAND", 50, Scaling.Action, Health);
        public static readonly BonusDef Clutch = new BonusDef("CLUTCH", 150, Scaling.Flat, Health);

        // 10. Elements and environment. Element bonuses stack on the kill type; a level hazard is the kill type itself.
        public static readonly BonusDef Seared = new BonusDef("SEARED", 40, Scaling.Action, World);
        public static readonly BonusDef Flambe = new BonusDef("FLAMBÉ", 60, Scaling.Action, World);
        public static readonly BonusDef Toxic = new BonusDef("TOXIC", 40, Scaling.Action, World);
        public static readonly BonusDef Shocking = new BonusDef("SHOCKING", 40, Scaling.Action, World);
        public static readonly BonusDef Thunderstruck = new BonusDef("THUNDERSTRUCK", 100, Scaling.Move, World);
        public static readonly BonusDef Shredded = new BonusDef("SHREDDED", 40, Scaling.Action, World);
        public static readonly BonusDef Impaled = new BonusDef("IMPALED", 40, Scaling.Action, World);
        public static readonly BonusDef Kaboom = new BonusDef("KABOOM", 45, Scaling.Action, World);
        public static readonly BonusDef ChainReaction = new BonusDef("CHAIN REACTION", 120, Scaling.Move, World);
        public static readonly BonusDef Crossfire = new BonusDef("CROSSFIRE", 200, Scaling.Move, World);
        public static readonly BonusDef Spiked = new BonusDef("SPIKED", 80, Scaling.Move, World);
        public static readonly BonusDef Melted = new BonusDef("MELTED", 100, Scaling.Move, World);
        public static readonly BonusDef Dissolved = new BonusDef("DISSOLVED", 80, Scaling.Move, World);
        public static readonly BonusDef Trapped = new BonusDef("TRAPPED", 80, Scaling.Move, World);
        public static readonly BonusDef Blasted = new BonusDef("KABOOM", 45, Scaling.Move, World);
        public static readonly BonusDef AwMan = new BonusDef("AW MAN", 0, Scaling.Flat, World);
        public static readonly BonusDef DominoEffect = new BonusDef("DOMINO EFFECT", 150, Scaling.Move, World);
        public static readonly BonusDef ThisIsSparta = new BonusDef("THIS IS SPARTA!", 150, Scaling.Move, World);

        // 11. Enemies.
        public static readonly BonusDef Severed = new BonusDef("SEVERED", 40, Scaling.Action, Enemy);
        public static readonly BonusDef BigGame = new BonusDef("BIG GAME", 120, Scaling.Action, Enemy);
        public static readonly BonusDef WhitePalace = new BonusDef("WHITE PALACE", 80, Scaling.Action, Enemy);
        public static readonly BonusDef StopThief = new BonusDef("STOP, THIEF!", 80, Scaling.Action, Enemy);
        public static readonly BonusDef CaughtOffGuard = new BonusDef("CAUGHT OFF GUARD", 60, Scaling.Action, Enemy);
        public static readonly BonusDef Flanked = new BonusDef("FLANKED", 60, Scaling.Action, Enemy);
        public static readonly BonusDef ParryThis = new BonusDef("PARRY THIS!", 100, Scaling.Move, Enemy);
        public static readonly BonusDef ReturnToSender = new BonusDef("RETURN TO SENDER", 120, Scaling.Action, Enemy);
        public static readonly BonusDef ThreadTheNeedle = new BonusDef("THREAD THE NEEDLE", 120, Scaling.Action, Enemy);

        // 12. Bosses: the result of the fight goes once, when the boss is beaten.
        public static readonly BonusDef Finale = new BonusDef("FINALE", 500, Scaling.Flat, Boss);
        public static readonly BonusDef FlawlessVictory = new BonusDef("FLAWLESS VICTORY", 400, Scaling.Flat, Boss);
        public static readonly BonusDef Showstopper = new BonusDef("SHOWSTOPPER", 500, Scaling.Flat, Boss);
        public static readonly BonusDef Jackpot = new BonusDef("JACKPOT!", 300, Scaling.Flat, Boss);
        public static readonly BonusDef GreatEnemyFelled = new BonusDef("GREAT ENEMY FELLED", 300, Scaling.Flat, Boss);
        public static readonly BonusDef EnGarde = new BonusDef("EN GARDE!", 150, Scaling.Move, Boss);
        public static readonly BonusDef StudentBecomesTheMaster =
            new BonusDef("STUDENT BECOMES THE MASTER", 300, Scaling.Flat, Boss);
        public static readonly BonusDef Staggered = new BonusDef("STAGGERED", 100, Scaling.Move, Boss);
        public static readonly BonusDef BellRinger = new BonusDef("BELL RINGER", 150, Scaling.Move, Boss);
        public static readonly BonusDef BindBreaker = new BonusDef("BIND BREAKER", 200, Scaling.Move, Boss);
        public static readonly BonusDef Unforgiven = new BonusDef("UNFORGIVEN", 200, Scaling.Flat, Boss);

        // 13. Multikills and combinations.
        public static readonly BonusDef Duet = new BonusDef("DUET", 25, Scaling.Action, Combo);
        public static readonly BonusDef Trio = new BonusDef("TRIO", 50, Scaling.Action, Combo);
        public static readonly BonusDef Choir = new BonusDef("CHOIR", 100, Scaling.Action, Combo);
        public static readonly BonusDef MixedMedia = new BonusDef("MIXED MEDIA", 50, Scaling.Action, Combo);
        public static readonly BonusDef Patchwork = new BonusDef("PATCHWORK", 100, Scaling.Action, Combo);
        public static readonly BonusDef Tapestry = new BonusDef("TAPESTRY", 200, Scaling.Action, Combo);
        public static readonly BonusDef Medley = new BonusDef("MEDLEY", 250, Scaling.Move, Combo);
        public static readonly BonusDef SetPiece = new BonusDef("SET PIECE", 200, Scaling.Move, Combo);
        public static readonly BonusDef Toolbox = new BonusDef("TOOLBOX", 150, Scaling.Move, Combo);
        public static readonly BonusDef Pincushion = new BonusDef("PINCUSHION", 80, Scaling.Action, Combo);
        public static readonly BonusDef OnBeat = new BonusDef("ON BEAT", 100, Scaling.Move, Combo);
        public static readonly BonusDef Hadouken = new BonusDef("HADOUKEN", 80, Scaling.Action, Combo);
        public static readonly BonusDef WomboCombo = new BonusDef("WOMBO COMBO", 300, Scaling.Move, Combo);

        // Kill streaks (Tuning.StreakWindows): kills over seconds, unlike DUET, TRIO and CHOIR at once.
        public static readonly BonusDef DoubleKill = new BonusDef("DOUBLE KILL", 50, Scaling.Flat, Combo);
        public static readonly BonusDef TripleKill = new BonusDef("TRIPLE KILL", 100, Scaling.Flat, Combo);
        public static readonly BonusDef UltraKill = new BonusDef("ULTRAKILL", 200, Scaling.Flat, Combo);
        public static readonly BonusDef Rampage = new BonusDef("RAMPAGE", 300, Scaling.Flat, Combo);

        // Running for 10 s without stopping during a combo.
        public static readonly BonusDef RunForrestRun = new BonusDef("RUN, FORREST, RUN!", 60, Scaling.Flat, Air);

        // Every 5 s of SILKSONG gives one silk: a line without points that says so.
        public static readonly BonusDef SilkReward = new BonusDef("SILK", 0, Scaling.Flat, None, weight: 4);

        // 14. Streaks and fight results.
        public static readonly BonusDef Untouchable = new BonusDef("UNTOUCHABLE", 150, Scaling.Flat, Streak);
        // A line without points: at the cap its points could only refill the meter and keep SILKSONG alive for free.
        public static readonly BonusDef HoldingTheNote = new BonusDef("HOLDING THE NOTE", 0, Scaling.Flat, Streak);
        public static readonly BonusDef NoStringsAttached =
            new BonusDef("NO STRINGS ATTACHED", 300, Scaling.Flat, Streak);
        public static readonly BonusDef ZeroToHero = new BonusDef("ZERO TO HERO", 300, Scaling.Flat, Streak);
        public static readonly BonusDef CurtainCall = new BonusDef("CURTAIN CALL", 150, Scaling.Flat, Streak);
        public static readonly BonusDef Perfect = new BonusDef("PERFECT", 300, Scaling.Flat, Streak);
        public static readonly BonusDef GitGud = new BonusDef("GIT GUD", 0, Scaling.Flat, Streak);

        // Kill bonus of a silk skill by its tool name; null for skills without one.
        public static BonusDef SkillKill(string skill)
        {
            switch (skill)
            {
                case Skills.Silkspear: return Skewered;
                case Skills.ThreadStorm: return CycloneSlash;
                case Skills.CrossStitch: return CrossCounter;
                case Skills.Sharpdart: return Darted;
                case Skills.RuneRage: return Runic;
                case Skills.PaleNails: return PaleNailed;
                case Skills.SilkSoar: return Liftoff;
                default: return null;
            }
        }

        // Kill bonus of a tool by its internal name; null for tools whose kills have no name of their own.
        public static BonusDef ToolKill(string tool)
        {
            switch (tool)
            {
                case Tools.StraightPin: return Pinned;
                case Tools.ThreefoldPin: return Forked;
                case Tools.StingShard: return Stung;
                case Tools.Tacks: return Caltropped;
                case Tools.Longpin: return Longshot;
                case Tools.ThrowingRing: return Ringer;
                case Tools.Conchcutter: return ShellShocked;
                case Tools.SilkshotForge:
                case Tools.SilkshotArchitect:
                    return Slugger;
                case Tools.DelversDrill: return Screwed;
                case Tools.CogworkWheel: return Roadkill;
                case Tools.Cogfly: return Wingman;
                case Tools.RosaryCannon: return FistfulOfRosaries;
                case Tools.Voltvessels: return LightningRod;
                case Tools.SnareSetter: return Snared;
                case Tools.SawtoothCirclet: return BuzzCut;
                case Tools.ClawMirror:
                case Tools.ClawMirrors:
                    return MirrorMatch;
                case Tools.MemoryCrystal: return SubstitutionJutsu;
                case Tools.WispfireLantern: return WillOWisp;
                default: return null;
            }
        }
    }
}
