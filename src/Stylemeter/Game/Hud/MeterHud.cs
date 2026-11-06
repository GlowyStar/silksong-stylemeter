using System.Collections.Generic;
using Stylemeter.Core;
using TMProOld;
using UnityEngine;
using UnityEngine.UI;

namespace Stylemeter.Game.Hud
{
    // The style meter on screen: top right, the rank word centred over the needle with its letters as a coloured
    // initial and as large faint letters behind, the needle that lights up as it fills (bloody at SSSILKSTORM, a
    // silk-hilted rapier at SILKSONG), the height and thread line, and the feed of bonuses coloured by weight. Laid
    // out on a 1280x720 canvas that scales with the screen height.
    // Text is the game's own TextMeshPro (TMProOld, inside Assembly-CSharp): its fonts exist only as that type.
    // Rank word looks: MeterHud.Look.cs. How SSSILKSTORM and SILKSONG come and go: MeterHud.Moments.cs. Sandbox tools
    // for looking at it live: MeterHud.Dev.cs.
    internal static partial class MeterHud
    {
        private const float BarWidth = 360f;
        // The rank word, its initial (1.35 times larger) and SILKSONG, which grows.
        private const float WordSize = 40f, InitialSize = 54f, TopSize = 52f;
        // Rows of the block from its top: the word's baseline, the needle, the height line, the feed.
        private const float WordY = -42f, NeedleTop = -46f, MetaY = -102f, FeedTop = -130f, FeedStep = 24f;
        // The feed and the height line are set in Trajan like the game's own HUD numbers: Perpetua's thin strokes turn
        // grey and ragged at this size over busy backgrounds.
        private const float FeedSize = 19f, MetaSize = 16f;
        // Every letter the meter writes: rank words, bonus names, counts, the height and thread line.
        private const string Needed = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+.,:!?'-";
        private const int FeedRows = 6;
        // Where the light starts and ends along the needle art (the eye ends at 10%, the point at 99%), and where the
        // rapier's guard ends (27%): the guard always shows lit.
        private const float LitStart = 0.10f, LitSpan = 0.89f, HiltEnd = 0.27f;

        private static readonly float[] FeedOpacity = { 1f, 0.95f, 0.88f, 0.8f, 0.72f, 0.64f };
        private static readonly Color Cream = Hex(0xF3E9D6);
        // The unfilled part of the needle: the same drawing, darkened.
        private static readonly Color Unlit = new Color(0.42f, 0.40f, 0.46f, 0.92f);

        // The text effects as (softness, dilate, offset). TextMeshPro's underlay reaches only a few pixels past a
        // letter in the game's atlases; stronger settings than these fill each letter's box.
        private static Vector3 initialGlow = new Vector3(0.55f, 0.25f, 0f);
        private static Vector3 silksongRim = new Vector3(0.35f, 0.15f, 0f);
        private static Vector3 textShadow = new Vector3(0.25f, 0.2f, -0.4f);

        private static GameObject root;
        private static CanvasGroup group;
        private static RectTransform block, word, litMask, glowMask, needle, raysHolder;
        private static TextMeshProUGUI backdrop, prefix, rest, meta;
        private static readonly TextMeshProUGUI[] Feed = new TextMeshProUGUI[FeedRows];
        private static Image unlit, lit, glow, aura, rays;
        private static TMP_FontAsset titleFont, textFont;
        // The game's fonts and the meter's copies of them with an empty border round the atlas (see WithBorder).
        private static TMP_FontAsset gameTitle, gameText, copyTitle, copyText;
        // The copy's empty border as a share of the atlas's own size: its whole height above and below (the atlas sits
        // in the middle third of the texture), 15% of its width left and right.
        private static float borderX = 0.15f, borderY = 1f;
        private static float nextFontSearch, fontSearchSince = -1f;
        private static float barHeight;

        private static int shownRank = -2;
        private static float shownFill;
        private static float alpha;
        private static float rankUpAt = -10f, hitAt = -10f;
        private static readonly Dictionary<FeedLine, float> FeedBorn = new Dictionary<FeedLine, float>();

        public static void Tick(StyleEngine engine)
        {
            if (engine == null)
                return;
            var wanted = Wanted(engine);
            if (!root)
            {
                if (!wanted || !FindFonts())
                    return;
                Build();
            }
            var dt = Time.unscaledDeltaTime;
            alpha = Mathf.MoveTowards(alpha, wanted ? 1f : 0f, dt * 7f);
            group.alpha = alpha;
            if (alpha <= 0f)
            {
                if (root.activeSelf)
                    root.SetActive(false);
                shownRank = -2;
                return;
            }
            if (!root.activeSelf)
                root.SetActive(true);
            Refresh(engine, dt);
        }

        private static bool Wanted(StyleEngine engine)
        {
            if (!Plugin.MeterEnabled.Value || !Plugin.ShowOverlay.Value)
                return false;
            if (!engine.Meter.InCombo && engine.Feed.Lines.Count == 0)
                return false;
            var gm = GameManager.instance;
            return HeroController.instance && gm && !gm.isPaused && HudCanvas.IsVisible && !HudGlobalHide.IsHidden;
        }

        // The game's own fonts: Trajan for the rank, Perpetua for the rest. They load with the game's UI. The game also
        // holds cut-down copies of both (perpetua_tmpro has no W), so the copy with every letter the meter writes wins.
        // After 10 s without them any font will do.
        private static bool FindFonts()
        {
            if (titleFont && textFont)
                return true;
            var now = Time.unscaledTime;
            if (now < nextFontSearch)
                return false;
            nextFontSearch = now + 2f;
            if (fontSearchSince < 0f)
                fontSearchSince = now;
            TMP_FontAsset any = null, title = null, text = null;
            int titleMissing = int.MaxValue, textMissing = int.MaxValue;
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (!font || font.name.Contains("Fallback") || font.name.EndsWith(" +border"))
                    continue;
                any = any ? any : font;
                var name = font.name.ToLowerInvariant();
                if (name.Contains("trajan"))
                    Better(font, ref title, ref titleMissing);
                else if (name.Contains("perpetua"))
                    Better(font, ref text, ref textMissing);
            }
            if (title && text)
            {
                gameTitle = title;
                gameText = text;
                // Only Trajan is drawn from a copy: the meter writes nothing in Perpetua.
                copyTitle = WithBorder(title);
                copyText = text;
                titleFont = copyTitle;
                textFont = copyText;
            }
            else if (any && now - fontSearchSince > 10f)
            {
                Plugin.Log.LogWarning($"Stylemeter: game fonts not found (Trajan {(bool)title}, "
                    + $"Perpetua {(bool)text}); using {any.name}");
                titleFont = title ? title : any;
                textFont = text ? text : any;
            }
            return titleFont && textFont;
        }

        // Fewer missing letters wins; on a tie, the game's main atlas ("... SDF").
        private static void Better(TMP_FontAsset font, ref TMP_FontAsset best, ref int bestMissing)
        {
            var missing = Missing(font);
            if (missing < bestMissing
                || missing == bestMissing && font.name.EndsWith("SDF") && !best.name.EndsWith("SDF"))
            {
                best = font;
                bestMissing = missing;
            }
        }

        private static int Missing(TMP_FontAsset font)
        {
            if (font.characterDictionary == null)
                font.ReadFontDefinition();
            var missing = 0;
            foreach (var c in Needed)
            {
                if (!font.HasCharacter(c))
                    missing++;
            }
            return missing;
        }

        // On the meter's canvas the game's text shader blurs a letter drawn from below about v 0.33 of its texture,
        // though the atlas there is clean and the glyph's coordinates and scale match the sharp letters. Trajan's
        // letters fill its atlas from top to bottom, so the meter draws from a copy with the atlas in the middle third
        // of a texture three times as tall (v 0.33 to 0.67, about 8 MB of video memory). The copy is made on the
        // graphics card (CopyTexture); the game's own font, material and atlas are not touched. Without CopyTexture
        // the game's font is used as it is.
        private static TMP_FontAsset WithBorder(TMP_FontAsset font)
        {
            var atlas = font.atlas;
            if (!atlas || !font.material
                || SystemInfo.copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None)
                return font;
            try
            {
                int bx = Mathf.CeilToInt(atlas.width * borderX), by = Mathf.CeilToInt(atlas.height * borderY);
                int w = atlas.width + bx * 2, h = atlas.height + by * 2;
                var copy = new Texture2D(w, h, atlas.format, false)
                {
                    name = atlas.name + " +border",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = atlas.filterMode,
                };
                copy.LoadRawTextureData(new byte[w * h * (atlas.format == TextureFormat.Alpha8 ? 1 : 4)]);
                copy.Apply(false, true);
                Graphics.CopyTexture(atlas, 0, 0, 0, 0, atlas.width, atlas.height, copy, 0, 0, bx, by);

                var material = new Material(font.material) { name = font.material.name + " +border" };
                material.SetTexture("_MainTex", copy);
                material.SetFloat("_TextureWidth", w);
                material.SetFloat("_TextureHeight", h);
                var clone = Object.Instantiate(font);
                clone.name = font.name + " +border";
                clone.atlas = copy;
                clone.material = material;
                clone.fontInfo.AtlasWidth = w;
                clone.fontInfo.AtlasHeight = h;
                clone.ReadFontDefinition();
                // Glyph rows count from the top of the atlas; the border is the same at the top and the bottom.
                foreach (var glyph in clone.characterDictionary.Values)
                {
                    glyph.x += bx;
                    glyph.y += by;
                }
                return clone;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Stylemeter: could not copy the atlas of {font.name}, "
                    + $"using the game's font: {e}");
                return font;
            }
        }

        private static string Describe(TMP_FontAsset font) => font
            ? $"{font.name} ({(font.material ? font.material.shader.name : "no material")}, "
                + $"{Missing(font)} letters missing)"
            : "-";

        private static bool TitleIsTrajan => titleFont && titleFont.name.StartsWith("TrajanPro");

        private static void Build()
        {
            HudArt.Load();
            root = new GameObject("Stylemeter HUD");
            Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            group = root.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            block = Node("Block", root.transform);
            Place(block, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -48f), new Vector2(380f, 330f));

            backdrop = Text("Rank letters", block, titleFont, BackdropSize, TextAlignmentOptions.Baseline);
            PlaceBackdrop();

            // The word is centred over the needle: a right-aligned word leaves the needle's eye bare.
            word = Node("Rank word", block);
            Place(word, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-BarWidth / 2f, WordY),
                new Vector2(440f, 120f));
            wordGroup = word.gameObject.AddComponent<CanvasGroup>();
            raysHolder = Node("Silksong rays", word);
            Place(raysHolder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, TopSize * 0.34f),
                Vector2.zero);
            // Rays turn inside a flattened holder: a halo seen at a slant.
            raysHolder.localScale = new Vector3(1f, 0.34f, 1f);
            rays = Picture("Rays", raysHolder, HudArt.Rays);
            Place(rays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(560f, 560f));
            raysHolder.gameObject.SetActive(false);
            aura = Picture("Silksong glow", word, HudArt.Halo ? HudArt.Halo : HudArt.Aura);
            aura.enabled = false;
            rest = Text("Rank", word, titleFont, WordSize, TextAlignmentOptions.BaselineLeft);
            prefix = Text("Rank initial", word, titleFont, InitialSize, TextAlignmentOptions.BaselineLeft);

            barHeight = HudArt.Needle ? BarWidth * HudArt.Needle.rect.height / HudArt.Needle.rect.width : 45f;
            needle = Node("Needle", block);
            Place(needle, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, NeedleTop),
                new Vector2(BarWidth, barHeight));
            unlit = Picture("Needle, unlit", needle, HudArt.Needle);
            unlit.color = Unlit;
            Stretch(unlit.rectTransform);
            var margin = new Vector2(HudArt.GlowMargin.x * BarWidth, HudArt.GlowMargin.y * barHeight);
            // Each reveal window starts left of its picture, so its soft left edge falls on empty space.
            glowMask = Mask("Glow reveal", needle, new Vector2(-margin.x - 12f, -margin.y),
                barHeight + margin.y * 2f, 22);
            glow = Picture("Needle glow", glowMask, HudArt.NeedleGlow);
            Pin(glow.rectTransform, new Vector2(12f, 0f),
                new Vector2(BarWidth + margin.x * 2f, barHeight + margin.y * 2f));
            litMask = Mask("Lit reveal", needle, new Vector2(-12f, 0f), barHeight, 12);
            lit = Picture("Needle, lit", litMask, HudArt.Needle);
            Pin(lit.rectTransform, new Vector2(12f, 0f), new Vector2(BarWidth, barHeight));

            meta = Text("Height and thread", block, titleFont, MetaSize, TextAlignmentOptions.MidlineRight);
            meta.color = Hex(0xE8DFCC);
            meta.characterSpacing = 2f;
            meta.fontStyle = TitleIsTrajan ? FontStyles.Normal : FontStyles.SmallCaps;
            Place(meta.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, MetaY),
                new Vector2(380f, 22f));
            Shadow(meta, 0.9f);

            for (var i = 0; i < FeedRows; i++)
            {
                var line = Text("Feed " + i, block, titleFont, FeedSize, TextAlignmentOptions.MidlineRight);
                line.color = Cream;
                line.characterSpacing = 2f;
                Place(line.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                    new Vector2(0f, FeedTop - FeedStep * i), new Vector2(380f, 24f));
                Shadow(line, 0.95f);
                Feed[i] = line;
            }
            root.SetActive(false);
            Plugin.Out($"HUD built: fonts {Describe(titleFont)} / {Describe(textFont)}, "
                + $"needle art {(HudArt.Needle && HudArt.Rapier ? "ok" : "missing")}");
        }

        private static void Refresh(StyleEngine engine, float dt)
        {
            var now = Time.unscaledTime;
            var meter = engine.Meter;
            var rank = meter.InCombo ? meter.Rank : -1;
            if (rank != shownRank)
            {
                if (shownRank >= 0 && rank > shownRank)
                    rankUpAt = now;
                else if (shownRank >= 0 && rank < shownRank)
                    hitAt = now;
                LeaveRank(shownRank, rank, now);
                SetRank(rank);
                EnterRank(shownRank, rank, now);
                shownFill = meter.InCombo ? meter.Fill / meter.Size : 0f;
                shownRank = rank;
            }
            var target = meter.InCombo ? Mathf.Clamp01(meter.Fill / meter.Size) : 0f;
            shownFill += (target - shownFill) * (1f - Mathf.Exp(-12f * dt));
            SetFill(rank, shownFill, now);
            Animate(rank, now);
            PlayMoments(now);
            SetMeta(engine, rank);
            SetFeed(engine, now);
        }

        // The needle lights up from the eye to the fill point with a soft edge; the rapier's guard is always lit.
        private static void SetFill(int rank, float fill, float now)
        {
            var top = rank == Tuning.Silksong;
            var edge = fill <= 0f && !top ? 0f : LitStart + fill * LitSpan;
            if (top)
                edge = Mathf.Max(edge, HiltEnd);
            var width = edge * BarWidth;
            litMask.sizeDelta = new Vector2(edge <= 0f ? 0f : width + 12f, litMask.sizeDelta.y);
            var margin = HudArt.GlowMargin.x * BarWidth;
            glowMask.sizeDelta = new Vector2(edge <= 0f ? 0f : width + margin + 12f + 8f, glowMask.sizeDelta.y);
            var pulse = top ? 0.8f + 0.2f * Mathf.Sin(now * 3f) : 0.75f;
            glow.color = new Color(1f, 1f, 1f, pulse);
            if (top)
                Halo(now, pulse);
        }

        // A rank up pops the word (SILKSONG harder) and lights its letters; a rank lost to a hit shakes the meter and
        // flashes it red.
        private static void Animate(int rank, float now)
        {
            var up = now - rankUpAt;
            var swell = rank == Tuning.Silksong ? 0.35f : 0.25f;
            var pop = up < 0.3f ? 1f + swell * Square(1f - up / 0.3f) : 1f;
            word.localScale = new Vector3(pop, pop, 1f);
            var color = RankColor(rank);
            var letters = up < 0.6f ? Mathf.Lerp(0.42f, BackdropAlpha, up / 0.6f) : BackdropAlpha;
            backdrop.color = new Color(color.r, color.g, color.b, letters);

            var hit = now - hitAt;
            var shake = hit < 0.3f ? (1f - hit / 0.3f) * 7f : 0f;
            var offset = shake > 0f ? Random.insideUnitCircle * shake : Vector2.zero;
            block.anchoredPosition = new Vector2(-48f, -48f) + offset;
            if (rank == Tuning.Silksong)
                return;
            var flash = hit < 0.25f ? 1f - hit / 0.25f : 0f;
            rest.color = Color.Lerp(color, new Color(1f, 0.23f, 0.23f), flash);
        }

        // "height x1.2      thread: taut", in Trajan's small capitals (its lower case).
        private static void SetMeta(StyleEngine engine, int rank)
        {
            if (rank < 0)
            {
                meta.text = "";
                return;
            }
            var height = engine.Height.Current(engine.Now);
            var source = engine.Threads.LastSource;
            var thread = source == null
                ? ""
                : "      thread: " + ThreadSet.StateOf(engine.Threads.Tension(source)).ToString().ToLowerInvariant();
            meta.text = "height x" + height.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)
                + thread;
        }

        // Newest line on top, coloured by the bonus's weight; a new line slides in from the right; the last half
        // second of a line fades it out.
        private static void SetFeed(StyleEngine engine, float now)
        {
            var lines = engine.Feed.Lines;
            var gameNow = engine.Now;
            for (var i = 0; i < FeedRows; i++)
            {
                var text = Feed[i];
                if (i >= lines.Count)
                {
                    text.text = "";
                    continue;
                }
                var line = lines[i];
                if (!FeedBorn.TryGetValue(line, out var born))
                    FeedBorn[line] = born = now;
                text.text = FeedText(line);
                var fade = Mathf.Clamp01((line.ExpiresAt - gameNow) / 0.5f);
                var color = FeedColor(line.Weight);
                text.color = new Color(color.r, color.g, color.b, FeedOpacity[i] * fade);
                var slide = Mathf.Clamp01((now - born) / 0.15f);
                text.rectTransform.anchoredPosition = new Vector2(24f * Square(1f - slide), FeedTop - FeedStep * i);
            }
            if (FeedBorn.Count > 32)
            {
                var gone = new List<FeedLine>();
                foreach (var key in FeedBorn.Keys)
                {
                    if (!Contains(lines, key))
                        gone.Add(key);
                }
                foreach (var key in gone)
                    FeedBorn.Remove(key);
            }
        }

        private static bool Contains(IReadOnlyList<FeedLine> lines, FeedLine line)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i] == line)
                    return true;
            }
            return false;
        }

        // TextMeshPro's underlay: a soft copy of the text under it, used here as a glow or a shadow.
        private static void Underlay(TMP_Text text, Color color, Vector3 look)
        {
            var material = text.fontMaterial;
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", color);
            material.SetFloat("_UnderlaySoftness", look.x);
            material.SetFloat("_UnderlayDilate", look.y);
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", look.z);
            text.UpdateMeshPadding();
        }

        // A thin line round each letter, inside the atlas's spread; width 0 takes it off.
        private static void Outline(TMP_Text text, Color color, float width)
        {
            var material = text.fontMaterial;
            material.SetColor("_OutlineColor", color);
            material.SetFloat("_OutlineWidth", width);
            text.UpdateMeshPadding();
        }

        // A tight dark shadow just under the letters: it lifts them off a busy background without blurring them.
        private static void Shadow(TMP_Text text, float strength) =>
            Underlay(text, new Color(0f, 0f, 0f, strength), textShadow);

        private static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size,
            TextAlignmentOptions alignment)
        {
            var node = Node(name, parent);
            var text = node.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.OverflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            text.raycastTarget = false;
            text.text = "";
            return text;
        }

        private static Image Picture(string name, Transform parent, Sprite sprite)
        {
            var node = Node(name, parent);
            var image = node.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = false;
            image.enabled = sprite;
            return image;
        }

        // A soft-edged window from the given bottom-left corner of the needle, growing to the right as the meter fills.
        private static RectTransform Mask(string name, RectTransform parent, Vector2 corner, float height, int softness)
        {
            var node = Node(name, parent);
            node.anchorMin = node.anchorMax = new Vector2(0f, 0f);
            node.pivot = new Vector2(0f, 0f);
            node.anchoredPosition = corner;
            node.sizeDelta = new Vector2(0f, height);
            var mask = node.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(softness, 0);
            return node;
        }

        // A picture of fixed size at a fixed place inside its window, whatever the window's width.
        private static void Pin(RectTransform rect, Vector2 corner, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = corner;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Color Mix(Color from, Color to, float k) => Color.Lerp(from, to, k);

        private static float Square(float x) => x * x;

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        private static string HexOf(Color c, float a) =>
            ((int)(c.r * 255f)).ToString("X2") + ((int)(c.g * 255f)).ToString("X2")
                + ((int)(c.b * 255f)).ToString("X2") + ((int)(a * 255f)).ToString("X2");
    }
}
