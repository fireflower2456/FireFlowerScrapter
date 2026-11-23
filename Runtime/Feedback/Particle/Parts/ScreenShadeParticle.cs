using UnityEngine;
using UnityEngine.Rendering;

namespace FireFlower
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ScreenShadeParticle : ParticleBase
    {
        [SerializeField, Range(0f, 1f)] float darkness = 0.65f;
        [SerializeField, Min(0.01f)] float fadeIn = 0.15f;
        [SerializeField, Min(0.01f)] float fadeOut = 0.15f;

        SpriteRenderer sr;
        float blend;
        float blendVelocity;
        bool active;

        void Awake() => sr = GetComponent<SpriteRenderer>();

        void OnEnable() => RenderPipelineManager.beginCameraRendering += FitToCamera;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= FitToCamera;

        protected override void OnInit()
        {
            active = false;
            blend = 0f;
            blendVelocity = 0f;
            SetAlpha(0f);
        }

        protected override void OnPlay() => active = true;

        public override bool Finish()
        {
            active = false;
            return false;
        }

        void Update()
        {
            blend = Mathf.SmoothDamp(blend, active ? 1f : 0f, ref blendVelocity,
                Mathf.Max(0.01f, active ? fadeIn : fadeOut), Mathf.Infinity, Time.unscaledDeltaTime);
            SetAlpha(blend);

            if (!active && blend <= 0.001f) Done();
        }

        void SetAlpha(float amount) => sr.color = new Color(0f, 0f, 0f, darkness * amount);

        void FitToCamera(ScriptableRenderContext context, Camera output)
        {
            if (output != Camera.main) return;

            float distance = output.nearClipPlane + 1f;
            float height = output.orthographic
                ? 2f * output.orthographicSize
                : 2f * distance * Mathf.Tan(output.fieldOfView * Mathf.Deg2Rad * 0.5f);

            transform.SetPositionAndRotation(
                output.transform.position + output.transform.forward * distance,
                output.transform.rotation);
            transform.localScale = new Vector3(height * output.aspect * 1.02f, height * 1.02f, 1f);
        }
    }
}
