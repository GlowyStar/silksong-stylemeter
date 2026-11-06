namespace Stylemeter.Core
{
    // Every number of the scoring system, as in MECHANICS.md.
    internal static class Tuning
    {
        // Rank sizes and drains per second (MECHANICS.md §2). While Hornet keeps scoring, the 2 s drain delay holds the
        // meter, so the drains mostly matter once the fight is over. SILKSONG has no delay and melts from full in 5 s:
        // it is held only by scoring all the time, and pays silk for every 5 s held (SilkRewardPeriod).
        public static readonly RankInfo[] Ranks =
        {
            new RankInfo("DREAMER", 200, 7.2f),
            new RankInfo("CHRYSALIS", 300, 9.1f),
            new RankInfo("BELLSONG", 400, 11f),
            new RankInfo("ASCENDED", 500, 13f),
            new RankInfo("SUBLIME", 700, 26f),
            new RankInfo("SSERENADE", 850, 32.5f),
            new RankInfo("SSSILKSTORM", 1000, 44f),
            new RankInfo("SILKSONG", 1500, 300f),
        };

        public const int Sublime = 4;
        public const int Silksong = 7;

        // Meter (§2, §5). Balancing knobs, set by the sweeps: DrainScale multiplies every rank's drain, DrainDelay
        // holds the drain for that long after any points, SilentScale multiplies the silent hit points.
        public static float DrainScale = 1f;
        public static float TopDrainScale = 1f;
        public static float DrainDelay = 2f;
        public static float SilentScale = 1f;
        // What a hit on Hornet costs: 1 = one whole rank (ULTRAKILL); below 1 = that share of the current meter, so a
        // rank goes only when the meter empties. A share hardly punishes spikes at a full meter, hence a whole rank.
        public static float HitPenalty = 1f;
        // Extra drain on SILKSONG alone: the peak has to be held by style, not just reached.
        public static float PeakDrainScale = 1f;
        public const float ComboStartFill = 0.25f;
        public const float RankUpFloor = 0.25f;
        public const float RankDownFill = 0.75f;
        public const float SoftDamagePenalty = 50f;
        public static float BossMultiplier = 2f;

        // Thread (§3).
        public const float ThreadMax = 10f;
        public const float ThreadWear = 0.7f;
        public const float ThreadRecoverOthers = 0.5f;
        public const float ThreadRecoverPerSecond = 0.1f;
        public const float ThreadBossWear = 1.5f;
        public const float ThreadResetDelay = 10f;
        public const float WearNeedleHit = 0.4f;
        public const float WearPogo = 0.3f;
        public const float WearKill = 0.5f;
        public const float WearToolHit = 0.6f;
        public const float WearSkillHit = 0.75f;

        // Height (§4).
        public const float HeightStep = 0.1f;
        public const float HeightMax = 2f;
        public const float LandingGrace = 0.25f;

        // Feed and multikills (§6, §8).
        public const float FeedLineLife = 3f;
        public const float MultiKillWindow = 0.5f;
        // A bonus's weight for its colour in the feed, by its base points: below 100 plain, then gold, rose and red.
        public const float FeedWeightGold = 100f;
        public const float FeedWeightRose = 200f;
        public const float FeedWeightRed = 300f;

        // Kill streaks: 2 kills within 4 s, 3 within 8 s, 4 within 15 s, 5 within 20 s.
        public static readonly float[] StreakWindows = { 4f, 8f, 15f, 20f };
        // RUN, FORREST, RUN!: running on the ground one way for this long during a combo. The gap covers a bump in the
        // floor and a dash turning into a sprint, not a jump.
        public const float RunTime = 10f;
        public const float RunMinSpeed = 5f;
        public const float RunGap = 0.25f;
        // SILKSONG held for this long without a break gives Hornet one silk.
        public const float SilkRewardPeriod = 5f;

        // Windows and thresholds of individual bonuses (BONUSES.md).
        public const float RisingSpeed = 0.5f;
        public const float FallingSpeed = 0.5f;
        public const float DeathFromAboveFall = 1.5f;
        public const int CleanCutMinHp = 40;
        public const int OverstitchedMinDamage = 30;
        public const float SwingWindow = 0.15f;
        public const int NeedleworkHits = 10;
        public const float NeedleworkGap = 2f;
        public const float ToastyChance = 0.05f;
        public const float PogoPairWindow = 0.2f;
        public const float DeflectWindow = 3f;
        public const float CounterpointWindow = 0.5f;
        public const float RiposteWindow = 1f;
        public const float GetOverHereWindow = 1f;
        public const float GrazeCooldown = 1.5f;
        // A near miss pays only if no hit follows within this time: a hit often comes within about half a second of a
        // near miss (the next bullet of a volley, the next stab of a combo).
        public const float GrazeConfirm = 0.6f;
        public const float SpringboardWindow = 1f;
        public const float DiveBombWindow = 0.5f;
        public const float OffTheWallWindow = 0.5f;
        public const float HangTimeAir = 5f;
        public const float TouchGrassAir = 10f;
        public const int AirSeriesMinActions = 3;
        public const float TauntCooldown = 5f;
        public const float UntouchablePeriod = 30f;
        public const int UntouchableMinActions = 10;
        public const float HoldingTheNotePeriod = 10f;
        public const float ZeroToHeroTime = 15f;
        public const float GitGudWindow = 5f;
        public const float LateKillWindow = 0.5f;

        // Bonuses of BONUSES.md §1, §4-7 and §9-13. Distances are in game units (the screen is about 30 wide).
        public const int BigKillMinHp = 100;
        public const float AnimationCancelWindow = 0.4f;
        public const float DrifterWindow = 0.3f;
        public const int OldHabitsPogos = 5;
        public const float UseWindow = 3f;
        public const float FlintslateMaxTime = 60f;
        public const float FleaBrewTime = 10f;
        public const int UseKills = 3;
        public const int MakeItRainHits = 10;
        public const float MakeItRainWindow = 2f;
        public const int PincushionPins = 3;
        public const float PinPointRange = 12f;
        public const float HadoukenRange = 10f;
        public const float RiskyBusinessRange = 4f;
        public const float EnvironmentKillWindow = 3f;
        public const float CrossfireCooldown = 1f;
        public const float BurnWindow = 1.5f;
        public const float ExplosionWindow = 0.2f;
        public const float ThunderstruckWindow = 0.5f;
        public const float MedleyWindow = 5f;
        public const float SetPieceWindow = 2f;
        public const float WomboComboWindow = 3f;
        public const float OnBeatTolerance = 0.05f;
        public const float OnBeatMinGap = 0.45f;
        public const float OnBeatMaxGap = 1.5f;
        public const float OnBeatSameHit = 0.1f;
        public const float BossEndFallback = 8f;

        public const float PatienceHold = 10f;
        // Multibinder's second stage completes about a second after the first.
        public const float DoubleKnotWindow = 3f;
        // One down whip lands twice, the strike and the pull 0.15 s later, and can hit two enemies at once; a new whip
        // comes after this gap (chained whips come 0.6-0.8 s apart).
        public const float WitchDownGap = 0.3f;
        public const int CatsCradleHits = 3;
        // A dash finisher or a dart is one move: one bonus per move, a new move after this gap.
        public const float MoveGap = 0.4f;
        // NO COST TOO GREAT: the skill's use and its silk leaving the spool arrive within this of each other.
        public const float NoCostSkillWindow = 0.5f;
        public const int StandingOvationKills = 3;
        public const float SpartaWindow = 4f;
        public const int DominoChain = 3;
        public const float DominoWindow = 1.5f;
        public const float GloryKillGrace = 0.3f;
        // Quick Sling throws the second tool 0.15 s after the first.
        public const float QuickSlingGap = 0.5f;
        public const float ReturnToSenderGap = 0.5f;
    }

    internal readonly struct RankInfo
    {
        public readonly string Name;
        public readonly float Size;
        public readonly float Drain;

        public RankInfo(string name, float size, float drain)
        {
            Name = name;
            Size = size;
            Drain = drain;
        }
    }
}
