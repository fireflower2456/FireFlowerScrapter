using System;
using UnityEngine;

namespace FireFlower
{
    // Changes camera damping until Stop. Higher means the camera follows more slowly
    [Serializable] public class CameraDampingFeedback : FeedbackBase
    {
        [SerializeField] Vector3 damping = new(1.5f, 2.5f, 1f);

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
            => VFXManager.Instance.HoldCameraDamping(damping);
    }
}
