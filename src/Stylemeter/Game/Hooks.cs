using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using Stylemeter.Core;
using UnityEngine;

namespace Stylemeter.Game
{
    // Hooks are split into groups that are patched one by one, so a renamed game method costs one group, not the mod.

    // Hits and deaths. A kill runs Die inside HealthManager.Hit, so the hit is kept from the prefix and sent once:
    // from Die when it kills, or from the postfix when it only wounds. Hits that are not Hornet's (spikes, lava,
    // another enemy) go to the engine as world hits.
    [HarmonyPatch]
    internal static class HitHooks
    {
        private sealed class PendingHit
        {
            public HealthManager Target;
            public HitInstance Hit;
            public int HpBefore;
            public int Frame;
            public bool Sent;
            public HitContext Context;
        }

        private static readonly Stack<PendingHit> Pending = new Stack<PendingHit>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
        private static void BeforeHit(HealthManager __instance, HitInstance hitInstance) =>
            HookGuard.Run("hits", () => Remember(__instance, hitInstance));

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
        private static void AfterHit(HealthManager __instance) => HookGuard.Run("hits", () => SendWound(__instance));

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die), new[]
        {
            typeof(float?), typeof(AttackTypes), typeof(NailElements), typeof(GameObject),
            typeof(bool), typeof(float), typeof(bool), typeof(bool),
        })]
        private static void BeforeDie(HealthManager __instance, AttackTypes attackType, NailElements nailElement) =>
            HookGuard.Run("deaths", () => SendDeath(__instance, attackType, nailElement));

        private static void Remember(HealthManager hm, HitInstance hit)
        {
            // Entries from an earlier frame were left by a hit that threw before its postfix.
            while (Pending.Count > 0 && Pending.Peek().Frame != Time.frameCount)
                Pending.Pop();
            Pending.Push(new PendingHit
            {
                Target = hm,
                Hit = hit,
                HpBefore = hm.hp,
                Frame = Time.frameCount,
                Context = HookGuard.Get("hit context", () => HitContext.Read(hm, hit), default(HitContext)),
            });
        }

        private static void SendWound(HealthManager hm)
        {
            if (Pending.Count == 0)
                return;
            var p = Pending.Pop();
            if (p.Sent || p.Target != hm || hm.hp >= p.HpBefore)
                return;
            if (p.Hit.IsHeroDamage)
                EngineHost.Hit(hm, p.Hit, p.HpBefore, hm.hp, killed: false, p.Context);
            else
                EngineHost.WorldHit(hm, p.Hit, killed: false);
        }

        private static void SendDeath(HealthManager hm, AttackTypes attackType, NailElements nailElement)
        {
            if (hm.GetIsDead())
                return;
            var p = Pending.Count > 0 ? Pending.Peek() : null;
            if (p != null && !p.Sent && p.Target == hm)
            {
                p.Sent = true;
                if (p.Hit.IsHeroDamage)
                    EngineHost.Hit(hm, p.Hit, p.HpBefore, hm.hp, killed: true, p.Context);
                else
                    EngineHost.WorldHit(hm, p.Hit, killed: true);
                return;
            }
            EngineHost.EnemyDied(hm, TickHooks.CauseOf(hm, attackType, nailElement));
        }
    }

    // Damage over time: tag ticks (poison, lightning) and lag hits (Flintslate fire, Witch poison, Claw Mirror). A kill
    // by them runs Die inside these calls, so the context set here tells Die that the death was Hornet's, and of what.
    [HarmonyPatch]
    internal static class TickHooks
    {
        private static HealthManager target;
        private static DeathCause cause;
        private static int depth;
        private static int frame = -1;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.ApplyTagDamage))]
        private static void BeforeTag(HealthManager __instance, DamageTag.DamageTagInstance damageTagInstance) =>
            HookGuard.Run("damage ticks",
                () => Enter(__instance, damageTagInstance.isHeroDamage, ElementOf(damageTagInstance)));

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.ApplyTagDamage))]
        private static void AfterTag() => HookGuard.Run("damage ticks", Exit);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.ApplyExtraDamage), new[] { typeof(HitInstance) })]
        private static void BeforeExtra(HealthManager __instance, HitInstance hitInstance) =>
            HookGuard.Run("damage ticks",
                () => Enter(__instance, hitInstance.IsHeroDamage, EngineHost.ElementOf(hitInstance)));

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.ApplyExtraDamage), new[] { typeof(HitInstance) })]
        private static void AfterExtra() => HookGuard.Run("damage ticks", Exit);

        public static DeathCause CauseOf(HealthManager hm, AttackTypes attackType, NailElements nailElement)
        {
            if (hm == target && frame == Time.frameCount)
                return cause;
            return new DeathCause { Element = EngineHost.ElementOf(nailElement, attackType) };
        }

        private static Element ElementOf(DamageTag.DamageTagInstance tag) =>
            tag.specialDamageType == DamageTag.SpecialDamageTypes.Lightning
                ? Element.Lightning
                : EngineHost.ElementOf(tag.nailElements, AttackTypes.Generic);

        // Tag damage applies its ticks through ApplyExtraDamage, so the outer call sets the context.
        private static void Enter(HealthManager hm, bool byHornet, Element element)
        {
            if (frame != Time.frameCount)
                depth = 0;
            frame = Time.frameCount;
            if (depth++ > 0)
                return;
            target = hm;
            cause = new DeathCause { ByHornet = byHornet, Element = element };
            if (byHornet && element != Element.None)
                EngineHost.TickDamage(hm, element);
        }

        private static void Exit()
        {
            if (--depth > 0)
                return;
            depth = 0;
            target = null;
        }
    }

    // Hornet's public methods: damage taken, bounces, taunt and bind.
    [HarmonyPatch]
    internal static class HeroHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.SendHeroDamagedEvent),
            new[] { typeof(HeroController.DamageInfo) })]
        private static void AfterHeroDamaged(HeroController.DamageInfo damageInfo) =>
            HookGuard.Run("hurt", () => EngineHost.HeroDamaged(damageInfo.hazardType));

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.DownspikeBounce))]
        private static void AfterDownspikeBounce() => HookGuard.Run("pogo", EngineHost.PogoBounce);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.DownspikeBounceShort))]
        private static void AfterDownspikeBounceShort() => HookGuard.Run("pogo", EngineHost.PogoBounce);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.DownspikeBounceSlightlyShort))]
        private static void AfterDownspikeBounceSlightlyShort() => HookGuard.Run("pogo", EngineHost.PogoBounce);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.ShroomBounce))]
        private static void AfterShroomBounce() => HookGuard.Run("spring", EngineHost.SpringBounce);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.BounceHigh))]
        private static void AfterBounceHigh() => HookGuard.Run("spring", EngineHost.SpringBounce);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.SilkTaunted))]
        private static void AfterSilkTaunted() => HookGuard.Run("taunt", EngineHost.Taunted);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.SilkTauntEffectConsume))]
        private static void AfterTauntConsume(bool __result)
        {
            if (__result)
                HookGuard.Run("taunt", EngineHost.TauntSpent);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.BindCompleted))]
        private static void AfterBindCompleted() => HookGuard.Run("bind", EngineHost.BindCompleted);
    }

    // Hornet's private movement methods: the riskiest names, so they live alone.
    [HarmonyPatch]
    internal static class MoveHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "DoWallJump")]
        private static void AfterWallJump() => HookGuard.Run("wall jump", EngineHost.WallJump);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "DoDoubleJump")]
        private static void AfterDoubleJump() => HookGuard.Run("double jump", EngineHost.DoubleJump);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "HeroDash")]
        private static void AfterDash(HeroController __instance)
        {
            if (__instance.dashingDown)
                HookGuard.Run("down dash", EngineHost.DownDash);
        }
    }

    // Tools and skills: every throw or cast goes through ThrowTool, and Flintslate's fire ends in SetElement.
    [HarmonyPatch]
    internal static class ToolHooks
    {
        private static readonly AccessTools.FieldRef<HeroController, ToolItem> WillThrowTool =
            GameField.Find<HeroController, ToolItem>("willThrowTool");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "ThrowTool")]
        private static void BeforeThrowTool(HeroController __instance, bool isAutoThrow) => HookGuard.Run("tools", () =>
        {
            var tool = WillThrowTool(__instance);
            if (tool)
                EngineHost.ToolUsed(tool, isAutoThrow);
        });

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroNailImbuement), nameof(HeroNailImbuement.SetElement))]
        private static void BeforeSetElement(HeroNailImbuement __instance, NailElements element) =>
            HookGuard.Run("flintslate", () =>
            {
                if (__instance.CurrentElement == NailElements.Fire && element != NailElements.Fire)
                    EngineHost.FlintslateExpired();
            });
    }

    // Cross Stitch parries and Scuttlebrace evades, both decided in CheckParry; the evade flag is private.
    [HarmonyPatch]
    internal static class ParryHooks
    {
        private struct ParryState
        {
            public bool Parrying;
            public bool EvadeClashed;
        }

        private static readonly AccessTools.FieldRef<HeroController, bool> EvadeClashed =
            GameField.Find<HeroController, bool>("evadingDidClash");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CheckParry))]
        private static void BeforeCheckParry(HeroController __instance, out ParryState __state)
        {
            var hero = __instance;
            __state = new ParryState
            {
                Parrying = HookGuard.Get("parry", () => hero.cState.parrying, false),
                EvadeClashed = HookGuard.Get("evade", () => EvadeClashed(hero), false),
            };
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CheckParry))]
        private static void AfterCheckParry(HeroController __instance, DamageHero damageHero, ParryState __state)
        {
            HookGuard.Run("parry", () =>
            {
                if (__state.Parrying && !__instance.cState.parrying && __instance.cState.parryAttack)
                    EngineHost.Parried(EnemyFinder.Owner(damageHero));
            });
            HookGuard.Run("evade", () =>
            {
                if (!__state.EvadeClashed && EvadeClashed(__instance))
                    EngineHost.Evaded(EnemyFinder.Owner(damageHero));
            });
        }
    }

    // The world around Hornet: clashes with enemy attacks, game events, arenas and boss title cards.
    [HarmonyPatch]
    internal static class WorldHooks
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DamageHero), "NailClash")]
        private static void BeforeNailClash(DamageHero __instance, string colliderTag)
        {
            if (colliderTag != "Nail Attack")
                return;
            HookGuard.Run("deflect", () => ToolWatch.Clashed(__instance));
            // An enemy's parry counter (the Reed's "ParrySlash Hit") is a clash with a parrying enemy: PARRY THIS!.
            var parryCounter = HookGuard.Get("parry this",
                () => Describe.PathOf(__instance.gameObject).Contains("Parry"), false);
            HookGuard.Run("clash", () => EngineHost.Clash(EnemyFinder.Owner(__instance), parryCounter));
        }

        private static readonly Dictionary<string, Action> GameEvents = new Dictionary<string, Action>
        {
            ["STARTED BIND"] = EngineHost.BindStarted,
            ["RESERVE BIND USED"] = EngineHost.ReserveBind,
            ["BROLLY START"] = EngineHost.GlideStarted,
            ["BROLLY END"] = EngineHost.GlideEnded,
            ["BOSS DEATH"] = EngineHost.BossDeath,
        };

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EventRegister), nameof(EventRegister.SendEvent),
            new[] { typeof(string), typeof(GameObject) })]
        private static void BeforeSendEvent(string eventName)
        {
            if (eventName != null && GameEvents.TryGetValue(eventName, out var handler))
                HookGuard.Run("game events", handler);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BattleScene), nameof(BattleScene.StartBattle))]
        private static void AfterStartBattle() => HookGuard.Run("arena", EngineHost.ArenaStarted);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BattleScene), nameof(BattleScene.BattleCompleted))]
        private static void AfterBattleCompleted() => HookGuard.Run("arena", EngineHost.ArenaCleared);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DisplayBossTitle), nameof(DisplayBossTitle.OnEnter))]
        private static void AfterBossTitle(DisplayBossTitle __instance) =>
            HookGuard.Run("boss title", () => MarkBoss(__instance));

        private static void MarkBoss(DisplayBossTitle title)
        {
            var owner = title.Owner;
            if (!owner)
                return;
            var hm = owner.GetComponentInParent<HealthManager>();
            if (!hm)
                hm = owner.GetComponentInChildren<HealthManager>();
            if (hm)
                EngineHost.BossTitle(hm, BossKey(title.bossTitle?.Value, hm));
        }

        // Two fights share a title card: Lace (first and second fight) and Moss Mother (alone, then two at once). The
        // Void Moss Mother of Act 3 has no title of her own either: she is the one in black thread.
        private static string BossKey(string title, HealthManager hm)
        {
            var pd = PlayerData.instance;
            if (pd == null || string.IsNullOrEmpty(title))
                return title;
            if (title == "LACE")
                return pd.defeatedLace1 ? "LACE_2" : "LACE_1";
            if (title == "MOSSBONE_MOTHER" && IsVoid(hm))
                return BossBonuses.VoidMossMother;
            if (title == "MOSSBONE_MOTHER" && pd.defeatedMossMother)
                return "MOSSBONE_MOTHER_2";
            return title;
        }

        private static bool IsVoid(HealthManager hm)
        {
            var thread = hm.GetComponent<BlackThreadState>();
            return (thread && thread.IsBlackThreaded) || hm.name.Contains("Void") || hm.name.Contains("Abyss");
        }
    }

    // Hornet's defences, all decided inside HeroController.TakeDamage: Magnetite Dice zero a hit and reset their
    // counter, the Warding Bell shield is asked whether it takes the hit, and darts pass through attacks while every
    // overlap still calls TakeDamage.
    [HarmonyPatch]
    internal static class DefenseHooks
    {
        private static readonly AccessTools.FieldRef<HeroController, int> DiceHits =
            GameField.Find<HeroController, int>("luckyDiceShieldedHits");

        // The frame of the TakeDamage call in progress; a call that threw past its postfix expires with its frame.
        private static int takeDamageFrame = -1;
        private static GameObject attacker;

        // The object of the last TakeDamage call (who hurt Hornet, for STOP, THIEF!).
        public static GameObject LastAttacker => attacker;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        private static void BeforeTakeDamage(HeroController __instance, GameObject go, int damageAmount,
            GlobalEnums.HazardType hazardType, out int __state)
        {
            takeDamageFrame = Time.frameCount;
            attacker = go;
            var hero = __instance;
            __state = HookGuard.Get("dice", () => DiceHits(hero), 0);
            HookGuard.Run("dart", () => HeroWatch.Touched(go, hazardType, damageAmount));
        }

        // Dice that roll and fail count up; a roll that saves sets the count back to 0. A first hit can never be
        // saved (its chance is 0), so a save always shows as a count that dropped to 0.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        private static void AfterTakeDamage(HeroController __instance, GlobalEnums.HazardType hazardType, int __state)
        {
            takeDamageFrame = -1;
            HookGuard.Run("dice", () =>
            {
                var dice = GlobalSettings.Gameplay.LuckyDiceTool;
                if (__state > 0 && DiceHits(__instance) == 0 && hazardType == GlobalEnums.HazardType.ENEMY
                    && dice && dice.IsEquipped)
                    EngineHost.DiceSaved();
            });
        }

        // TakeDamage asks the bell only on the path where the bell then takes the hit; grabs ask with sendEvent.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.WillDoBellBindHit), new[] { typeof(bool) })]
        private static void AfterBellCheck(bool sendEvent, bool __result)
        {
            if (__result && (sendEvent || takeDamageFrame == Time.frameCount))
                HookGuard.Run("bell",
                    () => EngineHost.BellBlocked(attacker ? EnemyFinder.Owner(attacker.transform) : null));
        }

        // Fractured Mask turns a lethal hit into one that leaves a single mask.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.TakeHealth),
            new[] { typeof(int), typeof(bool), typeof(bool) })]
        private static void BeforeTakeHealth(PlayerData __instance, int amount, out bool __state)
        {
            var pd = __instance;
            __state = HookGuard.Get("mask", () =>
            {
                var mask = GlobalSettings.Gameplay.FracturedMaskTool;
                return pd.healthBlue <= 0 && amount > 0 && pd.health - amount <= 0
                    && mask && mask.IsEquipped && mask.SavedData.AmountLeft > 0;
            }, false);
        }

        // Only when the mask broke: a hit in the middle of a multi-hit attack (DamagePropertyFlags.MidChain) is stopped
        // at one mask too but leaves the mask whole, and the last hit of the chain breaks it.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.TakeHealth),
            new[] { typeof(int), typeof(bool), typeof(bool) })]
        private static void AfterTakeHealth(PlayerData __instance, bool __state)
        {
            if (!__state || __instance.health <= 0)
                return;
            HookGuard.Run("mask", () =>
            {
                var mask = GlobalSettings.Gameplay.FracturedMaskTool;
                if (mask && mask.SavedData.AmountLeft <= 0)
                    EngineHost.MaskSaved();
            });
        }
    }

    // BLUEPRINT: the Architect has no wall slash. HeroController.Attack turns her round, drops her off the wall and
    // swings an ordinary slash; the slash it picked is remembered as one swung from the wall.
    [HarmonyPatch]
    internal static class WallAttackHooks
    {
        private static readonly AccessTools.FieldRef<HeroController, NailSlash> CurrentSlash =
            GameField.Find<HeroController, NailSlash>("_slashComponent");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "Attack")]
        private static void BeforeAttack(HeroController __instance, GlobalEnums.AttackDirection attackDir,
            out bool __state)
        {
            var hc = __instance;
            __state = HookGuard.Get("blueprint", () => attackDir == GlobalEnums.AttackDirection.normal
                && (hc.cState.wallSliding || hc.cState.wallScrambling)
                && GlobalSettings.Gameplay.ToolmasterCrest.IsEquipped, false);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "Attack")]
        private static void AfterAttack(HeroController __instance, bool __state)
        {
            if (!__state)
                return;
            var hc = __instance;
            HookGuard.Run("blueprint", () => HeroWatch.WallAttack(CurrentSlash(hc), Time.time));
        }
    }

    // The Architect's quick craft refills tools through TryReplenishTools; it returns true even when nothing was
    // refilled, so the charges are counted before and after.
    [HarmonyPatch]
    internal static class CraftHooks
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.TryReplenishTools))]
        private static void BeforeReplenish(out int __state) => __state = HookGuard.Get("restock", EquippedCharges, 0);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.TryReplenishTools))]
        private static void AfterReplenish(bool doReplenish, ToolItemManager.ReplenishMethod method, bool __result,
            int __state)
        {
            if (!doReplenish || method != ToolItemManager.ReplenishMethod.QuickCraft || !__result)
                return;
            var before = __state;
            HookGuard.Run("restock", () =>
            {
                var gained = EquippedCharges() - before;
                if (gained > 0)
                    EngineHost.Restocked(gained);
            });
        }

        private static int EquippedCharges()
        {
            var pd = PlayerData.instance;
            var tools = pd == null ? null : ToolItemManager.GetEquippedToolsForCrest(pd.CurrentCrestID);
            var sum = 0;
            if (tools != null)
            {
                foreach (var tool in tools)
                {
                    if (tool)
                        sum += tool.SavedData.AmountLeft;
                }
            }
            return sum;
        }
    }

    // Snitch Pick: a Clawline hit lets Hornet take rosaries out of the enemy (HealthManager.StealCurrency).
    [HarmonyPatch]
    internal static class StealHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.StealCurrency))]
        private static void AfterSteal(HealthManager __instance, ref int stolenGeo, ref int flingGeo)
        {
            var rosaries = stolenGeo + flingGeo;
            var enemy = __instance;
            if (rosaries > 0)
                HookGuard.Run("pickpocket", () => EngineHost.Pickpocket(enemy, rosaries));
        }
    }

    // A down strike on spikes tinks off their TinkEffect and bounces Hornet (BED OF NAILS).
    [HarmonyPatch]
    internal static class TinkHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TinkEffect), nameof(TinkEffect.Hit))]
        private static void AfterTinkHit(TinkEffect __instance, HitInstance damageInstance) =>
            HookGuard.Run("spike pogo", () =>
            {
                var hero = HeroController.instance;
                if (!hero || !hero.cState.downAttacking || !damageInstance.IsNailDamage)
                    return;
                var spikes = __instance.GetComponent<DamageHero>();
                if (spikes && (spikes.hazardType == GlobalEnums.HazardType.SPIKES
                    || spikes.hazardType == GlobalEnums.HazardType.COAL_SPIKES))
                    EngineHost.SpikeTink();
            });
    }

    // Throwing Ring: its terrain collisions are wall bounces (TRICK SHOT); a pooled ring starts clean on enable.
    [HarmonyPatch]
    internal static class RingHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ToolRing), "OnCollisionEnter2D")]
        private static void AfterRingCollision(ToolRing __instance, Collision2D collision) =>
            HookGuard.Run("trick shot", () => ToolWatch.RingTouchedTerrain(__instance, collision));

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ToolRing), "OnEnable")]
        private static void AfterRingEnable(ToolRing __instance) =>
            HookGuard.Run("trick shot", () => ToolWatch.RingThrown(__instance));
    }
}
