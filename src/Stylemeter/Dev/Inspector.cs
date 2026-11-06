using System;
using System.Linq;
using System.Reflection;
using InControl;
using UnityEngine;

namespace Stylemeter.Dev
{
    // Hotkeys and one-off dumps for development: F9 = nearest enemy, F10 = Hornet, crests, tools and key bindings.
    internal static class Inspector
    {
        private static bool dumpedOnce;
        private static bool keysBroken;

        // The game raises this after loading keyboard settings, after a rebind and after a soft reset.
        public static void WatchKeyboard() => InputHandler.OnUpdateHeroActions += DumpKeyboard;

        public static void Tick()
        {
            var hero = HeroController.instance;
            if (!hero)
                return;
            if (!dumpedOnce)
            {
                dumpedOnce = true;
                DumpGameplayNumbers();
                DumpHero();
            }
            if (keysBroken)
                return;
            try
            {
                if (Input.GetKeyDown(KeyCode.F9))
                    DumpNearestEnemy(hero);
                if (Input.GetKeyDown(KeyCode.F10))
                    DumpHero();
            }
            catch (Exception e)
            {
                keysBroken = true;
                Plugin.Log.LogWarning($"Hotkeys disabled, legacy input is unavailable: {e}");
            }
        }

        private static void DumpNearestEnemy(HeroController hero)
        {
            var heroPos = hero.transform.position;
            var nearest = HealthManager.EnumerateActiveEnemies()
                .Where(hm => hm)
                .OrderBy(hm => (hm.transform.position - heroPos).sqrMagnitude)
                .FirstOrDefault();
            if (!nearest)
            {
                Plugin.Out("F9 no enemy nearby");
                return;
            }
            var go = nearest.gameObject;
            Plugin.Out($"F9 {Game.Describe.Enemy(nearest)} path={Game.Describe.PathOf(go)} "
                + $"dist={Vector3.Distance(heroPos, nearest.transform.position):0.0} invincible={nearest.IsInvincible} "
                + $"invincibleFrom={nearest.InvincibleFromDirection}");
            Plugin.Out("F9 components: " + string.Join(", ",
                go.GetComponents<Component>().Where(c => c).Select(c => c.GetType().Name).Distinct()));
            foreach (var fsm in go.GetComponentsInChildren<PlayMakerFSM>(true))
                Plugin.Out($"F9 fsm {Game.Describe.PathOf(fsm.gameObject)} [{fsm.FsmName}] "
                    + $"state={fsm.ActiveStateName}");
        }

        private static void DumpHero()
        {
            var pd = PlayerData.instance;
            if (pd != null)
                Plugin.Out($"F10 hero crest={pd.CurrentCrestID} health={pd.health}/{pd.maxHealth} silk={pd.silk}");
            foreach (var crest in ToolItemManager.GetAllCrests().Where(c => c))
                Plugin.Out($"F10 crest {crest.name} equipped={crest.IsEquipped}");
            foreach (var tool in ToolItemManager.GetAllTools().Where(t => t))
                Plugin.Out($"F10 tool {tool.name} type={tool.Type} unlocked={tool.IsUnlocked} "
                    + $"equipped={tool.IsEquipped}");
            var input = InputHandler.Instance;
            if (input)
                DumpKeyboard(input.inputActions);
        }

        private static void DumpKeyboard(HeroActions actions)
        {
            var input = InputHandler.Instance;
            if (!input || actions == null)
                return;
            var named = new (string Name, PlayerAction Action)[]
            {
                ("Up", actions.Up), ("Down", actions.Down), ("Left", actions.Left), ("Right", actions.Right),
                ("Jump", actions.Jump), ("Attack", actions.Attack), ("Dash", actions.Dash), ("Cast", actions.Cast),
                ("SuperDash", actions.SuperDash), ("DreamNail", actions.DreamNail), ("QuickCast", actions.QuickCast),
                ("Taunt", actions.Taunt), ("QuickMap", actions.QuickMap), ("Inventory", actions.OpenInventory),
            };
            Plugin.Out("KEYS " + string.Join(", ",
                named.Select(n => $"{n.Name}={input.GetKeyBindingForAction(n.Action)}")));
        }

        // Crest and tool tuning (Hunter focus, Wanderer crits, Beast fury...) as the game itself reads it.
        private static void DumpGameplayNumbers()
        {
            var props = typeof(GlobalSettings.Gameplay).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(float)
                    || p.PropertyType == typeof(bool));
            foreach (var p in props)
            {
                try
                {
                    Plugin.Out($"GAMEPLAY {p.Name} = {p.GetValue(null)}");
                }
                catch (Exception e)
                {
                    Plugin.Out($"GAMEPLAY {p.Name} unreadable: {e.GetType().Name}");
                }
            }
        }
    }
}
