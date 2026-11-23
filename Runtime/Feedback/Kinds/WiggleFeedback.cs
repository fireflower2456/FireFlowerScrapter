using System;
using UnityEngine;

namespace FireFlower
{
    // Shakes the target side to side in place, then restores it. Used to signal "not allowed"
    [Serializable] public class WiggleFeedback : FeedbackBase
    {
        [SerializeField, Min(0.01f)] float duration = 0.25f;
        [Tooltip("Initial shake amplitude. Decays over time")]
        [SerializeField, Min(0f)] float amplitude = 8f;
        [Tooltip("Number of back-and-forth swings")]
        [SerializeField, Min(1)] int swings = 3;
        [Tooltip("Shakes even when time is stopped, like paused UI")]
        [SerializeField] bool realtime = true;

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
        {
            if (target == null || !target.activeInHierarchy) return null;

            if (!target.TryGetComponent(out FeedbackWiggler wiggler))
                wiggler = target.AddComponent<FeedbackWiggler>();

            wiggler.Play(duration, amplitude, swings, realtime);
            return new Handle(wiggler);
        }

        sealed class Handle : IFeedbackHandle
        {
            readonly FeedbackWiggler wiggler;

            public Handle(FeedbackWiggler wiggler) => this.wiggler = wiggler;

            public void Stop()
            {
                if (wiggler != null) wiggler.Stop();
            }
        }
    }
}
