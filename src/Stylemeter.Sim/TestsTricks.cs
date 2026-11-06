using System.Linq;
using Stylemeter.Core;

namespace Stylemeter.Sim
{
    // Checks for Hornet's tricks read off a hit, her defensive tools and binds.
    internal static partial class Tests
    {
        private static void HitTricks()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground(Crests.Beast, rage: true));
            var rage = Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 70);
            rage.RageHit = true;
            e.OnHit(rage);
            Check(!Has(awards, "UNSTOPPABLE"), "a fury hit that met no guard is not UNSTOPPABLE");
            rage.Time = 0.5f;
            rage.PiercedGuard = true;
            e.OnHit(rage);
            Check(Has(awards, "UNSTOPPABLE"), "a fury hit through a guard is UNSTOPPABLE");
            awards.Clear();
            var longpin = ToolHit(1f, Enemy(2, maxHp: 90), Tools.Longpin, before: 90, after: 70);
            longpin.PiercedGuard = true;
            e.OnHit(longpin);
            Check(Has(awards, "ARMOR PIERCER"), "Longpin through a guard is ARMOR PIERCER");
            awards.Clear();
            for (var i = 0; i < 2; i++)
            {
                var flank = Hit(2f + i, Enemy(3, maxHp: 90), AttackKind.NeedleSide, 90 - i * 10, 80 - i * 10);
                flank.Flanked = true;
                e.OnHit(flank);
            }
            Check(awards.Count(a => a.Name == "FLANKED") == 1, "hits around a shield are FLANKED once per enemy");

            var w = NewEngine(out var wander);
            w.Observe(0f, Ground(Crests.Wanderer));
            var finisher = Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSpecial, 90, 80);
            finisher.DashFinisher = true;
            w.OnHit(finisher);
            finisher.Time = 0.05f;
            finisher.Enemy = Enemy(2, maxHp: 90);
            w.OnHit(finisher);
            Check(wander.Count(a => a.Name == "WANDERLUST") == 1,
                "one dash finisher hitting two enemies is one WANDERLUST");
            finisher.Time = 2f;
            w.OnHit(finisher);
            Check(wander.Count(a => a.Name == "WANDERLUST") == 2, "the next dash finisher is WANDERLUST again");
            var h = NewEngine(out var hunter);
            h.Observe(0f, Ground(Crests.Hunter));
            h.OnHit(finisher);
            Check(!Has(hunter, "WANDERLUST"), "WANDERLUST is the Wanderer's");

            var d = NewEngine(out var dart);
            d.Observe(0f, Ground());
            var through = SkillHit(0f, Enemy(1, maxHp: 90), Skills.Sharpdart, before: 90, after: 60);
            d.OnHit(through);
            Check(!Has(dart, "THREAD THE NEEDLE"), "a dart that met no attack is not THREAD THE NEEDLE");
            through.Time = 1f;
            through.DodgedThrough = true;
            d.OnHit(through);
            Check(Has(dart, "THREAD THE NEEDLE"),
                "a dart through the enemy's attack that hits it is THREAD THE NEEDLE");
        }

        private static void KillTricks()
        {
            var a = NewEngine(out var awards);
            a.Observe(0f, Ground(Crests.Architect));
            var wall = Hit(0f, Enemy(1), AttackKind.NeedleSide, 20, -5, killed: true);
            wall.WallSlash = true;
            a.OnHit(wall);
            Check(Has(awards, "BLUEPRINT"), "an Architect kill from a wall is BLUEPRINT");
            var other = NewEngine(out var others);
            other.Observe(0f, Ground(Crests.Wanderer));
            other.OnHit(wall);
            Check(!Has(others, "BLUEPRINT"), "BLUEPRINT is the Architect's");

            var p = NewEngine(out var patience);
            p.Observe(0f, Ground());
            var charge = Hit(0f, Enemy(1), AttackKind.NeedleCharge, 20, -5, killed: true);
            charge.ChargeHeld = 4f;
            p.OnHit(charge);
            Check(!Has(patience, "PATIENCE"), "a charge held 4 s is not PATIENCE");
            charge.Enemy = Enemy(2);
            charge.Time = 2f;
            charge.ChargeHeld = 10.5f;
            p.OnHit(charge);
            Check(Has(patience, "PATIENCE"), "a charged slash held 10+ s that kills is PATIENCE");

            var b = NewEngine(out var back);
            b.Observe(0f, Ground());
            var stab = Hit(0f, Enemy(1), AttackKind.NeedleSide, 20, -5, killed: true);
            stab.Backstab = true;
            b.OnHit(stab);
            Check(Has(back, "BACKSTITCH"), "a kill with a hit into the back is BACKSTITCH");
        }

        private static void WitchWhips()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Air(-1f, Crests.Witch));
            for (var i = 0; i < 4; i++)
            {
                // One whip lands twice: the strike, then the pull 0.15 s later.
                var t = 0.6f * i;
                e.Observe(t, Air(-1f, Crests.Witch));
                e.OnHit(Hit(t, Enemy(1 + i, maxHp: 90), AttackKind.NeedleDown, 90, 80));
                e.OnHit(Hit(t + 0.15f, Enemy(1 + i, maxHp: 90), AttackKind.NeedleDown, 80, 70));
                if (i == 1)
                    Check(!Has(awards, "CAT'S CRADLE"), "two whips, four hits, are not yet CAT'S CRADLE");
            }
            Check(awards.Count(x => x.Name == "CAT'S CRADLE") == 1,
                "three down whips in one air series are CAT'S CRADLE, once");
            var two = NewEngine(out var twice);
            two.Observe(0f, Air(-1f, Crests.Witch));
            two.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleDown, 90, 80));
            two.OnHit(Hit(0.05f, Enemy(2, maxHp: 90), AttackKind.NeedleDown, 90, 80));
            two.OnHit(Hit(0.4f, Enemy(3, maxHp: 90), AttackKind.NeedleDown, 90, 80));
            Check(!Has(twice, "CAT'S CRADLE"), "one whip hitting two enemies counts once");
        }

        private static void Defences()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnDiceSaved(0f);
            Check(!Has(awards, "LOADED DICE"), "the dice outside a fight give nothing");
            e.OnHit(Hit(0.5f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            e.OnDiceSaved(1f);
            Check(Has(awards, "LOADED DICE"), "Magnetite Dice cancelling a hit in a fight is LOADED DICE");
            e.OnMaskSaved(1.5f);
            Check(Has(awards, "DEATH DEFIANCE"), "Fractured Mask saving Hornet is DEATH DEFIANCE");
            e.OnReserveBind(2f);
            Check(Has(awards, "RESERVE TANK"), "Reserve Bind in a fight is RESERVE TANK");
            e.OnRestock(2.5f);
            Check(Has(awards, "RESTOCK"), "a quick craft refill in a fight is RESTOCK");
            e.OnPickpocket(3f, Enemy(2));
            Check(Has(awards, "PICKPOCKET"), "Snitch Pick stealing rosaries is PICKPOCKET");
            awards.Clear();
            e.OnBindStarted(4f);
            e.OnBellBlocked(4.2f, Enemy(3));
            e.OnBellBlocked(4.4f, Enemy(3));
            Check(awards.Count(a => a.Name == "SAVED BY THE BELL") == 1,
                "the bell taking hits during one bind is one SAVED BY THE BELL");
            e.OnBindCompleted(5f);
            e.OnBindCompleted(5.8f);
            Check(Has(awards, "DOUBLE KNOT") && awards.Count(a => a.Name == "MENDED") == 1,
                "Multibinder finishing one bind twice is one MENDED and DOUBLE KNOT");
            awards.Clear();
            e.OnBindStarted(9f);
            e.OnBindCompleted(10f);
            e.OnBindCompleted(15f);
            Check(!Has(awards, "DOUBLE KNOT"), "a stray finish long after the bind is not DOUBLE KNOT");
            e.OnDesolateDive(16f);
            Check(Has(awards, "DESOLATE DIVE"), "the Shaman's slam landing next to an enemy is DESOLATE DIVE");
        }

        private static void ToolTricks()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            var back = ToolHit(0f, Enemy(1), Tools.Curveclaw, killed: true);
            back.ReturnLeg = true;
            e.OnHit(back);
            Check(Has(awards, "BOOMERANG"), "a Curveclaw kill on its way back is BOOMERANG");
            var outward = ToolHit(0.5f, Enemy(2), Tools.Curveclaw, killed: true);
            awards.Clear();
            e.OnHit(outward);
            Check(!Has(awards, "BOOMERANG"), "a Curveclaw kill on the way out is not BOOMERANG");
            var ring = ToolHit(1f, Enemy(3), Tools.ThrowingRing, killed: true);
            ring.AfterWallBounce = true;
            e.OnHit(ring);
            Check(Has(awards, "TRICK SHOT"), "a ring kill after a wall bounce is TRICK SHOT");
            var wheel = ToolHit(2f, Enemy(4), Tools.CogworkWheel, killed: true);
            wheel.WheelClimbed = true;
            e.OnHit(wheel);
            Check(Has(awards, "WALL RIDE"), "a wheel kill after it climbed a wall is WALL RIDE");
            var spike = ToolHit(3f, Enemy(5), Tools.StingShard, killed: true);
            spike.Shrapnel = true;
            e.OnHit(spike);
            Check(Has(awards, "SHRAPNEL") && Has(awards, "STUNG"),
                "a kill by a broken Sting Shard's spikes is STUNG and SHRAPNEL");

            var q = NewEngine(out var sling);
            q.Observe(0f, Ground());
            q.OnToolUsed(0f, Tools.StraightPin, false);
            q.OnToolUsed(0.15f, Tools.StraightPin, false, auto: true);
            q.OnHit(ToolHit(0.3f, Enemy(1, maxHp: 90), Tools.StraightPin, swing: 11, before: 90, after: 80));
            Check(!Has(sling, "DOUBLE TAP"), "one pin of the two hitting is not DOUBLE TAP");
            q.OnHit(ToolHit(0.4f, Enemy(1, maxHp: 90), Tools.StraightPin, swing: 12, before: 80, after: 70));
            Check(Has(sling, "DOUBLE TAP"), "both tools of one Quick Sling throw hitting is DOUBLE TAP");
            q.OnToolUsed(2f, Tools.StraightPin, false);
            q.OnHit(ToolHit(2.2f, Enemy(2, maxHp: 90), Tools.StraightPin, swing: 13, before: 90, after: 80));
            q.OnHit(ToolHit(2.3f, Enemy(2, maxHp: 90), Tools.StraightPin, swing: 14, before: 80, after: 70));
            Check(sling.Count(a => a.Name == "DOUBLE TAP") == 1,
                "without Quick Sling's second throw there is no DOUBLE TAP");

            var r = NewEngine(out var sent);
            r.Observe(0f, Ground());
            r.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            r.OnReturnToSender(0.5f, Enemy(2));
            Check(Has(sent, "RETURN TO SENDER") && !Has(sent, "BELL RINGER"),
                "a batted shot hurting an enemy is RETURN TO SENDER");
            r.OnReturnToSender(2f, Boss(3, BossBonuses.Widow));
            Check(Has(sent, "BELL RINGER"), "a batted bell hitting Widow is BELL RINGER");
        }

        private static EnemyInfo Kind(int id, string species, int maxHp = 30) =>
            new EnemyInfo { Id = id, Name = species, MaxHp = maxHp, Species = species };

        private static void NamedEnemies()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnHit(Hit(0f, Kind(1, "GLOOM_BEAST", 200), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(awards, "BIG GAME"), "killing Gargant Gloom is BIG GAME");
            e.OnHit(Hit(0.5f, Kind(2, Species.Wingmould), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(awards, "WHITE PALACE"), "killing a Wingmould is WHITE PALACE");
            var thief = Hit(1f, Kind(3, Species.Snitchfly), AttackKind.NeedleSide, 20, -5, killed: true);
            thief.Thief = true;
            e.OnHit(thief);
            Check(Has(awards, "STOP, THIEF!"), "killing a Snitchfly with Hornet's rosaries is STOP, THIEF!");
            var shell = Hit(1.5f, Kind(4, "UNDERSTORE_SMALL"), AttackKind.NeedleSide, 20, -5, killed: true);
            shell.Exposed = true;
            e.OnHit(shell);
            Check(Has(awards, "CAUGHT OFF GUARD"), "killing an Underworker before it hides is CAUGHT OFF GUARD");
            var worm = Hit(2f, Kind(5, Species.Gromling), AttackKind.NeedleSide, 20, -5, killed: true);
            worm.Lured = true;
            e.OnHit(worm);
            Check(Has(awards, "PEEKABOO"), "killing a Gromling lured by Needolin is PEEKABOO");
            awards.Clear();
            for (var i = 0; i < 3; i++)
            {
                var singer = Hit(3f + i * 0.5f, Enemy(10 + i), AttackKind.NeedleSide, 20, -5, killed: true);
                singer.SongId = 1;
                e.OnHit(singer);
            }
            Check(awards.Count(a => a.Name == "SWAN SONG") == 3 && awards.Count(a => a.Name == "STANDING OVATION") == 1,
                "three singers of one song killed are three SWAN SONG and one STANDING OVATION");

            var g = NewEngine(out var glory);
            g.Observe(0f, Ground());
            var down = Hit(0f, Enemy(1), AttackKind.NeedleDown, 20, -5, killed: true);
            down.Stunned = true;
            g.OnHit(down);
            Check(Has(glory, "GLORY KILL"), "a down strike killing a stunned enemy is GLORY KILL");
            var side = Hit(0.5f, Enemy(2), AttackKind.NeedleSide, 20, -5, killed: true);
            side.Stunned = true;
            g.OnHit(side);
            Check(glory.Count(a => a.Name == "GLORY KILL") == 1, "a side slash on a stunned enemy is not GLORY KILL");
        }

        private static void WorldTricks()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnHit(Hit(0f, Kind(1, "ROCK_ROLLER"), AttackKind.NeedleSide, 20, -5, killed: true));
            e.OnEnemyDied(0.4f, Kind(2, "ROCK_ROLLER"), new DeathCause { Hazard = Hazard.Explosion });
            Check(!Has(awards, "DOMINO EFFECT"), "two explosive enemies are not yet a DOMINO EFFECT");
            e.OnEnemyDied(0.8f, Kind(3, "SWAMP_GOOMBA"), new DeathCause { Hazard = Hazard.Explosion });
            Check(Has(awards, "DOMINO EFFECT"), "three explosive enemies blowing each other up is DOMINO EFFECT");
            e.OnEnemyFell(1f, Enemy(4));
            e.OnEnemyFell(1.2f, Enemy(4));
            Check(awards.Count(a => a.Name == "THIS IS SPARTA!") == 1,
                "an enemy Hornet hit falling out of the room is THIS IS SPARTA!, once");
            e.OnClash(2f, Kind(5, "SONG_REED"), parryCounter: true);
            e.OnClash(2.5f, Kind(5, "SONG_REED"), parryCounter: true);
            Check(awards.Count(a => a.Name == "PARRY THIS!") == 1,
                "a clash with an enemy's parry counter is PARRY THIS!, once per enemy");
            e.OnClash(3f, Kind(6, "BONE_HUNTER"));
            Check(awards.Count(a => a.Name == "PARRY THIS!") == 2,
                "a clash with Skarr Stalker, who parries, is PARRY THIS!");
        }

        private static void BossTricks()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            var sinner = Boss(1, BossBonuses.FirstSinner);
            e.OnBossTitle(0f, sinner);
            e.OnHit(Hit(1f, sinner, AttackKind.NeedleSide, 800, 780));
            e.OnEnemyStunned(2f, sinner);
            Check(Has(awards, "STAGGERED"), "a boss going into its stun is STAGGERED");
            e.OnBindBroken(3f, sinner);
            Check(Has(awards, "BIND BREAKER"), "breaking off First Sinner's heal is BIND BREAKER");
            e.OnHit(Hit(4f, sinner, AttackKind.NeedleSide, 20, -5, killed: true));
            e.OnBossDeath(5f);
            Check(Has(awards, "ABSOLVED") && Has(awards, "UNFORGIVEN"),
                "First Sinner beaten without a finished heal is UNFORGIVEN");

            var h = NewEngine(out var healed);
            h.Observe(0f, Ground());
            var again = Boss(1, BossBonuses.FirstSinner);
            h.OnBossTitle(0f, again);
            h.OnHit(Hit(1f, again, AttackKind.NeedleSide, 800, 780));
            h.OnBossHealed(2f, again);
            h.OnHit(Hit(3f, again, AttackKind.NeedleSide, 20, -5, killed: true));
            h.OnBossDeath(4f);
            Check(Has(healed, "ABSOLVED") && !Has(healed, "UNFORGIVEN"), "a heal she finished loses UNFORGIVEN");

            var v = NewEngine(out var voidWin);
            v.Observe(0f, Ground());
            var mother = Boss(1, BossBonuses.VoidMossMother);
            v.OnBossTitle(0f, mother);
            v.OnHit(Hit(1f, mother, AttackKind.NeedleSide, 20, -5, killed: true));
            v.OnBossDeath(2f);
            Check(Has(voidWin, "INTO THE VOID"), "beating the Void Moss Mother is INTO THE VOID");
        }

        private static void Bounces()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            e.Observe(0.5f, Air(-2f, null));
            e.OnSpikePogo(0.6f);
            e.OnSpikePogo(1.2f);
            Check(awards.Count(a => a.Name == "BED OF NAILS") == 1,
                "pogos off spikes in one air series are one BED OF NAILS");
        }
    }
}
