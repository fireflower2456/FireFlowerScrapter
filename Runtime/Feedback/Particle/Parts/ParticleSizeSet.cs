using UnityEngine;

namespace FireFlower
{
    public class ParticleSizeSet : ParticleBase
    {
        [SerializeField, Tooltip("If off, uses the prefab's original size regardless of target size")] bool fitTarget = true;
        [SerializeField] float multiplier = 1.5f;
        [SerializeField] bool scaleByDamage;
        [SerializeField, Tooltip("Maps this damage range onto the scale range below. Clamped outside the range")] Vector2 damageRange = new(0f, 50f);
        [SerializeField] Vector2 scaleRange = new(0.8f, 1.5f);

        SpriteRenderer sr;
        Vector3 baseScale;

        void Awake() => baseScale = transform.localScale;

        protected override void OnInit()
        {
            SpriteRenderer target = FindTargetSprite();
            if (fitTarget && target != null && target.sprite != null) Fit(target);
            else transform.localScale = baseScale * DamageScale();
        }

        public void Fit(SpriteRenderer target)
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();

            Vector2 size = Vector2.Scale(target.sprite.bounds.size, target.transform.lossyScale);
            float side = Mathf.Max(size.x, size.y) * multiplier * DamageScale();

            Vector2 own = sr.sprite.bounds.size;
            transform.localScale = new Vector3(side / own.x, side / own.y, 1f);
        }

        float DamageScale()
        {
            if (!scaleByDamage || Target == null || Target.GetComponentInParent<ILastDamage>()?.LastDamage is not float damage) return 1f;
            return Mathf.Lerp(scaleRange.x, scaleRange.y, Mathf.InverseLerp(damageRange.x, damageRange.y, damage));
        }
    }
}
