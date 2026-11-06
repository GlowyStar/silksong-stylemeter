using System;
using System.Collections.Generic;

namespace Stylemeter.Core
{
    internal struct Award
    {
        public float Time;
        public string Name;
        public bool Silent;
        public string Source;
        public float Base;
        public float Thread;
        public float Height;
        public float Boss;
        public float Total;
    }

    // Turns game events into style points (MECHANICS.md). Pure C#: the game side feeds it Hornet's state every frame
    // and events as they happen, then reads the meter, threads, height and feed.
    internal sealed partial class StyleEngine
    {
        private const float Never = float.NegativeInfinity;

        private struct Scoring
        {
            public string Source;
            public float Thread;
            public float Height;
            public float Boss;
        }

        private static readonly Scoring FlatScoring = new Scoring { Thread = 1f, Height = 1f, Boss = 1f };

        private readonly Queue<BonusDef> pendingFlat = new Queue<BonusDef>();
        private bool flushing;
        private float comboEndedAt = Never;
        private float lastPointsAt = Never;

        public readonly StyleMeter Meter = new StyleMeter();
        public readonly ThreadSet Threads = new ThreadSet();
        public readonly HeightTracker Height = new HeightTracker();
        public readonly StyleFeed Feed = new StyleFeed();

        public Func<double> Random = new Random().NextDouble;
        public event Action<Award> Awarded;

        public float Now { get; private set; }
        public HeroState Hero { get; private set; }

        // The player's choices from the mod menu (bonuses switched off, the points multiplier).
        public StyleSettings Settings { get; } = new StyleSettings();

        public StyleEngine()
        {
            Meter.RankChanged += OnRankChanged;
        }

        public void Tick(float now, float dt, bool drainActive)
        {
            Now = now;
            if (drainActive && Meter.InCombo)
            {
                // SILKSONG drains at once: it stays only while Hornet keeps scoring.
                if (now - lastPointsAt >= Tuning.DrainDelay || Meter.Rank == Tuning.Silksong)
                    Meter.Drain(dt);
                Threads.Recover(dt);
                TickStreaks();
                TickSilkReward(dt);
            }
            if (!Meter.InCombo && now - comboEndedAt >= Tuning.ThreadResetDelay)
                Threads.Reset();
            TickGraze();
            TickBoss();
            Feed.Expire(now);
            Flush();
        }

        public void Observe(float now, HeroState hero)
        {
            Now = now;
            var was = Hero;
            Hero = hero;
            Height.Observe(now, hero.OnGround, hero.OnWall);
            ObserveRun();
            ObserveAir();
            ObserveHealth(was);
            ObserveCrest(was);
            Flush();
        }

        public void OnSceneChanged()
        {
            BossSceneChanged();
            ClearEnemyState();
            enemies.Clear();
            swings.Clear();
            uses.Clear();
            arenaActive = false;
        }

        // Scoring context of one action: thread of its source, height (grown by an action in the air) and boss.
        private Scoring Begin(string source, bool vsBoss)
        {
            untouchableActions++;
            if (air != null && Height.Airborne(Now))
                air.Actions++;
            return new Scoring
            {
                Source = source,
                Thread = source == null ? 1f : Threads.Multiplier(source),
                Height = Height.Take(Now),
                Boss = vsBoss ? Tuning.BossMultiplier : 1f,
            };
        }

        // Scoring context of a bonus that follows an action already counted (chains, late kills).
        private Scoring Peek(string source, bool vsBoss) => new Scoring
        {
            Source = source,
            Thread = source == null ? 1f : Threads.Multiplier(source),
            Height = Height.Current(Now),
            Boss = vsBoss ? Tuning.BossMultiplier : 1f,
        };

        private void Give(BonusDef bonus, Scoring s, float? points = null, int chainCount = 0)
        {
            // Switched off in the mod menu: no points, no line, as if it never happened.
            if (!Settings.IsEnabled(bonus.Name))
                return;
            var thread = bonus.Scaling == Scaling.Action ? s.Thread : 1f;
            var height = bonus.Scaling == Scaling.Flat ? 1f : s.Height;
            var boss = bonus.Scaling == Scaling.Flat ? 1f : s.Boss;
            var basePoints = (points ?? bonus.Points) * (bonus.Silent ? Tuning.SilentScale : 1f);
            var total = basePoints * thread * height * boss * Settings.PointsMultiplier;
            Meter.Add(total);
            if (total > 0f)
                lastPointsAt = Now;
            if (!bonus.Silent)
            {
                if (chainCount > 0)
                    Feed.PushChain(bonus.Name, chainCount, total, Now, bonus.Weight);
                else
                    Feed.Push(bonus.Name, total, Now, bonus.Weight);
            }
            Awarded?.Invoke(new Award
            {
                Time = Now,
                Name = bonus.Name,
                Silent = bonus.Silent,
                Source = s.Source,
                Base = basePoints,
                Thread = thread,
                Height = height,
                Boss = boss,
                Total = total,
            });
            Flush();
        }

        private void GiveFlat(BonusDef bonus, int chainCount = 0) => Give(bonus, FlatScoring, chainCount: chainCount);

        // Bonuses raised while the meter changes rank wait here, so they never re-enter it mid-change.
        private void Queue(BonusDef bonus) => pendingFlat.Enqueue(bonus);

        private void Flush()
        {
            if (flushing)
                return;
            flushing = true;
            try
            {
                while (pendingFlat.Count > 0)
                    GiveFlat(pendingFlat.Dequeue());
            }
            finally
            {
                flushing = false;
            }
        }

        private void Wear(Scoring s, float wear)
        {
            if (s.Source != null && wear > 0f)
                Threads.Use(s.Source, wear, s.Boss > 1f);
        }
    }
}
