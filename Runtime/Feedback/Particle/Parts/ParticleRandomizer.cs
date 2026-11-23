using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace FireFlower
{
    public enum ParticleScatter
    {
        None = 0,
        Offset = 1,
        SpriteBounds = 2,
    }

    public class ParticleRandomizer : ParticleBase
    {
        [SerializeField] bool UseSpriteRandomize;
        [Tooltip("Leave empty to keep the sprite unchanged")]
        [Header("SpriteRandomize")]
        [ShowIf("UseSpriteRandomize")]
        [SerializeField] List<Sprite> shapes = new();
        [SerializeField] bool UsePositionRandomize;
        [Header("PositionRandomize")]
        [ShowIf("UsePositionRandomize")]
        [SerializeField] ParticleScatter scatter = ParticleScatter.None;
        public bool ScatterOffset => scatter == ParticleScatter.Offset;
        public bool ScatterSpr => scatter == ParticleScatter.SpriteBounds;
        [Header("Relay on Offset")]
        [ShowIf(EConditionOperator.And, "ScatterOffset", "UsePositionRandomize")]
        [Tooltip("Side length of the jitter square. Picks within ±range/2")]
        [SerializeField] float range = 1f;
        [Header("Relay on SpriteRate")]
        [ShowIf(EConditionOperator.And, "ScatterSpr", "UsePositionRandomize")]
        [Tooltip("Fraction of the target size to use. 1 reaches the edge of the body")]
        [SerializeField] float srRate = 0.8f;

        SpriteRenderer sr;

        void Awake() => sr = GetComponent<SpriteRenderer>();

        protected override void OnInit()
        {
            PickSprite();
            Scatter();
        }

        void PickSprite()
        {
            if (sr == null || shapes.Count == 0 || !UseSpriteRandomize) return;
            sr.sprite = shapes[Random.Range(0, shapes.Count)];
        }

        void Scatter()
        {
            if (!UsePositionRandomize) return;
            switch (scatter)
            {
                case ParticleScatter.Offset:
                    transform.position += Offset(range, range);
                    break;

                case ParticleScatter.SpriteBounds:
                    SpriteRenderer target = FindTargetSprite();
                    if (target == null) return;

                    Vector2 size = target.bounds.size * srRate;
                    transform.position += Offset(size.x, size.y);
                    break;
            }
        }

        static Vector3 Offset(float width, float height)
            => Random.Range(-width / 2f, width / 2f) * Vector3.right
             + Random.Range(-height / 2f, height / 2f) * Vector3.up;
    }
}
