using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace FireFlower
{
    // A single mob behavior. When to enter (condition, priority) is set by the AI state (mob data); this only holds what to do once entered
    [Serializable]
    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "MobBaseState")]
    public abstract class ComposableState : EntityState
    {
        protected new ComposableController owner => (ComposableController)base.owner;
        // Can't enter, even if the condition holds, when the behavior isn't ready (target, body part, etc.)
        public virtual bool CanStart => true;
        // When on, stays while CanContinue regardless of the condition (for behaviors with an end, like attacks or wandering)
        public virtual bool HoldsItself => false;
        public virtual bool CanContinue => true;

        public override void Init(EntityController controller)
        {
            if (controller is not ComposableController)
                throw new ArgumentException("ComposableState requires a ComposableController.", nameof(controller));
            base.Init(controller);
        }
    }
}
