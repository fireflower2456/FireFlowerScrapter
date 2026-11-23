using System;
using UnityEngine;

namespace FireFlower
{
    [Serializable] public class WhiteFlashFeedback : FeedbackBase
    {
        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
        {
            if (target == null) return null;
            SpriteRenderer sr = FeedbackTarget.MainSprite(target, false);
            VFXManager.Instance.FlashWhite(sr, realTime: true);
            return null;
        }
    }
}
