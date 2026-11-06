using System;
using GlobalEnums;
using Stylemeter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using static System.FormattableString;

namespace Stylemeter.Game
{
    // Owns the style engine inside the game: feeds it Hornet's state every frame, turns hooked game events into
    // engine events and logs what it awards. Any error stops the engine instead of breaking the game.
    internal static class EngineHost
    {
        private const float MeterLogPeriod = 2f;
        private static bool stopped;
        private static int tauntFrame = -1;
        private static float sourcesCheckedAt = float.NegativeInfinity;
        private static float meterLoggedAt = float.NegativeInfinity;
        private static float silentPoints;

        public static StyleEngine Engine { get; private set; }

        private static bool Running => Engine != null && !stopped && Plugin.MeterEnabled.Value;

        public static void Init()
        {
            Engine = new StyleEngine();
            BindSettings(Engine.Settings);
            Engine.Awarded += a =>
            {
                if (a.Silent)
                    silentPoints += a.Total;
                else if (Plugin.LogStyle.Value)
                    Plugin.Out(Invariant($"STYLE +{a.Total:0.#} {a.Name} ({a.Base:0.#} x{a.Thread:0.##} ")
                        + Invariant($"x{a.Height:0.##} x{a.Boss:0.#}) src={a.Source ?? "-"} {MeterText()}"));
            };
            Engine.Meter.RankChanged += (from, to) =>
            {
                if (Plugin.LogStyle.Value)
                    Plugin.Out($"RANK {RankName(from)} -> {RankName(to)}");
            };
            // Holding SILKSONG pays one silk for every 5 s, through the game's own AddSilk with the flash Hornet shows
            // when silk comes in.
            Engine.SilkEarned += () =>
            {
                var hero = HeroController.instance;
                if (!Plugin.SilkReward.Value || !hero)
                    return;
                hero.AddSilk(1, true);
                Plugin.Out("SILK +1 for SILKSONG held");
            };
        }

        // The menu's settings live in the config file: read at start, written back whenever the menu changes them.
        // The config's own events cover edits made while the game runs (a config manager, or the file itself).
        private static void BindSettings(StyleSettings settings)
        {
            settings.PointsMultiplier = Plugin.PointsMultiplier.Value;
            settings.LoadDisabled(Plugin.DisabledBonuses.Value);
            settings.Changed += () =>
            {
                Plugin.PointsMultiplier.Value = settings.PointsMultiplier;
                if (Plugin.DisabledBonuses.Value != settings.DisabledText)
                    Plugin.DisabledBonuses.Value = settings.DisabledText;
            };
            Plugin.PointsMultiplier.SettingChanged += (_, __) =>
                settings.PointsMultiplier = Plugin.PointsMultiplier.Value;
            Plugin.DisabledBonuses.SettingChanged += (_, __) =>
            {
                if (Plugin.DisabledBonuses.Value != settings.DisabledText)
                    settings.LoadDisabled(Plugin.DisabledBonuses.Value);
            };
        }

        public static void Update()
        {
            if (!Running)
                return;
            Guard(() =>
            {
                if (!HeroProbe.TryRead(out var hero))
                    return;
                var now = Time.time;
                Engine.Observe(now, hero);
                Engine.Tick(now, Time.deltaTime, HeroProbe.DrainActive());
                if (now - sourcesCheckedAt > 5f)
                {
                    sourcesCheckedAt = now;
                    Engine.Threads.SourceCount = HeroProbe.SourceCount();
                    Engine.AttackTools = HeroProbe.AttackTools();
                }
                GrazeWatcher.Tick(HeroController.instance, now);
                HookGuard.Run("hero watch", () => HeroWatch.Tick(HeroController.instance, now));
                HookGuard.Run("tool watch", () => ToolWatch.Tick(now));
                HookGuard.Run("enemy watch", () => EnemyWatch.Tick(now));
                LogMeter(now);
            });
        }

        // The meter curve for balancing: every 2 s of a combo, with the silent points scored since the last line.
        private static void LogMeter(float now)
        {
            if (!Plugin.LogStyle.Value || !Engine.Meter.InCombo || now - meterLoggedAt < MeterLogPeriod)
                return;
            meterLoggedAt = now;
            var source = Engine.Threads.LastSource;
            var thread = source == null ? "-" : ThreadSet.StateOf(Engine.Threads.Tension(source)) + " " + source;
            // "paused": the drain is stopped while Hornet is not in control (dialogue, cutscene, bench, transition).
            var paused = HeroProbe.DrainActive() ? "" : " paused";
            Plugin.Out(Invariant($"METER {MeterText()} height=x{Engine.Height.Current(now):0.0#} thread={thread}")
                + Invariant($" silent=+{silentPoints:0}{paused}"));
            silentPoints = 0f;
        }

        public static void OnSceneChanged(Scene from, Scene to)
        {
            EnemyRegistry.Clear();
            GrazeWatcher.Clear();
            HeroWatch.Clear();
            ToolWatch.Clear();
            EnemyWatch.Clear();
            if (!Running)
                return;
            Guard(() =>
            {
                if (to.name == "Quit_To_Menu" || to.name == "Menu_Title")
                    Engine.OnQuitToMenu();
                Engine.OnSceneChanged();
            });
        }

        public static void Hit(HealthManager target, HitInstance hit, int hpBefore, int hpAfter, bool killed,
            HitContext context)
        {
            if (!Running || !hit.IsHeroDamage)
                return;
            Guard(() =>
            {
                var tool = hit.RepresentingTool;
                var path = Describe.PathOf(hit.Source);
                var kind = Attacks.Classify(path, tool ? tool.name : null, tool && tool.Type == ToolItemType.Skill,
                    out var name);
                var enemy = EnemyRegistry.Get(target);
                var distance = DistanceTo(target);
                // What the guessed thresholds of HADOUKEN, PIN-POINT, BIG KILL and WINGCLIPPED read, for tuning.
                if (killed && Plugin.LogStyle.Value)
                    Plugin.Out(Invariant($"FOE {target.name} kind={kind} tool=\"{name ?? "-"}\" dist={distance:0.0}")
                        + Invariant($" large={enemy.Large} flying={enemy.Flying} black={enemy.BlackThreaded}"));
                Observe();
                var ev = new HitEvent
                {
                    Time = Time.time,
                    Enemy = enemy,
                    Kind = kind,
                    Tool = name,
                    SwingId = hit.Source ? hit.Source.GetInstanceID() : 0,
                    HpBefore = hpBefore,
                    HpAfter = hpAfter,
                    Killed = killed,
                    Crit = hit.CriticalHit,
                    Taunted = tauntFrame == Time.frameCount,
                    Element = ElementOf(hit),
                    Explosion = hit.AttackType == AttackTypes.Explosion,
                    Flags = (DamageFlags)(int)hit.ToolDamageFlags,
                    Zap = hit.ZapDamageTicks > 0,
                    HunterCombo = hit.HunterCombo,
                    RageHit = hit.RageHit,
                    FromBind = Attacks.IsBindEffect(path),
                    Distance = distance,
                    PiercedGuard = context.Pierced,
                    Flanked = context.Flanked,
                    Backstab = context.Backstab,
                    WallSlash = Leaf(path).Contains("Wall"),
                    DashFinisher = Leaf(path).Contains("RecoilStab"),
                };
                // Each of these checks may fail on a game update and then only loses its own bonus.
                var source = hit.Source;
                var now = Time.time;
                ev.ChargeHeld = kind == AttackKind.NeedleCharge
                    ? HookGuard.Get("patience", () => HeroWatch.ChargeHeldFor(now), 0f) : 0f;
                ev.DodgedThrough = name == Skills.Sharpdart
                    && HookGuard.Get("thread the needle", () => HeroWatch.DartedThrough(target), false);
                ev.ReturnLeg = Tools.IsCurveclaw(name)
                    && HookGuard.Get("boomerang", () => ToolWatch.OnReturnLeg(source), false);
                ev.AfterWallBounce = name == Tools.ThrowingRing
                    && HookGuard.Get("trick shot", () => ToolWatch.AfterWallBounce(source), false);
                ev.WheelClimbed = name == Tools.CogworkWheel
                    && HookGuard.Get("wall ride", () => ToolWatch.WheelClimbed(source), false);
                ev.Shrapnel = name == Tools.StingShard
                    && HookGuard.Get("shrapnel", () => ToolWatch.IsShrapnel(source), false);
                ev.WallSlash = ev.WallSlash || HookGuard.Get("blueprint", () => HeroWatch.FromWall(source, now), false);
                var read = ev;
                ev = HookGuard.Get("enemy watch", () =>
                {
                    var copy = read;
                    EnemyWatch.Read(target, now, ref copy);
                    return copy;
                }, ev);
                HookGuard.Run("enemy watch", () => EnemyWatch.HitByHornet(target, now));
                Engine.OnHit(ev);
            });
        }

        // Damage to an enemy that was not Hornet's: level hazards, explosions, other enemies' attacks.
        public static void WorldHit(HealthManager target, HitInstance hit, bool killed)
        {
            On(() =>
            {
                var enemy = EnemyRegistry.Get(target);
                var hazard = HazardOf(target, hit);
                var source = hit.Source;
                var deflected = HookGuard.Get("return to sender", () => ToolWatch.WasDeflected(source)
                    || (enemy.BossKey == BossBonuses.Widow && ToolWatch.RecentLooseClash(2f)), false);
                // Enemies hurt by something else are rare (no attack of the Act 3 Citadel enemies hurts other
                // enemies); this line shows what CROSSFIRE could count.
                if (Plugin.LogStyle.Value)
                    Plugin.Out($"WORLDHIT {target.name} hazard={hazard} type={hit.AttackType} killed={killed} "
                        + $"deflected={deflected} src={Describe.PathOf(hit.Source)}");
                if (deflected)
                {
                    // Her own shot back at them: RETURN TO SENDER, not CROSSFIRE.
                    Engine.OnReturnToSender(Time.time, enemy);
                    hazard = Hazard.Other;
                }
                if (killed)
                    Engine.OnEnemyDied(Time.time, enemy, new DeathCause { Hazard = hazard, Element = ElementOf(hit) });
                else
                    Engine.OnWorldHit(Time.time, enemy, hazard);
            });
        }

        public static void EnemyDied(HealthManager target, DeathCause cause) =>
            On(() => Engine.OnEnemyDied(Time.time, EnemyRegistry.Get(target), cause));

        public static void TickDamage(HealthManager target, Element element) => On(() =>
        {
            // Damage over time is otherwise invisible in the log (FLAMBÉ reads the fire ticks).
            if (Plugin.LogStyle.Value)
                Plugin.Out($"TICK {target.name} element={element} hp={target.hp}");
            Engine.OnTickDamage(Time.time, EnemyRegistry.Get(target), element);
        });

        public static void ToolUsed(ToolItem tool, bool auto)
        {
            ToolWatch.ToolThrown(tool);
            On(() => Engine.OnToolUsed(Time.time, tool.name, tool.Type == ToolItemType.Skill, auto));
        }

        public static void FlintslateExpired() => On(() => Engine.OnFlintslateExpired(Time.time));

        public static void GlideStarted() => On(() => Engine.OnGlideStarted(Time.time));

        public static void GlideEnded() => On(() => Engine.OnGlideEnded(Time.time));

        public static void BossDeath() => On(() => Engine.OnBossDeath(Time.time));

        public static void BossTitle(HealthManager boss, string key)
        {
            EnemyRegistry.MarkBoss(boss, key);
            On(() => Engine.OnBossTitle(Time.time, EnemyRegistry.Get(boss)));
        }

        internal static Element ElementOf(HitInstance hit)
        {
            var element = ElementOf(hit.NailElement, hit.AttackType);
            return element == Element.None && hit.ZapDamageTicks > 0 ? Element.Lightning : element;
        }

        internal static Element ElementOf(NailElements element, AttackTypes type)
        {
            if (element == NailElements.Fire || type == AttackTypes.Fire)
                return Element.Fire;
            if (element == NailElements.Poison)
                return Element.Poison;
            return type == AttackTypes.Lightning ? Element.Lightning : Element.None;
        }

        private static Hazard HazardOf(HealthManager target, HitInstance hit)
        {
            switch (hit.AttackType)
            {
                case AttackTypes.Spikes: return Hazard.Spikes;
                case AttackTypes.Lava:
                case AttackTypes.Coal:
                    return Hazard.Lava;
                case AttackTypes.Acid: return Hazard.Acid;
                case AttackTypes.Trap: return Hazard.Trap;
                case AttackTypes.Explosion: return Hazard.Explosion;
                // Hunters' weapons: Bone Hunter Children throw sickles at the Bone Hopper Giant they hunt. The sickle
                // flies loose, so it has no enemy above it; the attack type itself says whose it is.
                case AttackTypes.Hunter: return Hazard.Enemy;
            }
            if ((hit.SpecialType & SpecialTypes.Acid) != 0)
                return Hazard.Acid;
            if ((hit.SpecialType & SpecialTypes.Lava) != 0)
                return Hazard.Lava;
            // Only an attack that belongs to another enemy counts as that enemy's (CROSSFIRE), never a guess.
            var owner = hit.Source ? hit.Source.GetComponentInParent<HealthManager>() : null;
            return owner && owner != target ? Hazard.Enemy : Hazard.Other;
        }

        private static string Leaf(string path) => path == null ? "" : path.Substring(path.LastIndexOf('/') + 1);

        private static float DistanceTo(HealthManager target)
        {
            var hero = HeroController.instance;
            return hero && target ? Vector2.Distance(hero.transform.position, target.transform.position) : 0f;
        }

        public static float NearestEnemyDistance() => NearestEnemy();

        // How far the closest living enemy is from Hornet (RISKY BUSINESS).
        private static float NearestEnemy()
        {
            var hero = HeroController.instance;
            if (!hero)
                return float.PositiveInfinity;
            var at = (Vector2)hero.transform.position;
            var nearest = float.PositiveInfinity;
            foreach (var hm in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (!hm || !hm.isActiveAndEnabled || hm.GetIsDead() || hm.hp <= 0)
                    continue;
                nearest = Mathf.Min(nearest, Vector2.Distance(at, hm.transform.position));
            }
            return nearest;
        }

        public static void Clash(HealthManager enemy, bool parryCounter)
        {
            GrazeWatcher.Spoil();
            On(() => Engine.OnClash(Time.time, Info(enemy), parryCounter));
        }

        // Enemy states from EnemyWatch.
        public static void EnemyStunned(HealthManager enemy) =>
            Say($"STUN {(enemy ? enemy.name : "-")}", () => Engine.OnEnemyStunned(Time.time, Info(enemy)));

        public static void EnemyFell(HealthManager enemy) =>
            Say($"FELL {(enemy ? enemy.name : "-")}", () => Engine.OnEnemyFell(Time.time, Info(enemy)));

        public static void BossHealed(HealthManager enemy) =>
            Say($"HEALED {(enemy ? enemy.name : "-")} hp={(enemy ? enemy.hp : 0)}",
                () => Engine.OnBossHealed(Time.time, Info(enemy)));

        public static void BindBroken(HealthManager enemy) =>
            Say($"BINDBREAK {(enemy ? enemy.name : "-")}", () => Engine.OnBindBroken(Time.time, Info(enemy)));

        public static void Parried(HealthManager enemy)
        {
            GrazeWatcher.Spoil();
            On(() => Engine.OnParry(Time.time, Info(enemy)));
        }

        public static void Evaded(HealthManager enemy) => On(() => Engine.OnEvade(Time.time, Info(enemy)));

        public static void Grazed(HealthManager enemy) => On(() => Engine.OnGraze(Time.time, Info(enemy)));

        public static void HeroDamaged(HazardType hazard)
        {
            GrazeWatcher.Spoil();
            var attacker = DefenseHooks.LastAttacker;
            if (attacker)
                EnemyWatch.HurtBy(EnemyFinder.Owner(attacker.transform), Time.time);
            On(() => Engine.OnHeroDamaged(Time.time, hazard == HazardType.NON_HAZARD, hazard == HazardType.EXPLOSION));
        }

        public static void PogoBounce()
        {
            lastBounce = Time.time;
            if (lastBounce - lastSpikeTink <= SpikePogoWindow)
                SpikePogo();
            On(() => Engine.OnPogoBounce(Time.time));
        }

        public static void SpringBounce() => On(() => Engine.OnSpringBounce(Time.time));

        public static void DownDash() => On(() => Engine.OnDownDash(Time.time));

        public static void WallJump() => On(() => Engine.OnWallJump(Time.time));

        public static void DoubleJump() => On(() => Engine.OnDoubleJump(Time.time));

        public static void Taunted() => On(() => Engine.OnTaunt(Time.time));

        // The taunt bonus is spent while the next needle hit is built, in the same frame as that hit.
        public static void TauntSpent() => tauntFrame = Time.frameCount;

        public static void BindStarted()
        {
            HeroWatch.BindStarted();
            On(() => Engine.OnBindStarted(Time.time));
        }

        public static void BindCompleted() => On(() =>
        {
            var nearest = NearestEnemy();
            var dive = HeroWatch.TakeDive();
            if (Plugin.LogStyle.Value)
                Plugin.Out(Invariant($"BIND nearest={nearest:0.0}{(dive ? " dive" : "")}"));
            if (dive)
                Engine.OnDesolateDive(Time.time);
            Engine.OnBindCompleted(Time.time, nearest);
        });

        // Defensive and support tools (BONUSES.md section 7) and the Architect's quick craft.
        public static void DiceSaved() => Say("SAVE dice", () => Engine.OnDiceSaved(Time.time));

        public static void MaskSaved() => Say("SAVE mask", () => Engine.OnMaskSaved(Time.time));

        public static void BellBlocked(HealthManager enemy) =>
            Say("SAVE bell", () => Engine.OnBellBlocked(Time.time, Info(enemy)));

        public static void ReserveBind() => Say("BIND reserve", () => Engine.OnReserveBind(Time.time));

        public static void Restocked(int charges) => Say($"RESTOCK +{charges}", () => Engine.OnRestock(Time.time));

        public static void Pickpocket(HealthManager enemy, int rosaries) =>
            Say($"STEAL {(enemy ? enemy.name : "-")} rosaries={rosaries}",
                () => Engine.OnPickpocket(Time.time, Info(enemy)));

        // BED OF NAILS: the spike tink and the bounce come in either order, within a fraction of a second.
        private const float SpikePogoWindow = 0.3f;
        private static float lastSpikeTink = float.NegativeInfinity;
        private static float lastBounce = float.NegativeInfinity;

        public static void SpikeTink()
        {
            lastSpikeTink = Time.time;
            if (lastSpikeTink - lastBounce <= SpikePogoWindow)
                SpikePogo();
        }

        private static void SpikePogo()
        {
            lastSpikeTink = lastBounce = float.NegativeInfinity;
            Say("POGO spikes", () => Engine.OnSpikePogo(Time.time));
        }

        // Logs the line (balancing reads it), then hands the event to the engine.
        private static void Say(string line, Action action)
        {
            if (Running && Plugin.LogStyle.Value)
                Plugin.Out(line);
            On(action);
        }

        public static void ArenaStarted() => On(() => Engine.OnArenaStarted(Time.time));

        public static void ArenaCleared() => On(() => Engine.OnArenaCleared(Time.time));

        public static string RankName(int rank) => rank < 0 ? "-" : Tuning.Ranks[rank].Name;

        private static EnemyInfo Info(HealthManager enemy) => enemy ? EnemyRegistry.Get(enemy) : null;

        private static string MeterText() =>
            Engine.Meter.InCombo
                ? $"rank={RankName(Engine.Meter.Rank)} {Engine.Meter.Fill:0}/{Engine.Meter.Size:0}"
                : "rank=-";

        private static void On(Action action)
        {
            if (!Running)
                return;
            Guard(() =>
            {
                Observe();
                action();
            });
        }

        private static void Observe()
        {
            if (HeroProbe.TryRead(out var hero))
                Engine.Observe(Time.time, hero);
        }

        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                stopped = true;
                Plugin.Log.LogError($"Style engine stopped after an error: {e}");
            }
        }
    }
}
