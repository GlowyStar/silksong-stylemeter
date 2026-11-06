using System.Collections.Generic;

namespace Stylemeter.Core
{
    // Enemies by what they are and what they were doing (BONUSES.md sections 3, 8, 10, 11 and 12): named kinds,
    // Needolin singers, stuns, pits, explosive chains, parrying enemies and First Sinner's heal.
    internal sealed partial class StyleEngine
    {
        private readonly Dictionary<int, int> songKills = new Dictionary<int, int>();
        private readonly HashSet<int> ovationSongs = new HashSet<int>();
        private readonly HashSet<int> parriedBy = new HashSet<int>();
        private readonly HashSet<int> fellIn = new HashSet<int>();
        private int dominoCount;
        private float lastDomino = Never;

        // Kill bonuses for what the enemy is or was doing; they stack on the kill type.
        private void NamedKill(HitEvent hit, Scoring s)
        {
            var species = hit.Enemy.Species;
            if (!hit.Enemy.IsBoss && species != null && Species.BigGame.Contains(species))
                Give(Bonuses.BigGame, s);
            if (species == Species.Wingmould)
                Give(Bonuses.WhitePalace, s);
            if (hit.Thief)
                Give(Bonuses.StopThief, s);
            if (hit.Exposed)
                Give(Bonuses.CaughtOffGuard, s);
            if (hit.Lured)
                Give(Bonuses.Peekaboo, s);
            if (hit.SongId > 0)
            {
                Give(Bonuses.SwanSong, s);
                songKills.TryGetValue(hit.SongId, out var kills);
                songKills[hit.SongId] = ++kills;
                if (kills >= Tuning.StandingOvationKills && ovationSongs.Add(hit.SongId))
                    Give(Bonuses.StandingOvation, Peek(null, hit.Enemy.IsBoss));
            }
            if (species != null && Species.Explosive.Contains(species))
                DominoLink(hit.Enemy);
        }

        // Explosive enemies setting each other off: Hornet's kill of the first one starts the chain, every explosive
        // enemy blown up within the window adds a link.
        private void DominoLink(EnemyInfo enemy)
        {
            if (Now - lastDomino > Tuning.DominoWindow)
                dominoCount = 0;
            lastDomino = Now;
            if (++dominoCount == Tuning.DominoChain && Meter.InCombo)
                Give(Bonuses.DominoEffect, Peek(null, enemy.IsBoss));
        }

        private void WorldDeath(EnemyInfo enemy, DeathCause cause)
        {
            if (cause.Hazard == Hazard.Explosion && enemy.Species != null && Species.Explosive.Contains(enemy.Species))
                DominoLink(enemy);
        }

        // A clash with an enemy's parry counter, or with an enemy known to parry: once per enemy.
        private void ParryThis(EnemyInfo enemy, bool parryCounter, Scoring s)
        {
            if (enemy == null)
                return;
            var parrier = parryCounter || (enemy.Species != null && Species.Parriers.Contains(enemy.Species));
            if (parrier && parriedBy.Add(enemy.Id))
                Give(Bonuses.ParryThis, s);
        }

        // A boss went into its stun.
        public void OnEnemyStunned(float now, EnemyInfo enemy)
        {
            Now = now;
            if (enemy != null && enemy.IsBoss && Meter.InCombo)
                Give(Bonuses.Staggered, Begin(null, true));
        }

        // An enemy Hornet hit a moment ago fell out of the room.
        public void OnEnemyFell(float now, EnemyInfo enemy)
        {
            Now = now;
            if (enemy == null || !Meter.InCombo || !fellIn.Add(enemy.Id))
                return;
            Give(Bonuses.ThisIsSparta, Begin(null, enemy.IsBoss));
        }

        // First Sinner healed herself: this fight cannot end UNFORGIVEN.
        public void OnBossHealed(float now, EnemyInfo enemy)
        {
            Now = now;
            if (boss != null && enemy != null && enemy.IsBoss)
                boss.Healed = true;
        }

        // Hornet's hit broke off First Sinner's heal.
        public void OnBindBroken(float now, EnemyInfo enemy)
        {
            Now = now;
            if (enemy != null && Meter.InCombo)
                Give(Bonuses.BindBreaker, Begin(null, enemy.IsBoss));
        }

        private void ClearEnemyState()
        {
            songKills.Clear();
            ovationSongs.Clear();
            parriedBy.Clear();
            fellIn.Clear();
            dominoCount = 0;
        }
    }
}
