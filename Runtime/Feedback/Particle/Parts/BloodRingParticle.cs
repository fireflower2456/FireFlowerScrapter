using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // Pixel blood ring. Bursts out from its spot then slows, thinning and darkening until it vanishes.
    // With repeat on, a new ring spreads every interval until stopped
    public class BloodRingParticle : ParticleBase
    {
        class Ring
        {
            public SpriteRenderer sr;
            public float age;
        }

        [Tooltip("Sprite to borrow material and sorting from. Keep it disabled")]
        [SerializeField] SpriteRenderer template;
        [SerializeField, Min(1)] int pixelsPerUnit = 16;
        [SerializeField] Color startColor = new(1f, 0.32f, 0.3f);
        [SerializeField] Color endColor = new(0.36f, 0.02f, 0.06f);
        [SerializeField, Tooltip("Pixels. start -> end")] Vector2 radius = new(3f, 14f);
        [SerializeField, Tooltip("Pixels. start -> end")] Vector2 thickness = new(4f, 1f);
        [SerializeField, Min(0.01f)] float duration = 0.25f;
        [SerializeField, Min(1f), Tooltip("Higher = faster burst at start, slower at end")] float ease = 3f;
        [SerializeField, Range(0f, 1f), Tooltip("From this point (fraction of lifetime) the ring breaks into dots and fades. 1 = never breaks")] float breakAt = 0.4f;
        [SerializeField, Tooltip("If off, detaches from the parent on burst (the ring stays even if the mob dies)")] bool follow = true;
        [SerializeField] bool loop;
        [SerializeField, Min(0.01f)] float interval = 0.4f;

        const int BreakSteps = 8;

        // Built once per (radius, thickness, break step, PPU)
        static readonly Dictionary<(int, int, int, int), Sprite> shapes = new();

        readonly List<Ring> live = new();
        readonly Stack<SpriteRenderer> pool = new();
        float timer;
        bool emitting, stopping;

        void Awake() => template.gameObject.SetActive(false);

        // Paints a pixel if its center is inside the outer circle and outside the inner one. Thickness >= radius gives a filled circle.
        // When breaking, directions drop out in a fixed order so remaining dots don't flicker as the step rises
        Sprite Shape(int r, int t, int broken)
        {
            if (shapes.TryGetValue((r, t, broken, pixelsPerUnit), out Sprite sprite) && sprite != null) return sprite;

            int size = r * 2 + 1;
            var tex = new Texture2D(size, size) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float outer = r + 0.5f, inner = Mathf.Max(0f, r + 0.5f - t);
            float drop = (float)broken / BreakSteps;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x - r, y - r).magnitude;
                bool fill = d <= outer && d >= inner && Order(Mathf.Atan2(y - r, x - r)) >= drop;
                tex.SetPixel(x, y, fill ? Color.white : Color.clear);
            }
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            shapes[(r, t, broken, pixelsPerUnit)] = sprite;
            return sprite;
        }

        // Fixed 0-1 value per direction (about 8-degree bins)
        static float Order(float angle)
        {
            int cell = Mathf.FloorToInt((angle + Mathf.PI) / (Mathf.PI * 2f) * 45f);
            return Mathf.Abs(Mathf.Sin(cell * 12.9898f) * 43758.5453f) % 1f;
        }

        protected override void OnInit()
        {
            foreach (Ring r in live) Put(r.sr);
            live.Clear();
            emitting = stopping = false;
            timer = 0f;
        }

        protected override void OnPlay()
        {
            if (!follow) transform.SetParent(null, true);
            emitting = loop;
            Emit();
        }

        public override bool Finish()
        {
            stopping = true;
            emitting = false;
            return live.Count == 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (emitting)
            {
                timer += dt;
                if (timer >= interval)
                {
                    timer -= interval;
                    Emit();
                }
            }

            for (int i = live.Count - 1; i >= 0; i--)
            {
                Ring r = live[i];
                r.age += dt;
                if (r.age >= duration)
                {
                    Put(r.sr);
                    live.RemoveAt(i);
                    continue;
                }
                Draw(r);
            }

            if (emitting || live.Count > 0) return;
            if (stopping) Done();
            else Stop();
        }

        void Emit()
        {
            SpriteRenderer sr = pool.Count > 0 ? pool.Pop() : Instantiate(template, transform);
            sr.gameObject.SetActive(true);
            var r = new Ring { sr = sr };
            live.Add(r);
            Draw(r);
        }

        void Draw(Ring r)
        {
            float t = r.age / duration;
            float k = 1f - Mathf.Pow(1f - t, ease);
            int rad = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(radius.x, radius.y, k)));
            int thick = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(thickness.x, thickness.y, t)), 1, rad + 1);
            int broken = breakAt >= 1f ? 0 : Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(breakAt, 1f, t) * BreakSteps), 0, BreakSteps - 1);
            r.sr.sprite = Shape(rad, thick, broken);
            r.sr.color = Color.Lerp(startColor, endColor, t);
            // Snap to the pixel grid
            Vector2 snapped = new(Mathf.Round(transform.position.x * pixelsPerUnit) / pixelsPerUnit, Mathf.Round(transform.position.y * pixelsPerUnit) / pixelsPerUnit);
            r.sr.transform.position = new Vector3(snapped.x, snapped.y, transform.position.z);
            r.sr.transform.localScale = Vector3.one / Mathf.Max(0.0001f, transform.lossyScale.x);
        }

        void Put(SpriteRenderer sr)
        {
            sr.gameObject.SetActive(false);
            pool.Push(sr);
        }
    }
}
