using System.Collections.Generic;

namespace Stylemeter.Core
{
    internal sealed class FeedLine
    {
        public string Name;
        public int Count;
        public float Points;
        public float ExpiresAt;
        // The heaviest bonus on the line (BonusDef.Weight), for its colour.
        public int Weight;
    }

    // The style feed (MECHANICS.md §8): newest line on top, each lives 3 s, a repeat of the top line grows its
    // counter instead of adding a line.
    internal sealed class StyleFeed
    {
        private readonly List<FeedLine> lines = new List<FeedLine>();

        public IReadOnlyList<FeedLine> Lines => lines;

        public void Push(string name, float points, float now, int weight = 0)
        {
            if (lines.Count > 0 && lines[0].Name == name)
            {
                Refresh(lines[0], lines[0].Count + 1, points, now, weight);
                return;
            }
            lines.Insert(0, new FeedLine
            {
                Name = name,
                Count = 1,
                Points = points,
                ExpiresAt = now + Tuning.FeedLineLife,
                Weight = weight
            });
        }

        // Chains (POGO x5, DEFLECT x3, CHOIR x4) carry their own count and keep one line however they interleave.
        public void PushChain(string name, int count, float points, float now, int weight = 0)
        {
            var index = lines.FindIndex(l => l.Name == name);
            var line = index >= 0 ? lines[index] : new FeedLine { Name = name };
            if (index >= 0)
                lines.RemoveAt(index);
            lines.Insert(0, line);
            Refresh(line, count, points, now, weight);
        }

        public void Expire(float now) => lines.RemoveAll(l => l.ExpiresAt <= now);

        private static void Refresh(FeedLine line, int count, float points, float now, int weight)
        {
            line.Count = count;
            line.Points += points;
            line.ExpiresAt = now + Tuning.FeedLineLife;
            if (weight > line.Weight)
                line.Weight = weight;
        }
    }
}
