using System.Collections.Generic;
using System.Linq;

namespace Stylemeter.Core
{
    // Clashes, taunts, binds and damage: BONUSES.md sections 1 (taunt), 3 (clash) and 9 (healing), MECHANICS.md §5.
    internal sealed partial class StyleEngine
    {
        private readonly List<float> clashes = new List<float>();
        private readonly List<float> hurts = new List<float>();
        private float lastClash = Never;
        private int lastClashEnemy;
        private bool counterpointGiven = true;
        private bool deflecting;
        private int deflectCount;
        private float lastTauntBonus = Never;
        private float lastGraze = Never;
        private float pendingGraze = Never;
        private bool pendingGrazeBoss;
        private float lastGlideEnd = Never;
        private bool gliding;
        private int bindStartHealth = -1;
        private bool bindOpen;
        private bool spoolFull;
        private bool noCostGiven;
        private float spoolDrainedAt = Never;

        public void OnClash(float now, EnemyInfo enemy, bool parryCounter = false)
        {
            Now = now;
            var boss = enemy != null && enemy.IsBoss;
            var s = Begin(null, boss);
            Give(Bonuses.Clash, s);
            ParryThis(enemy, parryCounter, s);
            lastClash = now;
            lastClashEnemy = enemy?.Id ?? 0;
            counterpointGiven = enemy == null;
            BossClash(enemy);
            Medley(boss);
            clashes.RemoveAll(t => now - t > Tuning.DeflectWindow);
            clashes.Add(now);
            if (clashes.Count < 3)
            {
                deflecting = false;
                return;
            }
            // DEFLECT xN: the third clash within 3 s pays for all three, every next one in the run adds one more.
            deflectCount = deflecting ? deflectCount + 1 : clashes.Count;
            Give(Bonuses.Deflect, s, Bonuses.Deflect.Points * (deflecting ? 1 : deflectCount), deflectCount);
            deflecting = true;
        }

        // Cross Stitch caught an attack: PARRY, or NEEDLE AND THREAD against a boss. A kill right after is a RIPOSTE.
        public void OnParry(float now, EnemyInfo enemy)
        {
            Now = now;
            var boss = enemy != null && enemy.IsBoss;
            Give(boss ? Bonuses.NeedleAndThread : Bonuses.Parry, Begin(null, boss));
            lastClash = now;
            Medley(boss);
        }

        public void OnGlideStarted(float now)
        {
            Now = now;
            gliding = true;
        }

        // The glide (the game's brolly) ended; an attack is one of the things that ends it (DRIFTER). The game sends
        // BROLLY END on every tool throw and scene change too, so only an end after a start counts.
        public void OnGlideEnded(float now)
        {
            Now = now;
            if (!gliding)
                return;
            gliding = false;
            lastGlideEnd = now;
        }

        // An enemy attack passed right by Hornet in a jump or a dash without touching her: only in a fight, and at most
        // once per cooldown so several hitboxes of one swing pay once. It pays after GrazeConfirm, if no hit came.
        public void OnGraze(float now, EnemyInfo enemy)
        {
            Now = now;
            if (!Meter.InCombo || pendingGraze != Never || now - lastGraze < Tuning.GrazeCooldown)
                return;
            pendingGraze = now;
            pendingGrazeBoss = enemy != null && enemy.IsBoss;
        }

        private void TickGraze()
        {
            if (pendingGraze == Never || Now - pendingGraze < Tuning.GrazeConfirm)
                return;
            pendingGraze = Never;
            if (!Meter.InCombo)
                return;
            lastGraze = Now;
            Give(Bonuses.Graze, Begin(null, pendingGrazeBoss));
        }

        // Scuttlebrace's evade carried Hornet through an attack.
        public void OnEvade(float now, EnemyInfo enemy)
        {
            Now = now;
            Give(Bonuses.WitchTime, Begin(null, enemy != null && enemy.IsBoss));
        }

        public void OnTaunt(float now)
        {
            Now = now;
            if (!Meter.InCombo || now - lastTauntBonus < Tuning.TauntCooldown)
                return;
            lastTauntBonus = now;
            Give(Bonuses.Taunt, Begin(null, false));
        }

        public void OnBindStarted(float now)
        {
            Now = now;
            bindStartHealth = Hero.Health;
            bindOpen = true;
            hexedGiven = false;
            bellPaid = false;
            firstBindDoneAt = Never;
            // A bind spends silk too: NO COST TOO GREAT counts only a spool spent on skills.
            spoolFull = false;
        }

        // Hornet finished a bind; nearestEnemy is how far the closest living enemy was (RISKY BUSINESS).
        public void OnBindCompleted(float now, float nearestEnemy = float.PositiveInfinity)
        {
            Now = now;
            // Multibinder finishes one bind twice (two DID BIND after one STARTED BIND): one bind is one MENDED.
            if (!bindOpen)
            {
                SecondBindStage();
                return;
            }
            bindOpen = false;
            firstBindDoneAt = now;
            var startHealth = bindStartHealth;
            bindStartHealth = -1;
            if (!Meter.InCombo)
                return;
            bindsThisCombo++;
            var s = Begin(null, false);
            Give(Hero.OnGround ? Bonuses.Mended : Bonuses.MidairMend, s);
            if (startHealth == 1)
                Give(Bonuses.StitchInTime, s);
            if (nearestEnemy <= Tuning.RiskyBusinessRange)
                Give(Bonuses.RiskyBusiness, s);
        }

        // A real hit on Hornet costs a rank; soft damage only takes points off the meter (MECHANICS.md §5).
        public void OnHeroDamaged(float now, bool soft, bool explosion = false)
        {
            Now = now;
            pendingGraze = Never;
            needleStreak = 0;
            clashes.Clear();
            deflecting = false;
            untouchableSince = now;
            untouchableActions = 0;
            arenaDamaged = true;
            if (soft)
            {
                Meter.Remove(Tuning.SoftDamagePenalty);
                return;
            }
            BossDamaged();
            if (Tuning.HitPenalty >= 1f)
                Meter.DropRank();
            else
                Meter.Remove(Tuning.HitPenalty * Meter.Size);
            if (explosion)
                GiveFlat(Bonuses.AwMan);
            hurts.RemoveAll(t => now - t > Tuning.GitGudWindow);
            hurts.Add(now);
            if (hurts.Count == 3)
                GiveFlat(Bonuses.GitGud);
        }

        public void OnHeroDied(float now)
        {
            Now = now;
            Meter.EndCombo();
            air = null;
            boss = null;
        }

        // Quitting to the menu ends the combo; it must not wait frozen for the next load.
        public void OnQuitToMenu()
        {
            Meter.EndCombo();
            air = null;
        }

        private void ObserveHealth(HeroState was)
        {
            if (Hero.Dead && !was.Dead)
                OnHeroDied(Now);
            if (!Meter.InCombo)
                return;
            if (Hero.Health == 1)
                hadOneMask = true;
            if (Hero.SilkMax > 0 && was.SilkMax == Hero.SilkMax && was.Silk < was.SilkMax && Hero.Silk >= Hero.SilkMax)
                GiveFlat(Bonuses.WoundTight);
            // A full spool spent on skills (not binds) within one combo, down to less than one more skill: the spool
            // rarely divides by the cost (9 silk, 4 a skill), so an exact zero would almost never come.
            var cost = Hero.SkillCost > 0 ? Hero.SkillCost : 1;
            if (Hero.SilkMax > 0 && Hero.Silk >= Hero.SilkMax)
                spoolFull = true;
            else if (spoolFull && !noCostGiven && Hero.Silk < cost && was.Silk >= cost)
            {
                // Only a skill's silk counts. The Architect's craft bind empties the spool too, before any event says
                // so; a skill's own use event comes a frame before or after its silk leaves.
                if (Now - LastSkillUse() <= Tuning.NoCostSkillWindow)
                    SpoolSpent();
                else
                    spoolDrainedAt = Now;
            }
        }

        // A skill used right after the spool ran dry is the skill that drained it.
        private void SkillAfterDrain()
        {
            if (!noCostGiven && Meter.InCombo && Now - spoolDrainedAt <= Tuning.NoCostSkillWindow)
                SpoolSpent();
        }

        private void SpoolSpent()
        {
            noCostGiven = true;
            spoolDrainedAt = Never;
            GiveFlat(Bonuses.NoCostTooGreat);
        }

        private float LastSkillUse() => skillUses.Count == 0 ? Never : skillUses.Values.Max();
    }
}
