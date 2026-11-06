using HarmonyLib;
using Stylemeter.Game;
using UnityEngine;

namespace Stylemeter.Dev
{
    // Development logger: prints the raw game events the style meter is built on.
    [HarmonyPatch]
    internal static class EventLog
    {
        private const string Guard = "event log";

        private static readonly AccessTools.FieldRef<HeroController, ToolItem> WillThrowTool =
            GameField.Find<HeroController, ToolItem>("willThrowTool");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
        private static void AfterHit(HealthManager __instance, HitInstance hitInstance)
        {
            if (!DevTools.LogEvents.Value)
                return;
            HookGuard.Run(Guard, () =>
            {
                var tool = hitInstance.RepresentingTool ? hitInstance.RepresentingTool.name : "-";
                Plugin.Out($"HIT {__instance.name} hp={__instance.hp} type={hitInstance.AttackType} "
                    + $"dmg={hitInstance.DamageDealt} tool=\"{tool}\" element={hitInstance.NailElement} "
                    + $"src={Describe.PathOf(hitInstance.Source)} "
                    + $"dir={hitInstance.Direction:0} crit={hitInstance.CriticalHit} rage={hitInstance.RageHit} "
                    + $"harpoon={hitInstance.IsHarpoon} heroDmg={hitInstance.IsHeroDamage} "
                    + $"special={hitInstance.SpecialType}");
            });
        }

        // Follow-up damage (fire, poison, dazzle lag hits) that bypasses Hit.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.ApplyExtraDamage), new[] { typeof(HitInstance) })]
        private static void AfterExtraHit(HealthManager __instance, HitInstance hitInstance)
        {
            if (!DevTools.LogEvents.Value)
                return;
            HookGuard.Run(Guard, () => Plugin.Out(
                $"EXTRA {__instance.name} hp={__instance.hp} dmg={hitInstance.DamageDealt} "
                + $"type={hitInstance.AttackType} element={hitInstance.NailElement} "
                + $"src={Describe.PathOf(hitInstance.Source)}"));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.ApplyExtraDamage), new[] { typeof(int) })]
        private static void AfterExtraDamage(HealthManager __instance, int damageAmount)
        {
            if (DevTools.LogEvents.Value)
                HookGuard.Run(Guard, () =>
                    Plugin.Out($"EXTRA {__instance.name} hp={__instance.hp} dmg={damageAmount}"));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die), new[]
        {
            typeof(float?), typeof(AttackTypes), typeof(NailElements), typeof(GameObject),
            typeof(bool), typeof(float), typeof(bool), typeof(bool),
        })]
        private static void BeforeDie(HealthManager __instance, AttackTypes attackType, NailElements nailElement,
            GameObject damageSource)
        {
            if (!DevTools.LogEvents.Value)
                return;
            HookGuard.Run(Guard, () =>
            {
                if (!__instance.GetIsDead())
                    Plugin.Out($"KILL {Describe.Enemy(__instance)} by={attackType} element={nailElement} "
                        + $"src={Describe.PathOf(damageSource)}");
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.ApplyTagDamage))]
        private static void AfterTagDamage(HealthManager __instance)
        {
            if (!DevTools.LogEvents.Value)
                return;
            HookGuard.Run(Guard, () =>
            {
                if (__instance.hp <= 0)
                    Plugin.Out($"TAGKILL {__instance.name}");
            });
        }

        // The enemy attack Hornet's needle clashed with (DamageHero.NailClash calls HeroController.NailParry).
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DamageHero), "NailClash")]
        private static void BeforeNailClash(DamageHero __instance, string colliderTag)
        {
            if (!DevTools.LogEvents.Value || colliderTag != "Nail Attack")
                return;
            HookGuard.Run(Guard, () =>
            {
                var enemy = EnemyFinder.Owner(__instance);
                Plugin.Out($"CLASH enemy={(enemy ? enemy.name : "-")} attack={Describe.PathOf(__instance.gameObject)}");
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.SendHeroDamagedEvent),
            new[] { typeof(HeroController.DamageInfo) })]
        private static void AfterHeroDamaged(HeroController.DamageInfo damageInfo)
        {
            if (DevTools.LogEvents.Value)
                HookGuard.Run(Guard, () => Plugin.Out($"HURT hazard={damageInfo.hazardType}"));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "ThrowTool")]
        private static void BeforeThrowTool(HeroController __instance)
        {
            if (!DevTools.LogEvents.Value)
                return;
            HookGuard.Run(Guard, () =>
            {
                var tool = WillThrowTool(__instance);
                if (tool)
                    Plugin.Out($"TOOL {tool.name} type={tool.Type}");
            });
        }
    }
}
