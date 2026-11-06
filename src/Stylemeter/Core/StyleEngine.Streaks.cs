namespace Stylemeter.Core
{
    // Rank milestones, streaks and arenas: BONUSES.md sections 9 (CLUTCH) and 14. These go as they are (flat).
    internal sealed partial class StyleEngine
    {
        private float comboStart = Never;
        private float untouchableSince = Never;
        private int untouchableCount;
        private int untouchableActions;
        private float silksongSince = Never;
        private int bindsThisCombo;
        private bool hadOneMask;
        private bool zeroToHero;
        private bool clutch;
        private bool noStrings;
        private bool againstAllOdds;
        private bool glassCannon;
        private bool arenaActive;
        private bool arenaDamaged;

        public void OnArenaStarted(float now)
        {
            Now = now;
            arenaActive = true;
            arenaDamaged = false;
        }

        public void OnArenaCleared(float now)
        {
            Now = now;
            if (!arenaActive)
                return;
            arenaActive = false;
            GiveFlat(Bonuses.CurtainCall);
            if (!arenaDamaged)
                GiveFlat(Bonuses.Perfect);
        }

        private void OnRankChanged(int from, int to)
        {
            if (from < 0)
            {
                comboStart = Now;
                untouchableSince = Now;
                untouchableCount = 0;
                untouchableActions = 0;
                bindsThisCombo = 0;
                hadOneMask = Hero.Health == 1;
                zeroToHero = clutch = noStrings = againstAllOdds = glassCannon = false;
                spoolFull = Hero.SilkMax > 0 && Hero.Silk >= Hero.SilkMax;
                noCostGiven = false;
                spoolDrainedAt = Never;
                ResetComboTools();
            }
            if (to < 0)
            {
                comboEndedAt = Now;
                silksongSince = Never;
                return;
            }
            if (to < Tuning.Silksong)
                silksongSince = Never;
            else if (from < to)
                silksongSince = Now;
            if (to <= from)
                return;
            if (to == Tuning.Sublime)
            {
                if (!zeroToHero && Now - comboStart <= Tuning.ZeroToHeroTime)
                {
                    zeroToHero = true;
                    Queue(Bonuses.ZeroToHero);
                }
                if (!clutch && hadOneMask)
                {
                    clutch = true;
                    Queue(Bonuses.Clutch);
                }
                if (!againstAllOdds && Hero.Crest == Crests.Cursed)
                {
                    againstAllOdds = true;
                    Queue(Bonuses.AgainstAllOdds);
                }
                if (!glassCannon && Hero.BarbedBracelet)
                {
                    glassCannon = true;
                    Queue(Bonuses.GlassCannon);
                }
            }
            if (to == Tuning.Silksong && !noStrings && bindsThisCombo == 0)
            {
                noStrings = true;
                Queue(Bonuses.NoStringsAttached);
            }
        }

        private void TickStreaks()
        {
            // UNTOUCHABLE pays for fighting without damage, not for walking about while the combo lasts.
            if (Now - untouchableSince >= Tuning.UntouchablePeriod)
            {
                untouchableSince = Now;
                if (untouchableActions >= Tuning.UntouchableMinActions)
                {
                    untouchableCount++;
                    GiveFlat(Bonuses.Untouchable, untouchableCount);
                }
                untouchableActions = 0;
            }
            if (silksongSince != Never && Now - silksongSince >= Tuning.HoldingTheNotePeriod)
            {
                silksongSince = Now;
                GiveFlat(Bonuses.HoldingTheNote);
            }
            TickAir();
        }
    }
}
