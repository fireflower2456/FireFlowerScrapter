using System;
using UnityEngine;

namespace FireFlower
{
    // Plays an animation on the target's body. On a bullet's Spawn, the bullet itself is animated
    [Serializable] public class AnimationFeedback : FeedbackBase
    {
        [SerializeField] RuntimeAnimatorController controller;

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
        {
            if (target == null || controller == null) return null;

            if (!target.TryGetComponent(out FeedbackAnimator animator))
                animator = target.AddComponent<FeedbackAnimator>();

            animator.Play(controller);
            return new Handle(animator);
        }

        sealed class Handle : IFeedbackHandle
        {
            readonly FeedbackAnimator animator;

            public Handle(FeedbackAnimator animator) => this.animator = animator;

            public void Stop()
            {
                if (animator != null) animator.Stop();
            }
        }
    }
}
