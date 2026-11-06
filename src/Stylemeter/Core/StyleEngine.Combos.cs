using System;
using System.Collections.Generic;
using System.Linq;

namespace Stylemeter.Core
{
    // Combinations of different actions (BONUSES.md section 13): MEDLEY, SET PIECE, WOMBO COMBO and ON BEAT.
    internal sealed partial class StyleEngine
    {
        private float lastNeedleAt = Never;
        private float lastPogoAt = Never;
        private float lastToolAt = Never;
        private float lastSkillAt = Never;
        private float medleyAt = Never;
        private float setPieceClash = Never;
        private readonly List<float> beatTimes = new List<float>();
        private readonly List<AttackKind> beatKinds = new List<AttackKind>();

        private void ComboHit(HitEvent hit)
        {
            if (hit.Kind == AttackKind.Tool)
                lastToolAt = Now;
            else if (hit.Kind == AttackKind.Skill)
                lastSkillAt = Now;
            else if (Attacks.IsNeedle(hit.Kind) && hit.Kind != AttackKind.NeedleDown)
                lastNeedleAt = Now;
            Medley(hit.Enemy.IsBoss);
            OnBeat(hit);
        }

        // Needle, pogo, tool, skill and clash all within 5 s; the next MEDLEY needs all five again.
        private void Medley(bool vsBoss)
        {
            var after = Math.Max(Now - Tuning.MedleyWindow, medleyAt);
            if (lastNeedleAt > after && lastPogoAt > after && lastToolAt > after && lastSkillAt > after
                && lastClash > after)
            {
                medleyAt = Now;
                Give(Bonuses.Medley, Peek(null, vsBoss));
            }
        }

        // A clash or parry, then a pogo, then this kill, all within 2 s.
        private void SetPiece(bool vsBoss)
        {
            if (Now - lastClash > Tuning.SetPieceWindow || lastPogoAt < lastClash || setPieceClash == lastClash)
                return;
            setPieceClash = lastClash;
            Give(Bonuses.SetPiece, Peek(null, vsBoss));
        }

        // Clash, pogo, skill and tool within 3 s, crowned by a CHOIR.
        private void WomboCombo(Scoring s)
        {
            var since = Now - Tuning.WomboComboWindow;
            if (womboGiven || lastClash < since || lastPogoAt < since || lastSkillAt < since || lastToolAt < since)
                return;
            womboGiven = true;
            Give(Bonuses.WomboCombo, s);
        }

        // Four hits in a row at an even beat, slower than mashing and from at least two kinds of attack: mashing one
        // button lands hits at the needle's own cooldown, which would be perfectly even for free.
        private void OnBeat(HitEvent hit)
        {
            if (beatTimes.Count > 0 && Now - beatTimes[beatTimes.Count - 1] < Tuning.OnBeatSameHit)
                return;
            beatTimes.Add(Now);
            beatKinds.Add(hit.Kind);
            if (beatTimes.Count > 4)
            {
                beatTimes.RemoveAt(0);
                beatKinds.RemoveAt(0);
            }
            if (beatTimes.Count < 4)
                return;
            var mean = (beatTimes[3] - beatTimes[0]) / 3f;
            if (mean < Tuning.OnBeatMinGap || mean > Tuning.OnBeatMaxGap || beatKinds.Distinct().Count() < 2)
                return;
            for (var i = 1; i < 4; i++)
            {
                if (Math.Abs(beatTimes[i] - beatTimes[i - 1] - mean) > Tuning.OnBeatTolerance)
                    return;
            }
            beatTimes.Clear();
            beatKinds.Clear();
            Give(Bonuses.OnBeat, Peek(null, hit.Enemy.IsBoss));
        }
    }
}
