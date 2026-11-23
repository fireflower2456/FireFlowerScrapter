using UnityEngine;
using FireFlower;

namespace FireFlower
{
    // Wires up an entity's state execution, stats and animation
    public abstract class EntityController : BaseController
    {
        public FlagStat IsKnockStat { get; private set; }
        public FloatStat SpdStat { get; private set; }
        public FloatStat BehaviorStat { get; private set; }
        public FloatStat AtkStat { get; private set; }
        public bool IsKnock => IsKnockStat.Active;
        public float Spd => SpdStat.Value;
        public float BehaviorSpd => BehaviorStat.Value;
        public float AtkSpd => AtkStat.Value;
        public MobBase Owner { get; private set; }
        public Animator Anim { get; private set; }
        public Rigidbody2D Rigidbody { get; private set; }
        bool initialized;
        readonly StatMod knockStopMod = new(StatOp.Set, 0f);

        public virtual void Init(MobBase mob, Animator anim)
        {
            ChangeState(null);
            if (IsKnockStat != null) IsKnockStat.OnActiveChanged -= OnKnockChanged;
            if (BehaviorStat != null) BehaviorStat.OnChanged -= OnSpeedChanged;
            if (AtkStat != null) AtkStat.OnChanged -= OnSpeedChanged;
            Owner = mob;
            Rigidbody = mob.GetComponent<Rigidbody2D>();
            SpdStat = mob.Stat.Float(MobStats.MoveSpeed);
            BehaviorStat = mob.Stat.RequireFloat(MobStats.BehaviorSpeed, 1f);
            AtkStat = mob.Stat.RequireFloat(MobStats.AtkBehaviorSpeed, 1f);
            IsKnockStat = mob.Stat.Flag(MobStats.Flag.IsKnocked);
            IsKnockStat.OnActiveChanged += OnKnockChanged;
            BehaviorStat.OnChanged += OnSpeedChanged;
            AtkStat.OnChanged += OnSpeedChanged;
            Anim = anim;
            initialized = true;
        }

        public void AddTimeMod(StatMod m) { SpdStat.Add(m); BehaviorStat.Add(m); AtkStat.Add(m); }
        public void RemoveTimeMod(StatMod m) { SpdStat.Remove(m); BehaviorStat.Remove(m); AtkStat.Remove(m); }

        void OnKnockChanged(bool knocked)
        {
            if (knocked)
            {
                AddTimeMod(knockStopMod);
                if (Rigidbody != null) Rigidbody.linearVelocity = Vector2.zero;
            }
            else RemoveTimeMod(knockStopMod);
        }

        public override void ChangeState(IState newState)
        {
            if (ReferenceEquals(currentState, newState)) return;
            base.ChangeState(newState);
            RefreshAnimSpeed();
        }

        void OnSpeedChanged(float oldValue) => RefreshAnimSpeed();
        void RefreshAnimSpeed()
        {
            if (Anim != null && currentState is EntityState state) Anim.speed = state.Tempo;
        }

        public override void Update()
        {
            if (!initialized) return;
            TickState();
        }

        public override void FixedUpdate()
        {
            if (!initialized) return;
            FixedTickState();
        }

        protected virtual void TickState() => base.Update();
        protected virtual void FixedTickState() => base.FixedUpdate();
    }
}
