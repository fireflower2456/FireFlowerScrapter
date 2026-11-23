
namespace FireFlower
{
    public abstract class BaseState : IState
    {
        public virtual void Enter() { }
        public virtual void Execute() { }
        public virtual void FixedTick() { }
        public virtual void Exit() { }
    }
}
