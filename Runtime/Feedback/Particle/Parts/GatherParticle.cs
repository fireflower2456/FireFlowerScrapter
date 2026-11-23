using System.Collections.Generic;
using UnityEngine;
using FireFlower;

namespace FireFlower
{
    // Light motes are drawn from around the target into its body center. They grow in number and speed over rampTime.
    // On stop, even motes in flight vanish at once — so the moment of full charge reads as a clean cut
    public class GatherParticle : ParticleBase
    {
        class Mote
        {
            public Vector2 pos;
            public float swirl;
        }

        [SerializeField, Tooltip("Child sprite used as the canvas")] SpriteRenderer body;
        [SerializeField, Min(1)] int pixelsPerUnit = 16;
        [SerializeField, Tooltip("Far → near")] Color[] palette =
        {
            new(0.25f, 0.45f, 1f), new(0.55f, 0.8f, 1f), new(1f, 1f, 1f),
        };
        [SerializeField, Tooltip("Spawn distance (pixels)")] Vector2 spawnRadius = new(14f, 22f);
        [SerializeField, Tooltip("Counts as absorbed within this distance (pixels)")] float arriveRadius = 3f;
        [SerializeField, Tooltip("Spawns per second. Start → after rampTime")] Vector2 rate = new(15f, 60f);
        [SerializeField, Tooltip("Inward speed (pixels/s). Start → after rampTime")] Vector2 speed = new(40f, 110f);
        [SerializeField, Min(0.01f), Tooltip("Match the user's charge time")] float rampTime = 0.4f;
        [SerializeField, Tooltip("Swirl strength (pixels/s)")] float swirl = 20f;
        [SerializeField, Tooltip("Trail length (seconds of velocity)")] float trail = 0.03f;
        [SerializeField, Tooltip("Gathers at full speed even during slow motion")] bool ignoreSlowMo = true;

        PixelCanvas canvas;
        readonly List<Mote> motes = new();
        float age, spawnAcc;
        bool live;

        void Awake() => canvas = new PixelCanvas(body, pixelsPerUnit);

        protected override void OnInit()
        {
            age = spawnAcc = 0f;
            motes.Clear();
            live = true;
            canvas.Hide();
        }

        public override bool Finish()
        {
            live = false;
            motes.Clear();
            canvas.Hide();
            return true;
        }

        void LateUpdate()
        {
            if (!live || !TimeManager.Instance.IsRunning) return;
            float dt = ignoreSlowMo ? Time.unscaledDeltaTime : Time.deltaTime;
            age += dt;
            float k = Mathf.Clamp01(age / rampTime);
            float v = Mathf.Lerp(speed.x, speed.y, k);

            spawnAcc += dt * Mathf.Lerp(rate.x, rate.y, k);
            for (; spawnAcc >= 1f; spawnAcc--)
                motes.Add(new Mote
                {
                    pos = Random.insideUnitCircle.normalized * Random.Range(spawnRadius.x, spawnRadius.y),
                    swirl = Random.value < 0.5f ? -swirl : swirl,
                });

            SpriteRenderer sr = FindTargetSprite();
            Vector2 center = sr != null ? (Vector2)sr.bounds.center : (Vector2)transform.position;
            float reach = (spawnRadius.y + 2f) / pixelsPerUnit;
            canvas.Begin(center - Vector2.one * reach, center + Vector2.one * reach, 1);

            for (int i = motes.Count - 1; i >= 0; i--)
            {
                Mote m = motes[i];
                float dist = m.pos.magnitude;
                Vector2 inward = -m.pos / Mathf.Max(dist, 0.001f);
                Vector2 side = new(-inward.y, inward.x);
                m.pos += (inward * v + side * (m.swirl * dist / spawnRadius.y)) * dt;

                dist = m.pos.magnitude;
                if (dist <= arriveRadius)
                {
                    motes.RemoveAt(i);
                    continue;
                }
                float near = 1f - Mathf.Clamp01((dist - arriveRadius) / (spawnRadius.y - arriveRadius));
                Color32 c = palette[Mathf.Min(palette.Length - 1, (int)(near * palette.Length))];
                Vector2 head = center + m.pos / pixelsPerUnit;
                Vector2 tail = head - inward * (v * trail / pixelsPerUnit);
                canvas.Line(tail, head, 0, c);
            }
            canvas.End();
        }
    }
}
