using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // Blood orb. Droplets pulled toward the center push each other into a clump that floats above the target's head and follows it.
    // Color is chosen per pixel by distance from the center, not per droplet (bright inside, dark outside) — overlapping droplets look melted into one mass.
    // The clump rolls around its center and clenches then relaxes on each heartbeat. Droplets melt away one by one and new ones are drawn in from outside.
    // Thin blood strands spiral in along tilted orbits (back half behind the clump, front half over it).
    // The heartbeat speeds up as SetFill (remaining blood 0–1) drops. Squash deforms it briefly. Vanishes immediately on stop (the caller plays any burst)
    public class BloodOrbParticle : ParticleBase
    {
        class Drop
        {
            public Vector2 pos, vel;
            public float size, scale;
            public bool melting;
        }

        class Strand
        {
            public float angle, age, life, tilt, roll;
        }

        [SerializeField, Tooltip("Child sprite used as the canvas. Other effects target it")] SpriteRenderer body;
        [SerializeField, Min(1)] int pixelsPerUnit = 16;
        [SerializeField, Tooltip("Outside → inside: darkest blood … hot core")] Color[] palette =
        {
            new(0.09f, 0f, 0.02f), new(0.2f, 0.01f, 0.04f), new(0.38f, 0.02f, 0.05f), new(0.62f, 0.05f, 0.08f), new(0.86f, 0.24f, 0.2f),
        };
        [SerializeField, Tooltip("Distances where palette entries change (relative to clump radius). Innermost first")] float[] bands = { 0.2f, 0.4f, 0.6f, 0.8f };
        [SerializeField, Tooltip("Distance from the top of the head to the bottom of the orb (world)")] float height = 0.35f;

        [Header("Droplets")]
        [SerializeField, Min(1)] int drops = 20;
        [SerializeField, Tooltip("Droplet diameter (pixels)")] Vector2 dropSize = new(4f, 7f);
        [SerializeField, Tooltip("Pull toward the center")] float pull = 18f;
        [SerializeField, Tooltip("Swirl around the center")] float spin = 30f;
        [SerializeField, Tooltip("Repulsion between overlapping droplets")] float repel = 220f;
        [SerializeField, Tooltip("Jitter")] float jitter = 25f;
        [SerializeField, Min(0f)] float damping = 4f;
        [SerializeField, Min(1.1f), Tooltip("Droplet influence radius (× radius). Larger joins farther droplets stickily")] float merge = 2.2f;
        [SerializeField, Min(0.01f), Tooltip("Time to gather in from outside at the start")] float formTime = 0.35f;

        [Header("Renewal")]
        [SerializeField, Min(0.01f), Tooltip("Every interval one droplet melts away and one is drawn in from outside")] float renew = 0.18f;
        [SerializeField, Min(0.01f)] float meltTime = 0.15f;
        [SerializeField, Tooltip("Distance new droplets come in from (pixels)")] float feedFrom = 14f;

        [Header("Heartbeat")]
        [SerializeField, Tooltip("Heartbeat interval (s). Full blood → empty")] Vector2 beat = new(0.75f, 0.3f);
        [SerializeField, Tooltip("Pull multiplier at the moment of a beat")] float beatPull = 4f;
        [SerializeField, Min(0.01f)] float beatTime = 0.08f;

        [Header("Fling")]
        [SerializeField] Vector2 flingEvery = new(0.25f, 0.7f);
        [SerializeField] float flingSpeed = 90f;

        [Header("Spiral Strands")]
        [SerializeField, Min(0)] int strands = 4;
        [SerializeField] Vector2 strandLife = new(0.55f, 0.85f);
        [SerializeField, Tooltip("Starting orbit radius (× clump radius)")] float strandReach = 1.5f;
        [SerializeField, Tooltip("Turns a strand makes before being absorbed")] float strandTurns = 1.4f;
        [SerializeField, Min(2)] int strandPoints = 12;
        [SerializeField, Tooltip("Time gap between neighboring points (relative to lifetime)")] float strandGap = 0.035f;

        [Header("Squash")]
        [SerializeField, Range(0f, 1f)] float squash = 0.3f;
        [SerializeField, Min(0.01f)] float squashTime = 0.12f;

        PixelCanvas canvas;
        readonly List<Drop> list = new();
        readonly List<Strand> strandList = new();
        readonly List<Vector2Int> run = new();
        float age, fill, beatClock, squashAge, renewTimer, flingTimer, size, seed;
        Vector2 center;
        bool live;

        public Vector3 Center => new(center.x, center.y, transform.position.z);

        public void SetFill(float value) => fill = Mathf.Clamp01(value);

        public void Squash() => squashAge = 0f;

        void Awake() => canvas = new PixelCanvas(body, pixelsPerUnit);

        protected override void OnInit()
        {
            age = beatClock = renewTimer = 0f;
            fill = 1f;
            squashAge = -1f;
            flingTimer = flingEvery.y;
            seed = Random.value * 100f;
            live = true;

            // Starts scattered outside, then gathers
            list.Clear();
            for (int i = 0; i < drops; i++) list.Add(Feed(Random.Range(0.5f, 1f)));
            size = 1f;

            strandList.Clear();
            for (int i = 0; i < strands; i++)
            {
                var s = new Strand();
                Respawn(s);
                s.age = s.life * i / Mathf.Max(1, strands);
                strandList.Add(s);
            }
            Draw(0f);
        }

        public override bool Finish()
        {
            live = false;
            canvas.Hide();
            return true;
        }

        void LateUpdate()
        {
            if (live) Draw(Time.deltaTime);
        }

        Drop Feed(float reach)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            return new Drop
            {
                pos = dir * feedFrom * reach,
                vel = -dir * feedFrom * 4f,
                size = Random.Range(dropSize.x, dropSize.y),
                scale = 1f,
            };
        }

        void Respawn(Strand s)
        {
            s.angle = Random.value * Mathf.PI * 2f;
            s.age = 0f;
            s.life = Random.Range(strandLife.x, strandLife.y);
            s.tilt = Random.Range(0.25f, 0.75f) * (Random.value < 0.5f ? -1f : 1f);
            s.roll = Random.Range(-0.6f, 0.6f);
        }

        Color32 Tone(int i) => palette[Mathf.Clamp(i, 0, palette.Length - 1)];

        void Draw(float dt)
        {
            age += dt;
            if (squashAge >= 0f) squashAge += dt;
            Simulate(dt);

            // Clump radius: the larger droplet-edge distances. Follows slowly so a single flung droplet doesn't shake it
            float far = 0f;
            foreach (Drop d in list) far = Mathf.Max(far, Mathf.Min(d.pos.magnitude, feedFrom) + d.size * 0.5f);
            size = Mathf.Lerp(size, far, 1f - Mathf.Exp(-6f * dt));
            if (size < 1f) size = far;

            float sq = squashAge >= 0f && squashAge < squashTime ? squash * (1f - squashAge / squashTime) : 0f;
            Vector2 stretch = new(1f + sq, 1f - sq);

            SpriteRenderer head = FindTargetSprite();
            Vector2 top = head != null ? new Vector2(head.bounds.center.x, head.bounds.max.y) : (Vector2)transform.position;
            center = top + Vector2.up * (height + size / pixelsPerUnit);
            Vector2 c = center * pixelsPerUnit;

            float reach = Mathf.Max(size * strandReach, feedFrom) + 6f;
            canvas.Begin(center - Vector2.one * (reach / pixelsPerUnit), center + Vector2.one * (reach / pixelsPerUnit), 2);

            foreach (Strand s in strandList)
            {
                s.age += dt;
                if (s.age >= s.life) Respawn(s);
            }
            DrawStrands(c, size, false);
            DrawMass(c, stretch);
            DrawStrands(c, size, true);
            canvas.End();
        }

        void Simulate(float dt)
        {
            if (dt <= 0f) return;
            float period = Mathf.Lerp(beat.y, beat.x, fill);
            beatClock += dt / period;
            if (beatClock >= 1f) beatClock -= 1f;
            float k = beatClock * period < beatTime ? beatPull : 1f;

            // Melt one, draw one in
            if ((renewTimer += dt) >= renew)
            {
                renewTimer -= renew;
                foreach (Drop d in list)
                    if (!d.melting)
                    {
                        d.melting = true;
                        break;
                    }
                list.Add(Feed(1f));
            }
            if ((flingTimer -= dt) <= 0f && list.Count > 0)
            {
                flingTimer = Random.Range(flingEvery.x, flingEvery.y);
                Drop d = list[Random.Range(0, list.Count)];
                Vector2 dir = d.pos.sqrMagnitude > 0.01f ? d.pos.normalized : Random.insideUnitCircle.normalized;
                d.vel += dir * flingSpeed;
            }

            for (int i = 0; i < list.Count; i++)
            {
                Drop a = list[i];
                Vector2 acc = -a.pos * pull * k;
                float dist = a.pos.magnitude;
                if (dist > 0.01f) acc += new Vector2(-a.pos.y, a.pos.x) / dist * spin;
                acc += Random.insideUnitCircle * jitter;
                for (int j = 0; j < list.Count; j++)
                {
                    if (i == j) continue;
                    Vector2 apart = a.pos - list[j].pos;
                    float touch = (a.size * a.scale + list[j].size * list[j].scale) * 0.55f;
                    float m = apart.magnitude;
                    if (m >= touch) continue;
                    acc += (m > 0.01f ? apart / m : Random.insideUnitCircle.normalized) * (touch - m) * repel;
                }
                a.vel += acc * dt;
                a.vel *= Mathf.Exp(-damping * dt);
            }
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Drop a = list[i];
                a.pos += a.vel * dt;
                if (a.melting && (a.scale -= dt / meltTime) <= 0f) list.RemoveAt(i);
            }
            // At first, the pull ramps up while gathering
            if (age < formTime)
                foreach (Drop d in list) d.pos *= Mathf.Lerp(1f, 0.92f, age / formTime);
        }

        // Metaballs: sum droplet influence per pixel and paint it past a threshold. Nearby droplets join with a neck.
        // Threshold makes a lone droplet show at its own radius. Color comes from the pixel's distance to the center
        void DrawMass(Vector2 c, Vector2 stretch)
        {
            float threshold = Mathf.Pow(1f - 1f / (merge * merge), 2f);
            float ext = 0f;
            foreach (Drop d in list) ext = Mathf.Max(ext, d.pos.magnitude + d.size * merge);
            int e = Mathf.CeilToInt(ext * (1f + squash));
            Vector2Int origin = Vector2Int.FloorToInt(c);
            for (int y = -e; y <= e; y++)
            for (int x = -e; x <= e; x++)
            {
                Vector2 q = (Vector2)(origin + new Vector2Int(x, y)) + Vector2.one * 0.5f - c;
                float core = 1f - q.sqrMagnitude / Mathf.Max(1f, size * size * 0.5f);
                float field = core > 0f ? core * core : 0f;
                foreach (Drop d in list)
                {
                    float r = d.size * d.scale * 0.5f * merge;
                    if (r <= 0.2f) continue;
                    float k = 1f - (q - new Vector2(d.pos.x * stretch.x, d.pos.y * stretch.y)).sqrMagnitude / (r * r);
                    if (k > 0f) field += k * k;
                }
                if (field < threshold) continue;
                float t = q.magnitude / Mathf.Max(1f, size);
                t += (Mathf.PerlinNoise(q.x * 0.35f + seed, q.y * 0.35f + age * 1.5f) - 0.5f) * 0.12f;
                int tone = palette.Length - 1;
                for (int b = 0; b < bands.Length; b++)
                    if (t >= bands[b]) tone = palette.Length - 2 - b;
                canvas.Pixel(origin + new Vector2Int(x, y), Tone(tone));
            }
        }

        // 1-pixel strand on a tilted orbit, shrinking until absorbed into the surface. front draws only the front half, otherwise only the back half
        void DrawStrands(Vector2 c, float r, bool front)
        {
            foreach (Strand s in strandList)
            {
                float k = s.age / s.life;
                Vector2Int? prev = null;
                for (int i = strandPoints - 1; i >= 0; i--)
                {
                    float t = k - i * strandGap;
                    if (t < 0f) continue;
                    float a = s.angle + t * strandTurns * Mathf.PI * 2f;
                    float orbit = r * Mathf.Lerp(strandReach, 0.9f, t * t);
                    float x = Mathf.Cos(a) * orbit, z = Mathf.Sin(a) * orbit;
                    if (z > 0f != front)
                    {
                        prev = null;
                        continue;
                    }
                    Vector2 p = new(x, z * s.tilt);
                    p = new Vector2(p.x * Mathf.Cos(s.roll) - p.y * Mathf.Sin(s.roll), p.x * Mathf.Sin(s.roll) + p.y * Mathf.Cos(s.roll));
                    Vector2Int pi = Vector2Int.FloorToInt(c + p);
                    float head = 1f - (float)i / strandPoints;
                    Color32 col = Tone(front ? (head > 0.6f ? 3 : 2) : 1);
                    PixelCanvas.Trace(prev ?? pi, pi, run);
                    foreach (Vector2Int q in run) canvas.Pixel(q, col);
                    prev = pi;
                }
            }
        }
    }
}
