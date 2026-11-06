using System.Collections.Generic;

namespace Stylemeter.Core
{
    // Hits and kills: BONUSES.md sections 1 (needle), 4 (skill kills), 5 (crests), 6-7 (tool kills), 10 (elements),
    // 11 (enemies) and 13 (multikills, arsenal).
    internal sealed partial class StyleEngine
    {
        private sealed class EnemyRecord
        {
            public readonly HashSet<string> Families = new HashSet<string>();
            public HitEvent LastHit;
            public float LastHitAt = Never;
            public float LastPullAt = Never;
            public float BurningAt = Never;
            public int PinHits;
            public bool Crossfired;
            public bool Flanked;
        }

        private sealed class Swing
        {
            public float Time;
            public readonly HashSet<int> Enemies = new HashSet<int>();
            public int Kills;
        }

        private readonly Dictionary<int, EnemyRecord> enemies = new Dictionary<int, EnemyRecord>();
        private readonly Dictionary<int, Swing> swings = new Dictionary<int, Swing>();
        private int needleStreak;
        private float lastNeedleHit = Never;
        private int multiKills;
        private float lastKill = Never;
        private bool womboGiven;
        private float lastDashSlash = Never;
        private int lastDashSwing;
        private bool animationCancelGiven = true;

        public void OnHit(HitEvent hit)
        {
            Now = hit.Time;
            var source = SourceOf(hit.Kind, hit.Tool);
            var s = Begin(source, hit.Enemy.IsBoss);
            var record = RecordOf(hit.Enemy);
            if (source != null)
                record.Families.Add(FamilyOf(source));
            if (hit.Kind == AttackKind.Tool && Tools.IsPin(hit.Tool))
                record.PinHits++;
            record.LastHit = hit;
            record.LastHitAt = Now;
            GlideAttack(hit);

            var wear = HitPoints(hit, s);
            if (Attacks.IsNeedle(hit.Kind))
            {
                SwingHit(hit, s);
                Needlework(s);
                AnimationCancel(hit, s);
            }
            if (hit.Kind == AttackKind.NeedleDown)
                DownHit(hit.Enemy);
            if (hit.Kind == AttackKind.ClawlinePull)
                record.LastPullAt = Now;
            if (!counterpointGiven && hit.Enemy.Id == lastClashEnemy && Now - lastClash <= Tuning.CounterpointWindow)
            {
                counterpointGiven = true;
                Give(Bonuses.Counterpoint, s);
            }
            if (hit.Crit && !hit.Killed)
                Give(Bonuses.WanderersLuck, s);
            UseHit(hit, s);
            CrestHit(hit, s);
            TrickHit(hit, s, record);
            BossHit(hit, s);
            ComboHit(hit);
            if (hit.Killed)
            {
                Kill(hit, s, record, late: false);
                wear += Tuning.WearKill;
            }
            Wear(s, wear);
        }

        // Silent points of the hit itself; returns how much it wears the thread.
        private float HitPoints(HitEvent hit, Scoring s)
        {
            switch (hit.Kind)
            {
                case AttackKind.NeedleDown:
                    Give(Bonuses.Pogo, s);
                    return Tuning.WearPogo;
                case AttackKind.Clawline:
                    Give(Bonuses.Hooked, s);
                    return Tuning.WearToolHit;
                case AttackKind.ClawlinePull:
                    Give(Bonuses.ClawlinePull, s);
                    return Tuning.WearToolHit;
                case AttackKind.Skill:
                    Give(Bonuses.SkillHit, s);
                    return Tuning.WearSkillHit;
                case AttackKind.Tool:
                    Give(Bonuses.ToolHit, s);
                    return Tuning.WearToolHit;
                case AttackKind.Other:
                    return 0f;
                default:
                    Give(Hero.OnGround ? Bonuses.NeedleGround : Bonuses.NeedleAir, s);
                    return Tuning.WearNeedleHit;
            }
        }

        // One kill type per kill (the most specific), then every modifier that applies stacks on top. A late kill (the
        // enemy died later, of Hornet's ticks or a delayed death) keeps only what does not depend on her state now.
        private void Kill(HitEvent hit, Scoring s, EnemyRecord record, bool late)
        {
            var rankBefore = Meter.Rank;
            var riposte = !late && Now - lastClash <= Tuning.RiposteWindow;
            var type = KillType(hit, late);
            Give(type, s);
            if (!late)
            {
                StateKill(hit, s, record, type, riposte);
                TrickKill(hit, s);
            }
            ElementKill(hit, record, s, late);
            EnemyKill(hit, s);
            CrestKill(hit, s, late);
            UseKill(hit, s);
            Arsenal(record, s);
            MultiKill(s);
            KillStreak();
            ExplosionKill(hit);
            LightningKill(hit);
            if (!late)
                AirKill(s);
            BossKill(hit, rankBefore, riposte);
            enemies.Remove(hit.Enemy.Id);
        }

        // Modifiers that read Hornet's state and timing at the moment of the killing hit.
        private void StateKill(HitEvent hit, Scoring s, EnemyRecord record, BonusDef type, bool riposte)
        {
            if ((type == Bonuses.Uppercut || type == Bonuses.Shoryuken) && Random() < Tuning.ToastyChance)
                Give(Bonuses.Toasty, s);
            var airborne = !Hero.OnGround;
            if (airborne && hit.Kind != AttackKind.NeedleDown)
                Give(Bonuses.Airshot, s);
            if (airborne && (hit.Kind == AttackKind.NeedleSide || hit.Kind == AttackKind.NeedleUp)
                && Hero.VelocityY < -Tuning.FallingSpeed)
                Give(Bonuses.CriticalHit, s);
            if (hit.Enemy.MaxHp >= Tuning.CleanCutMinHp && hit.HpBefore >= hit.Enemy.MaxHp)
                Give(Bonuses.CleanCut, s);
            if (hit.Damage >= Tuning.OverstitchedMinDamage && hit.Damage >= 2 * hit.HpBefore)
                Give(Bonuses.Overstitched, s);
            if (hit.Taunted)
                Give(Bonuses.InsultToInjury, s);
            if (hit.Crit)
                Give(Bonuses.LuckyStrike, s);
            if (Hero.Health == 1)
                Give(Bonuses.LastStand, s);
            if (riposte)
                Give(Bonuses.Riposte, s);
            if (hit.Kind != AttackKind.ClawlinePull && Now - record.LastPullAt <= Tuning.GetOverHereWindow)
                Give(Bonuses.GetOverHere, s);
            if (Now - lastSpring <= Tuning.SpringboardWindow)
                Give(Bonuses.Springboard, s);
            if (Now - lastDownDash <= Tuning.DiveBombWindow)
                Give(Bonuses.DiveBomb, s);
            if (Now - lastWallJump <= Tuning.OffTheWallWindow)
                Give(Bonuses.OffTheWall, s);
            if (airborne && air != null && air.DoubleJumped)
                Give(Bonuses.SecondWind, s);
            DrifterKill(s);
            if (record.PinHits >= Tuning.PincushionPins)
                Give(Bonuses.Pincushion, s);
            if (hit.Kind == AttackKind.Tool && hit.Tool == Tools.StraightPin && hit.Distance >= Tuning.PinPointRange)
                Give(Bonuses.PinPoint, s);
            if (hit.Kind == AttackKind.Skill && hit.Tool != Skills.CrossStitch && hit.Distance >= Tuning.HadoukenRange)
                Give(Hero.Crest == Crests.Shaman ? Bonuses.VengefulSpirit : Bonuses.Hadouken, s);
            SetPiece(hit.Enemy.IsBoss);
        }

        private BonusDef KillType(HitEvent hit, bool late)
        {
            if (Hero.Crest == Crests.Witch && hit.FromBind)
                return Bonuses.Rooted;
            switch (hit.Kind)
            {
                case AttackKind.NeedleUp:
                    return !late && !Hero.OnGround && Hero.VelocityY > Tuning.RisingSpeed
                        ? Bonuses.Shoryuken
                        : Bonuses.Uppercut;
                case AttackKind.NeedleDown:
                    return !late && FallTime >= Tuning.DeathFromAboveFall ? Bonuses.DeathFromAbove : Bonuses.PinDrop;
                case AttackKind.NeedleDash:
                    return Hero.Crest == Crests.Architect ? Bonuses.DrillBit : Bonuses.DashSlash;
                case AttackKind.NeedleCharge:
                    return !late && !Hero.OnGround ? Bonuses.Suspended : Bonuses.GreatSlash;
                case AttackKind.ClawlinePull:
                    return Bonuses.ReeledIn;
                case AttackKind.Skill:
                    return Bonuses.SkillKill(hit.Tool) ?? Bonuses.Kill;
                case AttackKind.Tool:
                    return Bonuses.ToolKill(hit.Tool) ?? FlagKill(hit.Flags);
                case AttackKind.NeedleSide:
                    return Hero.Crest == Crests.Witch ? Bonuses.Whiplash : Bonuses.Kill;
                default:
                    return Bonuses.Kill;
            }
        }

        // A tool without a kill bonus of its own: named by how it cuts (the game's tool damage flags).
        private static BonusDef FlagKill(DamageFlags flags)
        {
            if ((flags & DamageFlags.Shredding) != 0)
                return Bonuses.Shredded;
            if ((flags & DamageFlags.Spearing) != 0)
                return Bonuses.Impaled;
            return Bonuses.Kill;
        }

        // Elements stack on the kill type: fire, poison, lightning, explosions; FLAMBÉ finishes a burning enemy.
        private void ElementKill(HitEvent hit, EnemyRecord record, Scoring s, bool late)
        {
            var fire = hit.Element == Element.Fire || (hit.Flags & DamageFlags.Searing) != 0;
            if (fire)
                Give(Bonuses.Seared, s);
            else if (Now - record.BurningAt <= Tuning.BurnWindow)
                Give(Bonuses.Flambe, s);
            if (hit.Element == Element.Poison)
                Give(Bonuses.Toxic, s);
            if (hit.Element == Element.Lightning)
                Give(late ? Bonuses.LiveWire : Bonuses.Shocking, s);
            if (hit.Explosion)
                Give(Bonuses.Kaboom, s);
        }

        private void EnemyKill(HitEvent hit, Scoring s)
        {
            // A boss has its own result; BIG KILL is for the big ones on the way.
            if (hit.Enemy.Large && !hit.Enemy.IsBoss)
                Give(Bonuses.BigKill, s);
            if (hit.Enemy.Flying)
                Give(hit.Kind == AttackKind.Tool && (hit.Tool == Tools.Curveclaw || hit.Tool == Tools.Curvesickle)
                    ? Bonuses.ClayPigeon
                    : Bonuses.Wingclipped, s);
            if (hit.Enemy.BlackThreaded)
                Give(Bonuses.Severed, s);
            NamedKill(hit, s);
        }

        // The needle counts as one source here, so MIXED MEDIA needs a tool, a skill or Clawline in the mix.
        private void Arsenal(EnemyRecord record, Scoring s)
        {
            var sources = record.Families.Count;
            if (sources >= 4)
                Give(Bonuses.Tapestry, s);
            else if (sources == 3)
                Give(Bonuses.Patchwork, s);
            else if (sources == 2)
                Give(Bonuses.MixedMedia, s);
        }

        private void MultiKill(Scoring s)
        {
            multiKills = Now - lastKill <= Tuning.MultiKillWindow ? multiKills + 1 : 1;
            lastKill = Now;
            if (multiKills == 1)
                womboGiven = false;
            if (multiKills == 2)
                Give(Bonuses.Duet, s);
            else if (multiKills == 3)
                Give(Bonuses.Trio, s);
            else if (multiKills >= 4)
            {
                Give(Bonuses.Choir, s, chainCount: multiKills);
                WomboCombo(s);
            }
        }

        // Hits from the same slash object within a short window are one swing (TWO BIRDS, CROWD CONTROL, HARVEST).
        private void SwingHit(HitEvent hit, Scoring s)
        {
            var swing = SwingOf(hit);
            if (swing == null || !swing.Enemies.Add(hit.Enemy.Id))
                return;
            if (swing.Enemies.Count == 2)
                Give(Bonuses.TwoBirds, s);
            else if (swing.Enemies.Count == 3)
                Give(Hero.Crest == Crests.Reaper ? Bonuses.Harvest : Bonuses.CrowdControl, s);
        }

        private Swing SwingOf(HitEvent hit)
        {
            if (hit.SwingId == 0)
                return null;
            if (!swings.TryGetValue(hit.SwingId, out var swing) || Now - swing.Time > Tuning.SwingWindow)
                swings[hit.SwingId] = swing = new Swing { Time = Now };
            return swing;
        }

        private void Needlework(Scoring s)
        {
            if (Now - lastNeedleHit > Tuning.NeedleworkGap)
                needleStreak = 0;
            lastNeedleHit = Now;
            needleStreak++;
            if (needleStreak % Tuning.NeedleworkHits == 0)
                Give(Bonuses.Needlework, s, chainCount: needleStreak / Tuning.NeedleworkHits);
        }

        // A run-slash resets the needle cooldown: a normal slash right after it, both landing, is ANIMATION CANCEL.
        private void AnimationCancel(HitEvent hit, Scoring s)
        {
            if (hit.Kind == AttackKind.NeedleDash)
            {
                if (hit.SwingId != lastDashSwing || Now - lastDashSlash > Tuning.AnimationCancelWindow)
                    animationCancelGiven = false;
                lastDashSlash = Now;
                lastDashSwing = hit.SwingId;
                return;
            }
            if (animationCancelGiven || Now - lastDashSlash > Tuning.AnimationCancelWindow)
                return;
            if (hit.Kind != AttackKind.NeedleSide && hit.Kind != AttackKind.NeedleUp)
                return;
            animationCancelGiven = true;
            Give(Bonuses.AnimationCancel, s);
        }

        private EnemyRecord RecordOf(EnemyInfo enemy)
        {
            if (!enemies.TryGetValue(enemy.Id, out var record))
                enemies[enemy.Id] = record = new EnemyRecord();
            return record;
        }

        // The thread a hit wears (MECHANICS.md §3): four needle sources, Clawline, each tool and each skill.
        private string SourceOf(AttackKind kind, string tool)
        {
            switch (kind)
            {
                case AttackKind.NeedleSide:
                case AttackKind.NeedleUp:
                    return Hero.OnGround ? "needle.ground" : "needle.air";
                case AttackKind.NeedleDown:
                    return "needle.down";
                case AttackKind.NeedleDash:
                case AttackKind.NeedleCharge:
                case AttackKind.NeedleSpecial:
                    return "needle.special";
                case AttackKind.Clawline:
                case AttackKind.ClawlinePull:
                    return "clawline";
                case AttackKind.Skill:
                    return "skill:" + tool;
                case AttackKind.Tool:
                    return "tool:" + tool;
                default:
                    return null;
            }
        }

        private static string FamilyOf(string source) => source.StartsWith("needle.") ? "needle" : source;
    }
}
