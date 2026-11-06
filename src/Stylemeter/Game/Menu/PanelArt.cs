using UnityEngine;

namespace Stylemeter.Game.Menu
{
    // The panel's shapes, drawn once in code: the framed backdrop, the glow under the chosen row, the thin rules and
    // the arrows. White where a shape takes the tint of its Image.
    internal static class PanelArt
    {
        public static readonly Color Gold = new Color(0.847f, 0.706f, 0.416f);
        public static readonly Color Ink = new Color(0.93f, 0.9f, 0.84f);
        public static readonly Color Dim = new Color(0.56f, 0.53f, 0.49f);
        public static readonly Color Off = new Color(0.66f, 0.33f, 0.3f);

        private const int FrameSize = 64;
        private const int FrameBorder = 20;

        private static Sprite frame;
        private static Sprite glow;
        private static Sprite rule;
        private static Sprite arrow;
        private static Sprite diamond;

        // Dark glass with a gold line and a fainter inner line, sliced so it scales to any size.
        public static Sprite Frame => frame ? frame : frame = MakeFrame();

        public static Sprite Glow => glow ? glow : glow = MakeGlow(256, 32);

        public static Sprite Rule => rule ? rule : rule = MakeRule(512, 4);

        // A right-pointing triangle; rotated for the other directions.
        public static Sprite Arrow => arrow ? arrow : arrow = MakeArrow(32);

        public static Sprite Diamond => diamond ? diamond : diamond = MakeDiamond(16);

        private static Sprite MakeFrame()
        {
            var texture = NewTexture(FrameSize, FrameSize);
            var fill = new Color(0.035f, 0.03f, 0.035f, 0.9f);
            var outer = new Color(Gold.r, Gold.g, Gold.b, 0.85f);
            var inner = new Color(Gold.r, Gold.g, Gold.b, 0.3f);
            for (var y = 0; y < FrameSize; y++)
            {
                for (var x = 0; x < FrameSize; x++)
                {
                    var edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(FrameSize - 1 - x, FrameSize - 1 - y));
                    var color = edge < 3 ? Color.clear
                        : edge < 5 ? outer
                        : edge == 9 ? Blend(fill, inner)
                        : fill;
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            var border = new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder);
            return Sprite.Create(texture, new Rect(0, 0, FrameSize, FrameSize), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }

        // Soft in both directions, brightest in the middle: the chosen row seems lit from within.
        private static Sprite MakeGlow(int width, int height)
        {
            var texture = NewTexture(width, height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var across = Mathf.Abs(x / (width - 1f) * 2f - 1f);
                    var up = Mathf.Abs(y / (height - 1f) * 2f - 1f);
                    var alpha = (1f - Smooth(0.35f, 1f, across)) * (1f - Smooth(0.2f, 1f, up));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            return Whole(texture);
        }

        private static Sprite MakeRule(int width, int height)
        {
            var texture = NewTexture(width, height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var across = Mathf.Abs(x / (width - 1f) * 2f - 1f);
                    var middle = y == height / 2 || y == height / 2 - 1;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, middle ? 1f - Smooth(0.5f, 1f, across) : 0f));
                }
            }
            texture.Apply();
            return Whole(texture);
        }

        private static Sprite MakeArrow(int size)
        {
            var texture = NewTexture(size, size);
            var middle = (size - 1) / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Inside when the distance to the slanted edges is positive; one pixel of falloff smooths them.
                    var tip = size - 6f;
                    var halfHeight = (tip - x) * 0.55f;
                    var inside = Mathf.Min(halfHeight - Mathf.Abs(y - middle), x - 6f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(inside + 0.5f)));
                }
            }
            texture.Apply();
            return Whole(texture);
        }

        private static Sprite MakeDiamond(int size)
        {
            var texture = NewTexture(size, size);
            var middle = (size - 1) / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var inside = middle - (Mathf.Abs(x - middle) + Mathf.Abs(y - middle));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(inside + 0.5f)));
                }
            }
            texture.Apply();
            return Whole(texture);
        }

        private static Texture2D NewTexture(int width, int height) =>
            new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

        private static Sprite Whole(Texture2D texture) =>
            Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);

        private static Color Blend(Color under, Color over) =>
            Color.Lerp(under, new Color(over.r, over.g, over.b, under.a), over.a);

        private static float Smooth(float from, float to, float value)
        {
            var t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }
    }
}
