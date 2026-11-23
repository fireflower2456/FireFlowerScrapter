using System;
using UnityEngine;

namespace FireFlower
{
    [Serializable] public class SoundFeedback : FeedbackBase
    {
        [SerializeField] string sound;

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
            => SoundManager.Instance.Play(sound, position);
    }
}
