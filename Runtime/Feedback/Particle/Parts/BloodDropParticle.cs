using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // Pixel blood drops. They burst out from where they are placed, slow down and hesitate, then accelerate into Target and splash slightly on contact.
    // The faster they go, the more they stretch along their trail (bending with it on curves), and they flatten when homing starts (squash).
    // With homing off they splash, fall, and fade out. With repeat on they spray again every interval until stopped.
    // If absorbable, spraying is announced via Emitted, and a receiver (e.g. a blood sac) can pull the drops toward itself with Redirect.
    // One-shot drops detach from the parent when sprayed (they survive the mob's death). Repeating drops follow the parent
    public class BloodDropParticle : ParticleBase
    {
        class Drop
        {
            public SpriteRenderer[] body;
            public SpriteRenderer[] tail;
            // Past positions; the last one is the current position. The stretched body is laid along these
            public readonly List<Vector2> path = new();
            public float pathMax;
            public Vector2 pos, vel, dir = Vector2.up;
            public float age, homeAt, life, squashAge = -1f;
            public int size;
            // Body color (index into colors). Shade is one step darker
            public int tone;
            public bool homing;
            // After the head arrives it stays stuck there and shrinks until the whole body has been absorbed
            public bool arrived;
            public float bodyLeft;
            // Share reported to ParticleCore on arrival. 0 for splash and repeat drops
            public float share;
            // Alternate destination received via Redirect. If it disappears, the drop disappears too
            public Transform home;
            public bool hasHome;
            public Action<float> arrive;
        }

        [Tooltip("Sprite to borrow material and sorting from. Keep it disabled")]
        [SerializeField] SpriteRenderer template;
        [SerializeField, Min(1)] int pixelsPerUnit = 16;
        [SerializeField, Tooltip("0: dark blood, 1: blood, 2: light blood")] Color[] colors =
        {
            new(0.36f, 0.02f, 0.06f), new(0.72f, 0.05f, 0.09f), new(1f, 0.32f, 0.3f),
        };
        [SerializeField, Tooltip("Shading lit from the top left. Bottom-right edge is shaded; 4 pixels or larger gets one highlight pixel")] bool shade;
        [SerializeField, Range(0f, 1f), Tooltip("How close the highlight is to white")] float highlight = 0.55f;

        public static event Action<BloodDropParticle> Emitted;

        [Header("Spray")]
        [SerializeField] Vector2Int count = new(4, 6);
        [SerializeField, Min(0f), Tooltip("One batch of count per this much Amount. 0 means one batch regardless of Amount")] float amountPerGroup;
        [SerializeField, Min(1)] int maxGroups = 10;
        [SerializeField, Tooltip("If on, spraying is announced so an absorber can take the drops")] bool absorbable;
        [SerializeField] bool loop;
        [SerializeField, Min(0.01f)] float interval = 0.08f;
        [SerializeField] Vector2 speed = new(3f, 5f);
        [SerializeField, Range(0f, 360f)] float spread = 120f;
        [SerializeField, Tooltip("If on, drops fan out evenly within spread")] bool fan;
        [SerializeField, Range(-1f, 1f), Tooltip("1 splashes toward Target, -1 away from it")] float aim;
        [SerializeField, Tooltip("Upward splash strength")] float lift = 1f;
        [SerializeField] float gravity = 10f;
        [SerializeField, Min(0f), Tooltip("Deceleration before homing. Higher stops sooner")] float drag;
        [SerializeField] Vector2Int sizePx = new(2, 3);

        [Header("Homing")]
        [SerializeField] bool home = true;
        [SerializeField, Tooltip("Delay from splash to homing. Each drop picks a value in this range")] Vector2 homeDelay = new(0.12f, 0.3f);
        [SerializeField] float accel = 300f;
        [SerializeField, Min(0f), Tooltip("Time after homing starts to reach full acceleration. 0 is instant")] float accelRamp;
        [SerializeField] float maxSpeed = 14f;
        [SerializeField, Min(0.01f)] float arriveRadius = 0.15f;

        [Header("Stretch & Squash")]
        [SerializeField, Min(0f), Tooltip("Maximum length multiplier minus 1")] float stretch = 8f;
        [SerializeField, Min(0.01f), Tooltip("Fully stretched at this speed")] float stretchSpeed = 15f;
        [SerializeField, Range(0.2f, 1f), Tooltip("Flattening along the travel direction when homing starts")] float squash = 0.5f;
        [SerializeField, Min(0.01f)] float squashTime = 0.08f;

        [Header("Fade (non-homing drops)")]
        [SerializeField] Vector2 life = new(0.3f, 0.45f);

        [Header("Tail")]
        [SerializeField, Min(0)] int tailPx = 3;
        [SerializeField, Tooltip("Tail is full length at this speed")] float tailFullSpeed = 12f;

        [Header("Splash on Contact")]
        [SerializeField] Vector2Int splashCount = new(2, 3);
        [SerializeField] Vector2 splashSpeed = new(1.5f, 3f);
        [SerializeField] Vector2 splashLife = new(0.15f, 0.25f);
        [SerializeField] Vector2Int splashSizePx = new(1, 2);

        readonly List<Drop> live = new();
        readonly Stack<SpriteRenderer> pool = new();
        Vector2 lastTarget;
        float timer;
        bool emitting, stopping;

        float Px => 1f / pixelsPerUnit;

        void Awake() => template.gameObject.SetActive(false);

        // With shading on, bake in the body color (tone); otherwise tint a white shape with the SpriteRenderer color
        Sprite Shape(int w, int h, int tone = -1)
        {
            if (!shade || tone < 0) return BloodPixels.Ellipse(w, h, pixelsPerUnit, Color.white, Color.white, Color.white);
            Color body = colors[tone];
            return BloodPixels.Ellipse(w, h, pixelsPerUnit, body, colors[Mathf.Max(0, tone - 1)], Color.Lerp(body, Color.white, highlight));
        }

        protected override void OnInit()
        {
            foreach (Drop d in live) Release(d);
            live.Clear();
            emitting = stopping = false;
            timer = 0f;
            lastTarget = transform.position;
        }

        protected override void OnPlay()
        {
            if (!loop) transform.SetParent(null, true);
            emitting = true;
            Emit();
            if (!loop) emitting = false;
        }

        public override bool Finish()
        {
            stopping = true;
            emitting = false;
            return live.Count == 0;
        }

        Vector2 TargetPos
        {
            get
            {
                if (Target == null) return lastTarget;
                SpriteRenderer sr = FindTargetSprite();
                lastTarget = sr != null ? sr.bounds.center : Target.transform.position;
                return lastTarget;
            }
        }

        void Update() => Tick(Time.deltaTime);

        void Tick(float dt)
        {
            if (emitting)
            {
                timer += dt;
                if (timer >= interval)
                {
                    timer -= interval;
                    Emit();
                }
            }

            Vector2 target = TargetPos;
            for (int i = live.Count - 1; i >= 0; i--)
                if (!Move(live[i], target, dt))
                {
                    Release(live[i]);
                    live.RemoveAt(i);
                }

            if (emitting || live.Count > 0) return;
            if (stopping) Done();
            else Stop();
        }

        void Emit()
        {
            Vector2 from = transform.position;
            Vector2 toTarget = (TargetPos - from).normalized;
            Vector2 baseDir = toTarget * aim + Vector2.up * lift;
            if (baseDir.sqrMagnitude < 0.0001f) baseDir = Vector2.up;
            baseDir.Normalize();

            int groups = amountPerGroup > 0f ? Mathf.Clamp(Mathf.CeilToInt(Core.Amount / amountPerGroup), 1, maxGroups) : 1;
            int n = 0;
            for (int g = 0; g < groups; g++) n += UnityEngine.Random.Range(count.x, count.y + 1);
            for (int i = 0; i < n; i++)
            {
                float angle = fan && n > 1 ? Mathf.Lerp(-spread, spread, (float)i / (n - 1)) * 0.5f + UnityEngine.Random.Range(-8f, 8f) : UnityEngine.Random.Range(-spread, spread) * 0.5f;
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;
                // Mostly small, occasionally thick drops
                int size = Mathf.RoundToInt(Mathf.Lerp(sizePx.x, sizePx.y, UnityEngine.Random.value * UnityEngine.Random.value));
                Drop d = Spawn(from, dir * UnityEngine.Random.Range(speed.x, speed.y), size, home,
                    UnityEngine.Random.Range(homeDelay.x, homeDelay.y), UnityEngine.Random.Range(life.x, life.y));
                if (home && !loop) d.share = 1f / n;
            }
            if (absorbable && !loop) Emitted?.Invoke(this);
        }

        // The sprayer (mob)
        public GameObject From => Target;

        // Distributes drops not yet homing evenly across homes. Each arriving drop reports its share (of a total of 1)
        public void Redirect(IReadOnlyList<Transform> homes, Action<float> onArrive)
        {
            var free = live.FindAll(d => !d.homing);
            if (homes.Count == 0 || free.Count == 0) return;
            for (int i = 0; i < free.Count; i++)
            {
                Drop d = free[i];
                d.homing = d.hasHome = true;
                d.home = homes[i % homes.Count];
                d.homeAt = UnityEngine.Random.Range(homeDelay.x, homeDelay.y);
                d.share = 1f / free.Count;
                d.arrive = onArrive;
            }
        }

        Drop Spawn(Vector2 at, Vector2 vel, int size, bool homing, float homeAt, float lifeTime)
        {
            var d = new Drop
            {
                pos = at, vel = vel, size = size, homing = homing, homeAt = homeAt, life = lifeTime,
                body = new SpriteRenderer[Mathf.CeilToInt(size * (1f + stretch))],
                tail = new SpriteRenderer[tailPx],
                pathMax = (size * (1f + stretch) + tailPx + 2) * Px,
            };
            d.path.Add(at);
            d.tone = UnityEngine.Random.value < 0.25f ? 2 : 1;
            Color c = shade ? Color.white : colors[d.tone];
            for (int i = 0; i < d.body.Length; i++) d.body[i] = Get(c);
            for (int i = 0; i < tailPx; i++) d.tail[i] = Get(colors[0]);
            live.Add(d);
            Draw(d, 1f);
            return d;
        }

        // true while alive
        bool Move(Drop d, Vector2 target, float dt)
        {
            if (d.hasHome)
            {
                if (d.home == null) return false;
                target = d.home.position;
            }
            d.age += dt;
            if (d.arrived)
            {
                d.bodyLeft -= d.vel.magnitude * dt;
                if (d.bodyLeft <= 0f) return false;
                d.pos = target;
                Record(d);
                Draw(d, 1f);
                return true;
            }
            if (d.homing && d.age >= d.homeAt)
            {
                Vector2 to = target - d.pos;
                if (to.magnitude <= Mathf.Max(arriveRadius, d.vel.magnitude * dt))
                {
                    Splash(target);
                    if (d.share > 0f)
                    {
                        if (d.arrive != null) d.arrive(d.share);
                        else Core.Arrive(d.share);
                    }
                    d.arrived = true;
                    d.bodyLeft = BodyLength(d, 1f) * Px;
                    return true;
                }
                if (d.squashAge < 0f) d.squashAge = 0f;
                d.squashAge += dt;
                float ramp = accelRamp > 0f ? Mathf.Lerp(0.1f, 1f, d.squashAge / accelRamp) : 1f;
                d.vel = Vector2.MoveTowards(d.vel, to.normalized * maxSpeed, accel * ramp * dt);
            }
            else
            {
                d.vel.y -= gravity * dt;
                d.vel *= Mathf.Exp(-drag * dt);
                if (!d.homing && d.age >= d.life) return false;
            }
            if (d.homing && d.age > d.homeAt + 3f) return false;

            d.pos += d.vel * dt;
            Record(d);
            Draw(d, d.homing ? 1f : 1f - d.age / d.life);
            return true;
        }

        // Keep only the body length; drop older positions
        static void Record(Drop d)
        {
            d.path.Add(d.pos);
            float total = 0f;
            for (int i = d.path.Count - 1; i > 0; i--)
            {
                total += (d.path[i] - d.path[i - 1]).magnitude;
                if (total <= d.pathMax) continue;
                d.path.RemoveRange(0, i - 1);
                return;
            }
        }

        // Point dist behind the head (current position) along the trail. If history runs out, extend opposite the travel direction
        static Vector2 Behind(Drop d, float dist)
        {
            for (int i = d.path.Count - 1; i > 0; i--)
            {
                Vector2 seg = d.path[i - 1] - d.path[i];
                float m = seg.magnitude;
                if (m < 0.0001f) continue;
                if (dist <= m) return d.path[i] + seg / m * dist;
                dist -= m;
            }
            return d.path[0] - d.dir * dist;
        }

        void Splash(Vector2 at)
        {
            int n = UnityEngine.Random.Range(splashCount.x, splashCount.y + 1);
            for (int i = 0; i < n; i++)
            {
                Vector2 v = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(splashSpeed.x, splashSpeed.y);
                Spawn(at, v + Vector2.up, UnityEngine.Random.Range(splashSizePx.x, splashSizePx.y + 1), false, 0f, UnityEngine.Random.Range(splashLife.x, splashLife.y));
            }
        }

        // When stretched, overlapping pixel circles are placed from the head along the trail to form an elongated ellipse.
        // Volume is roughly preserved (length × thickness ≈ size²). When fading it shrinks in whole pixels
        float Stretch(Drop d)
        {
            float k = 1f + stretch * Mathf.Clamp01(d.vel.magnitude / stretchSpeed);
            if (d.squashAge >= 0f && d.squashAge < squashTime)
                k = Mathf.Lerp(squash, k, d.squashAge / squashTime);
            return k;
        }

        // In pixels
        float BodyLength(Drop d, float remain) => Mathf.Max(1f, d.size * Mathf.Clamp01(remain)) * Stretch(d);

        void Draw(Drop d, float remain)
        {
            if (!d.arrived && d.vel.sqrMagnitude > 0.0001f) d.dir = d.vel.normalized;
            float size = Mathf.Max(1f, d.size * Mathf.Clamp01(remain));
            float k = Stretch(d);
            float len = size * k;
            if (d.arrived) len = Mathf.Min(len, d.bodyLeft / Px);
            // Middle thickness, slightly thicker to make up for the tapered ends
            int w = Mathf.Max(1, Mathf.RoundToInt(size * 1.25f / Mathf.Pow(k, 0.4f)));
            foreach (SpriteRenderer sr in d.body) sr.enabled = false;

            // Barely stretched: one circle (shrinking to the remaining length while being absorbed). Squashed: one ellipse shortened along the travel axis
            if (len <= size + 0.5f)
            {
                d.body[0].enabled = true;
                int along = Mathf.Max(1, Mathf.RoundToInt(len));
                int across = k < 1f ? Mathf.Max(1, Mathf.RoundToInt(size / Mathf.Sqrt(k))) : along;
                bool horizontal = Mathf.Abs(d.dir.x) > Mathf.Abs(d.dir.y);
                Place(d.body[0], d.pos, horizontal ? along : across, horizontal ? across : along, d.tone);
            }
            else
            {
                // Stretched ellipse: thickest in the middle, tapering to 1 pixel at both ends.
                // In the thick part, sparsely overlapped circles still look continuous
                float step = Mathf.Max(1f, w / 3f);
                int n = Mathf.Clamp(Mathf.FloorToInt(len / step), 1, d.body.Length);
                for (int i = 0; i < n; i++)
                {
                    float along = (i + 0.5f) * len / n;
                    float t = along / len * 2f - 1f;
                    int wi = Mathf.Max(1, Mathf.RoundToInt(w * Mathf.Sqrt(1f - t * t)));
                    d.body[i].enabled = true;
                    Place(d.body[i], Behind(d, along * Px), wi, wi, d.tone);
                }
            }

            float speedK = Mathf.Clamp01(d.vel.magnitude / tailFullSpeed);
            int shown = d.arrived ? 0 : Mathf.RoundToInt(d.tail.Length * speedK);
            float start = Mathf.Max(len, w) + 0.5f;
            for (int i = 0; i < d.tail.Length; i++)
            {
                bool on = i < shown;
                d.tail[i].enabled = on;
                if (on) Place(d.tail[i], Behind(d, (start + i) * Px), 1, 1);
            }
        }

        void Place(SpriteRenderer sr, Vector2 center, int w, int h, int tone = -1)
        {
            float px = Px;
            Vector2 corner = center / px - new Vector2(w, h) * 0.5f;
            corner = new Vector2(Mathf.Round(corner.x), Mathf.Round(corner.y));
            sr.transform.position = new Vector3((corner.x + w * 0.5f) * px, (corner.y + h * 0.5f) * px, transform.position.z);
            sr.sprite = Shape(w, h, tone);
            sr.transform.localScale = Vector3.one / LossyParent;
        }

        float LossyParent => Mathf.Max(0.0001f, transform.lossyScale.x);

        SpriteRenderer Get(Color c)
        {
            SpriteRenderer sr = pool.Count > 0 ? pool.Pop() : Instantiate(template, transform);
            sr.color = c;
            sr.enabled = true;
            sr.gameObject.SetActive(true);
            return sr;
        }

        void Release(Drop d)
        {
            foreach (SpriteRenderer b in d.body) Put(b);
            foreach (SpriteRenderer t in d.tail) Put(t);
        }

        void Put(SpriteRenderer sr)
        {
            sr.gameObject.SetActive(false);
            pool.Push(sr);
        }
    }
}
