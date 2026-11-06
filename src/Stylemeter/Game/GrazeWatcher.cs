using System.Collections.Generic;
using GlobalEnums;
using UnityEngine;

namespace Stylemeter.Game
{
    // GRAZE: an enemy attack passes right by Hornet's hurtbox while she jumps or dashes, and does not hurt her.
    // Every frame it looks for enemy attack hitboxes just outside the hurtbox and follows each one until it leaves.
    internal static class GrazeWatcher
    {
        private const float Margin = 1f;
        private const float LeaveGap = 0.15f;
        private const float MaxExposure = 1.5f;

        private sealed class Pass
        {
            public DamageHero Attack;
            public float First;
            public float Last;
            public bool Moving;
            public bool Spoiled;
        }

        private static readonly Collider2D[] Nearby = new Collider2D[32];
        private static readonly Dictionary<int, Pass> Passes = new Dictionary<int, Pass>();
        private static readonly List<int> Gone = new List<int>();
        private static readonly ContactFilter2D Filter =
            new ContactFilter2D { useTriggers = true, useLayerMask = false };
        private static BoxCollider2D hurtbox;

        public static void Tick(HeroController hero, float now)
        {
            if (!hurtbox)
            {
                var box = hero.GetComponentInChildren<HeroBox>(true);
                hurtbox = box ? box.GetComponent<BoxCollider2D>() : null;
                if (!hurtbox)
                    return;
            }
            var cs = hero.cState;
            var exposed = hero.CanTakeDamage() && !cs.invulnerable && !cs.parrying;
            var moving = !cs.onGround || cs.dashing;
            var bounds = hurtbox.bounds;
            var size = (Vector2)bounds.size + Vector2.one * (2f * Margin);
            var count = Physics2D.OverlapBox(bounds.center, size, 0f, Filter, Nearby);
            for (var i = 0; i < count; i++)
            {
                var attack = AttackOf(Nearby[i]);
                if (!attack)
                    continue;
                var id = attack.GetInstanceID();
                if (!Passes.TryGetValue(id, out var pass))
                    Passes[id] = pass = new Pass { Attack = attack, First = now };
                pass.Last = now;
                pass.Moving |= moving;
                pass.Spoiled |= !exposed;
            }
            Gone.Clear();
            foreach (var entry in Passes)
            {
                if (now - entry.Value.Last > LeaveGap)
                    Gone.Add(entry.Key);
            }
            foreach (var id in Gone)
            {
                var pass = Passes[id];
                Passes.Remove(id);
                // Attacks that hang around longer are standing hitboxes, not passing attacks.
                if (pass.Spoiled || !pass.Moving || pass.Last - pass.First > MaxExposure || !pass.Attack)
                    continue;
                var enemy = EnemyFinder.Owner(pass.Attack);
#if STYLEMETER_DEV
                if (Dev.DevTools.LogEvents.Value)
                    Plugin.Out($"NEARMISS enemy={(enemy ? enemy.name : "-")} "
                        + $"attack={Describe.PathOf(pass.Attack.gameObject)}");
#endif
                EngineHost.Grazed(enemy);
            }
        }

        // A hit on Hornet, a clash or a parry spoils every pass in progress: none of them is a near miss.
        public static void Spoil()
        {
            foreach (var pass in Passes.Values)
                pass.Spoiled = true;
        }

        public static void Clear() => Passes.Clear();

        // Enemy bodies carry contact damage, either on the HealthManager's own object or on a child such as
        // "Pilgrim 02 Song/Body Collider"; jumping past those is not dodging an attack.
        private static DamageHero AttackOf(Collider2D collider)
        {
            if (!collider || collider.GetComponent<HealthManager>()
                || collider.name.IndexOf("Body", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return null;
            var attack = collider.GetComponent<DamageHero>();
            return attack && attack.enabled && attack.damageDealt > 0 && attack.hazardType == HazardType.ENEMY
                ? attack : null;
        }
    }
}
