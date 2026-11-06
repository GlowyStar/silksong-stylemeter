using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Stylemeter.Game
{
    // Hornet's thrown tools after they leave her hand: the Curveclaw's way back, Throwing Ring wall bounces, the
    // Cogwork Wheel climbing a wall, the spikes of a Sting Shard she broke, and enemy shots she batted back.
    internal static class ToolWatch
    {
        private const float WallNormal = 0.7f;
        private const float ClimbHeight = 2f;
        private const float ClimbDrift = 0.6f;
        private const float ClimbTime = 1f;
        private const float DeflectWindow = 5f;

        private static readonly AccessTools.FieldRef<ToolBoomerang, float> BoomerangElapsed =
            GameField.Find<ToolBoomerang, float>("elapsedTime");
        private static readonly AccessTools.FieldRef<ToolBoomerang, float> BoomerangDuration =
            GameField.Find<ToolBoomerang, float>("flyOutCurveDuration");
        private static readonly AccessTools.FieldRef<ToolBoomerang, float> BoomerangReturnPoint =
            GameField.Find<ToolBoomerang, float>("flyOutReturnPoint");
        private static readonly AccessTools.FieldRef<ToolBoomerang, bool> BoomerangNailHit =
            GameField.Find<ToolBoomerang, bool>("wasNailHit");
        private static readonly AccessTools.FieldRef<FireballProjectile, bool> FireballWasHit =
            GameField.Find<FireballProjectile, bool>("wasHit");

        private static string shardName;
        private static string wheelName;
        private static readonly HashSet<int> BouncedRings = new HashSet<int>();
        private static readonly Dictionary<GameObject, WheelTrack> Wheels = new Dictionary<GameObject, WheelTrack>();
        private static readonly Dictionary<GameObject, float> Deflected = new Dictionary<GameObject, float>();
        private static float wheelThrownAt = float.NegativeInfinity;
        private static float lastLooseClash = float.NegativeInfinity;

        private sealed class WheelTrack
        {
            public float LowY;
            public float LowX;
            public float LowAt;
            public bool Climbed;
        }

        // BOOMERANG: the Curveclaw past the turn of its curve, or flying free afterwards without having been batted.
        public static bool OnReturnLeg(GameObject source)
        {
            var boomerang = source ? source.GetComponentInParent<ToolBoomerang>() : null;
            if (!boomerang)
                return false;
            var state = Traverse.Create(boomerang).Field("currentState").GetValue()?.ToString();
            if (state == "Translating")
                return BoomerangElapsed(boomerang) / BoomerangDuration(boomerang) >= BoomerangReturnPoint(boomerang);
            return state == "Body" && !BoomerangNailHit(boomerang);
        }

        // TRICK SHOT: the ring hit a wall (terrain, normal mostly sideways) before this hit.
        public static void RingTouchedTerrain(ToolRing ring, Collision2D collision)
        {
            if (!ring || collision == null || collision.contactCount == 0 || collision.gameObject.layer != 8)
                return;
            if (Mathf.Abs(collision.GetContact(0).normal.x) >= WallNormal)
                BouncedRings.Add(ring.GetInstanceID());
        }

        public static void RingThrown(ToolRing ring)
        {
            if (ring)
                BouncedRings.Remove(ring.GetInstanceID());
        }

        public static bool AfterWallBounce(GameObject source)
        {
            var ring = source ? source.GetComponentInParent<ToolRing>() : null;
            return ring && BouncedRings.Contains(ring.GetInstanceID());
        }

        // WALL RIDE: the Cogwork Wheel has no class of its own, so its clones are found by the throw prefab's name and
        // watched: gaining height while staying in place is climbing a wall.
        public static void ToolThrown(ToolItem tool)
        {
            if (tool && tool.name == Core.Tools.CogworkWheel)
                wheelThrownAt = Time.time;
        }

        public static void Tick(float now)
        {
            if (now - wheelThrownAt < 0.5f)
                FindWheels(now);
            if (Wheels.Count == 0)
                return;
            List<GameObject> gone = null;
            foreach (var pair in Wheels)
            {
                var wheel = pair.Key;
                if (!wheel || !wheel.activeInHierarchy)
                {
                    (gone ?? (gone = new List<GameObject>())).Add(wheel);
                    continue;
                }
                var track = pair.Value;
                var at = wheel.transform.position;
                if (at.y < track.LowY || now - track.LowAt > ClimbTime || Mathf.Abs(at.x - track.LowX) > ClimbDrift)
                {
                    track.LowY = at.y;
                    track.LowX = at.x;
                    track.LowAt = now;
                }
                else if (at.y - track.LowY >= ClimbHeight)
                {
                    track.Climbed = true;
                }
            }
            if (gone != null)
            {
                foreach (var wheel in gone)
                    Wheels.Remove(wheel);
            }
        }

        private static void FindWheels(float now)
        {
            var name = CloneName(Core.Tools.CogworkWheel, ref wheelName);
            var wheel = name == null ? null : GameObject.Find(name);
            if (wheel && wheel.activeInHierarchy && !Wheels.ContainsKey(wheel))
            {
                var at = wheel.transform.position;
                Wheels[wheel] = new WheelTrack { LowY = at.y, LowX = at.x, LowAt = now };
            }
        }

        public static bool WheelClimbed(GameObject source)
        {
            if (!source)
                return false;
            foreach (var pair in Wheels)
            {
                if (pair.Key && pair.Value.Climbed && source.transform.IsChildOf(pair.Key.transform))
                    return true;
            }
            return false;
        }

        // A pooled tool is a clone of its throw prefab.
        private static string CloneName(string tool, ref string cache)
        {
            if (cache != null)
                return cache;
            var item = ToolItemManager.GetToolByName(tool);
            var prefab = item ? item.Usage.ThrowPrefab : null;
            if (prefab)
                cache = prefab.name + "(Clone)";
            return cache;
        }

        // RETURN TO SENDER: an enemy shot Hornet's needle met. Shots that keep a FireballProjectile remember being
        // batted themselves; any other loose object she clashed with is remembered here for a few seconds.
        public static void Clashed(Component attack)
        {
            if (!attack)
                return;
            var root = attack.transform.root.gameObject;
            if (root.GetComponentInChildren<HealthManager>())
                return;
            Deflected[root] = Time.time;
            lastLooseClash = Time.time;
        }

        // Widow's bell may hurt her through her own FSM, with herself as the source: then only the timing tells.
        public static bool RecentLooseClash(float window) => Time.time - lastLooseClash <= window;

        // SHRAPNEL: a Sting Shard hit that does not come from the shard's body but from what it fires when broken.
        public static bool IsShrapnel(GameObject source)
        {
            var name = CloneName(Core.Tools.StingShard, ref shardName);
            if (!source || name == null)
                return false;
            for (var t = source.transform; t; t = t.parent)
            {
                if (t.name == name)
                    return false;
            }
            return true;
        }

        public static bool WasDeflected(GameObject source)
        {
            if (!source)
                return false;
            var fireball = source.GetComponentInParent<FireballProjectile>();
            if (fireball && FireballWasHit(fireball))
                return true;
            return Deflected.TryGetValue(source.transform.root.gameObject, out var at)
                && Time.time - at <= DeflectWindow;
        }

        public static void Clear()
        {
            BouncedRings.Clear();
            Wheels.Clear();
            Deflected.Clear();
        }
    }
}
