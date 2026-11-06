using System;
using System.Collections.Generic;

namespace Stylemeter.Core
{
    internal enum ThreadState
    {
        Taut,
        Worn,
        Frayed,
        Snapped,
    }

    // Freshness of every scoring source ("thread tension", MECHANICS.md §3), ULTRAKILL's weapon freshness:
    // using a source wears its thread and slightly mends all the others.
    internal sealed class ThreadSet
    {
        private readonly Dictionary<string, float> tension = new Dictionary<string, float>();
        private readonly List<string> keys = new List<string>();

        // How many sources Hornet has right now (4 needle sources + attack tools + skills + Clawline).
        public int SourceCount { get; set; } = 5;
        public string LastSource { get; private set; }

        public float Tension(string source) => tension.TryGetValue(source, out var t) ? t : Tuning.ThreadMax;

        public float Multiplier(string source) => MultiplierOf(StateOf(Tension(source)));

        public static ThreadState StateOf(float t) =>
            t >= 7.5f ? ThreadState.Taut
            : t >= 5f ? ThreadState.Worn
            : t >= 1f ? ThreadState.Frayed
            : ThreadState.Snapped;

        public static float MultiplierOf(ThreadState state)
        {
            switch (state)
            {
                case ThreadState.Taut: return 1.5f;
                case ThreadState.Worn: return 1f;
                case ThreadState.Frayed: return 0.5f;
                default: return 0f;
            }
        }

        private float Floor => SourceCount <= 2 ? 5f : SourceCount <= 4 ? 1f : 0f;

        public void Use(string source, float wear, bool vsBoss)
        {
            var amount = wear * (vsBoss ? Tuning.ThreadBossWear : 1f);
            keys.Clear();
            keys.AddRange(tension.Keys);
            foreach (var key in keys)
            {
                if (key != source)
                    tension[key] = Math.Min(Tuning.ThreadMax, tension[key] + Tuning.ThreadRecoverOthers * amount);
            }
            tension[source] = Math.Max(Floor, Tension(source) - Tuning.ThreadWear * amount);
            LastSource = source;
        }

        public void Recover(float dt)
        {
            keys.Clear();
            keys.AddRange(tension.Keys);
            foreach (var key in keys)
                tension[key] = Math.Min(Tuning.ThreadMax, tension[key] + Tuning.ThreadRecoverPerSecond * dt);
        }

        public void Reset()
        {
            tension.Clear();
            LastSource = null;
        }
    }
}
