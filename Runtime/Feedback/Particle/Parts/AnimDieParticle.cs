using System.Collections;
using UnityEngine;

namespace FireFlower
{
    [RequireComponent(typeof(Animator))]
    public class AnimDieParticle : ParticleBase
    {
        [SerializeField] string AnimName;
        Animator animator;

        void Awake()
        {
            animator = GetComponent<Animator>();
        }

        protected override void OnPlay() => StartCoroutine(AnimDie());

        IEnumerator AnimDie()
        {
            animator.Play(AnimName, 0, 0f);
            yield return null;
            while (animator.GetCurrentAnimatorStateInfo(0).IsName(AnimName) &&
                   animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                yield return null;
            Stop();
        }
    }
}
