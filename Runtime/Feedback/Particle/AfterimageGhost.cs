using UnityEngine;
using FireFlower;

namespace FireFlower
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class AfterimageGhost : MonoBehaviour
    {
        SpriteRenderer spr;
        Color tint;
        float startAlpha;
        float lifetime;
        float hold;
        float age;

        void Awake() => spr = GetComponent<SpriteRenderer>();

        public static AfterimageGhost Get() => ObjectPoolExtention.Get<AfterimageGhost>();

        public void Play(SpriteRenderer source, Material mt, Color tint, float startAlpha, float lifetime, int sortingOrderOffset, Vector3? position = null, bool? flipX = null, float hold = 0f)
        {
            Transform t = transform;
            t.position = position ?? source.transform.position;
            t.rotation = source.transform.rotation;
            t.localScale = source.transform.lossyScale;

            spr.sprite = source.sprite;
            if (mt != null && mt == source.sharedMaterial && VFXManager.Instance != null) mt = VFXManager.Instance.BaseMaterial(source);
            if (mt != null) spr.sharedMaterial = mt;
            spr.flipX = flipX ?? source.flipX;
            spr.sortingLayerID = source.sortingLayerID;
            spr.sortingOrder = source.sortingOrder + sortingOrderOffset;

            this.tint = tint;
            this.startAlpha = startAlpha;
            this.lifetime = lifetime;
            this.hold = hold;
            age = 0f;

            Color c = tint;
            c.a = startAlpha;
            spr.color = c;

            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.deltaTime;
            float fading = age - hold;
            if (fading >= lifetime)
            {
                ObjectPoolExtention.Release(gameObject);
                return;
            }
            Color c = tint;
            c.a = Mathf.Lerp(startAlpha, 0f, fading / lifetime);
            spr.color = c;
        }
    }
}
