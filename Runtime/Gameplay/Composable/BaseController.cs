using UnityEngine;

namespace FireFlower
{
    public abstract class BaseController: MonoBehaviour
    {
        protected IState currentState;
        public IState CurrentState => currentState;   // For external type checks (is) on the current state

        public virtual void ChangeState(IState newState)
        {
            currentState?.Exit();
            currentState = newState;
            currentState?.Enter();
        }

        public virtual void Update() => currentState?.Execute();
        public virtual void FixedUpdate() => currentState?.FixedTick();
    }

    public interface IState
    {
        void Enter();    // Once on entering the state
        void Execute();  // Per-Update logic (transition checks usually go here)
        /// <summary>Synced with MonoBehaviour.FixedUpdate. Per-state fixed-step work (walking, dash deceleration, etc.).</summary>
        void FixedTick();
        void Exit();     // Once on leaving the state (cleans up side effects)
    }
}
