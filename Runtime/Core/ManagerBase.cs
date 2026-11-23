using UnityEngine;

namespace FireFlower
{
    public abstract class ManagerBase : MonoBehaviour
    {
        public int bootOrder;
        public abstract void Init();
        public virtual void Boot() { }
        public virtual bool IsReady => true;   // Override only in managers whose Boot continues asynchronously
        public virtual bool BootFailed => false;
    }

    public abstract class ManagerBase<T> : ManagerBase where T : ManagerBase<T>
    {
        public static T Instance { get; private set; }
        public override void Init() => Instance = (T)this;
    }
}
