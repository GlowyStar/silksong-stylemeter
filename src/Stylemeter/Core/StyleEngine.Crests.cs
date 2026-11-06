namespace Stylemeter.Core
{
    // Crest bonuses that are not a kill type (BONUSES.md section 5): Hunter focus, Beast rage, Reaper harvest, Witch
    // roots, Architect drill, Shaman skills, the curse, the cloakless prison and Wanderer's knightly pogo.
    internal sealed partial class StyleEngine
    {
        private int rageKills;
        private bool ripAndTearGiven;
        private bool hexedGiven;

        private void ObserveCrest(HeroState was)
        {
            if (Hero.InRage && !was.InRage)
            {
                rageKills = 0;
                ripAndTearGiven = false;
                if (Meter.InCombo)
                    Give(Bonuses.BeastMode, Peek(null, false));
            }
            if (Meter.InCombo && Hero.Crest == was.Crest && Hero.HunterStage > was.HunterStage)
                Give(Hero.HunterStage >= 2 ? Bonuses.ApexPredator : Bonuses.TheHuntIsOn, Peek(null, false));
        }

        // Witch's bind roots reached an enemy: once per bind.
        private void CrestHit(HitEvent hit, Scoring s)
        {
            if (Hero.Crest != Crests.Witch || !hit.FromBind || hit.Killed || hexedGiven)
                return;
            hexedGiven = true;
            Give(Bonuses.Hexed, s);
        }

        private void CrestKill(HitEvent hit, Scoring s, bool late)
        {
            if (hit.HunterCombo)
                Give(Bonuses.Trophy, s);
            if (hit.RageHit || (!late && Hero.InRage))
            {
                Give(Bonuses.Bloodlust, s);
                if (++rageKills >= Tuning.UseKills && !ripAndTearGiven)
                {
                    ripAndTearGiven = true;
                    Give(Bonuses.RipAndTear, s);
                }
            }
            if (!late && Hero.InReaperMode)
                Give(Bonuses.DontFearTheReaper, s);
            if (Hero.Crest == Crests.Shaman && hit.Kind == AttackKind.Skill)
                Give(Bonuses.Spellbound, s);
            if (Hero.Crest == Crests.Cursed)
                Give(Bonuses.CursedBlade, s);
            if (Hero.Crest == Crests.Cloakless)
                Give(Bonuses.NoCloakNoProblem, s);
            if (Hero.Crest == Crests.Architect && hit.Kind == AttackKind.NeedleDash)
            {
                var swing = SwingOf(hit);
                if (swing != null && ++swing.Kills == 2)
                    Give(Bonuses.PierceTheHeavens, s);
            }
        }
    }
}
