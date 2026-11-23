using System;
using UnityEngine;

namespace FireFlower
{
    /// <summary>A motion called inside a state. Pre- and post-attack motions share the same contract.</summary>
    [Serializable]
    public abstract class StateMotion
    {
        [Tooltip("Show an attack warning above the head on start")]
        [SerializeField] bool attackWarn = true;
        protected EntityController owner;
        public virtual void Init(EntityController controller) => owner = controller;
        public virtual void Enter() { }
        // Recovery motions branch on how the attack ended. Windup motions ignore it
        public virtual void Enter(AttackResult result)
        {
            if (attackWarn && owner != null && owner.Owner != null)
            {
                Transform mob = owner.Owner.transform;
                VFXManager.Instance.PlayParticle("AttackWarnHead", mob.position, mob, mob.gameObject);
            }
            Enter();
        }
        // Returns leftover time so none is lost when the phase changes within one physics tick.
        public abstract float Tick(float dt);
        public abstract bool IsComplete { get; }
        public virtual bool Succeeded => true;
        // Called on both normal completion and forced interruption. Clean up changed visuals, subscriptions, etc. here.
        public virtual void Exit() { }
    }

    [ScrapWeaver.Display("wait", "duration", "s")]
    [Serializable]
    public class WaitMotion : StateMotion
    {
        [SerializeField, Min(0f)] float duration = 0.3f;
        float remaining;
        public override bool IsComplete => remaining <= 0f;
        public override void Enter() => remaining = Mathf.Max(0f, duration);
        public override float Tick(float dt)
        {
            float used = Mathf.Min(remaining, Mathf.Max(0f, dt));
            remaining -= used;
            return dt - used;
        }
    }
}
