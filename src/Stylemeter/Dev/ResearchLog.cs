using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Stylemeter.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stylemeter.Dev
{
    // Development logger for the names that new bonuses are built on: game-wide events, enemy and tool FSM states,
    // boss title cards.
    [HarmonyPatch]
    internal static class ResearchLog
    {
        private const string Guard = "research log";

        private static readonly Dictionary<Fsm, bool> Watched = new Dictionary<Fsm, bool>();

        public static void OnSceneChanged(Scene from, Scene to)
        {
            Watched.Clear();
            if (DevTools.LogResearch.Value)
                Plugin.Out($"SCENE {to.name}");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EventRegister), nameof(EventRegister.SendEvent),
            new[] { typeof(string), typeof(GameObject) })]
        private static void BeforeSendEvent(string eventName)
        {
            if (DevTools.LogResearch.Value)
                HookGuard.Run(Guard, () => Plugin.Out($"EVENT {eventName}"));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Fsm), "EnterState")]
        private static void AfterEnterState(Fsm __instance, FsmState state)
        {
            if (!DevTools.LogResearch.Value || state == null)
                return;
            HookGuard.Run(Guard, () =>
            {
                if (IsWatched(__instance))
                    Plugin.Out($"FSM {Describe.PathOf(__instance.GameObject)} [{__instance.Name}] -> {state.Name}");
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DisplayBossTitle), nameof(DisplayBossTitle.OnEnter))]
        private static void AfterBossTitle(DisplayBossTitle __instance)
        {
            if (DevTools.LogResearch.Value)
                HookGuard.Run(Guard, () => LogBossTitle(__instance));
        }

        private static void LogBossTitle(DisplayBossTitle title)
        {
            var owner = title.Owner;
            var hm = owner ? owner.GetComponentInParent<HealthManager>() : null;
            if (!hm && owner)
                hm = owner.GetComponentInChildren<HealthManager>();
            Plugin.Out($"BOSS title={title.bossTitle?.Value} owner={Describe.PathOf(owner)} "
                + $"enemy={(hm ? Describe.Enemy(hm) : "-")}");
        }

        // Enemy FSMs (stun, shell, theft, heal states) and FSMs on the hero's tool objects (Cogwork Wheel).
        private static bool IsWatched(Fsm fsm)
        {
            if (Watched.TryGetValue(fsm, out var watched))
                return watched;
            var go = fsm.GameObject;
            watched = go && (go.GetComponentInParent<HealthManager>() || IsToolObject(go));
            Watched[fsm] = watched;
            return watched;
        }

        private static bool IsToolObject(GameObject go)
        {
            var damager = go.GetComponentInChildren<DamageEnemies>(true);
            return damager && damager.RepresentingTool;
        }
    }
}
