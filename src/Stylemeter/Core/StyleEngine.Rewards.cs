using System;
using System.Collections.Generic;

namespace Stylemeter.Core
{
    // Kill streaks over seconds (DOUBLE KILL to RAMPAGE), running without stopping (RUN, FORREST, RUN!) and silk for
    // holding SILKSONG.
    internal sealed partial class StyleEngine
    {
        // Raised for every 5 s of SILKSONG held; the game gives Hornet one silk.
        public event Action SilkEarned;

        private readonly List<float> killTimes = new List<float>();
        private int streakPaid;
        private float runSince = Never, runSeen = Never;
        private int runDirection;
        private float silksongHeld;

        // The highest streak that holds (2 kills within 4 s, 3 within 8 s, 4 within 15 s, 5 within 20 s) pays once;
        // a lone kill starts the count again.
        private void KillStreak()
        {
            var windows = Tuning.StreakWindows;
            killTimes.Add(Now);
            killTimes.RemoveAll(t => Now - t > windows[windows.Length - 1]);
            var tier = 1;
            for (var i = 0; i < windows.Length; i++)
            {
                var kills = 0;
                foreach (var t in killTimes)
                {
                    if (Now - t <= windows[i])
                        kills++;
                }
                if (kills >= i + 2)
                    tier = i + 2;
            }
            if (tier == 1)
            {
                streakPaid = 1;
                return;
            }
            if (tier <= streakPaid)
                return;
            streakPaid = tier;
            GiveFlat(tier == 2 ? Bonuses.DoubleKill
                : tier == 3 ? Bonuses.TripleKill
                : tier == 4 ? Bonuses.UltraKill
                : Bonuses.Rampage);
        }

        // Running on the ground one way for 10 s: a turn starts over at once, a jump or a stop longer than RunGap too.
        private void ObserveRun()
        {
            var isRunning = Hero.OnGround && Math.Abs(Hero.VelocityX) >= Tuning.RunMinSpeed;
            if (!isRunning)
            {
                if (runSince != Never && Now - runSeen > Tuning.RunGap)
                    runSince = Never;
                return;
            }
            var direction = Math.Sign(Hero.VelocityX);
            if (runSince == Never || direction != runDirection)
            {
                runSince = Now;
                runDirection = direction;
            }
            runSeen = Now;
            if (Now - runSince >= Tuning.RunTime && Meter.InCombo)
            {
                runSince = Now;
                GiveFlat(Bonuses.RunForrestRun);
            }
        }

        // Every 5 s of SILKSONG without dropping below it: one silk and a feed line.
        private void TickSilkReward(float dt)
        {
            // An empty SILKSONG is about to fall: its last instant does not count.
            if (Meter.Rank != Tuning.Silksong || Meter.Fill <= 0f)
            {
                silksongHeld = 0f;
                return;
            }
            silksongHeld += dt;
            if (silksongHeld < Tuning.SilkRewardPeriod)
                return;
            silksongHeld -= Tuning.SilkRewardPeriod;
            SilkEarned?.Invoke();
            GiveFlat(Bonuses.SilkReward);
        }
    }
}
