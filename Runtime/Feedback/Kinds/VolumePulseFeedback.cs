using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireFlower
{
    // Briefly overlays a post-processing profile on the whole screen, then clears it. Intensity follows a curve
    [Serializable] public class VolumePulseFeedback : FeedbackBase
    {
        [SerializeField] VolumeProfile profile;
        [SerializeField, Min(0.01f)] float duration = 0.3f;
        [Tooltip("X 0-1 is elapsed fraction, Y is profile intensity")]
        [SerializeField] AnimationCurve weight = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] float priority = 100f;

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
        {
            if (profile != null) VFXManager.Instance.PulseVolume(profile, duration, weight, priority);
            return null;
        }
    }
}
