using System;
using UnityEngine;
using FireFlower;

namespace FireFlower
{
    [Serializable] public class HitStopFeedback : FeedbackBase
    {
        [SerializeField, Min(0f)] float seconds = 0.05f;

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
        {
            if (seconds <= 0f) return null;
            TimeManager.Instance.HitStop(seconds);
            return null;
        }
    }
}
