using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace FireFlower
{
    // Mob controller. Outside code (AI, blocks, scripts) picks the state via ChangeState; this provides ground sensing, the blackboard and movement helpers.
    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "MobController")]
    [AddComponentMenu("State Machine/Composable Controller")]
    public class ComposableController : EntityController
    {
        public event System.Action<IState> StateChanged;
        public event System.Action<MonsterAttackState> AttackPhaseChanged;
        internal void NotifyAttackPhase(MonsterAttackState attack) => AttackPhaseChanged?.Invoke(attack);
        readonly Blackboard blackboard = new();
        [Header("Ground")]
        [Tooltip("Ground and side sensing boxes. States and motions use these.")]
        [SerializeField] GroundProbe ground = new();
        public GroundProbe Ground => ground;
        public Blackboard Blackboard => blackboard;

        public override void Init(MobBase mob, Animator anim)
        {
            base.Init(mob, anim);
            ground.Init(transform, Rigidbody);
            blackboard.Clear();
        }

        public override void ChangeState(IState newState)
        {
            if (ReferenceEquals(currentState, newState)) return;
            base.ChangeState(newState);
            StateChanged?.Invoke(newState);
        }

        // Doesn't run states while incapacitated
        protected override void TickState()
        {
            if (!IsKnock) base.TickState();
        }

        protected override void FixedTickState()
        {
            if (!IsKnock) base.FixedTickState();
        }

        // Replaces only x velocity. Leaves gravity (y) alone
        public void SetHorizontal(float speed)
        {
            if (Rigidbody != null) Rigidbody.linearVelocity = new Vector2(speed, Rigidbody.linearVelocity.y);
        }

        public void SetVelocity(Vector2 velocity)
        {
            if (Rigidbody != null) Rigidbody.linearVelocity = velocity;
        }

        public Vector2 Velocity => Rigidbody != null ? Rigidbody.linearVelocity : Vector2.zero;

        // Moves to point x. Decelerates when remaining distance and current speed show that braking at acceleration now would stop exactly there,
        // so acceleration on start and deceleration on arrival come naturally. Zero toward a blocking wall
        public void MoveToX(float x, float maxSpeed, float acceleration, float dt)
        {
            float gap = x - transform.position.x;
            float stoppable = Mathf.Sqrt(2f * acceleration * Mathf.Abs(gap));
            float goal = Mathf.Sign(gap) * Mathf.Min(maxSpeed, stoppable);
            float speed = Mathf.MoveTowards(Velocity.x, goal, acceleration * dt);
            if (speed != 0f && ground.WallAt(Mathf.Sign(speed))) speed = 0f;
            SetHorizontal(speed);
        }

        void OnDisable() => ChangeState(null);
        void OnDrawGizmosSelected()
        {
            ground.DrawGizmos(transform);
        }
    }
}
