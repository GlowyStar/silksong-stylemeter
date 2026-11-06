using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Stylemeter.Dev
{
    // brawl <seconds> [left x] [right x]: a sparring bot for showcase recordings. Hornet goes for the nearest enemy and
    // fights it the way a player strings moves together: slashes on the ground and in the air, pogos off its head, a
    // dash in with a lunge, a silk skill or tool now and then. An enemy she cannot finish within a few seconds is left
    // alone, and so is one outside the arena, so she does not wander off through a door or onto a lift.
    internal static class Brawler
    {
        private const float Reach = 2.3f, GiveUpAfter = 6f, PogoAtMost = 2.5f;

        public static IEnumerator Fight(float seconds, float left, float right)
        {
            var hero = HeroController.instance;
            var body = hero.GetComponent<Rigidbody2D>();
            var ignored = new HashSet<HealthManager>();
            var end = Time.unscaledTime + seconds;
            HealthManager target = null;
            float chosenAt = 0f, nextLook = 0f, nextBeat = 0f, nextSkill = Time.unscaledTime + 3f;
            float lungeAt = -1f, pogoSince = -1f;
            var kills = 0;
            while (Time.unscaledTime < end && hero)
            {
                var now = Time.unscaledTime;
                if (target && target.GetIsDead())
                {
                    kills++;
                    target = null;
                }
                if (target && now - chosenAt > GiveUpAfter)
                {
                    ignored.Add(target);
                    target = null;
                }
                if (!target || now >= nextLook)
                {
                    var nearest = Nearest(hero.transform.position, ignored, left, right);
                    if (nearest != target)
                        chosenAt = now;
                    target = nearest;
                    nextLook = now + 0.5f;
                }
                if (!target)
                {
                    yield return null;
                    continue;
                }

                var offset = Middle(target) - (Vector2)hero.transform.position;
                var toward = offset.x > 0f ? "right" : "left";
                var grounded = hero.cState.onGround;
                if (lungeAt > 0f && now >= lungeAt)
                {
                    Buttons.Hold("attack", 0.06f);
                    lungeAt = -1f;
                }
                if (pogoSince > 0f)
                {
                    // Over the enemy: steer onto it and strike down while falling onto it, until back on the ground.
                    if (Mathf.Abs(offset.x) > 0.3f)
                        Buttons.Hold(toward, 0.05f);
                    var falling = body && body.linearVelocity.y < -0.5f;
                    if (falling && offset.y < -0.3f && offset.y > -3f && Mathf.Abs(offset.x) < 1.3f && now >= nextBeat)
                    {
                        Buttons.Hold("down+attack", 0.06f);
                        nextBeat = now + 0.25f;
                    }
                    if (grounded && now >= nextBeat || now - pogoSince > PogoAtMost)
                        pogoSince = -1f;
                }
                else if (Mathf.Abs(offset.x) > Reach || offset.y > 3.5f || offset.y < -2.5f)
                {
                    // Closing in: run, dash in and lunge from afar, jump to what flies.
                    Buttons.Hold(toward, 0.1f);
                    if (grounded && now >= nextBeat && Mathf.Abs(offset.x) > 5f)
                    {
                        Buttons.Hold("dash", 0.08f);
                        if (Mathf.Abs(offset.x) < 8f)
                            lungeAt = now + 0.2f;
                        nextBeat = now + 1f;
                    }
                    else if (grounded && now >= nextBeat && offset.y > 2f && Mathf.Abs(offset.x) < 4f)
                    {
                        Buttons.Hold("jump", 0.4f);
                        nextBeat = now + 0.7f;
                    }
                }
                else if (now >= nextBeat)
                {
                    if (offset.x > 0.2f)
                        hero.FaceRight();
                    else if (offset.x < -0.2f)
                        hero.FaceLeft();
                    var roll = Random.value;
                    if (grounded && Mathf.Abs(offset.y) < 1.2f && roll < 0.25f)
                    {
                        Buttons.Hold("jump", 0.35f);
                        pogoSince = now;
                        nextBeat = now + 0.35f;
                    }
                    else if (now >= nextSkill && roll < 0.4f)
                    {
                        Puppet.UseTool(Random.value < 0.5f ? "neutral" : "up");
                        nextSkill = now + 4f;
                        nextBeat = now + 0.5f;
                    }
                    else
                    {
                        var aim = offset.y > 1.2f ? "up+attack"
                            : !grounded && offset.y < -0.8f ? "down+attack"
                            : "attack";
                        Buttons.Hold(aim, 0.06f);
                        nextBeat = now + 0.3f;
                    }
                }
                yield return null;
            }
            Buttons.ReleaseAll();
            yield return $"{kills} kills";
        }

        private static HealthManager Nearest(Vector2 from, HashSet<HealthManager> ignored, float left, float right)
        {
            HealthManager best = null;
            var bestDistance = 30f;
            foreach (var enemy in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (!enemy.isActiveAndEnabled || enemy.GetIsDead() || ignored.Contains(enemy))
                    continue;
                var middle = Middle(enemy);
                var distance = Vector2.Distance(from, middle);
                if (distance < bestDistance && middle.x >= left && middle.x <= right)
                {
                    best = enemy;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static Vector2 Middle(HealthManager enemy)
        {
            var box = enemy.GetComponent<Collider2D>();
            return box ? (Vector2)box.bounds.center : (Vector2)enemy.transform.position;
        }
    }
}
