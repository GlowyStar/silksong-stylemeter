using System;
using System.Collections.Generic;
using HarmonyLib;
using Stylemeter.Core;
using UnityEngine;

namespace Stylemeter.Game
{
    // One EnemyInfo per HealthManager in the current scene. Bosses are marked when their title card shows.
    internal static class EnemyRegistry
    {
        private static readonly AccessTools.FieldRef<HealthManager, int> InitHp =
            GameField.Find<HealthManager, int>("initHp");
        private static readonly AccessTools.FieldRef<HealthManager, HealthManager.EnemySize> SizeOf =
            GameField.Find<HealthManager, HealthManager.EnemySize>("enemySize");

        private static readonly Dictionary<int, EnemyInfo> Known = new Dictionary<int, EnemyInfo>();
        private static readonly Dictionary<int, BlackThreadState> Threads = new Dictionary<int, BlackThreadState>();
        private static readonly Dictionary<int, string> Bosses = new Dictionary<int, string>();

        public static EnemyInfo Get(HealthManager hm)
        {
            var id = hm.GetInstanceID();
            if (!Known.TryGetValue(id, out var info))
            {
                // A flier hangs in the air on its own: a dynamic body without gravity.
                var body = hm.GetComponent<Rigidbody2D>();
                var maxHp = InitHp != null ? InitHp(hm) : 0;
                info = new EnemyInfo
                {
                    Id = id,
                    Name = hm.name,
                    MaxHp = maxHp,
                    // The game marks almost no enemy Large (even Song Reed Grand with 130 hp is Regular), so a big
                    // health pool counts too.
                    Large = (SizeOf != null && SizeOf(hm) == HealthManager.EnemySize.Large)
                        || maxHp >= Tuning.BigKillMinHp,
                    Flying = body && body.bodyType == RigidbodyType2D.Dynamic && body.gravityScale == 0f,
                    Species = Species.Key(Describe.JournalName(hm)),
                };
                Known[id] = info;
                Threads[id] = hm.GetComponent<BlackThreadState>();
            }
            info.IsBoss = Bosses.TryGetValue(id, out var key);
            info.BossKey = key;
            var thread = Threads[id];
            info.BlackThreaded = thread && thread.IsBlackThreaded;
            return info;
        }

        public static void MarkBoss(HealthManager hm, string key)
        {
            var id = hm.GetInstanceID();
            Bosses[id] = key ?? "";
            if (Known.TryGetValue(id, out var info))
            {
                info.IsBoss = true;
                info.BossKey = key;
            }
        }

        public static void Clear()
        {
            Known.Clear();
            Threads.Clear();
            Bosses.Clear();
        }
    }
}
