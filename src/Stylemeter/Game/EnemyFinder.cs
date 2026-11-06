using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Stylemeter.Game
{
    // The enemy an attack object belongs to. Most attacks sit under their enemy; spawned ones (thrown javelins and
    // sickles, Song Knight's cross slash) do not, so they are matched by shared name words, then by distance.
    internal static class EnemyFinder
    {
        private const int MinSharedWords = 2;
        private const float NearbyDistance = 10f;
        private static readonly Regex Brackets = new Regex(@"\(.*?\)");
        private static readonly char[] Separators = { ' ', '/' };

        public static HealthManager Owner(Component attack)
        {
            if (!attack)
                return null;
            var parent = attack.GetComponentInParent<HealthManager>();
            if (parent)
                return parent;
            var words = Words(Describe.PathOf(attack.gameObject));
            var position = attack.transform.position;
            HealthManager byName = null;
            HealthManager nearest = null;
            var bestShared = 0;
            var byNameDistance = float.MaxValue;
            var nearestDistance = NearbyDistance * NearbyDistance;
            foreach (var enemy in HealthManager.EnumerateActiveEnemies())
            {
                if (!enemy)
                    continue;
                var distance = (enemy.transform.position - position).sqrMagnitude;
                var shared = Shared(words, Words(enemy.name));
                if (shared > bestShared || (shared > 0 && shared == bestShared && distance < byNameDistance))
                {
                    byName = enemy;
                    bestShared = shared;
                    byNameDistance = distance;
                }
                if (distance < nearestDistance)
                {
                    nearest = enemy;
                    nearestDistance = distance;
                }
            }
            return bestShared >= MinSharedWords ? byName : nearest;
        }

        private static string[] Words(string name) =>
            Brackets.Replace(name, "").Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        private static int Shared(string[] a, string[] b)
        {
            var shared = 0;
            foreach (var word in a)
            {
                if (Array.IndexOf(b, word) >= 0)
                    shared++;
            }
            return shared;
        }
    }
}
