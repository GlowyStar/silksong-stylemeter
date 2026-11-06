using System.Collections.Generic;
using System.Linq;
using Stylemeter.Core;

namespace Stylemeter.Sim
{
    // Checks for kill streaks, running, SILKSONG's melt and silk, and the weight that colours a feed line.
    internal static partial class Tests
    {
        private static void KillStreaks()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            float[] times = { 0f, 3f, 6f, 9f, 12f, 13f };
            for (var i = 0; i < times.Length; i++)
                e.OnHit(Hit(times[i], Enemy(100 + i), AttackKind.NeedleSide, 30, -5, killed: true));
            Check(Count(awards, "DOUBLE KILL") == 1 && Count(awards, "TRIPLE KILL") == 1
                && Count(awards, "ULTRAKILL") == 1 && Count(awards, "RAMPAGE") == 1,
                "kills 3 s apart climb DOUBLE KILL, TRIPLE KILL, ULTRAKILL and RAMPAGE once each");
            awards.Clear();
            e.OnHit(Hit(40f, Enemy(120), AttackKind.NeedleSide, 30, -5, killed: true));
            e.OnHit(Hit(42f, Enemy(121), AttackKind.NeedleSide, 30, -5, killed: true));
            Check(Has(awards, "DOUBLE KILL"), "after a lone kill the streak starts again");

            var f = NewEngine(out var gaps);
            f.Observe(0f, Ground());
            foreach (var t in new[] { 0f, 5f, 7f })
                f.OnHit(Hit(t, Enemy(130 + (int)t), AttackKind.NeedleSide, 30, -5, killed: true));
            Check(Has(gaps, "TRIPLE KILL") && !Has(gaps, "DOUBLE KILL"),
                "three kills in 7 s with a 5 s gap are TRIPLE KILL at once");
            Check(Bonuses.Rampage.Weight == 3, "RAMPAGE is red in the feed");
        }

        private static void RunStreak()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnHit(Hit(0f, Enemy(140), AttackKind.NeedleSide, 30, -5, killed: true));
            for (var i = 1; i <= 103; i++)
                e.Observe(i * 0.1f, Running(8f));
            Check(Count(awards, "RUN, FORREST, RUN!") == 1, "10 s of running one way in a combo is RUN, FORREST, RUN!");

            var f = NewEngine(out var stops);
            f.Observe(0f, Ground());
            f.OnHit(Hit(0f, Enemy(141), AttackKind.NeedleSide, 30, -5, killed: true));
            for (var i = 1; i <= 103; i++)
                f.Observe(i * 0.1f, Running(i >= 50 && i < 56 ? 0f : -8f));
            Check(!Has(stops, "RUN, FORREST, RUN!"), "a half-second stop starts the run again");

            var turns = NewEngine(out var turned);
            turns.Observe(0f, Ground());
            turns.OnHit(Hit(0f, Enemy(142), AttackKind.NeedleSide, 30, -5, killed: true));
            for (var i = 1; i <= 103; i++)
                turns.Observe(i * 0.1f, Running(i % 20 < 10 ? 8f : -8f));
            Check(!Has(turned, "RUN, FORREST, RUN!"), "running back and forth is not one run");

            var jumps = NewEngine(out var jumped);
            jumps.Observe(0f, Ground());
            jumps.OnHit(Hit(0f, Enemy(143), AttackKind.NeedleSide, 30, -5, killed: true));
            for (var i = 1; i <= 103; i++)
            {
                var state = Running(8f);
                state.OnGround = i < 40 || i > 48;
                jumps.Observe(i * 0.1f, state);
            }
            Check(!Has(jumped, "RUN, FORREST, RUN!"), "a jump in the middle starts the run again");

            var g = NewEngine(out var calm);
            for (var i = 0; i <= 103; i++)
                g.Observe(i * 0.1f, Running(8f));
            Check(!Has(calm, "RUN, FORREST, RUN!"), "running outside a combo pays nothing");
        }

        private static void SilksongRules()
        {
            var e = NewEngine(out _);
            var silk = 0;
            e.SilkEarned += () => silk++;
            e.Observe(0f, Ground());
            e.ForceMeter(Tuning.Silksong, 1f);
            e.Tick(0.5f, 0.5f, true);
            Check(e.Meter.Fill < Tuning.Ranks[Tuning.Silksong].Size, "SILKSONG drains at once, without the 2 s delay");
            for (var t = 1f; t <= 5.5f; t += 0.5f)
                e.Tick(t, 0.5f, true);
            Check(e.Meter.Rank < Tuning.Silksong, "a full SILKSONG melts in 5 s");
            Check(silk == 0, "a SILKSONG lost within 5 s gives no silk");

            var f = NewEngine(out var awards);
            var held = 0;
            f.SilkEarned += () => held++;
            f.Observe(0f, Ground());
            for (var t = 0.5f; t <= 10f; t += 0.5f)
            {
                f.ForceMeter(Tuning.Silksong, 1f);
                f.Tick(t, 0.5f, true);
            }
            Check(held == 2 && Count(awards, "SILK") == 2,
                "SILKSONG held for 10 s gives two silk, each with a SILK line");
            f.ForceMeter(Tuning.Silksong - 1, 0.5f);
            f.Tick(10.5f, 0.5f, true);
            f.ForceMeter(Tuning.Silksong, 1f);
            for (var t = 11f; t <= 14f; t += 0.5f)
            {
                f.ForceMeter(Tuning.Silksong, 1f);
                f.Tick(t, 0.5f, true);
            }
            Check(held == 2, "dropping below SILKSONG starts the 5 s again");
        }

        private static void FeedWeights()
        {
            Check(Bonuses.DoubleKill.Weight == 0 && Bonuses.TripleKill.Weight == 1 && Bonuses.UltraKill.Weight == 2
                && Bonuses.Rampage.Weight == 3 && Bonuses.SilkReward.Weight == 4,
                "weights follow base points; the silk line has its own");
            var feed = new StyleFeed();
            feed.Push("KILL", 30, 0f);
            feed.Push("KILL", 30, 0.5f, 3);
            Check(feed.Lines.Count == 1 && feed.Lines[0].Weight == 3, "a line keeps the heaviest weight it was given");
        }

        private static HeroState Running(float velocityX) =>
            new HeroState { OnGround = true, VelocityX = velocityX, Health = 5, MaxHealth = 5, SilkMax = 9 };

        private static int Count(List<Award> awards, string name) => awards.Count(a => a.Name == name);
    }
}
