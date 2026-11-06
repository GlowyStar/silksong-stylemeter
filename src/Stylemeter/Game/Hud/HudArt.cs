using System.IO;
using System.Reflection;
using UnityEngine;

namespace Stylemeter.Game.Hud
{
    // The needle of the meter (art/needle/drawn, drawn in the game's inked style): the needle and the silk-hilted
    // rapier on one layout, built into the plugin, and soft glows made from each picture's own shape, so the glow
    // follows the art and has no edges.
    internal static class HudArt
    {
        // Each glow texel covers this many art pixels; the glow reaches this many texels past the art.
        private const int GlowDown = 4;
        private const int GlowPad = 12;
        private const int GlowRadius = 3;

        public static Sprite Needle { get; private set; }
        public static Sprite Rapier { get; private set; }
        public static Sprite NeedleGlow { get; private set; }
        public static Sprite RapierGlow { get; private set; }
        // SSSILKSTORM: the needle with blood on it, and its crimson glow.
        public static Sprite NeedleBloody { get; private set; }
        public static Sprite BloodGlow { get; private set; }
        // SILKSONG: soft rays from a centre, white, tinted in use.
        public static Sprite Rays { get; private set; }
        // A soft capsule of white light, tinted in use.
        public static Sprite Aura { get; private set; }
        // A soft round light, stretched into an ellipse in use: the glow behind the SILKSONG word (a capsule would
        // show its flat top and bottom as a band).
        public static Sprite Halo { get; private set; }
        // SSSILKSTORM coming and going: a splash, a drop in flight and a run of blood; white, tinted in use.
        public static Sprite BloodSplash { get; private set; }
        public static Sprite BloodDrop { get; private set; }
        public static Sprite BloodRun { get; private set; }
        // A thin stroke of light, brightest along its middle: the slash through SILKSONG and the streak it arrives in.
        public static Sprite Stroke { get; private set; }
        // The glow's margin around the art, as fractions of the art's width and height.
        public static Vector2 GlowMargin { get; private set; }
        public static bool Loaded { get; private set; }

        public static void Load()
        {
            if (Loaded)
                return;
            Loaded = true;
            Needle = Load("needle_drawn.png", out var needle);
            Rapier = Load("rapier_drawn.png", out var rapier);
            // The needle is thin, so its blurred shape is faint: a stronger multiplier than the rapier's guard needs.
            if (needle)
            {
                NeedleGlow = Glow(needle, new Color32(255, 232, 190, 255), 1.9f);
                var bloody = Bloody(needle);
                NeedleBloody = Sprite.Create(bloody, new Rect(0, 0, bloody.width, bloody.height),
                    new Vector2(0.5f, 0.5f), 100f);
                BloodGlow = Glow(bloody, new Color32(214, 34, 52, 255), 1.7f);
            }
            Rays = MakeRays(256, 12);
            if (rapier)
                RapierGlow = Glow(rapier, new Color32(255, 196, 110, 255), 2.4f);
            Aura = MakeAura(256, 64);
            Halo = MakeAura(128, 128);
            BloodSplash = Shape("Stylemeter splash", 128, 128, (x, y) => SplashAt(x / 128f, y / 128f));
            BloodDrop = Shape("Stylemeter drop", 32, 48, DropAt);
            // The blob at its top and the drop at its foot keep their shape however long the run is drawn.
            BloodRun = Sliced(Shape("Stylemeter run", 16, 64, RunAt), new Vector4(0f, 12f, 0f, 10f));
            Stroke = Shape("Stylemeter stroke", 256, 32, StrokeAt);
        }

        // A white picture whose opacity at each pixel's centre comes from the given function.
        private static Sprite Shape(string name, int w, int h, System.Func<float, float, float> opacityAt)
        {
            var pixels = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var a = Mathf.Clamp01(opacityAt(x + 0.5f, y + 0.5f));
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        // The same picture cut for stretching: the border (left, bottom, right, top) keeps its size.
        private static Sprite Sliced(Sprite sprite, Vector4 border) => Sprite.Create(sprite.texture, sprite.rect,
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);

        // A splash, in a unit square: a ragged blot, thin spikes thrown out of it, and a drop past each spike's tip.
        private static float SplashAt(float u, float v)
        {
            float dx = u * 2f - 1f, dy = v * 2f - 1f;
            var r = Mathf.Sqrt(dx * dx + dy * dy);
            float cos = r > 0f ? dx / r : 1f, sin = r > 0f ? dy / r : 0f;
            var edge = 0.36f + 0.1f * Noise(cos * 2.2f + 5f, sin * 2.2f + 5f)
                + 0.05f * Noise(cos * 6f + 9f, sin * 6f + 9f);
            var a = Mathf.Clamp01((edge - r) * 40f);
            var angle = Mathf.Atan2(dy, dx);
            for (var k = 0; k < 11; k++)
            {
                var spike = Hash(k, 17) * Mathf.PI * 2f;
                var length = 0.55f + 0.28f * Hash(k, 29);
                var along = r * Mathf.Cos(angle - spike);
                var across = Mathf.Abs(r * Mathf.Sin(angle - spike));
                if (along > 0f && along < length)
                {
                    var width = (0.06f + 0.04f * Hash(k, 41)) * (1f - along / length);
                    a = Mathf.Max(a, Mathf.Clamp01((width - across) * 60f));
                }
                var tip = length + 0.06f;
                float tx = Mathf.Cos(spike) * tip - dx, ty = Mathf.Sin(spike) * tip - dy;
                var dropRadius = 0.03f + 0.03f * Hash(k, 53);
                a = Mathf.Max(a, Mathf.Clamp01((dropRadius - Mathf.Sqrt(tx * tx + ty * ty)) * 60f));
            }
            return a;
        }

        // A drop in flight, 32 by 48: round below, drawn out into a point above.
        private static float DropAt(float x, float y)
        {
            const float cx = 16f, cy = 15f, radius = 11f, tail = 30f;
            var dx = x - cx;
            var round = radius - Mathf.Sqrt(dx * dx + (y - cy) * (y - cy));
            var a = Mathf.Clamp01(round + 0.5f);
            if (y > cy)
            {
                var half = radius * Mathf.Pow(Mathf.Clamp01(1f - (y - cy) / tail), 1.5f);
                a = Mathf.Max(a, Mathf.Clamp01(half - Mathf.Abs(dx) + 0.5f));
            }
            return a;
        }

        // A run of blood, 16 by 64: a blob where it leaves the letter at the top, a thin stream, and a round drop at
        // the bottom.
        private static float RunAt(float x, float y)
        {
            const float cx = 8f, bulbY = 6f, bulb = 3.6f;
            var dx = Mathf.Abs(x - cx);
            var blob = 3.3f * Square(Mathf.Clamp01(1f - (64f - y) / 10f));
            var half = 2.2f + 0.3f * (y - bulbY) / (64f - bulbY) + blob;
            var stream = y > bulbY ? Mathf.Clamp01(half - dx + 0.5f) : 0f;
            var drop = Mathf.Clamp01(bulb - Mathf.Sqrt(dx * dx + (y - bulbY) * (y - bulbY)) + 0.5f);
            return Mathf.Max(stream, drop);
        }

        // A stroke of light, 256 by 32: a hard bright core in a soft glow, tapering to nothing at both ends.
        private static float StrokeAt(float x, float y)
        {
            var taper = Mathf.Pow(Mathf.Sin(Mathf.PI * x / 256f), 0.7f);
            var dy = Mathf.Abs(y - 16f);
            var core = Mathf.Exp(-Square(dy / 2.2f));
            var glow = 0.45f * Mathf.Exp(-Square(dy / 7f));
            return (core + glow) * taper;
        }

        private static float Square(float x) => x * x;

        // The needle after a lot of killing (SSSILKSTORM): the same drawing with blood on it, more of it toward the
        // point and ragged along the shaft, and a few drops hanging under it. The ink stays black.
        private static Texture2D Bloody(Texture2D needle)
        {
            int w = needle.width, h = needle.height;
            var src = needle.GetPixels32();
            var dst = new Color32[src.Length];
            Color maroon = new Color(0.30f, 0.02f, 0.06f), crimson = new Color(0.80f, 0.09f, 0.17f),
                pink = new Color(0.98f, 0.58f, 0.60f);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    var p = src[i];
                    dst[i] = p;
                    if (p.a == 0)
                        continue;
                    var c = (Color)p;
                    var lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    if (lum < 0.2f)
                        continue;
                    // Clean near the eye, soaked toward the point, ragged where it ends, and a few spatters before it.
                    var ragged = Noise(x / 37f, y / 9f) - 0.5f;
                    var soaked = Mathf.Clamp01(Mathf.SmoothStep(0.25f, 0.85f, x / (float)w) * 1.1f + ragged * 0.8f);
                    var spatter = Mathf.SmoothStep(0.66f, 0.76f, Noise(x / 14f, y / 7f + 40f)) * 0.9f;
                    var amount = Mathf.Max(soaked, spatter);
                    var blood = lum > 0.9f
                        ? Color.Lerp(crimson, pink, (lum - 0.9f) / 0.1f * 0.5f)
                        : Color.Lerp(maroon, crimson, lum / 0.9f);
                    var mixed = Color.Lerp(c, blood, amount);
                    dst[i] = new Color32((byte)(mixed.r * 255f), (byte)(mixed.g * 255f), (byte)(mixed.b * 255f), p.a);
                }
            }
            Drop(dst, w, h, (int)(0.57f * w), 30, maroon, crimson, pink);
            Drop(dst, w, h, (int)(0.71f * w), 44, maroon, crimson, pink);
            Drop(dst, w, h, (int)(0.86f * w), 36, maroon, crimson, pink);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "Stylemeter needle, bloody",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
            };
            texture.SetPixels32(dst);
            texture.Apply(true, false);
            return texture;
        }

        // A drop hanging under the shaft at column x: a neck widening into a round end, inked like the art.
        private static void Drop(Color32[] px, int w, int h, int x, int length, Color maroon, Color crimson, Color pink)
        {
            // The lowest opaque row of the shaft at this column (texture rows count up from the bottom).
            var edge = -1;
            for (var y = h / 2; y >= 0; y--)
            {
                if (px[y * w + x].a > 0)
                    edge = y;
                else if (edge >= 0)
                    break;
            }
            if (edge < 0)
                return;
            const float bulb = 11f, ink = 3.5f;
            var bulbY = edge - length + bulb;
            for (var y = Mathf.Max(0, edge - length - 4); y < edge; y++)
            {
                // Half width of the neck at this row: 3 at the shaft, nearly the bulb at its top.
                var along = Mathf.Clamp01((edge - y) / Mathf.Max(1f, edge - bulbY));
                var half = Mathf.Lerp(3f, bulb * 0.85f, along * along);
                for (var dx = -16; dx <= 16; dx++)
                {
                    var px0 = x + dx;
                    if (px0 < 0 || px0 >= w)
                        continue;
                    var neckD = y >= bulbY ? Mathf.Abs(dx) - half : 99f;
                    var bulbD = Mathf.Sqrt(dx * dx + (y - bulbY) * (y - bulbY)) - bulb;
                    var d = Mathf.Min(neckD, bulbD);
                    if (d >= 1f)
                        continue;
                    var cover = Mathf.Clamp01(1f - d);
                    var down = Mathf.Clamp01((edge - y) / (float)length);
                    var fill = Color.Lerp(maroon, crimson, 0.35f + 0.65f * down);
                    var shine = Mathf.Sqrt((dx + 4f) * (dx + 4f) + (y - bulbY - 3f) * (y - bulbY - 3f));
                    if (shine < 3f)
                        fill = Color.Lerp(fill, pink, (1f - shine / 3f) * 0.8f);
                    var colour = d > -ink ? Color.black : fill;
                    var i = y * w + px0;
                    var under = (Color)px[i];
                    var a = cover + under.a * (1f - cover);
                    var mixed = a > 0f ? (colour * cover + under * under.a * (1f - cover)) / a : colour;
                    px[i] = new Color32((byte)(mixed.r * 255f), (byte)(mixed.g * 255f), (byte)(mixed.b * 255f),
                        (byte)(a * 255f));
                }
            }
        }

        // Smooth value noise in 0..1, for the ragged edge of the blood.
        private static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), u),
                Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), u), v);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                var n = x * 374761393 + y * 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
            }
        }

        // Soft rays from the centre, every other one shorter, with no bright core (a core would wash out the letters
        // in the middle): the halo behind SILKSONG.
        private static Sprite MakeRays(int size, int count)
        {
            var pixels = new Color32[size * size];
            var centre = size / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - centre) / centre, dy = (y + 0.5f - centre) / centre;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var angle = Mathf.Atan2(dy, dx);
                    var ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(angle * count), 6f);
                    var reach = 0.75f + 0.25f * Mathf.Cos(angle * count / 2f);
                    var fall = Mathf.Pow(Mathf.Clamp01(1f - r / reach), 1.5f);
                    // Rays start a little out from the centre, behind the letters, and fade toward the edges.
                    var start = Mathf.Clamp01((r - 0.08f) / 0.25f);
                    var a = Mathf.Clamp01(ray * fall * start) * Mathf.Clamp01((1f - r) * 6f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Stylemeter rays",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // Made here, not painted: full along the middle line, fading to nothing at the edge (smoothstep of distance).
        private static Sprite MakeAura(int w, int h)
        {
            var pixels = new Color32[w * h];
            var radius = h / 2f;
            var half = w / 2f - radius;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var dx = Mathf.Max(Mathf.Abs(x + 0.5f - w / 2f) - half, 0f);
                    var dy = y + 0.5f - h / 2f;
                    var a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / radius);
                    a = a * a * (3f - 2f * a);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Stylemeter aura",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite Load(string name, out Texture2D texture)
        {
            texture = null;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Stylemeter.Art." + name))
            {
                if (stream == null)
                {
                    Plugin.Log.LogWarning($"Stylemeter: art {name} is missing from the plugin");
                    return null;
                }
                var bytes = new byte[stream.Length];
                var read = 0;
                while (read < bytes.Length)
                {
                    var n = stream.Read(bytes, read, bytes.Length - read);
                    if (n <= 0)
                        break;
                    read += n;
                }
                var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!loaded.LoadImage(bytes, false))
                {
                    Plugin.Log.LogWarning($"Stylemeter: art {name} could not be read");
                    return null;
                }
                // A copy with a mip chain: the art is drawn at a quarter of its size and must stay smooth.
                texture = new Texture2D(loaded.width, loaded.height, TextureFormat.RGBA32, true)
                {
                    name = "Stylemeter " + name,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 2,
                };
                texture.SetPixels32(loaded.GetPixels32());
                texture.Apply(true, false);
                Object.Destroy(loaded);
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
        }

        // A tinted halo from the art's alpha: shrunk, padded, blurred three times (close to a Gaussian).
        private static Sprite Glow(Texture2D art, Color32 tint, float strength)
        {
            var source = art.GetPixels32();
            int w = art.width / GlowDown, h = art.height / GlowDown;
            int W = w + GlowPad * 2, H = h + GlowPad * 2;
            var a = new float[W * H];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var sum = 0;
                    for (var dy = 0; dy < GlowDown; dy++)
                    {
                        var row = (y * GlowDown + dy) * art.width;
                        for (var dx = 0; dx < GlowDown; dx++)
                            sum += source[row + x * GlowDown + dx].a;
                    }
                    a[(y + GlowPad) * W + x + GlowPad] = sum / (255f * GlowDown * GlowDown);
                }
            }
            var tmp = new float[W * H];
            for (var pass = 0; pass < 3; pass++)
            {
                BoxBlur(a, tmp, W, H, GlowRadius, 1, W);
                BoxBlur(tmp, a, H, W, GlowRadius, W, 1);
            }
            var pixels = new Color32[W * H];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(tint.r, tint.g, tint.b, (byte)(Mathf.Clamp01(a[i] * strength) * 255f));
            var glow = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                name = art.name + " glow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            glow.SetPixels32(pixels);
            glow.Apply(false, true);
            GlowMargin = new Vector2(GlowPad * GlowDown / (float)art.width, GlowPad * GlowDown / (float)art.height);
            return Sprite.Create(glow, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

        // One box blur along rows (step 1, stride W) or columns (step W, stride 1).
        private static void BoxBlur(float[] from, float[] to, int length, int lines, int radius, int step, int stride)
        {
            var span = radius * 2 + 1;
            for (var line = 0; line < lines; line++)
            {
                var start = line * stride;
                var sum = 0f;
                for (var i = -radius; i <= radius; i++)
                    sum += from[start + Mathf.Clamp(i, 0, length - 1) * step];
                for (var i = 0; i < length; i++)
                {
                    to[start + i * step] = sum / span;
                    sum += from[start + Mathf.Min(i + radius + 1, length - 1) * step];
                    sum -= from[start + Mathf.Max(i - radius, 0) * step];
                }
            }
        }
    }
}
