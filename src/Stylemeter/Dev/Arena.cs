using System.Collections.Generic;
using UnityEngine;

namespace Stylemeter.Dev
{
    // arena <seconds> | arena off: every enemy in the room comes back that many seconds after it dies, where and as it
    // was when the arena was set (health, black thread), so a player can fight on for a recording. The copies wait on
    // an inactive shelf in the room, so they never wake up there, and go with the room when it unloads.
    internal static class Arena
    {
        private static readonly List<Spawn> Spawns = new List<Spawn>();
        private static GameObject shelf;
        private static float delay;

        public static string Start(float seconds)
        {
            Stop();
            delay = seconds;
            shelf = new GameObject("Stylemeter arena shelf");
            shelf.SetActive(false);
            foreach (var enemy in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (!enemy.isActiveAndEnabled || enemy.GetIsDead())
                    continue;
                var template = Object.Instantiate(enemy.gameObject, shelf.transform);
                Kept.Forget(template);
                Spawns.Add(new Spawn
                {
                    Name = enemy.name,
                    Template = template,
                    Parent = enemy.transform.parent,
                    Position = enemy.transform.position,
                    Health = enemy.hp,
                    Current = enemy,
                });
            }
            return System.FormattableString.Invariant(
                $"arena: {Spawns.Count} enemies come back {seconds}s after dying");
        }

        public static string Stop()
        {
            Spawns.Clear();
            if (shelf)
                Object.Destroy(shelf);
            return "arena off";
        }

        // Game time, so nothing comes back while the game is paused.
        public static void Tick()
        {
            if (Spawns.Count == 0)
                return;
            if (!shelf)
            {
                Spawns.Clear();
                return;
            }
            var now = Time.time;
            foreach (var spawn in Spawns)
            {
                // A copy may wake up threaded and double its health again (BlackThreadState), so it gets the
                // arena's health back once it is up.
                if (spawn.HealthDueAt > 0f && now >= spawn.HealthDueAt && spawn.Current)
                {
                    spawn.Current.hp = spawn.Health;
                    spawn.HealthDueAt = -1f;
                }
                if (spawn.Current && !spawn.Current.GetIsDead())
                    continue;
                if (spawn.BackAt < 0f)
                {
                    spawn.BackAt = now + delay;
                    continue;
                }
                if (now < spawn.BackAt)
                    continue;
                var fresh = Object.Instantiate(spawn.Template, spawn.Position, spawn.Template.transform.rotation,
                    spawn.Parent);
                fresh.name = spawn.Name;
                spawn.Current = fresh.GetComponent<HealthManager>();
                spawn.BackAt = -1f;
                spawn.HealthDueAt = now + 0.3f;
            }
        }

        private sealed class Spawn
        {
            public string Name;
            public GameObject Template;
            public Transform Parent;
            public Vector3 Position;
            public int Health;
            public HealthManager Current;
            public float BackAt = -1f;
            public float HealthDueAt = -1f;
        }
    }
}
