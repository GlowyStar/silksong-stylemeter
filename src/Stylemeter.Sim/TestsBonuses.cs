using System.Linq;
using Stylemeter.Core;

namespace Stylemeter.Sim
{
    // Checks for the bonuses of tools, skills per use, elements, hazards, enemy kinds, crests, needle extras,
    // combinations, health extras and boss fights.
    internal static partial class Tests
    {
        private static void ToolKills()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnHit(ToolHit(0f, Enemy(1), Tools.StraightPin, killed: true));
            Check(Has(awards, "PINNED") && !Has(awards, "KILL"), "a Straight Pin kill is PINNED instead of KILL");
            awards.Clear();
            var spear = ToolHit(1f, Enemy(2), "Mystery Tool", killed: true);
            spear.Flags = DamageFlags.Spearing;
            e.OnHit(spear);
            Check(Has(awards, "IMPALED"), "a piercing tool without a name of its own kills as IMPALED");
            awards.Clear();
            e.OnHit(ToolHit(2f, Enemy(3), Tools.StraightPin, killed: true, distance: 15f));
            Check(Has(awards, "PIN-POINT"), "a Straight Pin kill from half a screen away is PIN-POINT");
            awards.Clear();
            for (var i = 0; i < 3; i++)
                e.OnHit(ToolHit(3f + i * 0.4f, Enemy(4, maxHp: 60), Tools.StraightPin, before: 60 - i * 10,
                    after: 50 - i * 10));
            e.OnHit(Hit(4.5f, Enemy(4, maxHp: 60), AttackKind.NeedleSide, 30, -5, killed: true));
            Check(Has(awards, "PINCUSHION"), "a kill after three pin hits is PINCUSHION");
        }

        private static void ToolUses()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnToolUsed(0f, Tools.ThreefoldPin, false);
            for (var i = 0; i < 3; i++)
                e.OnHit(ToolHit(0.1f, Enemy(1 + i), Tools.ThreefoldPin, swing: 100 + i));
            Check(awards.Count(a => a.Name == "TRIFECTA") == 1,
                "all three pins of one Threefold Pin throw hitting is one TRIFECTA");
            awards.Clear();
            e.OnToolUsed(1f, Tools.ThrowingRing, false);
            e.OnHit(ToolHit(1.2f, Enemy(5), Tools.ThrowingRing, swing: 7));
            e.OnHit(ToolHit(1.3f, Enemy(6), Tools.ThrowingRing, swing: 7));
            Check(Has(awards, "RICOCHET"), "a ring hitting two enemies in one throw is RICOCHET");
            awards.Clear();
            e.OnHit(ToolHit(5f, Enemy(7), Tools.ThrowingRing, swing: 7));
            e.OnHit(ToolHit(9f, Enemy(8), Tools.ThrowingRing, swing: 7));
            Check(!Has(awards, "RICOCHET"), "hits seconds apart are separate throws");
            for (var i = 0; i < 6; i++)
                e.OnHit(ToolHit(10f + i * 0.1f, Enemy(9, maxHp: 100), Tools.Conchcutter, before: 100 - i * 5,
                    after: 95 - i * 5));
            Check(Has(awards, "FULL SPIN"), "six Conchcutter hits on one enemy are FULL SPIN");
            awards.Clear();
            for (var i = 0; i < 10; i++)
                e.OnHit(ToolHit(12f + i * 0.15f, Enemy(10, maxHp: 200), Tools.RosaryCannon, before: 200 - i * 2,
                    after: 198 - i * 2));
            Check(awards.Count(a => a.Name == "MAKE IT RAIN") == 1, "ten cannon hits within 2 s are MAKE IT RAIN");
            awards.Clear();
            e.OnHit(ToolHit(15f, Enemy(11), Tools.Tacks, killed: true));
            e.OnHit(ToolHit(15.5f, Enemy(12), Tools.SnareSetter, killed: true));
            Check(Has(awards, "CALTROPPED") && Has(awards, "SNARED") && Has(awards, "TRAP CARD"),
                "two trap kills in a combo are TRAP CARD");
            awards.Clear();
            e.Observe(16f, Air(2f));
            e.OnToolUsed(16f, Tools.Pimpillo, false);
            var bomb = ToolHit(16.5f, Enemy(13), Tools.Pimpillo, killed: true);
            bomb.Explosion = true;
            e.OnHit(bomb);
            Check(Has(awards, "BOMBS AWAY") && Has(awards, "KABOOM"),
                "a Pimpillo thrown from the air that kills is BOMBS AWAY and KABOOM");

            var t = NewEngine(out var tools);
            t.Observe(0f, Ground());
            t.AttackTools = new[] { Tools.StraightPin, Tools.Tacks };
            t.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            t.OnToolUsed(0.5f, Tools.StraightPin, false);
            Check(!Has(tools, "TOOLBOX"), "one of two equipped tools is not yet TOOLBOX");
            t.OnToolUsed(1f, Tools.Tacks, false);
            Check(Has(tools, "TOOLBOX"), "using every equipped attack tool in a combo is TOOLBOX");
            t.OnToolUsed(2f, Tools.PlasmiumPhial, false);
            Check(Has(tools, "LIFEBLOOD"), "drinking the Plasmium Phial mid-fight is LIFEBLOOD");
        }

        private static void SkillUses()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnToolUsed(0f, Skills.Silkspear, true);
            e.OnHit(SkillHit(0.1f, Enemy(1), Skills.Silkspear));
            e.OnHit(SkillHit(0.15f, Enemy(2), Skills.Silkspear));
            Check(Has(awards, "SHISH KEBAB"), "one Silkspear through two enemies is SHISH KEBAB");
            e.OnToolUsed(1f, Skills.PaleNails, true);
            e.OnHit(SkillHit(1.2f, Enemy(3), Skills.PaleNails, killed: true, swing: 31));
            e.OnHit(SkillHit(1.3f, Enemy(4), Skills.PaleNails, killed: true, swing: 32));
            e.OnHit(SkillHit(1.4f, Enemy(5, maxHp: 90), Skills.PaleNails, swing: 33, before: 90, after: 70));
            Check(Has(awards, "HAT TRICK") && Has(awards, "NAILED IT"),
                "three claws landing and two kills are HAT TRICK and NAILED IT");
            Check(Has(awards, "SILK ROAD") == false, "two skills are not yet SILK ROAD");
            e.OnToolUsed(2f, Skills.RuneRage, true);
            for (var i = 0; i < 3; i++)
                e.OnHit(SkillHit(2.2f + i * 0.1f, Enemy(6 + i), Skills.RuneRage, killed: true));
            Check(Has(awards, "RAGE QUIT"), "three kills from one Rune Rage are RAGE QUIT");
            Check(Has(awards, "SILK ROAD"), "three different skills in one combo are SILK ROAD");
            awards.Clear();
            e.OnHit(SkillHit(5f, Enemy(10), Skills.Silkspear, killed: true, distance: 12f));
            Check(Has(awards, "SKEWERED") && Has(awards, "HADOUKEN"), "a Silkspear kill from far away is HADOUKEN");
            awards.Clear();
            e.Observe(6f, Ground(crest: Crests.Shaman));
            e.OnHit(SkillHit(6f, Enemy(11), Skills.Silkspear, killed: true, distance: 12f));
            Check(Has(awards, "VENGEFUL SPIRIT") && !Has(awards, "HADOUKEN") && Has(awards, "SPELLBOUND"),
                "with the Shaman crest it is VENGEFUL SPIRIT, and every skill kill is SPELLBOUND");
            awards.Clear();
            e.OnToolUsed(8f, Skills.ThreadStorm, true);
            var z1 = SkillHit(8.1f, Enemy(12, maxHp: 90), Skills.ThreadStorm, before: 90, after: 80);
            var z2 = SkillHit(8.2f, Enemy(13, maxHp: 90), Skills.ThreadStorm, before: 90, after: 80);
            z1.Zap = z2.Zap = true;
            e.OnHit(z1);
            e.OnHit(z2);
            Check(Has(awards, "CONDUCTOR"), "a skill charged with lightning zapping two enemies is CONDUCTOR");
        }

        private static void Elements()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            var fire = Hit(0f, Enemy(1), AttackKind.NeedleSide, 20, -5, killed: true);
            fire.Element = Element.Fire;
            e.OnHit(fire);
            Check(Has(awards, "KILL") && Has(awards, "SEARED"), "a fire kill is KILL plus SEARED");
            awards.Clear();
            e.OnHit(Hit(1f, Enemy(2, maxHp: 60), AttackKind.NeedleSide, 60, 40));
            e.OnTickDamage(1.5f, Enemy(2, maxHp: 60), Element.Fire);
            e.OnHit(Hit(2f, Enemy(2, maxHp: 60), AttackKind.NeedleSide, 40, -5, killed: true));
            Check(Has(awards, "FLAMBÉ") && !Has(awards, "SEARED"),
                "finishing a burning enemy with plain steel is FLAMBÉ");
            awards.Clear();
            e.Observe(3f, Air(-3f));
            e.OnHit(Hit(3f, Enemy(3, maxHp: 60), AttackKind.NeedleSide, 60, 50));
            e.OnEnemyDied(6f, Enemy(3, maxHp: 60), new DeathCause { ByHornet = true, Element = Element.Poison });
            Check(Has(awards, "KILL") && Has(awards, "TOXIC") && !Has(awards, "AIRSHOT"),
                "poison ticks finishing an enemy are her kill with TOXIC, without the moment's air bonuses");
            awards.Clear();
            e.Observe(7f, Ground());
            e.OnHit(Hit(7f, Enemy(4, maxHp: 60), AttackKind.NeedleSide, 60, 50));
            e.OnEnemyDied(8f, Enemy(4, maxHp: 60), new DeathCause { ByHornet = true, Element = Element.Lightning });
            Check(Has(awards, "LIVE WIRE") && !Has(awards, "SHOCKING"),
                "lightning ticks finishing an enemy are LIVE WIRE");
            awards.Clear();
            var b1 = Hit(10f, Enemy(5), AttackKind.Skill, 20, -5, killed: true, tool: Skills.RuneRage);
            var b2 = Hit(10.05f, Enemy(6), AttackKind.Skill, 20, -5, killed: true, tool: Skills.RuneRage);
            b1.Explosion = b2.Explosion = true;
            b1.SwingId = b2.SwingId = 77;
            e.OnHit(b1);
            e.OnHit(b2);
            Check(awards.Count(a => a.Name == "KABOOM") == 2 && Has(awards, "CHAIN REACTION"),
                "one explosion killing two is CHAIN REACTION");
            awards.Clear();
            var l1 = Hit(12f, Enemy(7), AttackKind.Tool, 20, -5, killed: true, tool: Tools.Voltvessels);
            var l2 = Hit(12.2f, Enemy(8), AttackKind.Tool, 20, -5, killed: true, tool: Tools.Voltvessels);
            l1.Element = l2.Element = Element.Lightning;
            e.OnHit(l1);
            e.OnHit(l2);
            Check(Has(awards, "LIGHTNING ROD") && Has(awards, "SHOCKING") && Has(awards, "THUNDERSTRUCK"),
                "two lightning kills within half a second are THUNDERSTRUCK");

            var f = NewEngine(out var flint);
            f.Observe(0f, Ground());
            f.OnToolUsed(0f, Tools.Flintslate, false);
            for (var i = 0; i < 3; i++)
            {
                var h = Hit(1f + i, Enemy(20 + i), AttackKind.NeedleSide, 20, -5, killed: true);
                h.Element = Element.Fire;
                f.OnHit(h);
            }
            Check(flint.Count(a => a.Name == "SHE'S ON FIRE!") == 1,
                "three kills in one Flintslate are SHE'S ON FIRE!");
            f.OnToolUsed(10f, Tools.Flintslate, false);
            f.OnFlintslateExpired(11f);
            for (var i = 0; i < 3; i++)
            {
                var h = Hit(12f + i, Enemy(30 + i), AttackKind.NeedleSide, 20, -5, killed: true);
                h.Element = Element.Fire;
                f.OnHit(h);
            }
            Check(flint.Count(a => a.Name == "SHE'S ON FIRE!") == 1, "kills after the fire expired do not count");
        }

        private static void Environment()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnHit(Hit(0f, Enemy(1, maxHp: 60), AttackKind.NeedleSide, 60, 40));
            e.OnEnemyDied(1f, Enemy(1, maxHp: 60), new DeathCause { Hazard = Hazard.Spikes });
            Check(Has(awards, "SPIKED") && !Has(awards, "KILL"), "an enemy she hit dying on spikes is SPIKED");
            awards.Clear();
            e.OnHit(Hit(2f, Enemy(2, maxHp: 60), AttackKind.NeedleSide, 60, 40));
            e.OnEnemyDied(7f, Enemy(2, maxHp: 60), new DeathCause { Hazard = Hazard.Lava });
            Check(!Has(awards, "MELTED"), "a hazard death long after her last hit is not hers");
            e.OnEnemyDied(8f, Enemy(3), new DeathCause { Hazard = Hazard.Spikes });
            Check(!Has(awards, "SPIKED"), "an enemy she never hit dying on spikes is not hers");
            e.OnWorldHit(9f, Enemy(4), Hazard.Enemy);
            e.OnWorldHit(9.2f, Enemy(4), Hazard.Enemy);
            e.OnWorldHit(9.4f, Enemy(5), Hazard.Enemy);
            Check(awards.Count(a => a.Name == "CROSSFIRE") == 1,
                "CROSSFIRE goes once per victim and at most once a second");
            e.OnWorldHit(11f, Enemy(5), Hazard.Enemy);
            Check(awards.Count(a => a.Name == "CROSSFIRE") == 2, "another victim later is another CROSSFIRE");
            e.OnHeroDamaged(12f, soft: false, explosion: true);
            Check(Has(awards, "AW MAN") && awards.First(a => a.Name == "AW MAN").Total == 0f,
                "an explosion hitting Hornet is AW MAN, with no points");

            var idle = NewEngine(out var calm);
            idle.OnWorldHit(1f, Enemy(1), Hazard.Enemy);
            Check(!Has(calm, "CROSSFIRE"), "enemies hurting each other outside a fight give nothing");
        }

        private static void EnemyKinds()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            var big = Enemy(1);
            big.Large = true;
            e.OnHit(Hit(0f, big, AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(awards, "BIG KILL"), "killing a large enemy is BIG KILL");
            awards.Clear();
            var fly = Enemy(2);
            fly.Flying = true;
            e.OnHit(Hit(1f, fly, AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(awards, "WINGCLIPPED"), "killing a flying enemy is WINGCLIPPED");
            awards.Clear();
            var bird = Enemy(3);
            bird.Flying = true;
            e.OnHit(ToolHit(2f, bird, Tools.Curveclaw, killed: true));
            Check(Has(awards, "CLAY PIGEON") && !Has(awards, "WINGCLIPPED"),
                "a Curveclaw kill of a flier is CLAY PIGEON");
            awards.Clear();
            var black = Enemy(4);
            black.BlackThreaded = true;
            e.OnHit(Hit(3f, black, AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(awards, "SEVERED"), "killing a black-threaded enemy is SEVERED");

            var b = NewEngine(out var bossAwards);
            b.Observe(0f, Ground());
            var giant = Boss(9, "SONG_KNIGHT");
            giant.Large = true;
            b.OnHit(Hit(0f, giant, AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(bossAwards, "KILL") && !Has(bossAwards, "BIG KILL"),
                "a big boss is not a BIG KILL: it has its own result");
        }

        private static void CrestBonuses()
        {
            var h = NewEngine(out var hunt);
            h.Observe(0f, Ground(crest: Crests.HunterV3));
            h.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            h.Observe(0.5f, Ground(crest: Crests.HunterV3, hunterStage: 1));
            h.Observe(1f, Ground(crest: Crests.HunterV3, hunterStage: 2));
            Check(Has(hunt, "THE HUNT IS ON") && Has(hunt, "APEX PREDATOR"),
                "Hunter focus stages are THE HUNT IS ON and APEX PREDATOR");
            var trophy = Hit(1.5f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 80, -5, killed: true);
            trophy.HunterCombo = true;
            h.OnHit(trophy);
            Check(Has(hunt, "TROPHY"), "a kill with the focus bonus is TROPHY");

            var b = NewEngine(out var beast);
            b.Observe(0f, Ground(crest: Crests.Beast));
            b.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            b.Observe(0.5f, Ground(crest: Crests.Beast, rage: true));
            Check(Has(beast, "BEAST MODE"), "rage in a fight is BEAST MODE");
            for (var i = 0; i < 3; i++)
                b.OnHit(Hit(1f + i, Enemy(2 + i), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(beast.Count(a => a.Name == "BLOODLUST") == 3 && beast.Count(a => a.Name == "RIP AND TEAR") == 1,
                "three kills in one rage are BLOODLUST each and one RIP AND TEAR");

            var r = NewEngine(out var reap);
            r.Observe(0f, Ground(crest: Crests.Reaper, reaper: true));
            for (var i = 0; i < 3; i++)
                r.OnHit(Swung(0f, Enemy(1 + i), 5, i == 0));
            Check(Has(reap, "HARVEST") && !Has(reap, "CROWD CONTROL") && Has(reap, "DON'T FEAR THE REAPER"),
                "a scythe swing through three is HARVEST, a kill while harvesting DON'T FEAR THE REAPER");

            var w = NewEngine(out var wand);
            w.Observe(0f, Air(-1f, crest: Crests.Wanderer));
            for (var i = 0; i < 5; i++)
            {
                w.OnHit(Hit(i * 0.4f, Enemy(1, maxHp: 200), AttackKind.NeedleDown, 200 - i * 10, 190 - i * 10));
                w.OnPogoBounce(i * 0.4f);
            }
            Check(Has(wand, "OLD HABITS"), "five pogos in a row with the Wanderer crest are OLD HABITS");

            var x = NewEngine(out var witch);
            x.Observe(0f, Ground(crest: Crests.Witch));
            x.OnHit(Hit(0f, Enemy(1), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(witch, "WHIPLASH") && !Has(witch, "KILL"), "a Witch side kill is WHIPLASH");
            x.OnBindStarted(1f);
            var root = Hit(1.2f, Enemy(2, maxHp: 90), AttackKind.NeedleSpecial, 90, 80);
            root.FromBind = true;
            x.OnHit(root);
            var rootKill = Hit(1.3f, Enemy(3), AttackKind.NeedleSpecial, 20, -5, killed: true);
            rootKill.FromBind = true;
            x.OnHit(rootKill);
            Check(witch.Count(a => a.Name == "HEXED") == 1 && Has(witch, "ROOTED"),
                "Witch roots are HEXED once per bind, ROOTED when they kill");

            var a = NewEngine(out var arch);
            a.Observe(0f, Ground(crest: Crests.Architect));
            var d1 = Hit(0f, Enemy(1), AttackKind.NeedleDash, 20, -5, killed: true);
            var d2 = Hit(0.05f, Enemy(2), AttackKind.NeedleDash, 20, -5, killed: true);
            d1.SwingId = d2.SwingId = 9;
            a.OnHit(d1);
            a.OnHit(d2);
            Check(arch.Count(c => c.Name == "DRILL BIT") == 2 && Has(arch, "PIERCE THE HEAVENS")
                && !Has(arch, "DASH SLASH"),
                "an Architect drill dash killing two is DRILL BIT twice and PIERCE THE HEAVENS");

            var c = NewEngine(out var cursed);
            c.Observe(0f, Ground(crest: Crests.Cursed));
            c.OnHit(Hit(0f, Enemy(1), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(cursed, "CURSED BLADE"), "a kill while cursed is CURSED BLADE");
            c.Meter.Add(2000f);
            c.Tick(0.1f, 0.016f, true);
            Check(Has(cursed, "AGAINST ALL ODDS"), "reaching SUBLIME while cursed is AGAINST ALL ODDS");

            var n = NewEngine(out var cloak);
            n.Observe(0f, Ground(crest: Crests.Cloakless));
            n.OnHit(Hit(0f, Enemy(1), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(cloak, "NO CLOAK, NO PROBLEM"), "a kill without the cloak is NO CLOAK, NO PROBLEM");

            var g = NewEngine(out var glass);
            var bracelet = Ground();
            bracelet.BarbedBracelet = true;
            g.Observe(0f, bracelet);
            g.Meter.Add(2000f);
            g.Tick(0.1f, 0.016f, true);
            Check(Has(glass, "GLASS CANNON"), "reaching SUBLIME with the Barbed Bracelet is GLASS CANNON");
        }

        private static void NeedleExtras()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Air(0f));
            e.OnHit(Hit(0f, Enemy(1), AttackKind.NeedleCharge, 20, -5, killed: true));
            Check(Has(awards, "SUSPENDED") && !Has(awards, "GREAT SLASH"),
                "a charged strike released in the air that kills is SUSPENDED");
            awards.Clear();
            e.Observe(1f, Ground());
            var dash = Hit(1f, Enemy(2, maxHp: 90), AttackKind.NeedleDash, 90, 80);
            dash.SwingId = 3;
            e.OnHit(dash);
            var slash = Hit(1.2f, Enemy(2, maxHp: 90), AttackKind.NeedleSide, 80, 70);
            slash.SwingId = 4;
            e.OnHit(slash);
            Check(Has(awards, "ANIMATION CANCEL"), "a slash right after a landed run-slash is ANIMATION CANCEL");
            awards.Clear();
            e.Observe(3f, Air(-1f));
            e.OnGlideStarted(2f);
            e.OnGlideEnded(3f);
            e.OnHit(Hit(3.1f, Enemy(3), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(awards, "DRIFTER"), "a kill with a strike out of a glide is DRIFTER");
            awards.Clear();
            e.Observe(5f, Air(-1f));
            e.OnGlideEnded(5f);
            e.OnHit(Hit(5.1f, Enemy(4), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(!Has(awards, "DRIFTER"), "BROLLY END without a glide (a tool throw, a new room) is no DRIFTER");

            var d = NewEngine(out var drift);
            d.Observe(0f, Air(-1f));
            d.OnGlideStarted(0.5f);
            d.OnGlideEnded(1f);
            d.OnHit(Hit(1.03f, Enemy(1), AttackKind.NeedleUp, 30, 13));
            d.Observe(2f, Air(-3f));
            d.OnHit(Hit(3f, Enemy(1), AttackKind.NeedleDown, 13, -4, killed: true));
            Check(Has(drift, "DRIFTER"), "a kill later in the same jump as a strike out of a glide is DRIFTER");
            drift.Clear();
            d.OnHit(Hit(3.5f, Enemy(2), AttackKind.ClawlinePull, 5, -3, killed: true));
            Check(!Has(drift, "DRIFTER"), "one DRIFTER per glide");
            d.OnGlideStarted(4f);
            d.OnGlideEnded(4.5f);
            d.OnHit(Hit(4.6f, Enemy(3), AttackKind.Clawline, 30, 22));
            d.OnHit(Hit(5f, Enemy(3), AttackKind.ClawlinePull, 22, -2, killed: true));
            Check(Has(drift, "DRIFTER"), "a new glide pays again, and a Clawline kill counts");
            drift.Clear();
            d.OnGlideStarted(6f);
            d.OnGlideEnded(6.5f);
            d.OnHit(Hit(6.6f, Enemy(4), AttackKind.NeedleSide, 30, 13));
            d.Observe(7f, Ground());
            d.Observe(8f, Ground());
            d.OnHit(Hit(8f, Enemy(4), AttackKind.NeedleSide, 13, -4, killed: true));
            Check(!Has(drift, "DRIFTER"), "landing between the glide strike and the kill is no DRIFTER");
            d.Observe(9f, Air(-1f));
            d.OnHit(Hit(9.1f, Enemy(5), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(!Has(drift, "DRIFTER"), "a kill in the air without a glide is no DRIFTER");
        }

        private static void Combinations()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnClash(0f, Enemy(1, maxHp: 200));
            e.OnHit(Hit(0.5f, Enemy(1, maxHp: 200), AttackKind.NeedleSide, 200, 190));
            e.OnHit(ToolHit(1f, Enemy(1, maxHp: 200), Tools.StraightPin, before: 190, after: 180));
            e.OnHit(SkillHit(1.5f, Enemy(1, maxHp: 200), Skills.Silkspear, before: 180, after: 170));
            Check(!Has(awards, "MEDLEY"), "four of the five are not yet MEDLEY");
            e.Observe(2f, Air(-1f));
            e.OnHit(Hit(2f, Enemy(1, maxHp: 200), AttackKind.NeedleDown, 170, 160));
            e.OnPogoBounce(2f);
            Check(Has(awards, "MEDLEY"), "needle, tool, skill, clash and pogo within 5 s are MEDLEY");

            var s = NewEngine(out var set);
            s.Observe(0f, Ground());
            s.OnClash(0f, Enemy(1));
            s.Observe(0.5f, Air(-1f));
            s.OnHit(Hit(0.5f, Enemy(2, maxHp: 90), AttackKind.NeedleDown, 90, 80));
            s.OnPogoBounce(0.5f);
            s.OnHit(Hit(1.5f, Enemy(3), AttackKind.NeedleSide, 20, -5, killed: true));
            Check(Has(set, "SET PIECE"), "clash, pogo, kill within 2 s is SET PIECE");

            var w = NewEngine(out var wombo);
            w.Observe(0f, Ground());
            w.OnClash(0f, Enemy(1));
            w.Observe(0.3f, Air(-1f));
            w.OnHit(Hit(0.3f, Enemy(2, maxHp: 90), AttackKind.NeedleDown, 90, 80));
            w.OnPogoBounce(0.3f);
            w.OnHit(SkillHit(0.6f, Enemy(2, maxHp: 90), Skills.Silkspear, before: 80, after: 70));
            w.OnHit(ToolHit(0.9f, Enemy(2, maxHp: 90), Tools.StraightPin, before: 70, after: 60));
            for (var i = 0; i < 4; i++)
                w.OnHit(Hit(1.2f + i * 0.1f, Enemy(10 + i), AttackKind.NeedleSide, 10, -5, killed: true));
            Check(Has(wombo, "CHOIR") && wombo.Count(a => a.Name == "WOMBO COMBO") == 1,
                "clash, pogo, skill, tool and then CHOIR is WOMBO COMBO");

            var b = NewEngine(out var beat);
            b.Observe(0f, Ground());
            var kinds = new[]
            {
                AttackKind.NeedleSide, AttackKind.NeedleUp, AttackKind.NeedleSide, AttackKind.NeedleUp,
            };
            for (var i = 0; i < 4; i++)
                b.OnHit(Hit(i * 0.6f, Enemy(1, maxHp: 200), kinds[i], 200 - i * 10, 190 - i * 10));
            Check(Has(beat, "ON BEAT"), "four hits 0.6 s apart from two kinds of attack are ON BEAT");
            var m = NewEngine(out var mash);
            m.Observe(0f, Ground());
            for (var i = 0; i < 4; i++)
                m.OnHit(Hit(i * 0.3f, Enemy(1, maxHp: 200), kinds[i], 200 - i * 10, 190 - i * 10));
            for (var i = 0; i < 4; i++)
                m.OnHit(Hit(5f + i * 0.6f, Enemy(1, maxHp: 200), AttackKind.NeedleSide, 150 - i * 10, 140 - i * 10));
            Check(!Has(mash, "ON BEAT"), "mashing at the needle's cooldown or one attack on repeat is not ON BEAT");
        }

        private static void HealthExtras()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            e.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            e.OnBindStarted(1f);
            e.OnBindCompleted(2f, nearestEnemy: 2f);
            Check(Has(awards, "MENDED") && Has(awards, "RISKY BUSINESS"),
                "a bind finished right next to an enemy is RISKY BUSINESS");
            e.OnBindCompleted(2.7f, nearestEnemy: 2f);
            Check(awards.Count(a => a.Name == "MENDED") == 1 && awards.Count(a => a.Name == "RISKY BUSINESS") == 1,
                "a bind that completes twice (Multibinder) is one MENDED");
            awards.Clear();
            e.OnBindStarted(3f);
            e.OnBindCompleted(4f, nearestEnemy: 10f);
            Check(!Has(awards, "RISKY BUSINESS"), "a bind far from enemies is not");

            var s = NewEngine(out var silk);
            var full = Ground();
            full.Silk = 9;
            s.Observe(0f, full);
            s.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            for (var left = 6; left >= 0; left -= 3)
            {
                var spent = Ground();
                spent.Silk = left;
                s.OnToolUsed(1f + (6 - left) * 0.1f, Skills.Sharpdart, true);
                s.Observe(1f + (6 - left) * 0.1f, spent);
            }
            Check(Has(silk, "NO COST TOO GREAT"), "a full spool spent to nothing in a combo is NO COST TOO GREAT");
            var bind = NewEngine(out var bound);
            bind.Observe(0f, full);
            bind.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            bind.OnBindStarted(1f);
            var empty = Ground();
            empty.Silk = 0;
            bind.Observe(1.5f, empty);
            Check(!Has(bound, "NO COST TOO GREAT"), "silk spent on a bind does not count");

            // The real spool: 9 silk, 4 a skill. Two skills leave 1, too little for a third: the spool is spent.
            var uneven = NewEngine(out var unevenAwards);
            var nine = full;
            nine.SkillCost = 4;
            uneven.Observe(0f, nine);
            uneven.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            var five = nine;
            five.Silk = 5;
            uneven.OnToolUsed(1f, Skills.Sharpdart, true);
            uneven.Observe(1f, five);
            Check(!Has(unevenAwards, "NO COST TOO GREAT"),
                "one skill out of a full spool is not yet NO COST TOO GREAT");
            var one = nine;
            one.Silk = 1;
            uneven.Observe(1.8f, one);
            Check(!Has(unevenAwards, "NO COST TOO GREAT"),
                "the silk leaving before its skill's event is not yet counted");
            uneven.OnToolUsed(1.82f, Skills.Sharpdart, true);
            Check(Has(unevenAwards, "NO COST TOO GREAT"), "a spool spent until no skill is left is NO COST TOO GREAT");

            // The Architect's craft bind empties the spool with no skill and no bind event.
            var craft = NewEngine(out var crafted);
            craft.Observe(0f, nine);
            craft.OnHit(Hit(0f, Enemy(1, maxHp: 90), AttackKind.NeedleSide, 90, 80));
            var drained = nine;
            drained.Silk = 0;
            craft.Observe(1f, drained);
            craft.OnRestock(1.9f);
            Check(!Has(crafted, "NO COST TOO GREAT"), "silk spent on the Architect's craft bind does not count");
        }

        private static void BossFights()
        {
            var e = NewEngine(out var awards);
            e.Observe(0f, Ground());
            var knight = Boss(1, "SONG_KNIGHT");
            e.OnBossTitle(0f, knight);
            e.OnHit(Hit(1f, knight, AttackKind.NeedleSide, 800, 400));
            e.OnHit(Hit(2f, knight, AttackKind.NeedleSide, 400, -5, killed: true));
            Check(!Has(awards, "CHANGING OF THE GUARD"), "the result waits for the end of the fight");
            e.OnBossDeath(6f);
            var win = awards.FirstOrDefault(a => a.Name == "CHANGING OF THE GUARD");
            Check(Near(win.Total, 500f),
                "beating the Second Sentinel is CHANGING OF THE GUARD, 500 flat (the fight is over)");
            Check(Has(awards, "FLAWLESS VICTORY"), "a boss beaten without a hit is FLAWLESS VICTORY");
            Check(!Has(awards, "JACKPOT!") && !Has(awards, "SHOWSTOPPER"),
                "a needle blow below SILKSONG is neither JACKPOT! nor SHOWSTOPPER");

            var f = NewEngine(out var finale);
            f.Observe(0f, Ground());
            var stranger = Boss(1, "SOMEONE_NEW");
            f.OnBossTitle(0f, stranger);
            f.OnHeroDamaged(0.5f, soft: false);
            f.OnClash(1f, stranger);
            f.OnHit(ToolHit(1.2f, stranger, Tools.StraightPin, killed: true));
            f.Tick(5f, 0.016f, true);
            Check(!Has(finale, "FINALE"), "without BOSS DEATH the result waits a while");
            f.Tick(1.2f + Tuning.BossEndFallback, 0.016f, true);
            Check(Has(finale, "FINALE") && !Has(finale, "FLAWLESS VICTORY"),
                "a boss without a name of its own is FINALE; a hit loses FLAWLESS");
            Check(Has(finale, "JACKPOT!") && Has(finale, "GREAT ENEMY FELLED"),
                "a tool riposte as the final blow is JACKPOT! and GREAT ENEMY FELLED");

            var top = NewEngine(out var show);
            top.Observe(0f, Ground());
            var pin = Boss(1, BossBonuses.Pinstress);
            top.OnBossTitle(0f, pin);
            top.Meter.Add(20000f);
            Check(top.Meter.Rank == Tuning.Silksong, "the meter can be pushed to SILKSONG for the test");
            top.OnHit(Hit(1f, pin, AttackKind.NeedleCharge, 50, -5, killed: true));
            top.OnBossDeath(2f);
            Check(Has(show, "PINNED DOWN") && Has(show, "SHOWSTOPPER") && Has(show, "STUDENT BECOMES THE MASTER"),
                "Pinstress finished by a charged strike at SILKSONG is PINNED DOWN, SHOWSTOPPER, "
                + "STUDENT BECOMES THE MASTER");

            var lace = NewEngine(out var duel);
            lace.Observe(0f, Ground());
            var fencer = Boss(1, "LACE_2");
            lace.OnBossTitle(0f, fencer);
            lace.OnClash(1f, fencer);
            lace.OnClash(1.5f, fencer);
            Check(duel.Count(a => a.Name == "EN GARDE!") == 1, "a clash with Lace is EN GARDE!, once per fight");
            lace.OnHeroDied(2f);
            lace.OnHit(Hit(3f, fencer, AttackKind.NeedleSide, 20, -5, killed: true));
            lace.OnBossDeath(4f);
            Check(!Has(duel, "TOUCHÉ"), "dying ends the fight without a result");

            var scripted = NewEngine(out var spared);
            scripted.Observe(0f, Ground());
            var garmond = Boss(1, "GARMOND");
            scripted.OnBossTitle(0f, garmond);
            scripted.OnHit(Hit(1f, garmond, AttackKind.NeedleSide, 800, 700));
            scripted.OnBossDeath(5f);
            Check(spared.Any(a => a.Name == "GOOD SPORT" && Near(a.Total, 300f)),
                "a duel that ends without a death still pays its result");

            var tacks = NewEngine(out var step);
            tacks.Observe(0f, Ground());
            var walker = Boss(1, "SKULL_KING");
            tacks.OnBossTitle(0f, walker);
            tacks.OnHit(ToolHit(1f, walker, Tools.Tacks, before: 500, after: 495));
            tacks.OnHit(ToolHit(1.5f, walker, Tools.Tacks, before: 495, after: 490));
            Check(step.Count(a => a.Name == "WATCH YOUR STEP") == 1,
                "a boss stepping on Tacks is WATCH YOUR STEP, once per fight");
        }

        private static EnemyInfo Boss(int id, string key) =>
            new EnemyInfo { Id = id, Name = "boss " + id, MaxHp = 800, IsBoss = true, BossKey = key };

        private static HeroState Ground(string crest = null, int hunterStage = 0, bool rage = false,
            bool reaper = false) =>
            new HeroState
            {
                OnGround = true,
                Health = 5,
                MaxHealth = 5,
                SilkMax = 9,
                Crest = crest,
                HunterStage = hunterStage,
                InRage = rage,
                InReaperMode = reaper,
            };

        private static HeroState Air(float velocityY, string crest) =>
            new HeroState { VelocityY = velocityY, Health = 5, MaxHealth = 5, SilkMax = 9, Crest = crest };

        private static HitEvent ToolHit(float t, EnemyInfo enemy, string tool, bool killed = false, int swing = 0,
            float distance = 0f, int before = 20, int after = -5)
        {
            var hit = Hit(t, enemy, AttackKind.Tool, before, killed ? after : System.Math.Max(after, 1), killed,
                tool: tool);
            hit.SwingId = swing;
            hit.Distance = distance;
            return hit;
        }

        private static HitEvent SkillHit(float t, EnemyInfo enemy, string skill, bool killed = false, int swing = 0,
            float distance = 0f, int before = 20, int after = -5)
        {
            var hit = Hit(t, enemy, AttackKind.Skill, before, killed ? after : System.Math.Max(after, 1), killed,
                tool: skill);
            hit.SwingId = swing;
            hit.Distance = distance;
            return hit;
        }

        private static HitEvent Swung(float t, EnemyInfo enemy, int swing, bool killed)
        {
            var hit = Hit(t, enemy, AttackKind.NeedleSide, 20, killed ? -5 : 10, killed);
            hit.SwingId = swing;
            return hit;
        }
    }
}
