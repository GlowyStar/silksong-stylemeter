using System.Collections.Generic;
using System.Linq;

namespace Stylemeter.Core
{
    // What one throw of a tool or one cast of a skill did (BONUSES.md sections 4, 6 and 7), and the arsenal bonuses of
    // a whole combo: TOOLBOX, SILK ROAD, TRAP CARD.
    internal sealed partial class StyleEngine
    {
        private sealed class Use
        {
            public float Start;
            public bool FromAir;
            public bool Open = true;
            public int Hits;
            public int Kills;
            // Quick Sling threw the same tool again: both throws count as one use (DOUBLE TAP).
            public bool Auto;
            public readonly HashSet<int> Enemies = new HashSet<int>();
            public readonly HashSet<int> Objects = new HashSet<int>();
            public readonly HashSet<int> Zapped = new HashSet<int>();
            public readonly Dictionary<int, int> HitsOn = new Dictionary<int, int>();
            public readonly HashSet<BonusDef> Given = new HashSet<BonusDef>();
        }

        private readonly Dictionary<string, Use> uses = new Dictionary<string, Use>();
        private readonly Dictionary<string, float> toolUses = new Dictionary<string, float>();
        private readonly Dictionary<string, float> skillUses = new Dictionary<string, float>();
        private readonly List<float> cannonHits = new List<float>();
        private int trapKills;
        private bool toolboxGiven;
        private bool silkRoadGiven;

        // Attack tools Hornet has equipped (red slots), kept current by the game side.
        public string[] AttackTools = new string[0];

        // Hornet threw a tool or cast a skill (the game's ThrowTool); Quick Sling's second throw is auto.
        public void OnToolUsed(float now, string tool, bool skill, bool auto = false)
        {
            Now = now;
            if (string.IsNullOrEmpty(tool))
                return;
            if (auto && uses.TryGetValue(tool, out var first) && now - first.Start <= Tuning.QuickSlingGap)
            {
                first.Auto = true;
                return;
            }
            uses[tool] = new Use { Start = now, FromAir = !Hero.OnGround };
            (skill ? skillUses : toolUses)[tool] = now;
            if (skill)
                SkillAfterDrain();
            if (!Meter.InCombo)
                return;
            if (tool == Tools.PlasmiumPhial)
                Give(Bonuses.Lifeblood, Peek(null, false));
            if (!silkRoadGiven && UsedThisCombo(skillUses) >= 3)
            {
                silkRoadGiven = true;
                Give(Bonuses.SilkRoad, Peek(null, false));
            }
            if (!toolboxGiven && AttackTools.Length >= 2
                && AttackTools.All(t => toolUses.TryGetValue(t, out var at) && at >= ComboUseStart))
            {
                toolboxGiven = true;
                Give(Bonuses.Toolbox, Peek(null, false));
            }
        }

        // Flintslate's fire has left the needle (the game's FLINT SLATE EXPIRE).
        public void OnFlintslateExpired(float now)
        {
            Now = now;
            if (uses.TryGetValue(Tools.Flintslate, out var use))
                use.Open = false;
        }

        // A use made just before the first points of a combo still belongs to it.
        private float ComboUseStart => comboStart - Tuning.UseWindow;

        private int UsedThisCombo(Dictionary<string, float> used) => used.Values.Count(t => t >= ComboUseStart);

        private void UseHit(HitEvent hit, Scoring s)
        {
            if (hit.Kind != AttackKind.Tool && hit.Kind != AttackKind.Skill)
                return;
            if (hit.Tool == Tools.RosaryCannon)
                MakeItRain(s);
            var use = UseOf(hit.Tool);
            use.Hits++;
            use.Enemies.Add(hit.Enemy.Id);
            if (hit.SwingId != 0)
                use.Objects.Add(hit.SwingId);
            use.HitsOn.TryGetValue(hit.Enemy.Id, out var onThis);
            use.HitsOn[hit.Enemy.Id] = ++onThis;
            if (hit.Zap)
                use.Zapped.Add(hit.Enemy.Id);
            if (use.Auto && hit.Kind == AttackKind.Tool && !Tools.IsMultiShot(hit.Tool))
                Once(use, Bonuses.DoubleTap, use.Objects.Count >= 2, s);
            switch (hit.Tool)
            {
                case Skills.Silkspear: Once(use, Bonuses.ShishKebab, use.Enemies.Count >= 2, s); break;
                case Skills.ThreadStorm: Once(use, Bonuses.EyeOfTheStorm, use.Enemies.Count >= 3, s); break;
                case Skills.Sharpdart: Once(use, Bonuses.Pinball, use.Enemies.Count >= 2, s); break;
                case Skills.PaleNails: Once(use, Bonuses.HatTrick, use.Objects.Count >= 3, s); break;
                case Tools.ThreefoldPin: Once(use, Bonuses.Trifecta, use.Objects.Count >= 3, s); break;
                case Tools.ThrowingRing: Once(use, Bonuses.Ricochet, use.Enemies.Count >= 2, s); break;
                case Tools.SilkshotArchitect: Once(use, Bonuses.ThreeRoundBurst, use.Hits >= 3, s); break;
                case Tools.SilkshotWeaver: Once(use, Bonuses.Piercer, use.Enemies.Count >= 2, s); break;
                case Tools.Conchcutter: Once(use, Bonuses.FullSpin, onThis >= 6, s); break;
            }
            if (hit.Kind == AttackKind.Skill)
                Once(use, Bonuses.Conductor, use.Zapped.Count >= 2, s);
        }

        private void UseKill(HitEvent hit, Scoring s)
        {
            if (hit.Kind == AttackKind.Tool || hit.Kind == AttackKind.Skill)
            {
                var use = UseOf(hit.Tool);
                use.Kills++;
                switch (hit.Tool)
                {
                    case Skills.RuneRage: Once(use, Bonuses.RageQuit, use.Kills >= Tuning.UseKills, s); break;
                    case Skills.PaleNails: Once(use, Bonuses.NailedIt, use.Kills >= 2, s); break;
                    case Tools.Pimpillo: Once(use, Bonuses.BombsAway, use.FromAir, s); break;
                }
                if (Tools.IsTrap(hit.Tool) && ++trapKills == 2)
                    Give(Bonuses.TrapCard, s);
                if (Tools.IsCurveclaw(hit.Tool) && hit.ReturnLeg)
                    Give(Bonuses.Boomerang, s);
                if (hit.Tool == Tools.ThrowingRing && hit.AfterWallBounce)
                    Give(Bonuses.TrickShot, s);
                if (hit.Tool == Tools.CogworkWheel && hit.WheelClimbed)
                    Give(Bonuses.WallRide, s);
                if (hit.Tool == Tools.StingShard && hit.Shrapnel)
                    Give(Bonuses.Shrapnel, s);
            }
            if (hit.Element == Element.Fire && uses.TryGetValue(Tools.Flintslate, out var flint)
                && flint.Open && Now - flint.Start <= Tuning.FlintslateMaxTime)
            {
                flint.Kills++;
                Once(flint, Bonuses.ShesOnFire, flint.Kills >= Tuning.UseKills, s);
            }
            if (uses.TryGetValue(Tools.FleaBrew, out var brew) && Now - brew.Start <= Tuning.FleaBrewTime)
            {
                brew.Kills++;
                Once(brew, Bonuses.GottaGoFast, brew.Kills >= Tuning.UseKills, s);
            }
        }

        // The use a hit belongs to: the last throw of that tool, or a new one when the hook missed the throw.
        private Use UseOf(string tool)
        {
            if (!uses.TryGetValue(tool, out var use) || Now - use.Start > Tuning.UseWindow)
                uses[tool] = use = new Use { Start = Now, FromAir = !Hero.OnGround };
            return use;
        }

        private void Once(Use use, BonusDef bonus, bool condition, Scoring s)
        {
            if (condition && use.Given.Add(bonus))
                Give(bonus, s);
        }

        private void MakeItRain(Scoring s)
        {
            cannonHits.RemoveAll(t => Now - t > Tuning.MakeItRainWindow);
            cannonHits.Add(Now);
            if (cannonHits.Count < Tuning.MakeItRainHits)
                return;
            cannonHits.Clear();
            Give(Bonuses.MakeItRain, s);
        }

        private void ResetComboTools()
        {
            trapKills = 0;
            toolboxGiven = silkRoadGiven = false;
        }
    }
}
