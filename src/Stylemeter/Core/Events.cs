namespace Stylemeter.Core
{
    internal enum AttackKind
    {
        NeedleSide,
        NeedleUp,
        NeedleDown,
        NeedleDash,
        NeedleCharge,
        NeedleSpecial,
        Clawline,
        ClawlinePull,
        Skill,
        Tool,
        Other,
    }

    // The element a hit carries (needle imbued by Flintslate or Pollip Pouch, fire or lightning attacks, tag ticks).
    internal enum Element
    {
        None,
        Fire,
        Poison,
        Lightning,
    }

    // The game's tool damage flags (ToolDamageFlags): cutting, piercing and burning tools.
    [System.Flags]
    internal enum DamageFlags
    {
        None = 0,
        Shredding = 1,
        Spearing = 2,
        Searing = 4,
    }

    // What hurt an enemy when it was not Hornet: the level (spikes, lava, acid, traps), an explosion or another enemy.
    internal enum Hazard
    {
        None,
        Spikes,
        Lava,
        Acid,
        Trap,
        Explosion,
        Enemy,
        Other,
    }

    // Hornet as the engine needs her, read from the game every frame and before every event.
    internal struct HeroState
    {
        public bool OnGround;
        public bool OnWall;
        public float VelocityX;
        public float VelocityY;
        public int Health;
        public int MaxHealth;
        public int Silk;
        public int SilkMax;
        // Silk one skill costs (the game's SilkSkillCost: 4, or 3 with Egg of Flealia at full health); 0 = unknown.
        public int SkillCost;
        public bool Dead;
        // Crest by its game ID (Crests), Hunter focus stage (0, 1 or 2), Beast rage and Reaper harvest.
        public string Crest;
        public int HunterStage;
        public bool InRage;
        public bool InReaperMode;
        public bool BarbedBracelet;
    }

    internal sealed class EnemyInfo
    {
        public int Id;
        public string Name;
        public int MaxHp;
        public bool IsBoss;
        // Title card key of the boss fight this enemy belongs to (BossBonuses), null for ordinary enemies.
        public string BossKey;
        public bool Large;
        public bool Flying;
        public bool BlackThreaded;
        // Hunter's Journal key of its kind (Species), null when it has no journal record.
        public string Species;
    }

    // One hit by Hornet that took health off an enemy. Killed is set when this hit killed it.
    internal struct HitEvent
    {
        public float Time;
        public EnemyInfo Enemy;
        public AttackKind Kind;
        public string Tool;
        public int SwingId;
        public int HpBefore;
        public int HpAfter;
        public bool Killed;
        public bool Crit;
        public bool Taunted;
        public Element Element;
        public bool Explosion;
        public DamageFlags Flags;
        // A skill charged by Volt Filament (it leaves lightning ticks on the enemy).
        public bool Zap;
        public bool HunterCombo;
        public bool RageHit;
        // The hit came from Hornet's bind (Witch roots and other crest bind effects).
        public bool FromBind;
        // Hornet to the enemy when the hit landed, in game units.
        public float Distance;
        // The enemy's guard would have blocked this hit without its piercing (Beast fury, Longpin).
        public bool PiercedGuard;
        // The enemy held up a directional shield and the hit came from a side it does not cover.
        public bool Flanked;
        // Slash from a wall cling, the Wanderer's dash finisher (RecoilStab), a charged slash held this long (s).
        public bool WallSlash;
        public bool DashFinisher;
        public float ChargeHeld;
        // Sharpdart that carried Hornet through this enemy's attack in the same dart.
        public bool DodgedThrough;
        // A side hit into the enemy's back.
        public bool Backstab;
        // Thrown tools: the Curveclaw on its way back, a Throwing Ring after a wall bounce, the Cogwork Wheel after
        // climbing a wall, the spikes a broken Sting Shard fires.
        public bool ReturnLeg;
        public bool AfterWallBounce;
        public bool WheelClimbed;
        public bool Shrapnel;
        // What the enemy was doing (EnemyWatch): stunned, singing to Needolin in song number SongId (0 = none), lured
        // out by the song, never in its shell since Hornet first hit it, or a thief with her rosaries.
        public bool Stunned;
        public int SongId;
        public bool Lured;
        public bool Exposed;
        public bool Thief;

        public int Damage => HpBefore - HpAfter;
    }

    // How an enemy died outside one of Hornet's hits.
    internal struct DeathCause
    {
        public Hazard Hazard;
        public Element Element;
        // Hornet's own damage over time: poison, fire or lightning ticks she left on the enemy.
        public bool ByHornet;
    }

    // Tool names the game gives Hornet's silk skills; Silk Soar is not a tool, so it gets its own name.
    internal static class Skills
    {
        public const string Silkspear = "Silk Spear";
        public const string ThreadStorm = "Thread Sphere";
        public const string CrossStitch = "Parry";
        public const string Sharpdart = "Silk Charge";
        public const string RuneRage = "Silk Bomb";
        public const string PaleNails = "Silk Boss Needle";
        public const string SilkSoar = "Silk Soar";
    }

    // Internal names of Hornet's tools, each under its English name.
    internal static class Tools
    {
        public const string StraightPin = "Straight Pin";
        public const string ThreefoldPin = "Tri Pin";
        public const string StingShard = "Sting Shard";
        public const string Tacks = "Tack";
        public const string Longpin = "Harpoon";
        public const string Curveclaw = "Curve Claws";
        public const string Curvesickle = "Curve Claws Upgraded";
        public const string ThrowingRing = "Shakra Ring";
        public const string Pimpillo = "Pimpilo";
        public const string Conchcutter = "Conch Drill";
        public const string SilkshotForge = "WebShot Forge";
        public const string SilkshotArchitect = "WebShot Architect";
        public const string SilkshotWeaver = "WebShot Weaver";
        public const string DelversDrill = "Screw Attack";
        public const string CogworkWheel = "Cogwork Saw";
        public const string Cogfly = "Cogwork Flier";
        public const string RosaryCannon = "Rosary Cannon";
        public const string Voltvessels = "Lightning Rod";
        public const string Flintslate = "Flintstone";
        public const string FleaBrew = "Flea Brew";
        public const string PlasmiumPhial = "Lifeblood Syringe";
        public const string SnareSetter = "Silk Snare";
        public const string SawtoothCirclet = "Brolly Spike";
        public const string ClawMirror = "Dazzle Bind";
        public const string ClawMirrors = "Dazzle Bind Upgraded";
        public const string MemoryCrystal = "Revenge Crystal";
        public const string WispfireLantern = "Wisp Lantern";

        public static bool IsPin(string tool) => tool == StraightPin || tool == ThreefoldPin || tool == Longpin;

        public static bool IsTrap(string tool) => tool == StingShard || tool == Tacks || tool == SnareSetter;

        // Tools that fire several objects per throw: two objects hitting do not prove both throws of Quick Sling hit.
        public static bool IsMultiShot(string tool) => tool == ThreefoldPin || tool == Tacks || tool == RosaryCannon
            || tool == SilkshotForge || tool == SilkshotArchitect || tool == SilkshotWeaver;

        public static bool IsCurveclaw(string tool) => tool == Curveclaw || tool == Curvesickle;
    }

    // Crest IDs as the game stores them (PlayerData.CurrentCrestID).
    internal static class Crests
    {
        public const string Hunter = "Hunter";
        public const string HunterV2 = "Hunter_v2";
        public const string HunterV3 = "Hunter_v3";
        public const string Reaper = "Reaper";
        public const string Wanderer = "Wanderer";
        public const string Beast = "Warrior";
        public const string Witch = "Witch";
        public const string Architect = "Toolmaster";
        public const string Shaman = "Spell";
        public const string Cursed = "Cursed";
        public const string Cloakless = "Cloakless";
    }

    // What kind of attack a hit was, from the path of the damaging object and its tool.
    internal static class Attacks
    {
        public static AttackKind Classify(string path, string tool, bool toolIsSkill, out string name)
        {
            name = tool;
            if (!string.IsNullOrEmpty(tool))
                return toolIsSkill ? AttackKind.Skill : AttackKind.Tool;
            path = path ?? "";
            if (path.Contains("Harpoon Dash Damager"))
                return AttackKind.ClawlinePull;
            if (path.Contains("Harpoon Damager"))
                return AttackKind.Clawline;
            if (path.Contains("Screw Attack"))
            {
                name = Tools.DelversDrill;
                return AttackKind.Tool;
            }
            name = SkillByPath(path);
            if (name != null)
                return AttackKind.Skill;
            if (IsBindEffect(path))
                return AttackKind.NeedleSpecial;
            if (!path.Contains("Attacks/"))
                return AttackKind.Other;
            var leaf = path.Substring(path.LastIndexOf('/') + 1);
            if (leaf.Contains("Up"))
                return AttackKind.NeedleUp;
            if (leaf.Contains("Down"))
                return AttackKind.NeedleDown;
            if (leaf.Contains("Dash"))
                return AttackKind.NeedleDash;
            if (leaf.Contains("Charge"))
                return AttackKind.NeedleCharge;
            return leaf.Contains("Slash") ? AttackKind.NeedleSide : AttackKind.NeedleSpecial;
        }

        public static bool IsNeedle(AttackKind kind) => kind <= AttackKind.NeedleSpecial;

        public static bool IsBindEffect(string path) => path != null && path.Contains("Bind Effects/");

        private static string SkillByPath(string path)
        {
            if (path.Contains("Super Jump Needle Throw"))
                return Skills.SilkSoar;
            if (path.Contains("Needle Throw"))
                return Skills.Silkspear;
            if (path.Contains("Sphere Ball"))
                return Skills.ThreadStorm;
            if (path.Contains("ParryDash") || path.Contains("parry_stab"))
                return Skills.CrossStitch;
            if (path.Contains("Silk Charge"))
                return Skills.Sharpdart;
            return null;
        }
    }
}
