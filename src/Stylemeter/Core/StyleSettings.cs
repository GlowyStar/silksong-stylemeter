using System;
using System.Collections.Generic;
using System.Linq;

namespace Stylemeter.Core
{
    // What the player set in the mod menu: bonuses switched off and the points multiplier. Kept apart from the config
    // file, so the engine and its tests never touch BepInEx; the game side copies the config in and out.
    internal sealed class StyleSettings
    {
        public const float MinMultiplier = 0.25f;
        public const float MaxMultiplier = 3f;
        // Names in the config text are separated by this: several names hold commas (RUN, FORREST, RUN!).
        public const char Separator = '|';

        private readonly HashSet<string> disabled = new HashSet<string>(StringComparer.Ordinal);
        private float multiplier = 1f;

        public event Action Changed;

        // Every point of every bonus is multiplied by this before it reaches the meter.
        public float PointsMultiplier
        {
            get => multiplier;
            set
            {
                var clamped = Math.Max(MinMultiplier, Math.Min(MaxMultiplier, value));
                if (Math.Abs(clamped - multiplier) < 0.0001f)
                    return;
                multiplier = clamped;
                Changed?.Invoke();
            }
        }

        // Holding SILKSONG pays silk with a SILK line in the feed; when the player turns it off, neither comes.
        public bool IsSilkRewardOn { get; set; } = true;

        public int DisabledCount => disabled.Count;

        public bool IsEnabled(string name) => !disabled.Contains(name);

        public void Toggle(string name)
        {
            if (!disabled.Remove(name))
                disabled.Add(name);
            Changed?.Invoke();
        }

        // A whole section at once raises Changed once, so the config file is written once.
        public void Enable(IEnumerable<string> names)
        {
            var isChanged = false;
            foreach (var name in names)
                isChanged |= disabled.Remove(name);
            if (isChanged)
                Changed?.Invoke();
        }

        public void Disable(IEnumerable<string> names)
        {
            var isChanged = false;
            foreach (var name in names)
                isChanged |= disabled.Add(name);
            if (isChanged)
                Changed?.Invoke();
        }

        public string DisabledText =>
            string.Join(Separator.ToString(), disabled.OrderBy(name => name, StringComparer.Ordinal));

        // Unknown names are kept: a bonus renamed in a later version simply stays on.
        public void LoadDisabled(string text)
        {
            disabled.Clear();
            foreach (var part in (text ?? "").Split(Separator))
            {
                var name = part.Trim();
                if (name.Length > 0)
                    disabled.Add(name);
            }
            Changed?.Invoke();
        }
    }
}
