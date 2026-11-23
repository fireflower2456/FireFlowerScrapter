using System;
using UnityEngine;

namespace FireFlower
{
    // Blinks the target white until stopped. Each blink overlays white by strength and reverts after flash seconds
    [Serializable] public class WhiteBlinkFeedback : FeedbackBase
    {
        [SerializeField, Min(0.02f), Tooltip("Blink interval (real-time seconds)")] float interval = 0.16f;
        [SerializeField, Min(0.01f), Tooltip("Duration of one blink (real-time seconds)")] float flash = 0.08f;
        [SerializeField, Range(0f, 1f), Tooltip("White overlay strength")] float strength = 0.5f;

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
        {
            if (target == null) return null;
            SpriteRenderer sr = FeedbackTarget.MainSprite(target, false);
            return VFXManager.Instance.BlinkWhite(sr, interval, flash, strength);
        }
    }
}
