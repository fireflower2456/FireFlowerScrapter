using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // Strikes pixel lightning from the body center of the mob it sits on (parent) to the body center of Target. The shape chains hand-drawn templates (shapes) a few times between the two points, rotated and stretched.
    // 1) Warning: two different templates alternate as a single yellow line (blank in between)
    // 2) Flash: draws a white bolt with a one-pixel yellow outline. Sharp bends are thick and pointed, both ends break into dots, and rays burst at the hit point
    // 3) Cooling: another template shows once as a single yellow line, and the flashed shape remains in dark yellow
    // 4) Breakup: only fragments and dots around the bends remain, shrinking until gone
    public class LightningParticle : ParticleBase
    {
        [Tooltip("Sprite used to stamp the lightning. Material and sorting are set here")]
        [SerializeField] SpriteRenderer canvas;
        [SerializeField, Min(1)] int pixelsPerUnit = 32;
        [SerializeField, Tooltip("Lightning templates drawn as 1-pixel white lines. Left end is the shooter, right end is the hit side. Enable texture Read/Write")] Texture2D[] shapes;
        [SerializeField, Min(0.1f), Tooltip("Length one template covers (world). Templates are chained as many times as the distance divided by this")] float shapeLength = 1.5f;
        [SerializeField, Min(0f), Tooltip("When a template is shrunk, bends smaller than this many pixels are flattened so they don't smear")] float detail = 5f;
        [SerializeField] Color core = Color.white;
        [SerializeField] Color body = new(1f, 0.93f, 0.47f);
        [SerializeField, Tooltip("Cooled bolt color")] Color dim = new(0.84f, 0.63f, 0.16f);
        [SerializeField, Tooltip("Afterimage and final debris color")] Color ghost = new(0.4f, 0.29f, 0.08f);

        [Header("Shape")]
        [SerializeField, Range(0f, 180f), Tooltip("Only bends sharper than this angle are thickened and spiked")] float sharpTurn = 100f;
        [SerializeField, Range(0f, 0.4f), Tooltip("Section at each end that breaks into dots, as a fraction of bolt length")] float tail = 0.1f;
        [SerializeField, Min(0), Tooltip("Number of rays at the hit point")] int rays = 5;

        [Header("Timing")]
        [SerializeField, Min(0.01f), Tooltip("Time for the two blinks before the strike")] float strobe = 0.06f;
        [SerializeField, Min(0.01f)] float flash = 0.05f;
        [SerializeField, Min(0.01f)] float cool = 0.08f;
        [SerializeField, Min(0.01f)] float crumble = 0.1f;

        static readonly Dictionary<Texture2D, Vector2[]> traced = new();

        PixelCanvas pixels;
        Vector2[] strobeA, strobeB, main, after;
        float reachOut;
        float[] rolls;
        readonly List<Vector3> crumbs = new();
        readonly List<Vector2> points = new();
        readonly List<Vector2Int> run = new();

        void Awake() => pixels = new PixelCanvas(canvas, pixelsPerUnit);

        protected override void OnInit() => pixels.Hide();

        protected override void OnPlay() => StartCoroutine(Run());

        IEnumerator Run()
        {
            float distance = Vector2.Distance(Start, Center(Target, transform.position));
            int copies = Mathf.Max(1, Mathf.RoundToInt(distance / shapeLength));
            float copyPixels = distance * pixelsPerUnit / copies;
            strobeA = Pick(copies, copyPixels);
            strobeB = Pick(copies, copyPixels);
            main = Pick(copies, copyPixels);
            after = Pick(copies, copyPixels);
            reachOut = Mathf.Max(Reach(strobeA), Reach(strobeB), Reach(main), Reach(after));

            // Breakup order: each bend point/debris has a pre-rolled value and stays visible while it is below the remaining fraction
            rolls = new float[main.Length];
            for (int i = 0; i < rolls.Length; i++) rolls[i] = Random.value;
            crumbs.Clear();
            for (int i = 0; i < 6; i++) crumbs.Add(new Vector3(Random.Range(1, main.Length), Random.value, Random.value));

            Vector2 end = Center(Target, transform.position);
            float total = strobe + flash + cool + crumble;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                if (Target != null) end = Center(Target, end);
                Draw(Start, end, t);
                yield return null;
            }
            pixels.Hide();
            Stop();
        }

        Vector2 Start => transform.parent != null ? Center(transform.parent.gameObject, transform.position) : (Vector2)transform.position;

        static Vector2 Center(GameObject go, Vector2 fallback)
        {
            if (go == null) return fallback;
            if (go.TryGetComponent(out IMainSprite owner) && owner.MainSprite != null) return owner.MainSprite.bounds.center;
            return go.transform.position;
        }

        // Picks copies templates and chains them end to start. Each is picked independently and flipped vertically independently.
        // Shrinking smears small bends, so when one template is copyPixels pixels, bends smaller than detail pixels are flattened
        Vector2[] Pick(int copies, float copyPixels)
        {
            var joined = new List<Vector2> { Vector2.zero };
            var simple = new List<Vector2>();
            for (int c = 0; c < copies; c++)
            {
                Vector2[] shape = shapes != null && shapes.Length > 0 ? Read(shapes[Random.Range(0, shapes.Length)]) : null;
                shape ??= new[] { Vector2.zero, Vector2.right };
                simple.Clear();
                Simplify(shape, 0, shape.Length - 1, detail / Mathf.Max(1f, copyPixels), simple);
                simple.Add(shape[^1]);
                float flip = Random.value < 0.5f ? -1f : 1f;
                for (int i = 1; i < simple.Count; i++) joined.Add(new Vector2((c + simple[i].x) / copies, simple[i].y * flip / copies));
            }
            return joined.ToArray();
        }

        // How far the template strays from the line joining the two points (multiple of line length)
        static float Reach(Vector2[] shape)
        {
            float r = 0f;
            foreach (Vector2 s in shape) r = Mathf.Max(r, Mathf.Abs(s.y), -s.x, s.x - 1f);
            return r;
        }

        void Place(Vector2[] shape, Vector2 start, Vector2 end)
        {
            points.Clear();
            Vector2 dir = end - start;
            Vector2 normal = new(-dir.y, dir.x);
            foreach (Vector2 s in shape) points.Add(start + dir * s.x + normal * s.y);
        }

        void Draw(Vector2 start, Vector2 end, float t)
        {
            if (t < strobe)
            {
                int slot = Mathf.FloorToInt(t / strobe * 4f);
                if (slot % 2 == 1) { pixels.Hide(); return; }
                Begin(start, end);
                Thin(slot == 0 ? strobeA : strobeB, start, end, body);
                pixels.End();
                return;
            }

            Begin(start, end);
            Vector2 back = start - end;
            t -= strobe;
            if (t < flash)
            {
                Thin(strobeB, start, end, ghost);
                Bold(main, start, end, core);
                pixels.Outline(core, body);
                Tails(main, start, end, body);
                Splash(end, back, true, core);
            }
            else if ((t -= flash) < cool)
            {
                if (t < cool * 0.5f)
                {
                    Thin(after, start, end, body);
                    Splash(end, back, false, body);
                }
                else
                {
                    Bold(main, start, end, dim);
                    Tails(main, start, end, dim);
                }
            }
            else
            {
                float k = Mathf.Clamp01((t - cool) / crumble);
                Crumble(start, end, 1f - 0.6f * k, Mathf.Lerp(6f, 2f, k), k < 0.5f ? dim : ghost);
            }
            pixels.End();
        }

        void Begin(Vector2 start, Vector2 end)
        {
            Vector2 pad = Vector2.one * (reachOut * Vector2.Distance(start, end));
            pixels.Begin(Vector2.Min(start, end) - pad, Vector2.Max(start, end) + pad, 12);
        }

        void Thin(Vector2[] shape, Vector2 start, Vector2 end, Color c)
        {
            Place(shape, start, end);
            for (int i = 1; i < points.Count; i++) pixels.Line(points[i - 1], points[i], 0, c);
        }

        // Paints the bolt minus the dotted end sections; at sharp bends, thickens the incoming side by one pixel and spikes 2 pixels forward
        void Bold(Vector2[] shape, Vector2 start, Vector2 end, Color c)
        {
            Place(shape, start, end);
            float total = Length(), from = total * tail, to = total - total * tail * 0.6f, s = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                float length = Vector2.Distance(points[i - 1], points[i]) * pixelsPerUnit;
                PixelCanvas.Trace(pixels.ToPixel(points[i - 1]), pixels.ToPixel(points[i]), run);
                for (int j = 0; j < run.Count; j++)
                {
                    float at = s + length * j / Mathf.Max(1, run.Count - 1);
                    if (at >= from && at <= to) pixels.Pixel(run[j], c);
                }
                s += length;
            }

            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 a = points[i - 1], b = points[i], n = points[i + 1];
                if (Vector2.Angle(b - a, n - b) < sharpTurn) continue;
                Vector2 d = (b - a).normalized;
                Vector2Int u = new(Mathf.RoundToInt(d.x), Mathf.RoundToInt(d.y));
                Vector2Int tip = pixels.ToPixel(b);
                pixels.Pixel(tip + u, c);
                pixels.Pixel(tip + u * 2, c);
                Vector2Int side = Mathf.Abs(u.y) >= Mathf.Abs(u.x) ? Vector2Int.right : Vector2Int.up;
                PixelCanvas.Trace(tip, tip - u * 4, run);
                foreach (Vector2Int p in run) pixels.Pixel(p + side, c);
            }
        }

        // Both ends become broken dots: the start side sparsely behind the last point, the hit side one dot every three pixels
        void Tails(Vector2[] shape, Vector2 start, Vector2 end, Color c)
        {
            Place(shape, start, end);
            float total = Length(), head = total * tail, foot = total - total * tail * 0.6f, s = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                float length = Vector2.Distance(points[i - 1], points[i]) * pixelsPerUnit;
                PixelCanvas.Trace(pixels.ToPixel(points[i - 1]), pixels.ToPixel(points[i]), run);
                for (int j = 0; j < run.Count; j++)
                {
                    float at = s + length * j / Mathf.Max(1, run.Count - 1);
                    bool dash = (at < head && (at < 1f || (at > 3f && (int)at % 3 != 0)))
                                || (at > foot && (at > total - 1f || (int)(total - at) % 3 == 1));
                    if (dash) pixels.Pixel(run[j], c);
                }
                s += length;
            }
        }

        float Length()
        {
            float total = 0f;
            for (int i = 1; i < points.Count; i++) total += Vector2.Distance(points[i - 1], points[i]);
            return total * pixelsPerUnit;
        }

        // A fan of rays bursts from the hit point toward the shooter, some with a dot breaking off at the tip
        void Splash(Vector2 at, Vector2 back, bool big, Color c)
        {
            Vector2Int center = pixels.ToPixel(at);
            float baseAngle = Mathf.Atan2(back.y, back.x);
            int gap = big ? 2 : 3;
            for (int k = 0; k < rays; k++)
            {
                float angle = baseAngle + (k - (rays - 1) * 0.5f) * 0.55f + Random.Range(-0.12f, 0.12f);
                Vector2 d = new(Mathf.Cos(angle), Mathf.Sin(angle));
                int length = big ? Random.Range(3, 6) : Random.Range(2, 4);
                for (int s = gap; s < gap + length; s++) pixels.Pixel(center + Round(d * s), c);
                if (Random.value < 0.6f) pixels.Pixel(center + Round(d * (gap + length + 2)), c);
            }
            if (big) pixels.Dot(center, 1, c);
        }

        // From the flashed shape, keep only short arms around bend points and debris dots. Lower keep leaves less and shortens arm
        void Crumble(Vector2 start, Vector2 end, float keep, float arm, Color c)
        {
            Place(main, start, end);
            for (int i = 1; i < points.Count - 1; i++)
            {
                if (rolls[i] > keep) continue;
                Vector2 p = points[i];
                pixels.Line(p, p + (points[i - 1] - p).normalized * (arm / pixelsPerUnit), 0, c);
                pixels.Line(p, p + (points[i + 1] - p).normalized * (arm / pixelsPerUnit), 0, c);
            }
            foreach (Vector3 crumb in crumbs)
            {
                if (crumb.z > keep) continue;
                int i = Mathf.Min((int)crumb.x, points.Count - 1);
                pixels.Pixel(pixels.ToPixel(Vector2.Lerp(points[i - 1], points[i], crumb.y)), c);
            }
        }

        static Vector2Int Round(Vector2 v) => new(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));

        // Follows the path joining the two farthest ends of the template's white pixels and keeps only the bend points.
        // Aligned to the line joining the ends so the left end is (0,0) and the right end is (1,0)
        static Vector2[] Read(Texture2D tex)
        {
            if (tex == null) return null;
            if (traced.TryGetValue(tex, out Vector2[] cached)) return cached;

            Color32[] px = tex.GetPixels32();
            var on = new HashSet<Vector2Int>();
            for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
            {
                Color32 c = px[y * tex.width + x];
                if (c.a > 128 && c.r > 200 && c.g > 200 && c.b > 200) on.Add(new Vector2Int(x, y));
            }
            if (on.Count < 2) return traced[tex] = null;

            Vector2Int any = default;
            foreach (Vector2Int p in on) { any = p; break; }
            List<Vector2Int> path = Farthest(on, Farthest(on, any)[0]);
            if (path[0].x > path[^1].x) path.Reverse();

            var line = path.ConvertAll(p => (Vector2)p);
            var simple = new List<Vector2>();
            Simplify(line, 0, line.Count - 1, 1f, simple);
            simple.Add(line[^1]);

            Vector2 first = simple[0], chord = simple[^1] - first;
            float sq = Mathf.Max(chord.sqrMagnitude, 1e-4f);
            Vector2 normal = new(-chord.y, chord.x);
            var shape = new Vector2[simple.Count];
            for (int i = 0; i < simple.Count; i++)
            {
                Vector2 d = simple[i] - first;
                shape[i] = new Vector2(Vector2.Dot(d, chord) / sq, Vector2.Dot(d, normal) / sq);
            }
            return traced[tex] = shape;
        }

        // Path from from to the farthest pixel, flooding in 8 directions. [0] is that farthest pixel, the last is from
        static List<Vector2Int> Farthest(HashSet<Vector2Int> on, Vector2Int from)
        {
            var prev = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            Vector2Int last = from;
            while (queue.Count > 0)
            {
                last = queue.Dequeue();
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    Vector2Int n = last + new Vector2Int(dx, dy);
                    if (on.Contains(n) && !prev.ContainsKey(n)) { prev[n] = last; queue.Enqueue(n); }
                }
            }
            var path = new List<Vector2Int> { last };
            while (path[^1] != from) path.Add(prev[path[^1]]);
            return path;
        }

        // Douglas-Peucker: straight sections keep only their ends. The caller appends the last point
        static void Simplify(IList<Vector2> path, int a, int b, float epsilon, List<Vector2> into)
        {
            Vector2 p = path[a], q = path[b], d = q - p;
            float length = Mathf.Max(d.magnitude, 1e-4f);
            int far = -1;
            float most = epsilon;
            for (int i = a + 1; i < b; i++)
            {
                float off = Mathf.Abs(d.y * (path[i].x - p.x) - d.x * (path[i].y - p.y)) / length;
                if (off > most) { most = off; far = i; }
            }
            if (far < 0) { into.Add(p); return; }
            Simplify(path, a, far, epsilon, into);
            Simplify(path, far, b, epsilon, into);
        }
    }
}
