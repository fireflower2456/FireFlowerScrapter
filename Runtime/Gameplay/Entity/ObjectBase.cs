using UnityEngine;
using FireFlower;

namespace FireFlower
{
    [DisallowMultipleComponent]
    public abstract class ObjectBase : MonoBehaviour
    {
        [SerializeField] private Team team;
        private StatSheet stat;
        [SerializeField] private bool bootOnAwake = true;

        public virtual Team Team => team;
        public StatSheet Stat => stat;

        bool booted;

        void Awake()
        {
            if (bootOnAwake) Boot();
        }

        public void Boot()
        {
            if (booted) return;
            booted = true;
            OnBoot();
        }

        protected virtual IStatSource BaseStatSource => null;

        protected virtual StatSheet CreateStatSheet()
        {
            var source = BaseStatSource;
            if (source != null) return source.CreateSheet();
            return new StatSheet();
        }

        protected void InitializeStats() => stat = CreateStatSheet();

        protected virtual void OnBoot() => InitializeStats();
    }
}
