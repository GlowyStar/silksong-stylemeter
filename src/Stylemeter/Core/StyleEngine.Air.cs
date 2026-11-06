using System.Collections.Generic;

namespace Stylemeter.Core
{
    // Air series, pogo chains, bounces and jumps: BONUSES.md section 2.
    internal sealed partial class StyleEngine
    {
        // Everything between leaving the ground and landing (wall clings and bounces do not land).
        private sealed class AirSeries
        {
            public float Start;
            public int Actions;
            public int Kills;
            public int Pogos;
            public readonly HashSet<int> PogoEnemies = new HashSet<int>();
            public bool DoubleJumped;
            public bool HangTime;
            public bool SkyHigh;
            public bool Stairway;
            // The end of the glide that an attack out of it hit something after, and the glide DRIFTER was paid for.
            public float GlideAttack = Never;
            public float DrifterPaid = Never;
            // BED OF NAILS and CAT'S CRADLE: once per air series each.
            public bool SpikePogo;
            public int WitchDownHits;
            public float LastWitchDown = Never;
            public bool CatsCradle;
        }

        private AirSeries air;
        private float fallingSince = Never;
        private float lastDownHit = Never;
        private EnemyInfo lastDownHitEnemy;
        private bool downHitPaired = true;
        private float lastLoneBounce = Never;
        private float lastSpring = Never;
        private float lastDownDash = Never;
        private float lastWallJump = Never;

        private float FallTime => fallingSince == Never ? 0f : Now - fallingSince;

        // A downward strike bounces Hornet off what it hit; the hit and the bounce can arrive in either order.
        public void OnPogoBounce(float now)
        {
            Now = now;
            if (!downHitPaired && now - lastDownHit <= Tuning.PogoPairWindow)
                Pogo();
            else
                lastLoneBounce = now;
        }

        public void OnSpringBounce(float now)
        {
            Now = now;
            lastSpring = now;
        }

        public void OnDownDash(float now)
        {
            Now = now;
            lastDownDash = now;
        }

        public void OnWallJump(float now)
        {
            Now = now;
            lastWallJump = now;
        }

        public void OnDoubleJump(float now)
        {
            Now = now;
            if (air != null)
                air.DoubleJumped = true;
        }

        private void ObserveAir()
        {
            if (!Hero.OnGround && air == null)
                air = new AirSeries { Start = Now };
            else if (air != null && !Height.Airborne(Now))
                Land();
            if (!Hero.OnGround && Hero.VelocityY < -Tuning.FallingSpeed)
            {
                if (fallingSince == Never)
                    fallingSince = Now;
            }
            else
            {
                fallingSince = Never;
            }
        }

        private void Land()
        {
            if (air.Actions >= Tuning.AirSeriesMinActions && Now - air.Start >= Tuning.TouchGrassAir)
                GiveFlat(Bonuses.TouchGrass);
            air = null;
        }

        private void TickAir()
        {
            if (air == null || air.HangTime || air.Actions < Tuning.AirSeriesMinActions
                || Now - air.Start < Tuning.HangTimeAir)
                return;
            air.HangTime = true;
            GiveFlat(Bonuses.HangTime);
        }

        private void DownHit(EnemyInfo enemy)
        {
            lastDownHit = Now;
            lastDownHitEnemy = enemy;
            downHitPaired = false;
            WitchDownHit();
            if (Now - lastLoneBounce <= Tuning.PogoPairWindow)
            {
                lastLoneBounce = Never;
                Pogo();
            }
        }

        // POGO xN pays 15 per pogo from the third on, the first line carrying all three (BONUSES.md section 2).
        private void Pogo()
        {
            downHitPaired = true;
            lastPogoAt = Now;
            Medley(lastDownHitEnemy.IsBoss);
            if (air == null)
                return;
            air.Pogos++;
            air.PogoEnemies.Add(lastDownHitEnemy.Id);
            var s = Peek(null, lastDownHitEnemy.IsBoss);
            if (air.Pogos >= 3)
                Give(Bonuses.PogoChain, s, Bonuses.PogoChain.Points * (air.Pogos == 3 ? 3 : 1), air.Pogos);
            if (air.Pogos == 8)
                Give(Bonuses.OneUp, s);
            // Wanderer's straight down strike is the Knight's pogo from Hollow Knight.
            if (air.Pogos == Tuning.OldHabitsPogos && Hero.Crest == Crests.Wanderer)
                Give(Bonuses.OldHabits, s);
            if (!air.Stairway && air.PogoEnemies.Count >= 3)
            {
                air.Stairway = true;
                Give(Bonuses.Stairway, s);
            }
        }

        // DRIFTER: an attack out of a glide hit something, and Hornet kills before she lands; one per glide. A kill by
        // that first strike alone almost never happens, so any kill before landing counts.
        private void GlideAttack(HitEvent hit)
        {
            if (air != null && !Hero.OnGround && hit.Kind != AttackKind.Other
                && Now - lastGlideEnd <= Tuning.DrifterWindow)
                air.GlideAttack = lastGlideEnd;
        }

        private void DrifterKill(Scoring s)
        {
            if (air == null || Hero.OnGround || air.GlideAttack == Never || air.DrifterPaid == air.GlideAttack)
                return;
            air.DrifterPaid = air.GlideAttack;
            Give(Bonuses.Drifter, s);
        }

        private void AirKill(Scoring s)
        {
            if (air == null || !Height.Airborne(Now))
                return;
            air.Kills++;
            if (air.Kills == 3 && !air.SkyHigh)
            {
                air.SkyHigh = true;
                Give(Bonuses.SkyHigh, s);
            }
        }
    }
}
