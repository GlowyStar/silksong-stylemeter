using System.Collections.Generic;
using Stylemeter.Core;
using TMProOld;
using UnityEngine;

namespace Stylemeter.Game.Hud
{
    // Sandbox tools for the meter on screen (bridge verbs hud, hudglyph, hudchars, hudatlas, hudset): why it does or
    // does not show, which font and atlas draws each letter, and live changes to the text effects.
    internal static partial class MeterHud
    {
        // "hud": every condition for showing the meter, and the fonts the game holds.
        public static string Diagnose(StyleEngine engine)
        {
            var gm = GameManager.instance;
            var fonts = new List<string>();
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (font)
                    fonts.Add(font.name);
            }
            return $"enabled={Plugin.MeterEnabled.Value} show={Plugin.ShowOverlay.Value} combo={engine.Meter.InCombo} "
                + $"rank={engine.Meter.Rank} feed={engine.Feed.Lines.Count} hero={(bool)HeroController.instance} "
                + $"paused={(gm ? gm.isPaused.ToString() : "no gm")} hudVisible={HudCanvas.IsVisible} "
                + $"hudHidden={HudGlobalHide.IsHidden} built={(bool)root} alpha={alpha:0.00} "
                + $"title={Describe(titleFont)} text={Describe(textFont)}"
                + $"\nfonts: {string.Join(", ", fonts)}";
        }

        // "hudglyph <letter>": the letter's place in each font's atlas, and the atlas itself.
        public static string Glyph(char c) => GlyphOf(titleFont, c) + "\n" + GlyphOf(textFont, c);

        private static string GlyphOf(TMP_FontAsset font, char c)
        {
            if (!font)
                return "no font";
            var atlas = font.atlas;
            var info = font.fontInfo;
            var gradient = font.material && font.material.HasProperty("_GradientScale")
                ? font.material.GetFloat("_GradientScale") : -1f;
            var text = $"{font.name}: atlas "
                + (atlas
                    ? $"{atlas.width}x{atlas.height} {atlas.format} wrap={atlas.wrapMode} filter={atlas.filterMode} "
                        + $"mips={atlas.mipmapCount}"
                    : "none")
                + $" gradient={gradient} padding={info.Padding} point={info.PointSize} "
                + $"info atlas={info.AtlasWidth}x{info.AtlasHeight}";
            if (font.fallbackFontAssets != null && font.fallbackFontAssets.Count > 0)
                text += " fallbacks=" + string.Join("+", font.fallbackFontAssets.ConvertAll(f => f ? f.name : "null"));
            if (font.characterDictionary != null && font.characterDictionary.TryGetValue(c, out var g))
                text += $"\n'{c}' x={g.x} y={g.y} w={g.width} h={g.height} scale={g.scale}";
            else
                text += $"\nno '{c}'";
            return text;
        }

        // "hudchars <rest|prefix|meta|feedN>": the font, material and atlas coordinates of each letter of a text.
        public static string Chars(string which)
        {
            TMP_Text text = which == "rest" ? rest : which == "prefix" ? prefix : which == "meta" ? meta
                : which.StartsWith("feed") ? Feed[int.Parse(which.Substring(4))] : null;
            if (!text)
                return "no text " + which;
            text.ForceMeshUpdate();
            var info = text.textInfo;
            var texture = text.fontSharedMaterial ? text.fontSharedMaterial.mainTexture : null;
            var line = new System.Text.StringBuilder($"{which} \"{text.text}\" style={text.fontStyle} "
                + $"texture={(texture ? $"{texture.name} {texture.width}x{texture.height}" : "-")}:");
            for (var i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                line.Append(System.FormattableString.Invariant(
                    $" {c.character}={(c.fontAsset ? c.fontAsset.name : "-")}/{c.materialReferenceIndex}"));
                line.Append(System.FormattableString.Invariant(
                    $" uv=({c.vertex_BL.uv.x:0.0000},{c.vertex_BL.uv.y:0.0000})"));
                line.Append(System.FormattableString.Invariant(
                    $"-({c.vertex_TR.uv.x:0.0000},{c.vertex_TR.uv.y:0.0000})"));
                // uv2.y carries the letter's scale, which sets how sharp its edge is drawn.
                line.Append(System.FormattableString.Invariant(
                    $" uv2=({c.vertex_BL.uv2.x:0.###},{c.vertex_BL.uv2.y:0.###}) scale={c.scale:0.###}"));
            }
            return line.ToString();
        }

        // "hudatlas <title|text> <letter>": the letter's part of the atlas the meter draws from, as a grey picture
        // (x4) in the bridge folder, and the lowest and highest distance values on the edge of the picture.
        public static string Atlas(string which, char c, string dir)
        {
            var font = which == "title" ? titleFont : textFont;
            if (!font || font.characterDictionary == null || !font.characterDictionary.TryGetValue(c, out var g))
                return "no glyph";
            var atlas = font.atlas;
            var pad = (int)font.fontInfo.Padding;
            // TextMeshPro counts glyph rows from the top of the atlas; textures count from the bottom.
            int x0 = Mathf.Max(0, (int)g.x - pad - 6), x1 = Mathf.Min(atlas.width, (int)(g.x + g.width) + pad + 6);
            int y0 = Mathf.Max(0, atlas.height - (int)(g.y + g.height) - pad - 6),
                y1 = Mathf.Min(atlas.height, atlas.height - (int)g.y + pad + 6);
            var rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            Graphics.Blit(atlas, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            int w = x1 - x0, h = y1 - y0;
            var crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
            crop.ReadPixels(new Rect(x0, y0, w, h), 0, 0);
            crop.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            var px = crop.GetPixels32();
            Object.Destroy(crop);
            // An Alpha8 atlas blits as (0, 0, 0, a) on some graphics APIs and as (a, a, a, 1) on others.
            var useRed = true;
            foreach (var p in px)
            {
                if (p.a != 255)
                {
                    useRed = false;
                    break;
                }
            }
            int ringMin = 255, ringMax = 0;
            var outPx = new Color32[w * 4 * h * 4];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var p = px[y * w + x];
                    int v = useRed ? p.r : p.a;
                    if (x < 3 || y < 3 || x >= w - 3 || y >= h - 3)
                    {
                        ringMin = Mathf.Min(ringMin, v);
                        ringMax = Mathf.Max(ringMax, v);
                    }
                    var grey = new Color32((byte)v, (byte)v, (byte)v, 255);
                    for (var dy = 0; dy < 4; dy++)
                    {
                        for (var dx = 0; dx < 4; dx++)
                            outPx[(y * 4 + dy) * w * 4 + x * 4 + dx] = grey;
                    }
                }
            }
            var big = new Texture2D(w * 4, h * 4, TextureFormat.RGBA32, false);
            big.SetPixels32(outPx);
            big.Apply();
            var path = System.IO.Path.Combine(dir, $"atlas_{which}_{(int)c}.png");
            System.IO.File.WriteAllBytes(path, big.EncodeToPNG());
            Object.Destroy(big);
            return $"{font.name} '{c}' crop {x0},{y0} {w}x{h}: edge {ringMin}..{ringMax} -> {path}";
        }

        // "hudset": live changes, sandbox only.
        public static string Set(string[] w)
        {
            float F(int i) => float.Parse(w[i], System.Globalization.CultureInfo.InvariantCulture);
            if (!root)
                return "the meter is not built yet";
            switch (w.Length > 1 ? w[1] : "")
            {
                case "initial":
                    initialGlow = new Vector3(F(2), F(3), 0f);
                    break;
                case "rim":
                    silksongRim = new Vector3(F(2), F(3), 0f);
                    break;
                case "shadow":
                    textShadow = new Vector3(F(2), F(3), F(4));
                    break;
                case "atlas":
                    // game: the game's own fonts (letters low in the atlas break); copy: the bordered copies.
                    UseFonts(w[2] == "copy" ? copyTitle : gameTitle, w[2] == "copy" ? copyText : gameText);
                    break;
                case "border":
                    // border <share of height above and below> [<share of width left and right>]
                    borderY = F(2);
                    borderX = w.Length > 3 ? F(3) : borderX;
                    copyTitle = WithBorder(gameTitle);
                    copyText = WithBorder(gameText);
                    UseFonts(copyTitle, copyText);
                    break;
                case "text":
                    // text <rest|prefix> <letters> [size]: held until the rank changes.
                    var target = w[2] == "prefix" ? prefix : rest;
                    target.text = w[3];
                    if (w.Length > 4)
                        target.fontSize = F(4);
                    return $"{w[2]} = {w[3]}";
                case "initials":
                    // 1: deeper rank colour, ink line, own glow; 0: metal with a gold glow; 2: in a gold rim.
                    initialStyle = (int)F(2);
                    break;
                case "backdrop":
                    backdropRight = w[2] == "right";
                    PlaceBackdrop();
                    break;
                case "feedfont":
                    // trajan or perpetua: the font of the feed and the height line.
                    var font = w[2] == "perpetua" ? textFont : titleFont;
                    meta.font = font;
                    foreach (var line in Feed)
                        line.font = font;
                    break;
                case "feedstyle":
                    foreach (var line in Feed)
                        line.fontStyle = w[2] == "bold" ? FontStyles.Bold : FontStyles.Normal;
                    break;
                case "meta":
                    meta.fontStyle = w[2] == "italic" ? FontStyles.Italic : FontStyles.Normal;
                    meta.fontSize = F(3);
                    break;
                default:
                    return "hudset initial <soft> <dilate> | rim <soft> <dilate> | shadow <soft> <dilate> <offset> | "
                        + "atlas game|copy | border <fraction> | text rest|prefix <letters> [size] | initials 0|1|2 | "
                        + "backdrop center|right | feedfont trajan|perpetua | feedstyle bold|normal | "
                        + "meta italic|normal <size>";
            }
            Shadow(meta, 0.9f);
            foreach (var line in Feed)
                Shadow(line, 0.95f);
            // The rank's own effects are set again on the next frame.
            shownRank = -2;
            return System.FormattableString.Invariant(
                    $"initials={initialStyle} backdrop={(backdropRight ? "right" : "center")} rim={silksongRim} ")
                + System.FormattableString.Invariant(
                    $"shadow={textShadow} border={borderY}/{borderX} fonts={titleFont.name} / {textFont.name}");
        }

        private static void UseFonts(TMP_FontAsset title, TMP_FontAsset body)
        {
            if (!title || !body)
                return;
            titleFont = title;
            textFont = body;
            backdrop.font = rest.font = prefix.font = meta.font = title;
            foreach (var line in Feed)
                line.font = title;
        }
    }
}
