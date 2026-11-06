using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stylemeter.Dev
{
    // Enemies carried between rooms for a showcase arena. "keep <enemy> <name>" puts an inactive copy aside on a shelf
    // that outlives the room, "bring <name> <x> <y>" wakes a copy of it in the room Hornet is in, and "clearroom" puts
    // the room's own enemies away, so only the brought ones fight.
    internal static class Kept
    {
        private static readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>();
        private static GameObject shelf;

        public static string Keep(HealthManager enemy, string name)
        {
            if (!shelf)
            {
                shelf = new GameObject("Stylemeter kept enemies");
                shelf.SetActive(false);
                Object.DontDestroyOnLoad(shelf);
            }
            if (Templates.TryGetValue(name, out var old) && old)
                Object.Destroy(old);
            var template = Object.Instantiate(enemy.gameObject, shelf.transform);
            template.name = enemy.name;
            Forget(template);
            Templates[name] = template;
            return $"kept {enemy.name} as {name}";
        }

        // A copy keeps its original's persistence id, and the room remembers that id as dead once a copy dies; with it
        // every later copy would load dead and switch itself off (HealthManager.Awake), so templates go without.
        public static void Forget(GameObject template)
        {
            foreach (var item in template.GetComponentsInChildren<PersistentBoolItem>(true))
                Object.Destroy(item);
        }

        // A copy of a kept enemy made from the shelf lands in the shelf's scene, so it is moved into the room.
        public static string Bring(string name, float x, float y)
        {
            if (!Templates.TryGetValue(name, out var template) || !template)
                return "nothing kept as " + name;
            var at = new Vector3(x, y, template.transform.position.z);
            var copy = Object.Instantiate(template, at, template.transform.rotation);
            SceneManager.MoveGameObjectToScene(copy, SceneManager.GetActiveScene());
            copy.name = template.name;
            return copy.name + " at " + Bridge.V(copy.transform.position);
        }

        // Invisible walls from the floor far up, so the fight stays between them: a door out of the room would
        // reload it without the brought enemies. They go with the room.
        public static string Walls(float left, float right)
        {
            var terrain = LayerMask.NameToLayer("Terrain");
            foreach (var x in new[] { left, right })
            {
                var wall = new GameObject("Stylemeter arena wall") { layer = terrain };
                wall.transform.position = new Vector3(x, 30f, 0f);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 100f);
            }
            return $"walls at x {left} and {right}, layer {terrain}";
        }

        // An invisible floor from x0 to x1 with its top at y, over a hole or a door down out of the room.
        public static string Cover(float x0, float x1, float top)
        {
            var slab = new GameObject("Stylemeter arena floor") { layer = LayerMask.NameToLayer("Terrain") };
            slab.transform.position = new Vector3((x0 + x1) / 2f, top - 1f, 0f);
            slab.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0, 2f);
            return "floor from " + Bridge.V(new Vector2(x0, top)) + " to " + Bridge.V(new Vector2(x1, top));
        }

        // Where the ground is under each x, from far above: the top of the first piece of terrain.
        public static string Ground(float x0, float x1, float step)
        {
            var terrain = LayerMask.GetMask("Terrain");
            var found = new List<string>();
            for (var x = x0; x <= x1; x += step)
            {
                var hit = Physics2D.Raycast(new Vector2(x, 40f), Vector2.down, 80f, terrain);
                var top = hit.collider ? hit.point.y.ToString("0.00", CultureInfo.InvariantCulture) : "-";
                found.Add(x.ToString("0.0", CultureInfo.InvariantCulture) + ":" + top);
            }
            return string.Join(" ", found);
        }

        public static string ClearRoom()
        {
            var count = 0;
            foreach (var enemy in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                enemy.gameObject.SetActive(false);
                count++;
            }
            return count + " enemies put away";
        }
    }
}
