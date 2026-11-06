namespace Stylemeter.Core
{
    // Boss fights (BONUSES.md section 12): a fight runs from the boss's title card to its death, and its result (the
    // named win, FLAWLESS VICTORY, SHOWSTOPPER, JACKPOT!, GREAT ENEMY FELLED) goes once and flat: the fight is over,
    // so it must not buy a SILKSONG that was not fought for. Everything during the fight keeps the boss x2.
    internal sealed partial class StyleEngine
    {
        private sealed class BossFight
        {
            public string Key;
            public bool Damaged;
            public bool Hit;
            public bool Killed;
            public float KilledAt = Never;
            public HitEvent FinalBlow;
            public int RankAtBlow = -1;
            public bool Riposte;
            public bool EnGarde;
            public bool WatchYourStep;
            // First Sinner finished a heal during the fight (UNFORGIVEN needs her never to).
            public bool Healed;
        }

        private BossFight boss;

        // The boss's title card showed: a fight begins (or goes on, when a later phase shows the same card).
        public void OnBossTitle(float now, EnemyInfo enemy)
        {
            Now = now;
            if (enemy == null)
                return;
            if (boss != null && boss.Key == enemy.BossKey)
            {
                boss.Killed = false;
                return;
            }
            if (boss != null && boss.Killed)
                EndBossFight();
            boss = new BossFight { Key = enemy.BossKey };
        }

        // The game's BOSS DEATH event: the fight is over, even when the boss left by a scripted end rather than dying
        // to a hit; then Hornet's last hit on it counts as the final blow.
        public void OnBossDeath(float now)
        {
            Now = now;
            if (boss != null && (boss.Killed || boss.Hit))
                EndBossFight();
        }

        private void BossHit(HitEvent hit, Scoring s)
        {
            if (boss == null || !hit.Enemy.IsBoss)
                return;
            if (!boss.Killed)
            {
                boss.Hit = true;
                boss.FinalBlow = hit;
                boss.RankAtBlow = Meter.Rank;
                boss.Riposte = Now - lastClash <= Tuning.RiposteWindow;
            }
            if (boss.WatchYourStep || hit.Kind != AttackKind.Tool || hit.Tool != Tools.Tacks)
                return;
            boss.WatchYourStep = true;
            Give(Bonuses.WatchYourStep, s);
        }

        // A clash with Lace, the fencer: once per fight.
        private void BossClash(EnemyInfo enemy)
        {
            if (boss == null || enemy == null || !enemy.IsBoss || boss.EnGarde || !BossBonuses.IsLace(boss.Key))
                return;
            boss.EnGarde = true;
            Give(Bonuses.EnGarde, Peek(null, true));
        }

        private void BossKill(HitEvent hit, int rankBefore, bool riposte)
        {
            if (boss == null || !hit.Enemy.IsBoss)
                return;
            boss.Killed = true;
            boss.KilledAt = Now;
            boss.FinalBlow = hit;
            boss.RankAtBlow = rankBefore;
            boss.Riposte = riposte;
        }

        private void BossDamaged()
        {
            if (boss != null && !boss.Killed)
                boss.Damaged = true;
        }

        // Without a BOSS DEATH event (some fights may not send one) the result goes a while after the boss died.
        private void TickBoss()
        {
            if (boss != null && boss.Killed && Now - boss.KilledAt >= Tuning.BossEndFallback)
                EndBossFight();
        }

        private void BossSceneChanged()
        {
            if (boss != null && boss.Killed)
                EndBossFight();
            boss = null;
        }

        private void EndBossFight()
        {
            var fight = boss;
            boss = null;
            var blow = fight.FinalBlow;
            GiveFlat(BossBonuses.WinFor(fight.Key));
            if (!fight.Damaged)
                GiveFlat(Bonuses.FlawlessVictory);
            if (fight.RankAtBlow == Tuning.Silksong)
                GiveFlat(Bonuses.Showstopper);
            if (blow.Kind == AttackKind.Tool || blow.Kind == AttackKind.Skill)
                GiveFlat(Bonuses.Jackpot);
            if (fight.Riposte || (blow.Kind == AttackKind.Skill && blow.Tool == Skills.CrossStitch))
                GiveFlat(Bonuses.GreatEnemyFelled);
            if (fight.Key == BossBonuses.Pinstress && blow.Kind == AttackKind.NeedleCharge)
                GiveFlat(Bonuses.StudentBecomesTheMaster);
            if (fight.Key == BossBonuses.FirstSinner && !fight.Healed)
                GiveFlat(Bonuses.Unforgiven);
        }
    }
}
