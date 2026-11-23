using UnityEngine;

namespace FireFlower
{
    // Shockwave ring that spreads from the center to radius, pushing and distorting the background outward. Ends itself when fully spread
    [RequireComponent(typeof(SpriteRenderer))]
    public class ShockwaveParticle : ParticleBase
    {
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int WidthId = Shader.PropertyToID("_Width");
        static readonly int PushId = Shader.PropertyToID("_Push");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        [SerializeField, Min(0.01f)] float radius = 3f;     // World radius. Can be changed with SetRadius
        [SerializeField, Min(0.01f)] float duration = 0.45f;
        [SerializeField] float startWidth = 0.135f;           // Distortion band width (x radius). Thins to endWidth as it spreads
        [SerializeField] float endWidth = 0.04f;
        [SerializeField] float push = 0.11f;                  // Initial outward push (x radius). Falls to 0 as it spreads
        [SerializeField, Range(0f, 1f)] float flashTime = 0.2f;  // When the inner flash fades (fraction of total time)

        SpriteRenderer sr;
        MaterialPropertyBlock props;
        float elapsed;
        bool playing;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            props = new MaterialPropertyBlock();
        }

        protected override void OnInit()
        {
            elapsed = 0f;
            playing = false;
            SetRadius(radius);
            Apply(0f);
        }

        protected override void OnPlay() => playing = true;

        public void SetRadius(float worldRadius)
        {
            float spriteSize = sr.sprite.bounds.size.x;
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = Vector3.one * (worldRadius * 2f / (spriteSize * parentScale.x));
        }

        void Update()
        {
            if (!playing) return;
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            Apply(k);
            if (k >= 1f)
            {
                playing = false;
                Stop();
            }
        }

        void Apply(float k)
        {
            float spread = 1f - (1f - k) * (1f - k) * (1f - k);
            sr.GetPropertyBlock(props);
            props.SetFloat(RadiusId, Mathf.Lerp(0.015f, 1f, spread));
            props.SetFloat(WidthId, Mathf.Lerp(startWidth, endWidth, k));
            props.SetFloat(PushId, push * (1f - k));
            props.SetFloat(GlowId, Mathf.Pow(1f - k, 1.5f));
            props.SetFloat(FlashId, Mathf.Clamp01(1f - k / Mathf.Max(flashTime, 0.001f)));
            sr.SetPropertyBlock(props);
        }
    }
}
