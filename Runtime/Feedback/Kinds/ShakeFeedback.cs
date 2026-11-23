using System;
using UnityEngine;

namespace FireFlower
{
    [Serializable] public class ShakeFeedback : FeedbackBase
    {
        [SerializeField, Min(0f)] float duration = 0.1f;
        [SerializeField, Min(0f)] float strength = 1f;

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
        {
            if (duration <= 0f) return null;
            VFXManager.Instance.Shake(duration, strength);
            return null;
        }
    }
}
