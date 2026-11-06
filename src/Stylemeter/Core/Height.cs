using System;

namespace Stylemeter.Core
{
    // The height multiplier (MECHANICS.md §4): every scoring action in the air adds 0.1 up to x2, landing resets it
    // after a short grace, clinging to a wall holds it without growth.
    internal sealed class HeightTracker
    {
        private bool grounded = true;
        private bool onWall;
        private float landedAt = float.NegativeInfinity;

        public float Multiplier { get; private set; } = 1f;

        public void Observe(float now, bool onGround, bool wall)
        {
            onWall = wall && !onGround;
            if (!onGround)
            {
                grounded = false;
                return;
            }
            if (!grounded)
            {
                grounded = true;
                landedAt = now;
            }
            if (now - landedAt > Tuning.LandingGrace)
                Multiplier = 1f;
        }

        public bool Airborne(float now) => !grounded || now - landedAt <= Tuning.LandingGrace;

        // Multiplier for this action without growing it.
        public float Current(float now) => Airborne(now) ? Multiplier : 1f;

        // Multiplier for one scoring action; an action in the air then grows it for the next one.
        public float Take(float now)
        {
            if (!Airborne(now))
                return 1f;
            var current = Multiplier;
            if (!onWall)
                Multiplier = Math.Min(Tuning.HeightMax, Multiplier + Tuning.HeightStep);
            return current;
        }
    }
}
