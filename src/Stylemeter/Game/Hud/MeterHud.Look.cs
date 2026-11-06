using Stylemeter.Core;
using TMProOld;
using UnityEngine;

namespace Stylemeter.Game.Hud
{
    // How the rank word and the feed look: the initial in a deeper tone of the rank's colour, set tight, with an ink
    // line and a glow of its own colour; SILKSONG white-hot gold with a golden light spreading evenly from the middle
    // and a golden glint running along the letters; the bloody needle at SSSILKSTORM; feed lines coloured by weight.
    internal static partial class MeterHud
    {
        private const int BloodyRank = 6;
        private const float BackdropSize = 108f, BackdropAlpha = 0.14f;
        // The large letters stand at the right end of the needle with their baseline just above the shaft: the shaft's
        // top there is about 65 below the block's top.
        private const float BackdropBaseline = -60f;

        private static readonly string[] Tiers = { "D", "C", "B", "A", "S", "SS", "SSS", "" };
        private static readonly Color[] RankColors =
        {
            Hex(0x99B4D4), Hex(0xA9CD8B), Hex(0xDDB665), Hex(0xEEE7D5),
            Hex(0xF6CF58), Hex(0xF3879E), Hex(0xFF4D5A), Hex(0xFFF1DC),
        };
        // The initial's own colour: the rank's hue, deeper and more saturated than the word, so it stands out from it.
        private static readonly Color[] InitialColors =
        {
            Hex(0x7EA6DE), Hex(0x8CC968), Hex(0xE8AA3A), Hex(0xFFF4DA),
            Hex(0xFFC52E), Hex(0xFF6B93), Hex(0xFF2B45), Hex(0xFFFFFF),
        };
        // Feed weights (BonusDef.Weight): plain, gold, rose, red, and the silk reward.
        private static readonly Color[] FeedColors =
        {
            Hex(0xF3E9D6), Hex(0xE6C477), Hex(0xF7A1B4), Hex(0xFF4D5A), Hex(0xFFE08A),
        };
        private static readonly Color Count = Hex(0xD8B46A);
        // SILKSONG: warm white above, deep gold below (so the glint shows), a golden halo and a brown ink line.
        private static readonly Color DivineTop = Hex(0xFFF6DE), DivineBottom = Hex(0xE2A93C),
            GlintGold = Hex(0xFFEAA8);
        private static readonly Color DivineGlow = Hex(0xFFC548), DivineInk = Hex(0x5E360B);

        // 1: deeper rank colour with an ink line and its own glow; 0: metal with a gold glow; 2: deeper rank colour in
        // a thin gold rim. The sandbox's "hudset initials" switches them for comparison.
        private static int initialStyle = 1;
        private static bool backdropRight = true;

        private static Color RankColor(int rank) => rank >= 0 ? RankColors[rank] : Cream;

        private static Color FeedColor(int weight) => FeedColors[Mathf.Clamp(weight, 0, FeedColors.Length - 1)];

        private static string FeedText(FeedLine line)
        {
            var count = line.Count > 1
                ? " <size=80%><color=#" + HexOf(Count, 1f) + ">x" + line.Count + "</color></size>"
                : "";
            return "<color=#" + HexOf(FeedColor(line.Weight), 0.75f) + ">+</color> " + line.Name + count;
        }

        private static void SetRank(int rank)
        {
            var top = rank == Tuning.Silksong;
            var name = rank >= 0 ? Tuning.Ranks[rank].Name : "";
            var tier = rank >= 0 ? Tiers[rank] : "";
            var color = RankColor(rank);

            backdrop.text = tier;
            backdrop.color = new Color(color.r, color.g, color.b, BackdropAlpha);

            prefix.text = tier;
            StyleInitial(rank);

            // Another font than Trajan gets TextMeshPro's own small capitals for the lower case.
            rest.fontSize = top ? TopSize : WordSize;
            rest.characterSpacing = top ? 4f : 3f;
            rest.text = top ? name : name.Substring(tier.Length).ToLowerInvariant();
            rest.fontStyle = !top && !TitleIsTrajan ? FontStyles.SmallCaps : FontStyles.Normal;
            rest.enableVertexGradient = false;
            if (top)
            {
                // The letters' colours come from Shine every frame.
                rest.color = Color.white;
                Underlay(rest, new Color(DivineGlow.r, DivineGlow.g, DivineGlow.b, 0.9f), silksongRim);
                Outline(rest, DivineInk, 0.12f);
            }
            else
            {
                rest.color = color;
                Outline(rest, Color.black, 0f);
                Shadow(rest, 0.85f);
            }

            // The whole word, initial and rest, centred over the needle.
            var center = new Vector2(0.5f, 0.5f);
            var restWidth = rest.text.Length > 0 ? rest.GetPreferredValues(rest.text).x : 0f;
            var initialWidth = tier.Length > 0 ? prefix.GetPreferredValues(tier).x : 0f;
            var gap = tier.Length > 0 ? 2f : 0f;
            var left = -(initialWidth + gap + restWidth) / 2f;
            Place(prefix.rectTransform, center, new Vector2(0f, 0.5f), new Vector2(left, 0f),
                new Vector2(initialWidth + 40f, 120f));
            Place(rest.rectTransform, center, new Vector2(0f, 0.5f), new Vector2(left + initialWidth + gap, 0f),
                new Vector2(restWidth + 40f, 120f));

            aura.enabled = top && aura.sprite;
            raysHolder.gameObject.SetActive(top && rays.sprite);
            if (top)
            {
                Place(aura.rectTransform, center, center, new Vector2(0f, TopSize * 0.34f),
                    new Vector2(restWidth + 170f, TopSize * 2.6f));
                rays.rectTransform.sizeDelta = new Vector2(restWidth + 300f, restWidth + 300f);
                rest.ForceMeshUpdate();
            }

            unlit.sprite = top ? HudArt.Rapier : HudArt.Needle;
            var art = top ? HudArt.Rapier : rank == BloodyRank ? HudArt.NeedleBloody : HudArt.Needle;
            lit.sprite = art ? art : HudArt.Needle;
            glow.sprite = top ? HudArt.RapierGlow
                : rank == BloodyRank && HudArt.BloodGlow ? HudArt.BloodGlow
                : HudArt.NeedleGlow;
            unlit.enabled = unlit.sprite;
            lit.enabled = lit.sprite;
            glow.enabled = glow.sprite;
        }

        private static void StyleInitial(int rank)
        {
            if (rank < 0 || prefix.text.Length == 0)
                return;
            var deep = InitialColors[rank];
            prefix.characterSpacing = 0f;
            prefix.enableVertexGradient = true;
            prefix.color = Color.white;
            switch (initialStyle)
            {
                case 0:
                    var own = RankColors[rank];
                    prefix.colorGradient = Gradient(Mix(own, Color.white, 0.7f), Mix(own, Color.black, 0.4f));
                    Outline(prefix, Color.black, 0f);
                    Underlay(prefix, new Color(1f, 0.8f, 0.45f, 0.55f), initialGlow);
                    break;
                case 2:
                    prefix.colorGradient = Gradient(Mix(deep, Color.white, 0.3f), Mix(deep, Color.black, 0.18f));
                    Outline(prefix, Hex(0xE9C46A), 0.16f);
                    Underlay(prefix, new Color(0f, 0f, 0f, 0.8f), textShadow);
                    break;
                default:
                    prefix.colorGradient = Gradient(Mix(deep, Color.white, 0.3f), Mix(deep, Color.black, 0.18f));
                    Outline(prefix, Mix(deep, Color.black, 0.7f), 0.12f);
                    Underlay(prefix, new Color(deep.r, deep.g, deep.b, 0.55f), new Vector3(0.5f, 0.2f, 0f));
                    break;
            }
        }

        private static VertexGradient Gradient(Color top, Color bottom) => new VertexGradient(top, top, bottom, bottom);

        // The large faint letters: at the right end of the needle, just above it, or behind the middle of the word
        // (the sandbox's "hudset backdrop center").
        private static void PlaceBackdrop()
        {
            if (backdropRight)
            {
                backdrop.alignment = TextAlignmentOptions.BaselineRight;
                Place(backdrop.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                    new Vector2(0f, BackdropBaseline), new Vector2(400f, 160f));
            }
            else
            {
                backdrop.alignment = TextAlignmentOptions.Baseline;
                Place(backdrop.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(-BarWidth / 2f, -62f), new Vector2(400f, 160f));
            }
        }

        // SILKSONG every frame: the golden light behind the word breathes with the rapier and spreads evenly from the
        // middle to the edges (turning rays would cross the letters unevenly and a bright core would wash out the
        // middle ones), and a golden glint runs along the letters.
        private static void Halo(float now, float pulse)
        {
            aura.color = new Color(DivineGlow.r, DivineGlow.g, DivineGlow.b, 0.36f * pulse);
            rays.color = new Color(DivineGlow.r, DivineGlow.g, DivineGlow.b, 0.22f * pulse);
            var breath = 1f + 0.06f * Mathf.Sin(now * 1.6f);
            rays.rectTransform.localScale = new Vector3(breath, breath, 1f);
            Shine(now);
        }

        // SILKSONG's letters: warm white at the top, deep gold below, and a golden glint running along them from left
        // to right, crossing in 1.4 s every 3 s.
        private static void Shine(float now)
        {
            var info = rest.textInfo;
            if (info == null || info.characterCount == 0 || info.meshInfo == null || info.meshInfo.Length == 0)
                return;
            var colors = info.meshInfo[0].colors32;
            if (colors == null)
                return;
            var phase = now % 3f / 1.4f;
            var band = -0.25f + phase * 1.5f;
            var first = info.characterInfo[0].bottomLeft.x;
            var span = Mathf.Max(1f, info.characterInfo[info.characterCount - 1].topRight.x - first);
            for (var i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                var v = c.vertexIndex;
                if (!c.isVisible || v + 3 >= colors.Length)
                    continue;
                var x = ((c.bottomLeft.x + c.topRight.x) * 0.5f - first) / span;
                var glint = phase <= 1f ? Mathf.Exp(-Square((x - band) / 0.16f)) : 0f;
                Color32 above = Color.Lerp(DivineTop, Color.white, glint);
                Color32 below = Color.Lerp(DivineBottom, GlintGold, glint);
                colors[v] = below;
                colors[v + 1] = above;
                colors[v + 2] = above;
                colors[v + 3] = below;
            }
            rest.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
