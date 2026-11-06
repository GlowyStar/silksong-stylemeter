using System;
using System.Collections.Generic;
using System.Linq;
using Stylemeter.Core;

namespace Stylemeter.Sim
{
    // Rule checks for the scoring core, one method per area of MECHANICS.md and BONUSES.md.
    internal static partial class Tests
    {
        private static int passed;
        private static int failed;

        public static int Run()
        {
            MeterRules();
            ThreadRules();
            HeightRules();
            FeedRules();
            GroundKill();
            AirKills();
            MultiKills();
            PogoChain();
            Clashes();
            Parries();
            Grazes();
            HeroDamage();
            Taunts();
            Healing();
            Streaks();
            PeakFalls();
            SnappedThread();
            Arenas();
            ToolKills();
            ToolUses();
            SkillUses();
            Elements();
            Environment();
            EnemyKinds();
            CrestBonuses();
            NeedleExtras();
            Combinations();
            HealthExtras();
            BossFights();
            HitTricks();
            KillTricks();
            WitchWhips();
            Defences();
            Bounces();
            ToolTricks();
            NamedEnemies();
            WorldTricks();
            BossTricks();
            KillStreaks();
            RunStreak();
            SilksongRules();
            FeedWeights();
            MenuSettings();
            Console.WriteLine($"{passed} passed, {failed} failed");
            return failed == 0 ? 0 : 1;
        }

        private static void MeterRules()
        {
            var m = new StyleMeter();
            m.Add(10);
            Check(m.Rank == 0 && Near(m.Fill, 60), "first points start the combo at a quarter of DREAMER");
            m.Add(150);
            Check(m.Rank == 1 && Near(m.Fill, 75), "ranking up keeps at least a quarter of the new meter");
            m.Add(300);
            Check(m.Rank == 2 && Near(m.Fill, 100), "a small overflow rises to the quarter floor");
            m.DropRank();
            Check(m.Rank == 1 && Near(m.Fill, 225), "a hit drops one rank to three quarters");
            m.Drain(100);
            Check(m.Rank == 0 && Near(m.Fill, 150), "an emptied meter ranks down to three quarters");
            m.DropRank();
            Check(!m.InCombo, "a hit at DREAMER ends the combo");
            var top = new StyleMeter();
            top.Add(100000);
            Check(top.Rank == Tuning.Silksong && Near(top.Fill, 1500), "SILKSONG caps the meter");
        }

        private static void ThreadRules()
        {
            var t = new ThreadSet { SourceCount = 7 };
            Check(Near(t.Multiplier("a"), 1.5f), "an unused thread is taut");
            for (var i = 0; i < 9; i++)
                t.Use("a", Tuning.WearNeedleHit, false);
            Check(Near(t.Multiplier("a"), 1f), "nine needle hits wear a thread to worn");
            t.Use("b", Tuning.WearNeedleHit, false);
            Check(t.Tension("a") > 7.5f, "using another source mends the first back to taut");
            for (var i = 0; i < 60; i++)
                t.Use("a", Tuning.WearNeedleHit, false);
            Check(t.Multiplier("a") == 0f, "spamming one source snaps its thread");
            var few = new ThreadSet { SourceCount = 4 };
            for (var i = 0; i < 60; i++)
                few.Use("a", Tuning.WearNeedleHit, false);
            Check(Near(few.Multiplier("a"), 0.5f), "with four sources a thread stops at frayed");
            var boss = new ThreadSet();
            boss.Use("a", 1f, true);
            Check(Near(boss.Tension("a"), 10f - 0.7f * 1.5f), "hits on a boss wear threads half again as fast");
        }

        private static void HeightRules()
        {
            var h = new HeightTracker();
            h.Observe(0f, false, false);
            Check(Near(h.Take(0f), 1f) && Near(h.Take(0.1f), 1.1f), "each action in the air grows height by 0.1");
            for (var i = 0; i < 20; i++)
                h.Take(0.2f);
            Check(Near(h.Multiplier, 2f), "height stops at x2");
            h.Observe(1f, true, false);
            Check(Near(h.Take(1.1f), 2f), "an action right after landing still counts as in the air");
            h.Observe(1.5f, true, false);
            Check(Near(h.Take(1.5f), 1f), "landing resets height after the grace");
            var wall = new HeightTracker();
            wall.Observe(0f, false, true);
            wall.Take(0f);
            Check(Near(wall.Multiplier, 1f), "on a wall height holds without growing");
        }

        private static void FeedRules()
        {
            var feed = new StyleFeed();
            feed.Push("CLASH", 50, 0f);
            feed.Push("CLASH", 50, 0.5f);
            Check(feed.Lines.Count == 1 && feed.Lines[0].Count == 2, "a repeat of the top line grows its counter");
            feed.Push("KILL", 30, 1f);
            feed.PushChain("POGO", 3, 45, 1.2f);
            feed.Push("KILL", 30, 1.3f);
            feed.PushChain("POGO", 4, 15, 1.4f);
            Check(feed.Lines.Count(l => l.Name == "POGO") == 1 && feed.Lines[0].Count == 4,
                "a chain keeps one line with its own count");
            feed.Expire(10f);
            Check(feed.Lines.Count == 0, "lines expire after three seconds");
        }

        private static void GroundKill()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnHit(Hit(0f, Enemy(1), AttackKind.NeedleSide, 30, 13));
            e.OnHit(Hit(0.5f, Enemy(1), AttackKind.NeedleSide, 13, -4, killed: true));
            Check(awards.Count(a => a.Name == "needle") == 2 && Near(awards[0].Total, 15f),
                "a ground needle hit is 10 x1.5 and silent");
            var kill = awards.FirstOrDefault(a => a.Name == "KILL");
            Check(Near(kill.Total, 45f), "a ground needle kill is KILL 30 x1.5");
            Check(e.Feed.Lines.Count == 1 && e.Feed.Lines[0].Name == "KILL", "only the kill reaches the feed");
            Check(Near(e.Meter.Fill, 125f), "the meter holds the quarter start plus every point");
            e.Tick(1.5f, 1f, true);
            Check(Near(e.Meter.Fill, 125f), "the meter waits 2 s after the last points before draining");
            e.Tick(3f, 1f, true);
            Check(Near(e.Meter.Fill, 125f - Tuning.Ranks[0].Drain), "then it drains at the rank's speed");
        }

        private static void AirKills()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Air(5f));
            e.OnHit(Hit(0f, Enemy(1), AttackKind.NeedleUp, 20, -5, killed: true));
            Check(Has(awards, "SHORYUKEN") && Has(awards, "AIRSHOT") && !Has(awards, "UPPERCUT"),
                "an up kill while rising is SHORYUKEN with AIRSHOT");
            Check(Near(awards.First(a => a.Name == "SHORYUKEN").Total, 120f),
                "the first action in the air has height x1");
            awards.Clear();
            e.Observe(0.2f, Air(-5f));
            e.OnHit(Hit(0.2f, Enemy(2), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(awards, "KILL") && Has(awards, "CRITICAL HIT"), "a side kill while falling is a CRITICAL HIT");
            Check(Near(awards.First(a => a.Name == "KILL").Height, 1.1f),
                "the second action in the air has height x1.1");
            awards.Clear();
            e.OnHit(Hit(0.4f, Enemy(3), AttackKind.NeedleDown, 20, -5, killed: true));
            Check(Has(awards, "PIN DROP") && !Has(awards, "AIRSHOT"), "a down kill is PIN DROP without AIRSHOT");
            Check(Has(awards, "SKY HIGH"), "a third kill in one air series gives SKY HIGH");
            awards.Clear();
            e.OnHit(Hit(0.5f, Enemy(4, maxHp: 60), AttackKind.NeedleSide, 60, -70, killed: true, crit: true));
            Check(Has(awards, "LUCKY STRIKE") && Has(awards, "CLEAN CUT") && Has(awards, "OVERSTITCHED"),
                "a crit one-shot is LUCKY STRIKE, CLEAN CUT and OVERSTITCHED");
            Check(!Has(awards, "SKY HIGH"), "SKY HIGH comes once per air series");
        }

        private static void MultiKills()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            for (var i = 0; i < 4; i++)
                e.OnHit(Hit(i * 0.2f, Enemy(10 + i), AttackKind.NeedleSide, 10, -5, killed: true));
            Check(Has(awards, "DUET") && Has(awards, "TRIO") && Has(awards, "CHOIR"),
                "kills 0.2 s apart give DUET, TRIO, CHOIR");
            Check(e.Feed.Lines.First(l => l.Name == "CHOIR").Count == 4, "CHOIR counts the kills");
            awards.Clear();
            e.OnHit(Hit(2f, Enemy(20), AttackKind.NeedleSide, 10, -5, killed: true));
            Check(!Has(awards, "DUET"), "a kill after the window starts over");
        }

        private static void PogoChain()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Air(-3f));
            for (var i = 0; i < 4; i++)
            {
                var t = i * 0.4f;
                if (i % 2 == 0)
                {
                    e.OnHit(Hit(t, Enemy(30 + i), AttackKind.NeedleDown, 100, 80));
                    e.OnPogoBounce(t);
                }
                else
                {
                    e.OnPogoBounce(t);
                    e.OnHit(Hit(t, Enemy(30 + i), AttackKind.NeedleDown, 100, 80));
                }
                if (i == 2)
                {
                    var pogo = awards.FirstOrDefault(a => a.Name == "POGO");
                    Check(Near(pogo.Total, 45f * 1.3f), "the third pogo pays POGO 45 at the current height");
                }
            }
            Check(e.Feed.Lines.First(l => l.Name == "POGO").Count == 4,
                "four pogos make POGO x4, whatever arrives first");
            Check(Has(awards, "STAIRWAY"), "pogos off three different enemies give STAIRWAY");
            e.Observe(2f, Ground());
            e.Observe(3f, Ground());
            e.Observe(3.1f, Air(-3f));
            awards.Clear();
            e.OnHit(Hit(3.2f, Enemy(40), AttackKind.NeedleDown, 100, 80));
            e.OnPogoBounce(3.2f);
            Check(!Has(awards, "POGO"), "landing ends the pogo chain");
        }

        private static void Clashes()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            var foe = Enemy(50);
            e.OnClash(0f, foe);
            e.OnClash(1f, foe);
            e.OnClash(2f, foe);
            Check(awards.Count(a => a.Name == "CLASH") == 3, "every clash is CLASH");
            Check(Near(awards.First(a => a.Name == "DEFLECT").Total, 150f),
                "the third clash within 3 s pays DEFLECT x3");
            e.OnClash(2.5f, foe);
            Check(e.Feed.Lines.First(l => l.Name == "DEFLECT").Count == 4, "the run continues as DEFLECT x4");
            e.OnHit(Hit(2.8f, foe, AttackKind.NeedleSide, 50, 30));
            Check(Has(awards, "COUNTERPOINT"), "hitting the clashed enemy within 0.5 s is COUNTERPOINT");
            e.OnHit(Hit(3.2f, foe, AttackKind.NeedleSide, 30, -5, killed: true));
            Check(Has(awards, "RIPOSTE"), "a kill within 1 s of a clash is RIPOSTE");
        }

        private static void Parries()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnParry(0f, Enemy(80));
            Check(Has(awards, "PARRY") && !Has(awards, "NEEDLE AND THREAD"), "a Cross Stitch parry is PARRY");
            e.OnHit(Hit(0.4f, Enemy(80), AttackKind.Skill, 30, -5, killed: true, tool: Skills.CrossStitch));
            Check(Has(awards, "CROSS COUNTER") && Has(awards, "RIPOSTE"),
                "the counter killing is CROSS COUNTER and RIPOSTE");
            awards.Clear();
            e.OnParry(5f, Enemy(81, boss: true));
            var parry = awards.FirstOrDefault(a => a.Name == "NEEDLE AND THREAD");
            Check(Near(parry.Total, 500f) && !Has(awards, "PARRY"), "parrying a boss is NEEDLE AND THREAD x2");
            e.OnEvade(6f, Enemy(82));
            Check(Has(awards, "WITCH TIME"), "evading through an attack is WITCH TIME");
        }

        private static void HeroDamage()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.Meter.Add(750);
            Check(e.Meter.Rank == 2 && Near(e.Meter.Fill, 300f), "750 points from nothing reach BELLSONG at 300");
            e.OnHeroDamaged(1f, soft: false);
            Check(e.Meter.Rank == 1 && Near(e.Meter.Fill, 225f),
                "a hit on Hornet costs a whole rank, to three quarters");
            e.OnHeroDamaged(1.5f, soft: true);
            Check(e.Meter.Rank == 1 && Near(e.Meter.Fill, 175f), "soft damage takes 50 points, not a rank");
            Check(!Has(awards, "GIT GUD"), "soft damage does not count towards GIT GUD");
            e.OnHeroDamaged(2f, soft: false);
            e.OnHeroDamaged(2.5f, soft: false);
            Check(!e.Meter.InCombo && Has(awards, "GIT GUD"), "three hits in 5 s end the combo with GIT GUD");
            var penalty = Tuning.HitPenalty;
            try
            {
                Tuning.HitPenalty = 0.5f;
                var half = NewEngine(out _);
                half.Observe(0f, Ground());
                half.Meter.Add(750);
                half.OnHeroDamaged(1f, soft: false);
                Check(half.Meter.Rank == 2 && Near(half.Meter.Fill, 100f),
                    "with HitPenalty 0.5 a hit takes half the meter and the rank holds");
            }
            finally
            {
                Tuning.HitPenalty = penalty;
            }
        }

        private static void Grazes()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Air(-2f));
            e.OnGraze(0f, Enemy(90));
            e.Tick(1f, 1f, false);
            Check(!Has(awards, "GRAZE"), "a near miss outside a combo gives nothing");
            e.Meter.Add(10);
            e.OnGraze(1f, Enemy(90));
            e.Tick(1.3f, 0.3f, false);
            Check(!Has(awards, "GRAZE"), "a near miss pays only once it is clear that no hit follows");
            e.OnGraze(1.5f, Enemy(90));
            e.Tick(1.7f, 0.4f, false);
            Check(awards.Count(a => a.Name == "GRAZE") == 1, "one GRAZE per 1.5 s");
            e.OnGraze(4f, Enemy(91, boss: true));
            e.Tick(4.7f, 1f, false);
            var graze = awards.Last(a => a.Name == "GRAZE");
            Check(Near(graze.Boss, 2f) && Near(graze.Height, 1.1f),
                "a GRAZE in the air off a boss takes height and x2");
            awards.Clear();
            e.Meter.Add(2000);
            e.OnGraze(7f, Enemy(90));
            e.OnHeroDamaged(7.03f, soft: false);
            e.Tick(8f, 1f, false);
            Check(e.Meter.InCombo && !Has(awards, "GRAZE"), "a near miss with a hit right after it is no GRAZE");
        }

        private static void Taunts()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnTaunt(0f);
            Check(!Has(awards, "TAUNT"), "a taunt outside a combo gives nothing");
            e.Meter.Add(10);
            e.OnTaunt(1f);
            e.OnTaunt(3f);
            e.OnTaunt(6.5f);
            Check(awards.Count(a => a.Name == "TAUNT") == 2, "TAUNT at most once per 5 s");
            e.OnHit(Hit(7f, Enemy(60), AttackKind.NeedleSide, 10, -16, killed: true, taunted: true));
            Check(Has(awards, "INSULT TO INJURY"), "a taunt-boosted kill is INSULT TO INJURY");
        }

        private static void Healing()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Air(1f, health: 1));
            e.Meter.Add(10);
            e.OnBindStarted(0.1f);
            e.OnBindCompleted(1f);
            Check(Has(awards, "MIDAIR MEND") && Has(awards, "STITCH IN TIME"),
                "a bind in the air at one mask is MIDAIR MEND and STITCH IN TIME");
            var silk = Ground();
            silk.Silk = 8;
            e.Observe(2f, silk);
            silk.Silk = 9;
            e.Observe(2.1f, silk);
            Check(Has(awards, "WOUND TIGHT"), "a full spool during a combo is WOUND TIGHT");
        }

        private static void Streaks()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.Meter.Add(20000);
            e.Tick(0.1f, 0f, false);
            Check(Has(awards, "ZERO TO HERO"), "reaching SUBLIME within 15 s is ZERO TO HERO");
            Check(Has(awards, "NO STRINGS ATTACHED"), "reaching SILKSONG without binding is NO STRINGS ATTACHED");
            // 400 points a second outpace SILKSONG's 300 a second of drain.
            for (var t = 0.5f; t <= 31f; t += 0.5f)
            {
                e.Meter.Add(200);
                e.Tick(t, 0.5f, true);
            }
            Check(!Has(awards, "UNTOUCHABLE"), "30 s of combo without fighting is not UNTOUCHABLE");
            var notes = awards.Where(a => a.Name == "HOLDING THE NOTE").ToList();
            Check(notes.Count == 3 && notes.All(a => a.Total == 0f),
                "every 10 s at SILKSONG is HOLDING THE NOTE, a line without points");
            for (var t = 31.5f; t <= 62f; t += 0.5f)
            {
                e.OnHit(Hit(t, Enemy(100), AttackKind.NeedleSide, 1000, 990));
                e.Tick(t, 0.5f, true);
            }
            Check(Has(awards, "UNTOUCHABLE"), "30 s of fighting without damage is UNTOUCHABLE");
        }

        private static void PeakFalls()
        {
            var e = NewEngine(out _);
            e.Observe(0f, Ground());
            e.Meter.Add(20000);
            for (var t = 0.5f; t <= 20f; t += 0.5f)
                e.Tick(t, 0.5f, true);
            Check(e.Meter.Rank < Tuning.Silksong, "a full SILKSONG falls within 20 s of doing nothing");
        }

        private static void SnappedThread()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            for (var i = 0; i < 39; i++)
                e.OnHit(Hit(i * 0.3f, Enemy(70), AttackKind.NeedleSide, 1000, 990));
            Check(awards.Last().Thread == 0f && e.Threads.Multiplier("needle.ground") == 0f,
                "39 ground hits in a row snap the needle thread");
            awards.Clear();
            e.Observe(13f, Air(-1f));
            e.OnHit(Hit(13f, Enemy(70), AttackKind.NeedleSide, 990, 980));
            Check(Near(awards.Last(a => a.Name == "needle in air").Thread, 1.5f),
                "the needle in the air has its own taut thread");
            Check(Has(awards, "NEEDLEWORK"), "the fortieth needle hit in a row is NEEDLEWORK x4");
        }

        private static void Arenas()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnArenaStarted(0f);
            e.OnArenaCleared(5f);
            Check(Has(awards, "CURTAIN CALL") && Has(awards, "PERFECT"),
                "an arena cleared without damage is CURTAIN CALL and PERFECT");
            awards.Clear();
            e.OnArenaStarted(10f);
            e.OnHeroDamaged(11f, soft: false);
            e.OnArenaCleared(15f);
            Check(Has(awards, "CURTAIN CALL") && !Has(awards, "PERFECT"), "damage in the arena loses PERFECT");
        }

        private static StyleEngine NewEngine(out List<Award> awards)
        {
            var e = new StyleEngine { Random = () => 1.0 };
            var list = new List<Award>();
            e.Awarded += list.Add;
            awards = list;
            return e;
        }

        private static HeroState Ground() => new HeroState { OnGround = true, Health = 5, MaxHealth = 5, SilkMax = 9 };

        private static HeroState Air(float velocityY, int health = 5) =>
            new HeroState { VelocityY = velocityY, Health = health, MaxHealth = 5, SilkMax = 9 };

        private static EnemyInfo Enemy(int id, int maxHp = 30, bool boss = false) =>
            new EnemyInfo { Id = id, Name = "enemy " + id, MaxHp = maxHp, IsBoss = boss };

        private static HitEvent Hit(float t, EnemyInfo enemy, AttackKind kind, int before, int after,
            bool killed = false, bool crit = false, bool taunted = false, string tool = null) => new HitEvent
            {
                Time = t,
                Enemy = enemy,
                Kind = kind,
                Tool = tool,
                SwingId = 0,
                HpBefore = before,
                HpAfter = after,
                Killed = killed,
                Crit = crit,
                Taunted = taunted,
            };

        private static bool Has(List<Award> awards, string name) => awards.Any(a => a.Name == name);

        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.01f;

        private static void Check(bool ok, string what)
        {
            if (ok)
            {
                passed++;
                return;
            }
            failed++;
            Console.WriteLine("FAIL " + what);
        }
    }
}
