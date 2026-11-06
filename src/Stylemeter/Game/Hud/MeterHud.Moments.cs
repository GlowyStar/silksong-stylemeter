using System.Collections.Generic;
using Stylemeter.Core;
using TMProOld;
using UnityEngine;
using UnityEngine.UI;

namespace Stylemeter.Game.Hud
{
    // The two ranks that come and go in their own way: SSSILKSTORM arrives in a splash of blood and, once lost, runs
    // off in blood; SILKSONG arrives in a burst of light and, once lost, is slashed in two and its halves fall away.
    // The other ranks only pop in and shake out (Animate).
    internal static partial class MeterHud
    {
        private const float DropGravity = 1500f, PieceGravity = 1000f;
        // The slash runs down to the right, close to the word's own diagonal, so it cuts through nearly every letter.
        private const float CutAngle = -10f;
        // How long a slashed SILKSONG keeps the next word out of sight, so its halves are seen coming apart.
        private const float SlashedFor = 0.36f;

        private static readonly Color Blood = new Color(0.5f, 0.02f, 0.05f);
        private static readonly Color BloodBright = new Color(0.84f, 0.08f, 0.15f);
        // The middle of SILKSONG's capitals, above the word's centre (its baseline).
        private static readonly Vector2 SilksongMiddle = new Vector2(0f, TopSize * 0.34f);
        private static readonly List<Moment> Moments = new List<Moment>();

        private static CanvasGroup wordGroup;
        // When SILKSONG last arrived: its letters and light run hot for a moment after (Halo, Shine).
        private static float radianceAt = -10f;
        // A rank leaving keeps the new word out of sight for a moment, so the old one is seen going.
        private static float wordHiddenUntil = -10f;
        // The rank word's width on screen, initial and rest (SetRank).
        private static float shownWordWidth;

        // 1 as SILKSONG arrives, down to 0 over 0.45 s.
        private static float Radiance(float now) => Mathf.Clamp01(1f - (now - radianceAt) / 0.45f);

        // Before the word changes: a rank leaving plays out on a copy of the old word.
        private static void LeaveRank(int from, int to, float now)
        {
            if (to >= from)
                return;
            if (from == BloodyRank)
            {
                Moments.Add(new BloodRunOff(now));
                wordHiddenUntil = now + 0.3f;
            }
            else if (from == Tuning.Silksong)
            {
                Moments.Add(new SlashCut(now));
                wordHiddenUntil = now + SlashedFor;
            }
        }

        // After the word changed. A meter coming back into view (from -2) shows its rank without an entrance.
        private static void EnterRank(int from, int to, float now)
        {
            if (from < 0 || to == from)
                return;
            if (to == BloodyRank)
                Moments.Add(new BloodSplash(now, from == Tuning.Silksong ? SlashedFor : 0f));
            else if (to == Tuning.Silksong)
            {
                radianceAt = now;
                Moments.Add(new Burst(now));
            }
        }

        private static void PlayMoments(float now)
        {
            for (var i = Moments.Count - 1; i >= 0; i--)
            {
                if (Moments[i].Tick(now))
                    continue;
                Moments[i].End();
                Moments.RemoveAt(i);
            }
            wordGroup.alpha = now < wordHiddenUntil ? 0f : Mathf.Clamp01((now - wordHiddenUntil) / 0.2f);
        }

        private static Vector2 WordCenter => word.anchoredPosition;

        // A picture on the block, centred on the word's centre plus an offset, over everything else.
        private static Image InFront(string name, Sprite sprite, Vector2 offset, Vector2 size)
        {
            var image = Picture(name, block, sprite);
            Place(image.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), WordCenter + offset, size);
            return image;
        }

        // A picture placed as InFront does, behind the rank word.
        private static Image Behind(string name, Sprite sprite, Vector2 offset, Vector2 size)
        {
            var image = InFront(name, sprite, offset, size);
            image.transform.SetSiblingIndex(word.GetSiblingIndex());
            return image;
        }

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static VertexGradient Toward(VertexGradient look, Color color, float amount) => new VertexGradient(
            Color.Lerp(look.topLeft, color, amount), Color.Lerp(look.topRight, color, amount),
            Color.Lerp(look.bottomLeft, color, amount), Color.Lerp(look.bottomRight, color, amount));

        // One effect playing; everything it made is destroyed once it has played out.
        private abstract class Moment
        {
            protected readonly float Start;
            private readonly List<Object> owned = new List<Object>();

            protected Moment(float start) => Start = start;

            // False once the effect has played out.
            public abstract bool Tick(float now);

            public void End()
            {
                foreach (var thing in owned)
                {
                    if (thing)
                        Object.Destroy(thing);
                }
            }

            protected T Own<T>(T thing) where T : Object
            {
                owned.Add(thing is Component part ? part.gameObject : (Object)thing);
                return thing;
            }

            // A copy of the rank word as it is now, left behind while the word itself changes at once.
            protected RectTransform Ghost(string name)
            {
                var ghost = Copy(word, block, name);
                Own(ghost.gameObject);
                return ghost;
            }

            // A copy of a word at full size and opacity with its lights off, on materials of its own: the word's
            // materials change with the next rank.
            protected RectTransform Copy(RectTransform original, Transform parent, string name)
            {
                var copy = (RectTransform)Object.Instantiate(original.gameObject, parent).transform;
                copy.name = name;
                copy.localScale = Vector3.one;
                copy.GetComponent<CanvasGroup>().alpha = 1f;
                foreach (var text in copy.GetComponentsInChildren<TextMeshProUGUI>(true))
                    text.fontSharedMaterial = Own(new Material(text.fontSharedMaterial));
                foreach (var light in new[] { "Silksong rays", "Silksong glow" })
                {
                    var part = copy.Find(light);
                    if (part)
                        part.gameObject.SetActive(false);
                }
                return copy;
            }
        }

        // SSSILKSTORM arriving: a small splash of blood behind the word and drops thrown up out of it.
        private sealed class BloodSplash : Moment
        {
            private const int DropCount = 16;
            private const float Life = 0.9f, Hold = 0.3f;
            private readonly Image splash;
            private readonly Image[] drops = new Image[DropCount];
            private readonly Vector2[] origins = new Vector2[DropCount];
            private readonly Vector2[] velocities = new Vector2[DropCount];
            private readonly float[] flights = new float[DropCount];

            public BloodSplash(float now, float delay) : base(now + delay)
            {
                var width = shownWordWidth * 0.85f + 40f;
                splash = Own(Behind("Blood splash", HudArt.BloodSplash, new Vector2(0f, 14f),
                    new Vector2(width, 140f)));
                splash.color = WithAlpha(Blood, 0f);
                for (var i = 0; i < DropCount; i++)
                {
                    origins[i] = new Vector2(Random.Range(-0.4f, 0.4f) * width, Random.Range(-4f, 26f));
                    var angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                    velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(170f, 430f);
                    flights[i] = Random.Range(0.45f, 0.8f);
                    var size = Random.Range(6f, 14f);
                    drops[i] = Own(InFront("Blood drop", HudArt.BloodDrop, origins[i],
                        new Vector2(size, size * 1.5f)));
                    drops[i].color = WithAlpha(Color.Lerp(Blood, BloodBright, Random.value), 0f);
                }
            }

            public override bool Tick(float now)
            {
                var age = now - Start;
                if (age < 0f)
                    return true;
                var scale = Mathf.Lerp(0.35f, 1f, 1f - Square(1f - Mathf.Clamp01(age / 0.12f)));
                splash.rectTransform.localScale = new Vector3(scale, scale, 1f);
                var fade = age < Hold ? 1f : Mathf.Clamp01(1f - (age - Hold) / (Life - Hold));
                splash.color = WithAlpha(Blood, 0.8f * fade);
                for (var i = 0; i < DropCount; i++)
                {
                    var flight = Mathf.Min(age, flights[i]);
                    var velocity = velocities[i] + new Vector2(0f, -DropGravity * flight);
                    var travel = velocities[i] * flight + new Vector2(0f, -0.5f * DropGravity * flight * flight);
                    var rect = drops[i].rectTransform;
                    rect.anchoredPosition = WordCenter + origins[i] + travel;
                    // The drop's round end leads.
                    rect.localEulerAngles =
                        new Vector3(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + 90f);
                    drops[i].color = WithAlpha(drops[i].color, age < flights[i] ? 1f - Square(age / flights[i]) : 0f);
                }
                return age < Life;
            }
        }

        // SSSILKSTORM lost: the old word turns to blood and runs down, stretching as it fades, and trickles of blood
        // run from its letters, faster as they go.
        private sealed class BloodRunOff : Moment
        {
            private const int RunCount = 14;
            private const float Life = 0.9f;
            private readonly RectTransform ghost;
            private readonly CanvasGroup ghostGroup;
            private readonly Vector2 ghostAt;
            private readonly TMP_Text[] texts;
            private readonly VertexGradient[] looks;
            private readonly Image[] runs = new Image[RunCount];
            private readonly Vector2[] origins = new Vector2[RunCount];
            private readonly float[] lengths = new float[RunCount];
            private readonly float[] starts = new float[RunCount];
            private readonly float[] durations = new float[RunCount];

            public BloodRunOff(float now) : base(now)
            {
                ghost = Ghost("SSSILKSTORM running off");
                // It stretches down from the top of its small capitals.
                var top = new Vector2(0.5f, 0.7f);
                ghost.anchoredPosition += Vector2.Scale(top - ghost.pivot, ghost.sizeDelta);
                ghost.pivot = top;
                ghostAt = ghost.anchoredPosition;
                ghostGroup = ghost.GetComponent<CanvasGroup>();
                texts = ghost.GetComponentsInChildren<TMP_Text>();
                looks = new VertexGradient[texts.Length];
                for (var i = 0; i < texts.Length; i++)
                {
                    // Every text's colours as a gradient, which darkens the same whether it had one or not.
                    var text = texts[i];
                    looks[i] = text.enableVertexGradient ? text.colorGradient : new VertexGradient(text.color);
                    text.enableVertexGradient = true;
                    text.color = Color.white;
                }
                var width = shownWordWidth;
                for (var i = 0; i < RunCount; i++)
                {
                    // Mostly short thin runs, a few long heavy ones, and the long ones run fastest.
                    var weight = Random.value;
                    origins[i] = new Vector2(Random.Range(-0.46f, 0.46f) * width, Random.Range(-2f, 10f));
                    lengths[i] = Mathf.Lerp(25f, 150f, weight * weight);
                    starts[i] = Random.Range(0f, 0.15f);
                    durations[i] = Mathf.Lerp(0.25f, 0.5f, weight);
                    var thickness = Mathf.Lerp(7f, 15f, weight);
                    var run = Own(InFront("Blood run", HudArt.BloodRun, origins[i], new Vector2(thickness, 0f)));
                    run.type = Image.Type.Sliced;
                    // The drop at the run's foot as wide as the run.
                    run.pixelsPerUnitMultiplier = 16f / thickness;
                    run.rectTransform.pivot = new Vector2(0.5f, 1f);
                    run.color = Color.Lerp(Blood, BloodBright, Random.Range(0.25f, 0.7f));
                    runs[i] = run;
                }
            }

            public override bool Tick(float now)
            {
                var age = now - Start;
                var darken = Mathf.Clamp01(age / 0.08f);
                // The blood drains down out of the letters, their tops first.
                var drain = Mathf.Clamp01((age - 0.08f) / 0.4f);
                for (var i = 0; i < texts.Length; i++)
                {
                    var look = Toward(looks[i], Blood, darken);
                    look.topLeft.a = look.topRight.a = 1f - drain;
                    texts[i].colorGradient = look;
                }
                var pull = Square(Mathf.Clamp01(age / 0.6f));
                ghost.localScale = new Vector3(1f, 1f + 1.4f * pull, 1f);
                ghost.anchoredPosition = ghostAt + new Vector2(0f, -30f * pull);
                ghostGroup.alpha = 1f - Mathf.Clamp01((age - 0.25f) / 0.4f);
                for (var i = 0; i < RunCount; i++)
                {
                    var running = age - starts[i];
                    var flow = Mathf.Clamp01(running / durations[i]);
                    // Run out, a trickle slips on down and fades.
                    var after = Mathf.Max(0f, running - durations[i]);
                    var rect = runs[i].rectTransform;
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, lengths[i] * Square(flow));
                    rect.anchoredPosition = WordCenter + origins[i] + new Vector2(0f, -2f * lengths[i] * after);
                    runs[i].color = WithAlpha(runs[i].color, 0.95f * (1f - Mathf.Clamp01(after / 0.25f)));
                }
                return age < Life;
            }
        }

        // SILKSONG arriving: a burst of light from the word and a streak across it.
        private sealed class Burst : Moment
        {
            private const float Life = 0.6f;
            private readonly float width = shownWordWidth;
            private readonly Image flare, streak;

            public Burst(float now) : base(now)
            {
                flare = Own(Behind("Silksong burst", HudArt.Halo, SilksongMiddle, Vector2.zero));
                streak = Own(InFront("Silksong streak", HudArt.Stroke, SilksongMiddle, Vector2.zero));
            }

            public override bool Tick(float now)
            {
                var progress = Mathf.Clamp01((now - Start) / Life);
                var open = 1f - Mathf.Pow(1f - progress, 3f);
                flare.rectTransform.sizeDelta =
                    Vector2.Lerp(new Vector2(width * 0.4f, 40f), new Vector2(width * 2.4f, 380f), open);
                flare.color = WithAlpha(Color.Lerp(Color.white, DivineGlow, progress),
                    0.95f * Mathf.Pow(1f - progress, 1.5f));
                streak.rectTransform.sizeDelta = new Vector2(
                    Mathf.Lerp(60f, width + 460f, 1f - Square(1f - progress)), Mathf.Lerp(28f, 8f, progress));
                streak.color = WithAlpha(Color.white, Square(1f - progress));
                return progress < 1f;
            }
        }

        // SILKSONG lost: a slash runs across the word, its halves slip apart along the cut and fall away, the lower
        // one first.
        private sealed class SlashCut : Moment
        {
            private const float Life = 1.15f, CutTime = 0.07f, SlipTime = 0.13f, SlashHeight = 22f;
            private static readonly Quaternion Turn = Quaternion.Euler(0f, 0f, CutAngle);
            // Along the cut (down to the right) and across it (toward the upper half).
            private static readonly Vector2 Along = Turn * Vector3.right, Across = Turn * Vector3.up;
            private readonly Vector2 cutPoint = WordCenter + SilksongMiddle;
            private readonly float slashLength = shownWordWidth + 160f;
            private readonly Image slash, fading;
            private readonly Piece[] pieces;

            public SlashCut(float now) : base(now)
            {
                fading = Own(Behind("Silksong light going out", HudArt.Halo, SilksongMiddle,
                    new Vector2(shownWordWidth + 170f, TopSize * 2.6f)));

                var template = Ghost("SILKSONG");
                // The word's own colours are painted on every frame (Shine); the copy gets them as a gradient.
                var letters = template.Find("Rank").GetComponent<TMP_Text>();
                letters.enableVertexGradient = true;
                letters.colorGradient = Gradient(DivineTop, DivineBottom);
                pieces = new[]
                {
                    new Piece(Half("SILKSONG, upper half", template, 1f))
                    {
                        Slip = Along * 30f + Across * 5f, FallsAt = 0.26f, Push = Along * 60f, Spin = -20f,
                    },
                    new Piece(Half("SILKSONG, lower half", template, -1f))
                    {
                        Slip = Along * -8f - Across * 5f, FallsAt = 0.16f, Push = new Vector2(-15f, 0f), Spin = 12f,
                    },
                };
                template.gameObject.SetActive(false);

                var start = SilksongMiddle - Along * (slashLength / 2f);
                slash = Own(InFront("Slash", HudArt.Stroke, start, new Vector2(0f, SlashHeight)));
                slash.rectTransform.pivot = new Vector2(0f, 0.5f);
                slash.rectTransform.localRotation = Turn;
            }

            // One side of the cut: a copy of the word in a window whose edge is the cut through the word's middle.
            private RectTransform Half(string name, RectTransform template, float side)
            {
                var center = new Vector2(0.5f, 0.5f);
                var pivot = Own(Node(name, block));
                Place(pivot, new Vector2(1f, 1f), center, cutPoint, Vector2.zero);
                var window = Node("Cut", pivot);
                Vector2 offset = Turn * new Vector3(0f, 500f * side, 0f);
                Place(window, center, center, offset, new Vector2(2400f, 1000f));
                window.localRotation = Turn;
                window.gameObject.AddComponent<Image>().raycastTarget = false;
                window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var copy = Copy(template, window, name);
                var back = Quaternion.Inverse(Turn);
                Place(copy, center, center, back * -(Vector3)(SilksongMiddle + offset), template.sizeDelta);
                copy.localRotation = back;
                return pivot;
            }

            public override bool Tick(float now)
            {
                var age = now - Start;
                slash.rectTransform.sizeDelta = new Vector2(slashLength * Mathf.Clamp01(age / CutTime), SlashHeight);
                slash.color = WithAlpha(Color.white, age < CutTime ? 1f : Mathf.Clamp01(1f - (age - CutTime) / 0.25f));
                fading.color = WithAlpha(DivineGlow, 0.4f * Mathf.Clamp01(1f - age / 0.3f));
                var slip = 1f - Square(1f - Mathf.Clamp01((age - CutTime) / SlipTime));
                var alpha = 1f - Mathf.Clamp01((age - 0.45f) / 0.55f);
                foreach (var piece in pieces)
                    piece.Show(cutPoint, age, slip, alpha);
                return age < Life;
            }

            // One half of the word: how far it slips along the cut, and how it falls from then on.
            private sealed class Piece
            {
                public Vector2 Slip, Push;
                public float FallsAt, Spin;
                private readonly RectTransform half;
                private readonly CanvasGroup group;

                public Piece(RectTransform half)
                {
                    this.half = half;
                    group = half.gameObject.AddComponent<CanvasGroup>();
                }

                public void Show(Vector2 cutPoint, float age, float slip, float alpha)
                {
                    var fall = Mathf.Max(0f, age - FallsAt);
                    half.anchoredPosition = cutPoint + Slip * slip + Push * fall
                        + new Vector2(0f, -0.5f * PieceGravity * fall * fall);
                    half.localEulerAngles = new Vector3(0f, 0f, Spin * fall);
                    group.alpha = alpha;
                }
            }
        }
    }
}
