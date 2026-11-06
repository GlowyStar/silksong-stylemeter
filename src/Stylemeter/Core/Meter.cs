using System;

namespace Stylemeter.Core
{
    // Rank and fill with ULTRAKILL's rules (MECHANICS.md §2): full meter ranks up keeping the overflow (at least a
    // quarter), an empty one ranks down to three quarters, and dropping below the first rank ends the combo.
    internal sealed class StyleMeter
    {
        public int Rank { get; private set; } = -1;
        public float Fill { get; private set; }

        public bool InCombo => Rank >= 0;
        public float Size => InCombo ? Tuning.Ranks[Rank].Size : 0f;

        // (from, to); -1 means no combo.
        public event Action<int, int> RankChanged;

        // Sandbox only (the bridge's "meter"): put the meter at a rank and fill for looking at the display.
        public void Force(int rank, float fill)
        {
            Rank = Math.Max(-1, Math.Min(Tuning.Ranks.Length - 1, rank));
            Fill = InCombo ? Math.Max(0f, Math.Min(1f, fill)) * Size : 0f;
        }

        public void Add(float points)
        {
            if (points <= 0f)
                return;
            if (!InCombo)
            {
                SetRank(0);
                Fill = Tuning.ComboStartFill * Size;
            }
            Fill += points;
            while (Fill >= Size)
            {
                if (Rank == Tuning.Ranks.Length - 1)
                {
                    Fill = Size;
                    break;
                }
                var overflow = Fill - Size;
                SetRank(Rank + 1);
                Fill = Math.Max(overflow, Tuning.RankUpFloor * Size);
            }
        }

        public void Drain(float dt)
        {
            if (!InCombo)
                return;
            var top = Rank >= Tuning.Sublime ? Tuning.TopDrainScale : 1f;
            var peak = Rank == Tuning.Silksong ? Tuning.PeakDrainScale : 1f;
            Remove(Tuning.Ranks[Rank].Drain * Tuning.DrainScale * top * peak * dt);
        }

        public void Remove(float points)
        {
            if (!InCombo || points <= 0f)
                return;
            Fill -= points;
            if (Fill < 0f)
                StepDown();
        }

        public void DropRank()
        {
            if (InCombo)
                StepDown();
        }

        public void EndCombo()
        {
            if (!InCombo)
                return;
            Fill = 0f;
            SetRank(-1);
        }

        private void StepDown()
        {
            if (Rank == 0)
            {
                EndCombo();
                return;
            }
            SetRank(Rank - 1);
            Fill = Tuning.RankDownFill * Size;
        }

        private void SetRank(int rank)
        {
            var from = Rank;
            Rank = rank;
            if (from != rank)
                RankChanged?.Invoke(from, rank);
        }
    }
}
