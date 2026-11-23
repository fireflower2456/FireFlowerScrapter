using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // Pixel shapes shared by blood effects. Bakes shading lit from the top left: bottom-right edge shaded, and a highlight inside the top left at 4 pixels or larger.
    // For no shading, pass white for all three colors and tint with SpriteRenderer.color
    public static class BloodPixels
    {
        static readonly Dictionary<(int, int, int, Color, Color, Color), Sprite> ellipses = new();

        public static Sprite Ellipse(int w, int h, int ppu, Color body, Color dark, Color light)
        {
            var key = (w, h, ppu, body, dark, light);
            // When entering Play again without a domain reload, sprites from the previous session have been destroyed
            if (ellipses.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;

            var tex = new Texture2D(w, h) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            // Paint pixels whose center is inside the ellipse. 3 pixels or less is a full square; corners are trimmed from 4 pixels
            float rx = w * 0.5f, ry = h * 0.5f;
            int small = Mathf.Min(w, h);
            // Highlight is a single pixel on small drops and grows with size
            float hx = Mathf.RoundToInt((w - 1) * 0.3f) + 0.5f, hy = h - Mathf.RoundToInt((h - 1) * 0.3f) - 0.5f;
            float spot = Mathf.Max(0.5f, small * 0.12f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry;
                Color c = Color.clear;
                if (dx * dx + dy * dy <= 1f)
                {
                    c = body;
                    if (small >= 3 && (dy - dx) * 0.7071f < -0.5f) c = dark;
                    else if (small >= 4 && new Vector2(x + 0.5f - hx, y + 0.5f - hy).sqrMagnitude <= spot * spot) c = light;
                }
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
            ellipses[key] = sprite;
            return sprite;
        }
    }
}
