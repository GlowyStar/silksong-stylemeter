using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Stylemeter.Dev
{
    // Test dummies: the room's enemies with their brains (FSMs) off, pinned where the tester puts them. Their bodies
    // keep their type and gravity, so the meter still tells fliers from walkers.
    internal static class Dummies
    {
        private static readonly Dictionary<HealthManager, Vector3> Pinned = new Dictionary<HealthManager, Vector3>();
        private static readonly List<Behaviour> Disabled = new List<Behaviour>();

        public static string Freeze(string mode)
        {
            if (mode == "off")
            {
                foreach (var behaviour in Disabled)
                {
                    if (behaviour)
                        behaviour.enabled = true;
                }
                Disabled.Clear();
                Pinned.Clear();
                return "unfrozen";
            }
            var count = 0;
            foreach (var hm in HealthManager.EnumerateActiveEnemies().ToList())
            {
                if (!hm || hm.GetIsDead() || Pinned.ContainsKey(hm))
                    continue;
                // Read size and flight before anything changes.
                Game.EnemyRegistry.Get(hm);
                foreach (var fsm in hm.GetComponentsInChildren<PlayMakerFSM>())
                {
                    if (!fsm.enabled)
                        continue;
                    fsm.enabled = false;
                    Disabled.Add(fsm);
                }
                Pinned[hm] = hm.transform.position;
                count++;
            }
            return $"frozen {count}, pinned {Pinned.Count}";
        }

        // Keeps every pinned enemy in place; called every frame.
        public static void Tick()
        {
            if (Pinned.Count == 0)
                return;
            foreach (var pair in Pinned.ToList())
            {
                var hm = pair.Key;
                if (!hm || hm.GetIsDead())
                {
                    Pinned.Remove(hm);
                    continue;
                }
                hm.transform.position = pair.Value;
                var body = hm.GetComponent<Rigidbody2D>();
                if (body)
                {
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
        }

        // "which" is an index from the last census or a name prefix.
        public static string Move(string which, float x, float y)
        {
            var hm = Find(which);
            if (!hm)
                return "no such enemy " + which;
            var to = new Vector3(x, y, hm.transform.position.z);
            hm.transform.position = to;
            if (Pinned.ContainsKey(hm))
                Pinned[hm] = to;
            return $"{hm.name} at {Bridge.V(to)}";
        }

        // "which" is an index from the last census or a name prefix with _ for spaces (Pilgrim_01).
        public static HealthManager Find(string which)
        {
            var census = Probe.LastCensus;
            if (int.TryParse(which, out var index))
                return index >= 0 && index < census.Count ? census[index] : null;
            var name = which.Replace('_', ' ');
            return census.FirstOrDefault(hm => hm && !hm.GetIsDead() && hm.name.StartsWith(name));
        }

        // One enemy gets its brain back (for clashes and parries), still pinned unless "free".
        public static string Wake(string which, bool free)
        {
            var hm = Find(which);
            if (!hm)
                return "no such enemy " + which;
            foreach (var fsm in hm.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (Disabled.Remove(fsm))
                    fsm.enabled = true;
            }
            if (free)
                Pinned.Remove(hm);
            return $"{hm.name} awake{(free ? ", free" : ", pinned")}";
        }

        // Level hazards that hurt enemies or Hornet: where the spikes and the like are.
        public static string Hazards()
        {
            var at = HeroController.instance ? HeroController.instance.transform.position : Vector3.zero;
            var found = Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None)
                .Where(d => d && d.isActiveAndEnabled && d.hazardType != GlobalEnums.HazardType.NON_HAZARD
                    && d.hazardType != GlobalEnums.HazardType.ENEMY)
                .OrderBy(d => (d.transform.position - at).sqrMagnitude)
                .Take(12)
                .Select(d =>
                {
                    var box = d.GetComponent<Collider2D>();
                    var bounds = box ? box.bounds : new Bounds(d.transform.position, Vector3.zero);
                    var hurtsEnemies = d.GetComponent<DamageEnemies>() != null;
                    return $"{d.hazardType} {d.name} center={Bridge.V(bounds.center)} size={Bridge.V(bounds.size)} "
                        + $"enemies={hurtsEnemies}";
                });
            return string.Join("\n", found);
        }
    }
}
