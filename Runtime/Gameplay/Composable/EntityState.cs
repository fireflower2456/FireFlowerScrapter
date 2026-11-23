using System;
using UnityEngine;

namespace FireFlower
{
    // State execution base shared by the direct-transition and composable kinds. Holds no selection condition.
    [Serializable]
    public abstract class EntityState : BaseState
    {
        protected EntityController owner;
        protected virtual string AnimName => null;
        public virtual float Tempo => owner.BehaviorSpd;
        protected float Dt => Time.fixedDeltaTime * Mathf.Max(0f, Tempo);

        public virtual void Init(EntityController controller) => owner = controller;

        public override void Enter()
        {
            if (AnimName != null && owner.Anim != null) owner.Anim.Play(AnimName);
        }
    }
}
