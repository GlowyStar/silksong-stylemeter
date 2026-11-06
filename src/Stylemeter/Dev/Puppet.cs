using System.Collections;
using UnityEngine;

namespace Stylemeter.Dev
{
    // Hornet moved and set up by the tester: position, facing, silk and health. Buttons are pressed by Buttons.
    internal static class Puppet
    {
        public static string Teleport(float x, float y)
        {
            var hero = HeroController.instance;
            if (!hero)
                return "no hero";
            hero.transform.position = new Vector3(x, y, hero.transform.position.z);
            var body = hero.GetComponent<Rigidbody2D>();
            if (body)
                body.linearVelocity = Vector2.zero;
            return "hornet at " + Bridge.V(hero.transform.position);
        }

        public static string Face(string side)
        {
            var hero = HeroController.instance;
            if (side == "left")
                hero.FaceLeft();
            else
                hero.FaceRight();
            return "facing " + side;
        }

        // There is no SetSilk: the game itself takes everything, then adds.
        public static string Silk(int amount)
        {
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            hero.TakeSilk(999);
            if (amount > 0)
                hero.AddSilk(Mathf.Min(amount, pd.CurrentSilkMax), false);
            return $"silk {pd.silk}/{pd.CurrentSilkMax}";
        }

        public static string Health(int amount)
        {
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            hero.RefillHealthToMax();
            var take = pd.health - Mathf.Clamp(amount, 1, pd.CurrentMaxHealth);
            if (take > 0)
                hero.TakeHealth(take);
            return $"health {pd.health}/{pd.CurrentMaxHealth}";
        }

        public static void ReleaseAll() => Buttons.ReleaseAll();

        // glide [s]: the jump held while falling with the double jump spent, which is when the game opens the brolly
        // (HeroController: Jump.WasPressed -> CanFloat -> StartFloat). Fails with the reason where the game would not.
        public static string Glide(float seconds)
        {
            var hero = HeroController.instance;
            var cs = hero.cState;
            var can = HarmonyLib.Traverse.Create(hero).Method("CanFloat", new[] { typeof(bool) }).GetValue<bool>(true);
            if (!can)
                return $"cannot glide: ground={cs.onGround} doubleJumping={cs.doubleJumping} dashing={cs.dashing} "
                    + $"attacking={cs.attacking}";
            Buttons.Hold("jump", seconds);
            return "holding jump " + seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // autopogo <enemy> <seconds>: Hornet kept over the enemy, striking down whenever she falls onto it. A pogo
        // chain needs frame timing that a script from outside cannot give.
        public static IEnumerator AutoPogo(string which, float seconds)
        {
            var target = Dummies.Find(which);
            if (!target)
            {
                yield return "no such enemy " + which;
                yield break;
            }
            var hero = HeroController.instance;
            var body = hero.GetComponent<Rigidbody2D>();
            var at = target.transform.position;
            hero.transform.position = new Vector3(at.x, at.y + 3f, hero.transform.position.z);
            var start = Time.unscaledTime;
            var lastStrike = -1f;
            var strikes = 0;
            Buttons.Hold("down", seconds + 0.5f);
            while (Time.unscaledTime - start < seconds && target && !target.GetIsDead())
            {
                at = target.transform.position;
                var pos = hero.transform.position;
                hero.transform.position = new Vector3(at.x, pos.y, pos.z);
                var falling = body && body.linearVelocity.y < 0f;
                var above = pos.y - at.y;
                if (!hero.cState.onGround && falling && above > 0.5f && above < 2.6f
                    && Time.unscaledTime - lastStrike > 0.25f)
                {
                    lastStrike = Time.unscaledTime;
                    strikes++;
                    Buttons.Hold("attack", 0.06f);
                }
                yield return null;
            }
            Buttons.Release("down+attack");
            yield return $"{strikes} strikes";
        }

        // With the KeyboardToolRebinds mod, direction + tool button always gives the neutral slot, and the up and down
        // tools have their own keys, which do exactly this (GetBoundAttackTool, CanThrowTool, ThrowTool).
        public static string UseTool(string direction)
        {
            var hero = HeroController.instance;
            var binding = direction == "up" ? AttackToolBinding.Up
                : direction == "down" ? AttackToolBinding.Down
                : AttackToolBinding.Neutral;
            var tool = ToolItemManager.GetBoundAttackTool(binding, ToolEquippedReadSource.Active, out var used);
            if (!tool)
                return "no tool on " + direction;
            var self = HarmonyLib.Traverse.Create(hero);
            self.Field("willThrowTool").SetValue(tool);
            var can = self.Method("CanThrowTool", new[] { typeof(ToolItem), typeof(AttackToolBinding), typeof(bool) })
                .GetValue<bool>(tool, used, true);
            if (!can)
                return "cannot throw " + tool.name;
            self.Method("ThrowTool", new[] { typeof(bool) }).GetValue(false);
            return "threw " + tool.name;
        }
    }
}
