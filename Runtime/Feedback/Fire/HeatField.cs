using UnityEngine;

namespace FireFlower
{
    // A heat grid sitting on world pixels. When the caller Adds heat, each cell takes heat from its upstream neighbor, mixed slightly with its sides,
    // and cools by grained noise that drifts with the flow. Less-cooling grains link into tongues of flame; more-cooling grains cut them off.
    // Sideways-flowing fire rises as it cools: straight while hot, curling upward as it nearly dies. Heat is painted in a few color bands.
    // Only the range of cells with heat is processed, so empty areas cost nothing even on a long grid
    public class HeatField
    {
        readonly FireLook look;
        readonly int ppu;
        float[] heat = new float[0], next = new float[0];
        RectInt area;
        // Cells with heat (grid coordinates). The buffer is always 0 outside this range
        RectInt live, stale;
        float simAcc, time;
        int shown;

        // Direction heat flows (one cell along an axis). Opposite the travel direction for flying fire, up for stationary fire
        public Vector2Int Flow = Vector2Int.up;
        // Line the fire sits on, perpendicular to the flow (world pixels). Farther from it cools more
        public float Axis;
        public int Ticks { get; private set; }
        public float Seed { get; private set; }
        public bool Empty => live.width <= 0;

        public HeatField(FireLook look, int pixelsPerUnit)
        {
            this.look = look;
            ppu = pixelsPerUnit;
            Reset();
        }

        public void Reset()
        {
            Clear(heat, live);
            Clear(next, stale);
            live = stale = default;
            simAcc = time = 0f;
            Ticks = 0;
            shown = -1;
            Seed = Random.Range(0f, 100f);
        }

        // World pixels the grid covers. Same size keeps the heat and moves it (attached fire); different size clears and reallocates
        public void Place(RectInt pixels)
        {
            if (pixels.size != area.size)
            {
                heat = new float[pixels.width * pixels.height];
                next = new float[heat.Length];
                live = stale = default;
            }
            area = pixels;
        }

        public void Add(Vector2Int pixel, float amount)
        {
            int x = pixel.x - area.x, y = pixel.y - area.y;
            if (amount <= 0f || x < 0 || y < 0 || x >= area.width || y >= area.height) return;
            int i = y * area.width + x;
            if (heat[i] >= amount) return;
            heat[i] = amount;
            live = Grow(live, x, y);
        }

        // How many times heat should spread for dt elapsed. The caller Steps that many times and adds heat each Step
        public int Advance(float dt)
        {
            time += dt;
            simAcc += dt * look.simFps;
            int n = Mathf.FloorToInt(simAcc);
            simAcc -= n;
            return n;
        }

        public void Step()
        {
            Ticks++;
            Clear(next, stale);
            RectInt reach = Clip(new RectInt(live.x - 1, live.y - 1, live.width + 2, live.height + 2));
            RectInt grown = default;
            if (live.width > 0)
            {
                Vector2Int side = Flow.x != 0 ? Vector2Int.up : Vector2Int.right;
                float lift = Flow.x != 0 ? look.lift : 0f;
                for (int y = reach.yMin; y < reach.yMax; y++)
                for (int x = reach.xMin; x < reach.xMax; x++)
                {
                    int sx = x - Flow.x, sy = y - Flow.y;
                    float here = Heat(sx, sy), below = Heat(sx - side.x, sy - side.y), above = Heat(sx + side.x, sy + side.y);
                    if (here <= 0f && below <= 0f && above <= 0f) continue;
                    int wx = area.x + x, wy = area.y + y;
                    float along = -(wx * Flow.x + wy * Flow.y) + Ticks;
                    float across = Flow.x != 0 ? wy : wx;
                    float sway = (Mathf.PerlinNoise(along * 0.08f + Seed, across * 0.15f + 50f) - 0.5f) * look.turbulence;
                    float up = Mathf.Clamp(lift * (1f - here) * (1f - here) + sway, -1f, 1f);
                    float mid = up >= 0f ? Mathf.Lerp(here, below, up) : Mathf.Lerp(here, above, -up);
                    float h = (mid * 2f + below + above) * 0.25f;
                    float n = Mathf.PerlinNoise(along * look.noiseScale.x + Seed, across * look.noiseScale.y + Seed * 0.5f);
                    float edge = 1f + Mathf.Abs(across - Axis) * look.edgeCooling;
                    float v = h - look.cooling * edge * n * n * 2.5f;
                    if (v <= 0f) continue;
                    next[y * area.width + x] = v;
                    grown = Grow(grown, x, y);
                }
            }
            (heat, next) = (next, heat);
            stale = live;
            live = grown;
        }

        // Repaints one frame per drawFps. Hides when all heat is gone.
        // Frame-flip timing is offset per fire so they don't bunch up in one frame, and the canvas spans the whole grid so its size never changes (a change recreates the sprite)
        public void Paint(PixelCanvas canvas)
        {
            int frame = Mathf.FloorToInt(time * look.drawFps + Seed);
            if (frame == shown) return;
            shown = frame;
            if (Empty)
            {
                canvas.Hide();
                return;
            }
            Vector2 min = new((area.x + 0.5f) / ppu, (area.y + 0.5f) / ppu);
            Vector2 max = new((area.xMax - 0.5f) / ppu, (area.yMax - 0.5f) / ppu);
            canvas.Begin(min, max, 1);
            int steps = Mathf.Min(look.palette.Length, look.heatAbove.Length);
            byte alpha = (byte)Mathf.RoundToInt(look.opacity * 255f);
            for (int y = live.yMin; y < live.yMax; y++)
            for (int x = live.xMin; x < live.xMax; x++)
            {
                float h = heat[y * area.width + x];
                if (h <= 0f) continue;
                // Picks color from heat mixed with neighbors so lone dots don't flicker
                h = (h * 4f + Heat(x - 1, y) + Heat(x + 1, y) + Heat(x, y - 1) + Heat(x, y + 1)) * 0.125f;
                for (int c = 0; c < steps; c++)
                    if (h > look.heatAbove[c])
                    {
                        Color32 col = look.palette[c];
                        col.a = alpha;
                        canvas.Pixel(new Vector2Int(area.x + x, area.y + y), col);
                        break;
                    }
            }
            canvas.End();
        }

        float Heat(int x, int y) => x >= 0 && x < area.width && y >= 0 && y < area.height ? heat[y * area.width + x] : 0f;

        RectInt Clip(RectInt r)
        {
            int x0 = Mathf.Max(0, r.xMin), y0 = Mathf.Max(0, r.yMin);
            int x1 = Mathf.Min(area.width, r.xMax), y1 = Mathf.Min(area.height, r.yMax);
            return x1 > x0 && y1 > y0 ? new RectInt(x0, y0, x1 - x0, y1 - y0) : default;
        }

        static RectInt Grow(RectInt r, int x, int y)
        {
            if (r.width <= 0) return new RectInt(x, y, 1, 1);
            int x0 = Mathf.Min(r.xMin, x), y0 = Mathf.Min(r.yMin, y);
            int x1 = Mathf.Max(r.xMax, x + 1), y1 = Mathf.Max(r.yMax, y + 1);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        void Clear(float[] buffer, RectInt r)
        {
            if (r.width <= 0) return;
            for (int y = r.yMin; y < r.yMax; y++) System.Array.Clear(buffer, y * area.width + r.xMin, r.width);
        }
    }
}
