using System;
using UnityEngine;

namespace FireFlower
{
    [Serializable] public class ParticleFeedback : FeedbackBase
    {
        [Tooltip("Particle name registered in ParticleCatalog")]
        [SerializeField] string particle;
        [Tooltip("When on, attaches to and follows the target")]
        [SerializeField] bool follow;

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
            => Play(position, target, parent, Vector2.zero);

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent, Vector2 direction)
        {
            if (string.IsNullOrEmpty(particle)) return null;
            return VFXManager.Instance.PlayParticle(particle, position, parent == null && follow && target != null ? target.transform : parent, target, direction);
        }
    }
}
