using UnityEngine;

namespace FireFlower
{
    // A pixel-art canvas snapped to the world pixel grid (1/PPU). Draws with one texture on one sprite renderer.
    // Set the covered area with Begin, paint with Dot/Line, then upload with End. Later paint goes on top
    public class PixelCanvas
    {
        readonly SpriteRenderer renderer;
        readonly int ppu;
        Texture2D tex;
        Sprite sprite;
        Color32[] pixels;
        int width, height;
        Vector2Int origin;

        public PixelCanvas(SpriteRenderer renderer, int pixelsPerUnit)
        {
            this.renderer = renderer;
            ppu = pixelsPerUnit;
        }

        public Vector2Int ToPixel(Vector2 world) => new(Mathf.FloorToInt(world.x * ppu), Mathf.FloorToInt(world.y * ppu));

        // Clears the area min~max (world) plus pad pixels
        public void Begin(Vector2 min, Vector2 max, int pad)
        {
            Vector2Int lo = ToPixel(min) - Vector2Int.one * pad;
            Vector2Int hi = ToPixel(max) + Vector2Int.one * (pad + 1);
            origin = lo;
            int w = Mathf.Max(1, hi.x - lo.x), h = Mathf.Max(1, hi.y - lo.y);
            if (tex == null || tex.width < w || tex.height < h)
            {
                if (tex != null) Object.Destroy(tex);
                tex = new Texture2D(Mathf.NextPowerOfTwo(w), Mathf.NextPowerOfTwo(h), TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                };
                pixels = new Color32[tex.width * tex.height];
            }
            else System.Array.Clear(pixels, 0, pixels.Length);
            if (sprite == null || w != width || h != height)
            {
                if (sprite != null) Object.Destroy(sprite);
                sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
            }
            width = w;
            height = h;
        }

        public void Pixel(Vector2Int p, Color32 c)
        {
            int x = p.x - origin.x, y = p.y - origin.y;
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            pixels[y * tex.width + x] = c;
        }

        // Round dot: radius 0 is 1 pixel wide, 1 is 3 pixels wide
        public void Dot(Vector2Int center, int radius, Color32 c)
        {
            int rr = radius * radius + radius;
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
                if (x * x + y * y <= rr) Pixel(center + new Vector2Int(x, y), c);
        }

        public void Line(Vector2 a, Vector2 b, int radius, Color32 c)
        {
            Trace(ToPixel(a), ToPixel(b), line);
            foreach (Vector2Int p in line) Dot(p, radius, c);
        }

        readonly System.Collections.Generic.List<Vector2Int> line = new();

        // Adds the cells of a 1-pixel line (Bresenham) from a to b into `into`, in order. Both ends included
        public static void Trace(Vector2Int a, Vector2Int b, System.Collections.Generic.List<Vector2Int> into)
        {
            into.Clear();
            int dx = Mathf.Abs(b.x - a.x), dy = -Mathf.Abs(b.y - a.y);
            int sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                into.Add(a);
                if (a == b) return;
                int e2 = err * 2;
                if (e2 >= dy) { err += dy; a.x += sx; }
                if (e2 <= dx) { err += dx; a.y += sy; }
            }
        }

        // Paints every other cell orthogonally adjacent to an `around`-colored cell with c, making a 1-pixel outline
        public void Outline(Color32 around, Color32 c)
        {
            int w = tex.width;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * w + x;
                if (Same(pixels[i], around)) continue;
                if ((x > 0 && Same(pixels[i - 1], around)) || (x < width - 1 && Same(pixels[i + 1], around))
                    || (y > 0 && Same(pixels[i - w], around)) || (y < height - 1 && Same(pixels[i + w], around)))
                    pixels[i] = c;
            }
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        public void End()
        {
            tex.SetPixels32(pixels);
            tex.Apply(false);
            renderer.sprite = sprite;
            renderer.enabled = true;
            Transform t = renderer.transform;
            t.SetPositionAndRotation(new Vector3((float)origin.x / ppu, (float)origin.y / ppu, t.position.z), Quaternion.identity);
            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(1f / NonZero(parent.x), 1f / NonZero(parent.y), 1f);
        }

        public void Hide() => renderer.enabled = false;

        static float NonZero(float v) => Mathf.Abs(v) < 0.0001f ? 0.0001f : v;
    }
}
