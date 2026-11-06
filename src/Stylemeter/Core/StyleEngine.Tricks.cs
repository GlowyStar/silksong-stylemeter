namespace Stylemeter.Core
{
    // Hornet's tricks (BONUSES.md sections 1, 2, 4, 5 and 7): what one hit did beyond its damage, and what her
    // defensive tools and binds saved.
    internal sealed partial class StyleEngine
    {
        private float lastWanderlust = Never;
        private float lastThreadTheNeedle = Never;
        private bool bellPaid;
        private float firstBindDoneAt = Never;
        private float lastReturn = Never;

        // An enemy shot Hornet batted back hurt an enemy; on Widow it is her own bell (BELL RINGER).
        public void OnReturnToSender(float now, EnemyInfo enemy)
        {
            Now = now;
            if (enemy == null || !Meter.InCombo || now - lastReturn < Tuning.ReturnToSenderGap)
                return;
            lastReturn = now;
            var s = Begin(null, enemy.IsBoss);
            Give(Bonuses.ReturnToSender, s);
            if (enemy.BossKey == BossBonuses.Widow)
                Give(Bonuses.BellRinger, s);
        }

        // Read off one hit: through a guard, around a shield, the Wanderer's dash finisher, a dart through an attack.
        private void TrickHit(HitEvent hit, Scoring s, EnemyRecord record)
        {
            if (hit.PiercedGuard && hit.RageHit)
                Give(Bonuses.Unstoppable, s);
            if (hit.PiercedGuard && hit.Kind == AttackKind.Tool && hit.Tool == Tools.Longpin)
                Give(Bonuses.ArmorPiercer, s);
            if (hit.Flanked && !record.Flanked)
            {
                record.Flanked = true;
                Give(Bonuses.Flanked, s);
            }
            if (hit.DashFinisher && Hero.Crest == Crests.Wanderer && Now - lastWanderlust > Tuning.MoveGap)
            {
                lastWanderlust = Now;
                Give(Bonuses.Wanderlust, s);
            }
            if (hit.DodgedThrough && hit.Kind == AttackKind.Skill && hit.Tool == Skills.Sharpdart
                && Now - lastThreadTheNeedle > Tuning.MoveGap)
            {
                lastThreadTheNeedle = Now;
                Give(Bonuses.ThreadTheNeedle, s);
            }
        }

        // Read off the killing hit: from a wall, after a long charge, into the back.
        private void TrickKill(HitEvent hit, Scoring s)
        {
            if (hit.WallSlash && Hero.Crest == Crests.Architect)
                Give(Bonuses.Blueprint, s);
            if (hit.Kind == AttackKind.NeedleCharge && hit.ChargeHeld >= Tuning.PatienceHold)
                Give(Bonuses.Patience, s);
            if (hit.Backstab)
                Give(Bonuses.Backstitch, s);
            if (hit.Stunned && hit.Kind == AttackKind.NeedleDown)
                Give(Bonuses.GloryKill, s);
        }

        // The Witch's down whip: three in one air series is CAT'S CRADLE.
        private void WitchDownHit()
        {
            if (Hero.Crest != Crests.Witch || air == null || air.CatsCradle
                || Now - air.LastWitchDown < Tuning.WitchDownGap)
                return;
            air.LastWitchDown = Now;
            if (++air.WitchDownHits < Tuning.CatsCradleHits || !Meter.InCombo)
                return;
            air.CatsCradle = true;
            Give(Bonuses.CatsCradle, Peek(null, lastDownHitEnemy != null && lastDownHitEnemy.IsBoss));
        }

        // Magnetite Dice cancelled a hit on Hornet.
        public void OnDiceSaved(float now)
        {
            Now = now;
            if (Meter.InCombo)
                Give(Bonuses.LoadedDice, Begin(null, false));
        }

        // Fractured Mask left Hornet one mask from a hit that would have killed her. A lethal hit is always a fight.
        public void OnMaskSaved(float now)
        {
            Now = now;
            Give(Bonuses.DeathDefiance, Begin(null, false));
        }

        // Warding Bell took a hit during a bind: once per bind.
        public void OnBellBlocked(float now, EnemyInfo enemy)
        {
            Now = now;
            if (bellPaid || !Meter.InCombo)
                return;
            bellPaid = true;
            Give(Bonuses.SavedByTheBell, Begin(null, enemy != null && enemy.IsBoss));
        }

        public void OnReserveBind(float now)
        {
            Now = now;
            if (Meter.InCombo)
                Give(Bonuses.ReserveTank, Begin(null, false));
        }

        // The Architect's quick craft put charges back into her tools.
        public void OnRestock(float now)
        {
            Now = now;
            if (Meter.InCombo)
                Give(Bonuses.Restock, Begin(null, false));
        }

        // Snitch Pick: Clawline knocked rosaries out of an enemy.
        public void OnPickpocket(float now, EnemyInfo enemy)
        {
            Now = now;
            if (Meter.InCombo)
                Give(Bonuses.Pickpocket, Begin(null, enemy != null && enemy.IsBoss));
        }

        // A down strike bounced Hornet off spikes (BED OF NAILS).
        public void OnSpikePogo(float now)
        {
            Now = now;
            if (!Meter.InCombo || air == null || air.SpikePogo)
                return;
            air.SpikePogo = true;
            Give(Bonuses.BedOfNails, Begin(null, false));
        }

        // The Shaman's bind slam fell from high up and landed next to an enemy.
        public void OnDesolateDive(float now)
        {
            Now = now;
            if (Meter.InCombo)
                Give(Bonuses.DesolateDive, Begin(null, false));
        }

        // Multibinder finishes one bind twice; the second finish within the window is DOUBLE KNOT.
        private void SecondBindStage()
        {
            if (Now - firstBindDoneAt > Tuning.DoubleKnotWindow)
                return;
            firstBindDoneAt = Never;
            if (Meter.InCombo)
                Give(Bonuses.DoubleKnot, Begin(null, false));
        }
    }
}
