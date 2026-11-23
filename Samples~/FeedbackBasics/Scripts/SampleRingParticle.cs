using UnityEngine;
using FireFlower;

namespace FireFlower.Samples
{
    // A code-made ring sprite grows and fades. Minimal example of making a particle part (ParticleBase)
    [RequireComponent(typeof(SpriteRenderer))]
    public class SampleRingParticle : ParticleBase
    {
        [SerializeField, Min(0.01f)] float duration = 0.4f;
        [SerializeField] float startSize = 0.3f;
        [SerializeField] float endSize = 2.5f;
        [SerializeField] Color color = Color.white;

        static Sprite ring;
        SpriteRenderer sr;
        float elapsed;
        bool playing;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            sr.sprite = ring ??= MakeRing(64, 0.12f);
        }

        protected override void OnInit()
        {
            elapsed = 0f;
            Apply(0f);
        }

        protected override void OnPlay() => playing = true;

        void Update()
        {
            if (!playing) return;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Apply(t);
            if (t < 1f) return;
            playing = false;
            Stop();
        }

        void Apply(float t)
        {
            transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, 1f - (1f - t) * (1f - t));
            sr.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
        }

        static Sprite MakeRing(int size, float thickness)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                texture.SetPixel(x, y, d <= 1f && d >= 1f - thickness * 2f ? Color.white : Color.clear);
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
