using System;
using UnityEngine;

namespace FireFlower
{
    // Flashes the whole screen in one color, then clears it. Alpha is the starting intensity
    [Serializable] public class ScreenFlashFeedback : FeedbackBase
    {
        [SerializeField] Color color = new(1f, 1f, 1f, 0.3f);
        [SerializeField, Min(0f)] float duration = 0.2f;

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
        {
            if (duration > 0f) VFXManager.Instance.FlashScreen(color, duration);
            return null;
        }
    }
}
