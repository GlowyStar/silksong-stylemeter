using HarmonyLib;
using UnityEngine;

namespace Stylemeter.Game
{
    // What a hit met, read in the HealthManager.Hit prefix before the enemy can turn or drop its guard: a guard the
    // hit pierced, a shield it went around, a back it struck.
    internal struct HitContext
    {
        public bool Pierced;
        public bool Flanked;
        public bool Backstab;

        private static readonly AccessTools.FieldRef<Walker, float> WalkerRight =
            GameField.Find<Walker, float>("rightScale");
        private static readonly AccessTools.FieldRef<Crawler, bool> CrawlerFacesRight =
            GameField.Find<Crawler, bool>("spriteFacesRight");

        public static HitContext Read(HealthManager hm, HitInstance hit)
        {
            var context = new HitContext();
            if (!hit.IsHeroDamage)
                return context;
            // The cardinal direction the hit travels, as HealthManager.Hit computes it: 0 right, 1 up, 2 left, 3 down.
            var card = DirectionUtils.GetCardinalDirection(
                hit.GetActualDirection(hm.transform, HitInstance.TargetType.Regular));
            if (hm.IsInvincible)
            {
                var blocks = hm.IsBlockingByDirection(card, hit.AttackType, hit.SpecialType);
                var wouldBlock = hm.IsBlockingByDirection(card, hit.AttackType,
                    hit.SpecialType & ~SpecialTypes.Piercer);
                context.Pierced = wouldBlock && !blocks;
                context.Flanked = hm.InvincibleFromDirection != 0 && !wouldBlock;
            }
            if (hit.IsNailDamage && (card == 0 || card == 2) && TryFacingRight(hm, out var facingRight))
                context.Backstab = card == 0 ? facingRight : !facingRight;
            return context;
        }

        // Only enemies with a walking or climbing brain have a known front; flyers and blobs have no back.
        private static bool TryFacingRight(HealthManager hm, out bool right)
        {
            right = false;
            var scale = hm.transform.localScale.x;
            var walker = hm.GetComponent<Walker>();
            if (walker && WalkerRight != null)
            {
                right = scale * WalkerRight(walker) >= 0f;
                return true;
            }
            var walker2 = hm.GetComponent<WalkerV2>();
            if (walker2)
            {
                right = Mathf.Sign(scale) * walker2.RightDirection > 0f;
                return true;
            }
            var climber = hm.GetComponent<Climber>();
            if (climber)
            {
                right = climber.IsFacingRight;
                return true;
            }
            var crawler = hm.GetComponent<Crawler>();
            if (crawler && CrawlerFacesRight != null)
            {
                right = Mathf.Sign(scale) * (CrawlerFacesRight(crawler) ? 1f : -1f) > 0f;
                return true;
            }
            return false;
        }
    }
}
