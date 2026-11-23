using UnityEngine;

namespace FireFlower
{
    // Added by AnimationFeedback. Clears its controller when returned to the pool so it doesn't override static-image bullets that share the prefab
    [RequireComponent(typeof(Animator))]
    public class FeedbackAnimator : MonoBehaviour
    {
        Animator animator;

        void Awake() => animator = GetComponent<Animator>();

        public void Play(RuntimeAnimatorController controller) => animator.runtimeAnimatorController = controller;

        public void Stop() => animator.runtimeAnimatorController = null;

        void OnDisable() => Stop();
    }
}
