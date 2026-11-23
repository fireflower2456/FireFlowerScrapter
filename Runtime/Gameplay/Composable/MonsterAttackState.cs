using System;
using UnityEngine;
using FireFlower;

namespace FireFlower
{
    // How the attack ended. The finishing motion branches on this.
    public enum AttackResult
    {
        Ongoing,    // still in progress
        Completed,  // ended without hitting anything
        Hit,        // ended after hitting something. Target or terrain alike
    }

    [Serializable]
    public abstract class MonsterAttackState : ComposableState
    {
        public enum AttackPhase { None, Preparation, Execution, Recovery, Complete }
        [SerializeReference, SubclassSelector] StateMotion preparation;
        [SerializeReference, SubclassSelector] StateMotion recovery;
        public AttackPhase Phase { get; private set; }
        public AttackResult Result { get; private set; }
        public bool Done => Phase == AttackPhase.Complete;
        public override bool CanContinue => !Done;
        StateMotion activeMotion;
        protected FloatStat damage;
        protected Transform Target => owner.Blackboard.GetOrDefault<Transform>(BlackboardKeys.Monster.Target);

        public override void Init(EntityController controller)
        {
            base.Init(controller);
            damage = controller.Owner.Stat.EnsureFloat(MobStats.AtkPower, 1f);
            preparation?.Init(controller);
            recovery?.Init(controller);
            Phase = AttackPhase.None;
            Result = AttackResult.Ongoing;
        }
        public override void Enter()
        {
            base.Enter();
            Result = AttackResult.Ongoing;
            BeginMotion(AttackPhase.Preparation, preparation);
        }
        public sealed override void FixedTick()
        {
            float remaining = Dt;
            // Goes through at most three phases. null motions and 0-second motions are skipped immediately.
            for (int i = 0; i < 3; i++)
            {
                if (Phase == AttackPhase.Preparation || Phase == AttackPhase.Recovery)
                {
                    if (activeMotion != null)
                    {
                        remaining = activeMotion.Tick(remaining);
                        if (!activeMotion.IsComplete) return;
                    }
                    bool succeeded = activeMotion == null || activeMotion.Succeeded;
                    EndMotion();
                    if (!succeeded || Phase == AttackPhase.Recovery) { SetPhase(AttackPhase.Complete); return; }
                    SetPhase(AttackPhase.Execution);
                    BeginAttack();
                }
                if (Phase == AttackPhase.Execution)
                {
                    remaining = TickAttack(remaining);
                    Result = Outcome;
                    if (Result == AttackResult.Ongoing) return;
                    EndAttack();
                    BeginMotion(AttackPhase.Recovery, recovery);
                }
                if (Phase == AttackPhase.Complete || Phase == AttackPhase.None) return;
            }
        }
        void BeginMotion(AttackPhase phase, StateMotion motion)
        {
            activeMotion = motion;
            activeMotion?.Enter(Result);
            SetPhase(phase);
        }
        void SetPhase(AttackPhase phase)
        {
            Phase = phase;
            owner?.NotifyAttackPhase(this);
        }
        void EndMotion()
        {
            var motion = activeMotion;
            activeMotion = null;
            motion?.Exit();
        }
        public override void Exit()
        {
            EndMotion();
            if (Phase == AttackPhase.Execution) EndAttack();
            SetPhase(AttackPhase.None);
        }
        protected abstract void BeginAttack();
        protected abstract float TickAttack(float dt);
        // Ongoing continues; anything else ends with that result. The finishing motion receives this value
        protected abstract AttackResult Outcome { get; }
        protected virtual void EndAttack() { }
    }
}
