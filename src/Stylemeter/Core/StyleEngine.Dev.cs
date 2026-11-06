namespace Stylemeter.Core
{
    // Sandbox only (the bridge's "meter" and "feed"): set the meter and the feed directly to look at the display.
    internal sealed partial class StyleEngine
    {
        public void ForceMeter(int rank, float fill)
        {
            Meter.Force(rank, fill);
            lastPointsAt = Now;
        }

        public void ForceFeed(string name, int count, int weight = 0)
        {
            if (count > 1)
                Feed.PushChain(name, count, 0f, Now, weight);
            else
                Feed.Push(name, 0f, Now, weight);
        }
    }
}
