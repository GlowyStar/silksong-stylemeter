using System.Collections;
using UnityEngine;

namespace Stylemeter.Dev
{
    // The sandbox: a copy of the player's save in its own slot. While it is loaded and the bridge is on, Hornet cannot
    // die and the game cannot save; leaving it switches the cheats back off, so the player's own slots are untouched.
    internal static class Sandbox
    {
        public static int Slot = 3;
        // Off for screenshots and recordings that show the meter in play.
        public static bool IsLabelShown = true;
        private static bool cheatsOn;

        // invuln full|prevent|off: FullInvincible by default; PreventDeath lets hits land (dice, bell, mask tests) but
        // keeps one mask.
        public static CheatManager.InvincibilityStates Invuln = CheatManager.InvincibilityStates.FullInvincible;

        public static int CurrentProfile => GameManager.instance ? GameManager.instance.profileID : -1;

        public static bool InSandbox => Bridge.Enabled && CurrentProfile == Slot && HeroController.instance;

        // Any save while the sandbox slot is loaded is skipped (see SaveBlock).
        public static bool BlockSaves => Bridge.Enabled && CurrentProfile == Slot;

        public static void Tick()
        {
            if (InSandbox)
            {
                if (!cheatsOn)
                    ToolItemManager.IsInfiniteToolUseEnabled = true;
                cheatsOn = true;
                CheatManager.Invincibility = Invuln;
                CheatManager.CanChangeEquipsAnywhere = true;
            }
            else if (cheatsOn)
            {
                cheatsOn = false;
                CheatManager.Invincibility = CheatManager.InvincibilityStates.Off;
                Invuln = CheatManager.InvincibilityStates.FullInvincible;
                CheatManager.NailDamage = CheatManager.NailDamageStates.Normal;
                CheatManager.CanChangeEquipsAnywhere = false;
                ToolItemManager.IsInfiniteToolUseEnabled = false;
                Core.Tuning.DrainScale = 1f;
                Dummies.Freeze("off");
                Puppet.ReleaseAll();
            }
        }

        public static string Nail(string mode)
        {
            switch (mode.ToLowerInvariant())
            {
                case "insta": CheatManager.NailDamage = CheatManager.NailDamageStates.InstaKill; break;
                case "nodmg": CheatManager.NailDamage = CheatManager.NailDamageStates.NoDamage; break;
                default: CheatManager.NailDamage = CheatManager.NailDamageStates.Normal; break;
            }
            return "nail " + CheatManager.NailDamage;
        }

        public static string Load(int slot)
        {
            if (slot != Slot)
                return "refused: only the sandbox slot " + Slot;
            if (HeroController.instance)
                return "refused: a game is already loaded";
            // As the menu does it: UI input off, then the slot's own load (GameManager.LoadGameFromUI).
            InputHandler.Instance.StopUIInput();
            GameManager.instance.LoadGameFromUI(slot);
            return "loading slot " + slot;
        }

        // scene Song_09 left1: a normal transition through that gate, reported once Hornet stands in the new room.
        public static IEnumerator Scene(string name, string gate)
        {
            var gm = GameManager.instance;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = name,
                EntryGateName = gate,
                EntrySkip = true,
                PreventCameraFadeOut = true,
            });
            var start = Time.unscaledTime;
            yield return null;
            while (Time.unscaledTime - start < 40f)
            {
                var hero = HeroController.instance;
                if (!gm.IsInSceneTransition && hero && hero.isHeroInPosition && !hero.cState.transitioning
                    && gm.GameState == GlobalEnums.GameState.PLAYING)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return $"in {gm.GetSceneNameString()} "
                        + $"at {Bridge.V(HeroController.instance.transform.position)}";
                    yield break;
                }
                yield return null;
            }
            yield return "timeout, scene " + gm.GetSceneNameString();
        }

        public static string Gates(string scene)
        {
            var map = SceneTeleportMap.GetTeleportMap();
            if (map == null || !map.TryGetValue(scene, out var info) || info == null)
                return "unknown scene " + scene;
            return $"gates: {string.Join(" ", info.TransitionGates)} "
                + $"| respawns: {string.Join(" ", info.RespawnPoints)}";
        }
    }
}
