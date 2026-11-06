using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Stylemeter.Core;

namespace Stylemeter.Sim
{
    // Plays a research session log (BepInEx/Stylemeter/logs/session-*.log) through the engine to see how the meter
    // would have moved. The log has no hero state, so Hornet counts as on the ground except right after a down strike.
    internal static class Replay
    {
        private const float Step = 0.05f;
        private static readonly Regex LineRx = new Regex(@"^(\d\d):(\d\d):(\d\d)\.(\d{3}) (\S+) ?(.*)$");

        private sealed class Result
        {
            public readonly float[] RankTime = new float[Tuning.Ranks.Length];
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
            public int BestRank = -1;
            public float Total;
            public float Duration;

            public float ComboTime => RankTime.Sum();
        }

        public static int Run(string path)
        {
            var result = Play(path, verbose: true);
            Console.WriteLine();
            Console.WriteLine($"replayed {result.Duration:0} s, {result.Total:0} points, "
                + $"best rank {RankName(result.BestRank)}");
            Console.WriteLine("time per rank: " + Distribution(result));
            Console.WriteLine("bonuses: " + string.Join(", ",
                result.Counts.OrderByDescending(c => c.Value).Select(c => $"{c.Key} {c.Value}")));
            return 0;
        }

        // The same logs under several balancing setups ("drain=0.5,delay=1.5;silent=2"), to see where the meter sits.
        public static int Sweep(IReadOnlyList<string> paths, string setups)
        {
            var list = setups.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in paths)
            {
                Console.WriteLine(Path.GetFileName(path));
                foreach (var setup in list)
                {
                    Apply(setup);
                    var result = Play(path, verbose: false);
                    Console.WriteLine($"  {setup,-30} combo {result.ComboTime,4:0}s avg {AverageRank(result):0.0} "
                        + $"best {RankName(result.BestRank),-11} {Distribution(result)}");
                }
            }
            Apply("");
            return 0;
        }

        private static readonly float DefaultDrainScale = Tuning.DrainScale;
        private static readonly float DefaultTopDrainScale = Tuning.TopDrainScale;
        private static readonly float DefaultDrainDelay = Tuning.DrainDelay;
        private static readonly float DefaultSilentScale = Tuning.SilentScale;
        private static readonly float DefaultHitPenalty = Tuning.HitPenalty;
        private static readonly float DefaultBossMultiplier = Tuning.BossMultiplier;
        private static readonly float DefaultPeakDrainScale = Tuning.PeakDrainScale;

        // Knobs not named in the setup keep the engine's defaults.
        private static void Apply(string setup)
        {
            Tuning.DrainScale = DefaultDrainScale;
            Tuning.TopDrainScale = DefaultTopDrainScale;
            Tuning.DrainDelay = DefaultDrainDelay;
            Tuning.SilentScale = DefaultSilentScale;
            Tuning.HitPenalty = DefaultHitPenalty;
            Tuning.BossMultiplier = DefaultBossMultiplier;
            Tuning.PeakDrainScale = DefaultPeakDrainScale;
            foreach (var pair in setup.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=');
                var value = float.Parse(kv[1], CultureInfo.InvariantCulture);
                switch (kv[0].Trim())
                {
                    case "drain": Tuning.DrainScale = value; break;
                    case "delay": Tuning.DrainDelay = value; break;
                    case "silent": Tuning.SilentScale = value; break;
                    case "hit": Tuning.HitPenalty = value; break;
                    case "top": Tuning.TopDrainScale = value; break;
                    case "boss": Tuning.BossMultiplier = value; break;
                    case "peak": Tuning.PeakDrainScale = value; break;
                    default: throw new ArgumentException("unknown knob " + kv[0]);
                }
            }
        }

        // Time-weighted rank in combo: 0 = DREAMER, 1 = CHRYSALIS, ..., 7 = SILKSONG.
        private static float AverageRank(Result result) =>
            result.ComboTime <= 0f ? 0f : result.RankTime.Select((t, i) => t * i).Sum() / result.ComboTime;

        private static Result Play(string path, bool verbose)
        {
            var result = new Result();
            var engine = new StyleEngine { Random = () => 1.0 };
            var hero = new HeroState { OnGround = true, Health = 9, MaxHealth = 9, SilkMax = 9 };
            var ids = new Dictionary<string, int>();
            var lastHp = new Dictionary<string, int>();
            var maxHp = new Dictionary<string, int>();
            var killedAt = new Dictionary<string, float>();
            var bosses = new HashSet<string>();
            float start = -1f, now = 0f, clock = 0f, airUntil = -1f, lastParry = -10f;

            EnemyInfo Enemy(string name)
            {
                if (!ids.TryGetValue(name, out var id))
                    ids[name] = id = ids.Count + 1;
                var max = maxHp.TryGetValue(name, out var m) ? m : 0;
                // As in the game (EnemyRegistry): the logs say Regular for everyone, so a big health pool is Large.
                return new EnemyInfo
                {
                    Id = id,
                    Name = name,
                    MaxHp = max,
                    Large = max >= Tuning.BigKillMinHp,
                    IsBoss = bosses.Contains(name),
                };
            }
            var bindStarted = false;
            var paused = true;

            engine.Awarded += a =>
            {
                result.Total += a.Total;
                if (a.Silent)
                    return;
                result.Counts[a.Name] = result.Counts.TryGetValue(a.Name, out var n) ? n + 1 : 1;
                if (verbose)
                    Console.WriteLine($"{a.Time,8:0.00}  +{a.Total,6:0.0}  {a.Name}");
            };
            engine.Meter.RankChanged += (from, to) =>
            {
                result.BestRank = Math.Max(result.BestRank, to);
                if (verbose)
                    Console.WriteLine($"{now,8:0.00}  RANK {RankName(from)} -> {RankName(to)}");
            };

            foreach (var raw in File.ReadLines(path))
            {
                var m = LineRx.Match(raw);
                if (!m.Success)
                    continue;
                var t = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60
                    + int.Parse(m.Groups[3].Value) + int.Parse(m.Groups[4].Value) / 1000f;
                if (start < 0f)
                    start = t;
                now = t - start;
                while (clock + Step <= now)
                {
                    clock += Step;
                    hero.OnGround = clock > airUntil;
                    engine.Observe(clock, hero);
                    engine.Tick(clock, Step, !paused);
                    if (engine.Meter.InCombo && !paused)
                        result.RankTime[engine.Meter.Rank] += Step;
                }
                var tag = m.Groups[5].Value;
                var rest = m.Groups[6].Value;
                switch (tag)
                {
                    case "KILL":
                        var killed = rest.Substring(0, rest.IndexOf(" journal=", StringComparison.Ordinal));
                        killedAt[killed] = now;
                        var hp = Regex.Match(rest, @" hp=-?\d+/(\d+)");
                        if (hp.Success)
                            maxHp[killed] = int.Parse(hp.Groups[1].Value);
                        break;
                    case "HIT":
                        var name = rest.Substring(0, rest.IndexOf(" hp=", StringComparison.Ordinal));
                        var type = Regex.Match(rest, @" type=(\w+)").Groups[1].Value;
                        if (!rest.Contains("heroDmg=True"))
                        {
                            // The level or another enemy hurt it: only its death counts (SPIKED and the like).
                            if (killedAt.TryGetValue(name, out var kw) && now - kw < 0.1f)
                                engine.OnEnemyDied(now, Enemy(name), new DeathCause { Hazard = HazardOf(type) });
                            break;
                        }
                        var after = int.Parse(Regex.Match(rest, @" hp=(-?\d+)").Groups[1].Value);
                        var dealt = int.Parse(Regex.Match(rest, @" dmg=(\d+)").Groups[1].Value);
                        var src = Regex.Match(rest, @" src=(.*?) dir=").Groups[1].Value;
                        var toolName = Regex.Match(rest, "tool=\"([^\"]*)\"").Groups[1].Value;
                        var element = Regex.Match(rest, @" element=(\w+)").Groups[1].Value;
                        var before = lastHp.TryGetValue(name, out var seen) ? seen : after + dealt;
                        lastHp[name] = after;
                        if (after >= before && !(killedAt.TryGetValue(name, out var k0) && now - k0 < 0.1f))
                            break;
                        if (toolName == "-")
                            toolName = null;
                        var kind = Attacks.Classify(src, toolName, SkillNames.Contains(toolName ?? ""), out var tool);
                        if (kind == AttackKind.NeedleDown)
                        {
                            airUntil = now + 0.6f;
                            hero.OnGround = false;
                        }
                        engine.Observe(now, hero);
                        if (tool == Skills.CrossStitch && now - lastParry > 1f)
                        {
                            lastParry = now;
                            engine.OnParry(now, null);
                        }
                        engine.OnHit(new HitEvent
                        {
                            Time = now,
                            Enemy = Enemy(name),
                            Kind = kind,
                            Tool = tool,
                            HpBefore = before,
                            HpAfter = after,
                            Killed = killedAt.TryGetValue(name, out var k) && now - k < 0.1f,
                            Crit = rest.Contains("crit=True"),
                            Element = element == "Fire" ? Element.Fire
                                : element == "Poison" ? Element.Poison
                                : Element.None,
                            Explosion = type == "Explosion",
                            FromBind = Attacks.IsBindEffect(src),
                        });
                        if (kind == AttackKind.NeedleDown)
                            engine.OnPogoBounce(now);
                        break;
                    case "CLASH":
                        engine.Observe(now, hero);
                        var clashed = Regex.Match(rest, @"^enemy=(.*?) attack=");
                        engine.OnClash(now, clashed.Success && clashed.Groups[1].Value != "-"
                            ? Enemy(clashed.Groups[1].Value) : null);
                        break;
                    case "NEARMISS":
                        engine.Observe(now, hero);
                        var grazed = Regex.Match(rest, @"^enemy=(.*?) attack=");
                        engine.OnGraze(now, grazed.Success && grazed.Groups[1].Value != "-"
                            ? Enemy(grazed.Groups[1].Value) : null);
                        break;
                    case "BOSS":
                        var boss = Regex.Match(rest, @" enemy=(.*?) journal=");
                        if (boss.Success)
                        {
                            bosses.Add(boss.Groups[1].Value);
                            var info = Enemy(boss.Groups[1].Value);
                            info.BossKey = Regex.Match(rest, @"^title=(\S+)").Groups[1].Value;
                            engine.OnBossTitle(now, info);
                        }
                        break;
                    case "TOOL":
                        var used = Regex.Match(rest, @"^(.*) type=(\w+)$");
                        if (used.Success)
                            engine.OnToolUsed(now, used.Groups[1].Value, used.Groups[2].Value == "Skill");
                        break;
                    case "HURT":
                        engine.OnHeroDamaged(now, rest.Contains("NON_HAZARD"), rest.Contains("EXPLOSION"));
                        break;
                    case "SCENE":
                        // In game the meter stands still in menus and cutscenes; quitting to the menu ends the combo.
                        paused = rest.Length == 0 || rest.StartsWith("Menu") || rest.StartsWith("Pre_Menu")
                            || rest.StartsWith("Quit_To_Menu") || rest.StartsWith("Cinematic");
                        if (rest.StartsWith("Quit_To_Menu"))
                            engine.OnQuitToMenu();
                        engine.OnSceneChanged();
                        break;
                    case "EVENT":
                        if (rest == "DID TAUNT")
                            engine.OnTaunt(now);
                        else if (rest == "STARTED BIND")
                        {
                            bindStarted = true;
                            engine.OnBindStarted(now);
                        }
                        else if (rest == "DID BIND" && bindStarted)
                        {
                            bindStarted = false;
                            engine.OnBindCompleted(now);
                        }
                        else if (rest == "BOSS DEATH")
                            engine.OnBossDeath(now);
                        else if (rest == "BROLLY START")
                            engine.OnGlideStarted(now);
                        else if (rest == "BROLLY END")
                            engine.OnGlideEnded(now);
                        else if (rest == "FLINT SLATE EXPIRE")
                            engine.OnFlintslateExpired(now);
                        break;
                }
            }
            result.Duration = now;
            return result;
        }

        private static string Distribution(Result result)
        {
            var combo = result.ComboTime;
            if (combo <= 0f)
                return "-";
            return string.Join(" ", Tuning.Ranks.Select((r, i) => (r, i))
                .Where(x => result.RankTime[x.i] > 0f)
                .Select(x => $"{x.r.Name.Substring(0, 3)} {100f * result.RankTime[x.i] / combo:0}%"));
        }

        private static string RankName(int rank) => rank < 0 ? "-" : Tuning.Ranks[rank].Name;

        private static readonly HashSet<string> SkillNames = new HashSet<string>
        {
            Skills.Silkspear, Skills.ThreadStorm, Skills.CrossStitch,
            Skills.Sharpdart, Skills.RuneRage, Skills.PaleNails,
        };

        private static Hazard HazardOf(string attackType)
        {
            switch (attackType)
            {
                case "Spikes": return Hazard.Spikes;
                case "Lava":
                case "Coal":
                    return Hazard.Lava;
                case "Acid": return Hazard.Acid;
                case "Trap": return Hazard.Trap;
                case "Explosion": return Hazard.Explosion;
                default: return Hazard.Other;
            }
        }
    }
}
