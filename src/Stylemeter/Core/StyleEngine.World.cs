namespace Stylemeter.Core
{
    // Deaths and hits that were not Hornet's blow (BONUSES.md section 10): her poison, fire and lightning ticks, level
    // hazards an enemy she hit fell into, explosions and enemies hurting each other.
    internal sealed partial class StyleEngine
    {
        private float lastCrossfire = Never;
        private int lastExplosionId;
        private float lastExplosionKill = Never;
        private int explosionKills;
        private float lastLightningKill = Never;
        private int lightningKills;

        // An enemy died outside one of Hornet's hits.
        public void OnEnemyDied(float now, EnemyInfo enemy, DeathCause cause)
        {
            Now = now;
            WorldDeath(enemy, cause);
            enemies.TryGetValue(enemy.Id, out var record);
            if (cause.ByHornet && record != null)
            {
                // Her ticks: the kill is hers, typed by her last hit and named by the element of the ticks.
                var tick = record.LastHit;
                tick.Killed = true;
                if (cause.Element != Element.None)
                    tick.Element = cause.Element;
                var ts = Peek(SourceOf(tick.Kind, tick.Tool), enemy.IsBoss);
                Kill(tick, ts, record, late: true);
                Wear(ts, Tuning.WearKill);
                return;
            }
            var hazard = HazardKill(cause.Hazard);
            if (hazard != null && record != null && now - record.LastHitAt <= Tuning.EnvironmentKillWindow)
            {
                EnvironmentKill(enemy, hazard);
                return;
            }
            if (cause.Hazard == Hazard.Enemy)
            {
                Crossfire(enemy);
                enemies.Remove(enemy.Id);
                return;
            }
            if (record == null || now - record.LastHitAt > Tuning.LateKillWindow)
                return;
            // A delayed death right after her hit: still her kill, scored as if the hit had killed.
            var hit = record.LastHit;
            hit.Killed = true;
            var s = Peek(SourceOf(hit.Kind, hit.Tool), enemy.IsBoss);
            Kill(hit, s, record, late: false);
            Wear(s, Tuning.WearKill);
        }

        // Something other than Hornet hurt an enemy without killing it.
        public void OnWorldHit(float now, EnemyInfo enemy, Hazard hazard)
        {
            Now = now;
            if (hazard == Hazard.Enemy)
                Crossfire(enemy);
        }

        // Hornet's damage over time landed on an enemy she hit: a burning one can be finished off (FLAMBÉ).
        public void OnTickDamage(float now, EnemyInfo enemy, Element element)
        {
            Now = now;
            if (element == Element.Fire && enemies.TryGetValue(enemy.Id, out var record))
                record.BurningAt = now;
        }

        // An enemy hurt by another enemy's attack, once per victim, only in a fight.
        private void Crossfire(EnemyInfo enemy)
        {
            if (!Meter.InCombo || Now - lastCrossfire < Tuning.CrossfireCooldown)
                return;
            var record = RecordOf(enemy);
            if (record.Crossfired)
                return;
            record.Crossfired = true;
            lastCrossfire = Now;
            Give(Bonuses.Crossfire, Peek(null, enemy.IsBoss));
        }

        // An enemy Hornet hit died in the level's hazards: the hazard is the kill type.
        private void EnvironmentKill(EnemyInfo enemy, BonusDef type)
        {
            var s = Peek(null, enemy.IsBoss);
            Give(type, s);
            if (enemy.Large && !enemy.IsBoss)
                Give(Bonuses.BigKill, s);
            MultiKill(s);
            KillStreak();
            enemies.Remove(enemy.Id);
        }

        private static BonusDef HazardKill(Hazard hazard)
        {
            switch (hazard)
            {
                case Hazard.Spikes: return Bonuses.Spiked;
                case Hazard.Lava: return Bonuses.Melted;
                case Hazard.Acid: return Bonuses.Dissolved;
                case Hazard.Trap: return Bonuses.Trapped;
                case Hazard.Explosion: return Bonuses.Blasted;
                default: return null;
            }
        }

        // One explosion (one damaging object) killed two or more.
        private void ExplosionKill(HitEvent hit)
        {
            if (!hit.Explosion)
                return;
            if (hit.SwingId != lastExplosionId || Now - lastExplosionKill > Tuning.ExplosionWindow)
            {
                explosionKills = 0;
                lastExplosionId = hit.SwingId;
            }
            lastExplosionKill = Now;
            if (++explosionKills == 2)
                Give(Bonuses.ChainReaction, Peek(null, hit.Enemy.IsBoss));
        }

        // Lightning killed two or more within half a second.
        private void LightningKill(HitEvent hit)
        {
            if (hit.Element != Element.Lightning)
                return;
            lightningKills = Now - lastLightningKill <= Tuning.ThunderstruckWindow ? lightningKills + 1 : 1;
            lastLightningKill = Now;
            if (lightningKills == 2)
                Give(Bonuses.Thunderstruck, Peek(null, hit.Enemy.IsBoss));
        }
    }
}
