using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Stylemeter.Game
{
    // Hornet's states that only show frame by frame: how long the needle charge was held (PATIENCE), Sharpdart darts
    // and the attacks she darted through (THREAD THE NEEDLE), and the Shaman bind slam (DESOLATE DIVE).
    internal static class HeroWatch
    {
        private const float ChargeReleaseWindow = 1f;
        private const float DiveMinHeight = 4f;
        private const float DiveRange = 3f;
        private const float WallAttackWindow = 0.5f;

        private static readonly AccessTools.FieldRef<HeroController, float> NailChargeTimer =
            GameField.Find<HeroController, float>("nailChargeTimer");

        private static HeroController hero;
        private static Transform dartDamager;
        private static bool charging;
        private static float chargeHeld;
        private static float chargeReleasedAt = float.NegativeInfinity;
        private static bool darting;
        private static int dartId;
        private static readonly Dictionary<HealthManager, int> Dodged = new Dictionary<HealthManager, int>();
        private static string bindState;
        private static float diveTopY = float.NaN;
        private static bool diveReady;
        private static GameObject wallSlash;
        private static float wallAttackAt = float.NegativeInfinity;

        public static void Tick(HeroController hc, float now)
        {
            if (!hc)
                return;
            if (hc != hero)
            {
                hero = hc;
                dartDamager = hc.transform.Find("Special Attacks/Silk Charge Damager");
                Dodged.Clear();
            }
            TickCharge(hc, now);
            var dart = dartDamager && dartDamager.gameObject.activeInHierarchy;
            if (dart && !darting)
                dartId++;
            darting = dart;
            TickBind(hc);
        }

        private static void TickCharge(HeroController hc, float now)
        {
            if (hc.cState.nailCharging)
            {
                charging = true;
                chargeHeld = NailChargeTimer(hc);
            }
            else if (charging)
            {
                charging = false;
                chargeReleasedAt = now;
            }
        }

        // How long the charge was held before the slash that is landing now.
        public static float ChargeHeldFor(float now) => now - chargeReleasedAt <= ChargeReleaseWindow ? chargeHeld : 0f;

        // BLUEPRINT: the Architect has no wall slash. Attacking from a wall turns her round, drops her off it and
        // swings an ordinary slash (HeroController.Attack), so the slash object she swung from the wall is remembered.
        public static void WallAttack(NailSlash slash, float now)
        {
            wallSlash = slash ? slash.gameObject : null;
            wallAttackAt = now;
        }

        public static bool FromWall(GameObject source, float now) =>
            source && wallSlash && now - wallAttackAt <= WallAttackWindow
            && (source == wallSlash || source.transform.IsChildOf(wallSlash.transform));

        // HeroController.TakeDamage is called on every physics step Hornet overlaps an attack, even while a dart makes
        // her invulnerable: an enemy attack (not its body) met during a dart is one she darted through.
        public static void Touched(GameObject attack, GlobalEnums.HazardType hazard, int damage)
        {
            if (!darting || !attack || damage <= 0 || attack.layer == 11)
                return;
            if (hazard != GlobalEnums.HazardType.ENEMY && hazard != GlobalEnums.HazardType.EXPLOSION)
                return;
            var owner = EnemyFinder.Owner(attack.transform);
            if (owner)
                Dodged[owner] = dartId;
        }

        public static bool DartedThrough(HealthManager enemy) =>
            enemy && Dodged.TryGetValue(enemy, out var id) && id == dartId;

        // The Shaman's bind: "Shaman Fall" starts the slam, "Shaman Impact" or "Bind Ground" lands it.
        private static void TickBind(HeroController hc)
        {
            var fsm = hc.spellControl;
            var state = fsm ? fsm.ActiveStateName : null;
            if (state == bindState)
                return;
            bindState = state;
            var y = hc.transform.position.y;
            if (state == "Shaman Fall")
            {
                diveTopY = y;
                diveReady = false;
            }
            else if ((state == "Shaman Impact" || state == "Bind Ground") && !float.IsNaN(diveTopY))
            {
                diveReady = diveTopY - y >= DiveMinHeight && EngineHost.NearestEnemyDistance() <= DiveRange;
                diveTopY = float.NaN;
            }
        }

        // Read once when the bind completes.
        public static bool TakeDive()
        {
            var ready = diveReady;
            diveReady = false;
            return ready;
        }

        public static void BindStarted()
        {
            diveReady = false;
            diveTopY = float.NaN;
        }

        public static void Clear()
        {
            Dodged.Clear();
            charging = false;
            diveReady = false;
            diveTopY = float.NaN;
            wallSlash = null;
        }
    }
}
