using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static System.FormattableString;

namespace Stylemeter.Dev
{
    // What the tester reads: Hornet's state, every enemy in the room, and the bonuses awarded since a mark.
    internal static class Probe
    {
        private static readonly List<string> Captured = new List<string>();
        private static bool subscribed;
        private static List<HealthManager> lastCensus = new List<HealthManager>();

        public static IReadOnlyList<HealthManager> LastCensus => lastCensus;

        public static string Mark()
        {
            Subscribe();
            Captured.Clear();
            return Invariant($"mark t={Time.time:0.00}");
        }

        public static string Awards()
        {
            Subscribe();
            return Captured.Count == 0 ? "none" : string.Join(", ", Captured);
        }

        private static void Subscribe()
        {
            if (subscribed || Game.EngineHost.Engine == null)
                return;
            subscribed = true;
            Game.EngineHost.Engine.Awarded += a =>
            {
                if (!a.Silent)
                    Captured.Add(Invariant($"{a.Name}+{a.Total:0}"));
            };
        }

        public static string State()
        {
            var hero = HeroController.instance;
            if (!hero)
                return "no hero";
            var pd = PlayerData.instance;
            var body = hero.GetComponent<Rigidbody2D>();
            var cs = hero.cState;
            var meter = Game.EngineHost.Engine?.Meter;
            return Invariant($"pos={Bridge.V(hero.transform.position)} ")
                + Invariant($"vel={Bridge.V(body ? (Vector3)body.linearVelocity : Vector3.zero)} ")
                + $"ground={cs.onGround} right={cs.facingRight} dash={cs.dashing} sprint={cs.isSprinting} "
                + $"attack={cs.attacking} float={cs.floating} djump={cs.doubleJumping} "
                + $"hp={pd.health}/{pd.maxHealth} silk={pd.silk}/{pd.CurrentSilkMax} crest={pd.CurrentCrestID} "
                + $"rank={(meter == null ? "-" : Game.EngineHost.RankName(meter.Rank))} "
                + $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
        }

        // field DASHCOMBO_WINDOW_TIME, field Config.AttackDuration: a field or property of HeroController, by path.
        public static string Field(string path)
        {
            object value = HeroController.instance;
            foreach (var part in path.Split('.'))
            {
                if (value == null)
                    break;
                var t = HarmonyLib.Traverse.Create(value);
                var next = t.Property(part);
                value = next.PropertyExists() ? next.GetValue() : t.Field(part).GetValue();
            }
            return path + " = " + (value == null ? "null" : Invariant($"{value}"));
        }

        // Everything in the room that hurts enemies and is not Hornet's (CROSSFIRE candidates), inactive ones too:
        // attacks under an enemy first, then loose objects such as pooled projectiles (owner "-").
        public static string EnemyDamage()
        {
            var found = Resources.FindObjectsOfTypeAll<DamageEnemies>()
                .Where(d => d && d.gameObject.scene.IsValid() && !d.RepresentingTool
                    && !d.GetComponentInParent<HeroController>(true))
                .Select(d => (Attack: d, Owner: d.GetComponentInParent<HealthManager>(true)))
                .OrderBy(p => p.Owner ? 0 : 1)
                .Select(p => $"{(p.Owner ? p.Owner.name : "-")} <- "
                    + $"{PathOf(p.Attack.transform, p.Owner ? p.Owner.transform : null)} "
                    + $"active={p.Attack.isActiveAndEnabled} type={p.Attack.attackType} dmg={p.Attack.damageDealt}")
                .ToList();
            return $"{found.Count} non-Hornet attacks that hurt enemies"
                + (found.Count > 0 ? "\n" + string.Join("\n", found.Take(40)) : "");
        }

        private static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (; t && t != root; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Facing(HealthManager hm) =>
            hm.GetComponent<Walker>() ? "walker" : hm.GetComponent<WalkerV2>() ? "walker2"
            : hm.GetComponent<Climber>() ? "climber" : hm.GetComponent<Crawler>() ? "crawler" : "-";

        // setstatic CheatManager SuperLuckyDice true: a static field or property of a game type, for test setups.
        public static string SetStatic(string typeName, string member, string value)
        {
            var type = HarmonyLib.AccessTools.TypeByName(typeName);
            if (type == null)
                return "no type " + typeName;
            var t = HarmonyLib.Traverse.Create(type);
            var target = t.Property(member).PropertyExists() ? t.Property(member) : t.Field(member);
            if (!target.FieldExists() && !target.PropertyExists())
                return $"no member {typeName}.{member}";
            var current = target.GetValue();
            var kind = current != null ? current.GetType() : typeof(string);
            object parsed = kind.IsEnum ? System.Enum.Parse(kind, value)
                : kind == typeof(bool) ? (object)bool.Parse(value)
                : kind == typeof(int) ? (object)int.Parse(value)
                : kind == typeof(float) ? (object)Bridge.F(value)
                : value;
            target.SetValue(parsed);
            return $"{typeName}.{member} = {target.GetValue()}";
        }

        // fsm <enemy>: every FSM of the enemy (children too) with its active state.
        public static string Fsms(string which)
        {
            var hm = Dummies.Find(which);
            if (!hm)
                return "no such enemy " + which;
            var text = new StringBuilder(hm.name);
            foreach (var fsm in hm.GetComponentsInChildren<PlayMakerFSM>(true))
                text.Append($"\n{fsm.gameObject.name} [{fsm.FsmName}] {(fsm.enabled ? "" : "(off) ")}"
                    + $"-> {fsm.ActiveStateName}");
            return text.ToString();
        }

        public static string Census()
        {
            var hero = HeroController.instance;
            var at = hero ? hero.transform.position : Vector3.zero;
            lastCensus = HealthManager.EnumerateActiveEnemies()
                .Where(hm => hm && !hm.GetIsDead())
                .OrderBy(hm => (hm.transform.position - at).sqrMagnitude)
                .ToList();
            var text = new StringBuilder($"{lastCensus.Count} enemies");
            for (var i = 0; i < lastCensus.Count; i++)
            {
                var hm = lastCensus[i];
                var info = Game.EnemyRegistry.Get(hm);
                var body = hm.GetComponent<Rigidbody2D>();
                text.Append(Invariant($"\n#{i} {hm.name} journal={Game.Describe.JournalName(hm)} "))
                    .Append(Invariant($"hp={hm.hp}/{info.MaxHp} "))
                    .Append($"large={info.Large} flying={info.Flying} black={info.BlackThreaded} ")
                    .Append(body ? Invariant($"body={body.bodyType} g={body.gravityScale:0.##} ") : "body=- ")
                    .Append($"inv={hm.IsInvincible}/{hm.InvincibleFromDirection} species={info.Species ?? "-"} ")
                    .Append($"face={Facing(hm)} ")
                    .Append(Invariant($"pos={Bridge.V(hm.transform.position)} "))
                    .Append(Invariant($"dist={Vector3.Distance(at, hm.transform.position):0.0}"));
            }
            return text.ToString();
        }
    }
}
