using UnityEngine;

namespace FireFlower
{
    [RequireComponent(typeof(ParticleCore))]
    public abstract class ParticleBase : MonoBehaviour
    {
        protected GameObject Target { get; private set; }

        protected ParticleCore Core { get; private set; }

        public void Bind(ParticleCore owner) => Core = owner;

        public void Init(GameObject target)
        {
            Target = target;
            StopAllCoroutines();
            OnInit();
        }

        public void Play() => OnPlay();

        public virtual bool Finish() => true;

        protected virtual void OnInit() { }
        protected virtual void OnPlay() { }

        protected void Stop() => Core.Stop();

        protected void Done() => Core.Done();

        protected SpriteRenderer FindTargetSprite()
        {
            if (Target == null)
                return transform.parent != null ? transform.parent.GetComponent<SpriteRenderer>() : null;

            return FeedbackTarget.MainSprite(Target, true);
        }
    }
}
