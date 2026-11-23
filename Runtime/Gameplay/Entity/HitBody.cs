using System;
using UnityEngine;
using FireFlower;

namespace FireFlower
{
    public class HitBody
    {
        public FloatStat MaxHpStat {  get; private set; }
        public FloatStat DefStat { get; private set; }
        public FlagStat InvStat { get; private set; }
        public float MaxHp => MaxHpStat.Value;
        public float Def => Mathf.Max(DefStat.Value, MinDefence);
        public bool Inv => InvStat.Active;
        float hp;
        float shield;
        bool dead;
        public bool IsDead => dead;

        public HitInfo? LastHit { get; private set; }

        public float Shield
        {
            get => shield;
            set
            {
                shield = Mathf.Max(0f, value);
                OnHpChanged?.Invoke();
            }
        }

        public float Hp
        {
            get => hp;
            set
            {
                LastHit = null;
                SetHp(value);
            }
        }

        public void TakeHit(in HitInfo info)
        {
            LastHit = info;
            float amount = info.Amount;
            if (amount > 0f && !info.IsHeal)
            {
                float absorbed = Mathf.Min(shield, amount);
                shield -= absorbed;
                amount -= absorbed;
            }
            SetHp(hp - amount);
        }

        void SetHp(float value)
        {
            hp = Mathf.Clamp(value, 0f, MaxHp);
            OnHpChanged?.Invoke();
            CheckDeath();
        }

        void CheckDeath()
        {
            if (dead)
            {
                if (hp > 0f) dead = false;
                return;
            }
            if (hp > 0f) return;
            dead = true;
            OnDie?.Invoke();
        }
        public void MaxHpChanged(float old)
        {
            if (old > 0f && old != MaxHp)
                Hp *= MaxHp / old;
        }
        public Action<HitInfo> OnHit { get; set; }
        public Action OnDie { get; set; }
        public Action OnHpChanged { get; set; }

        // Defense is a divisor, so 0 breaks the math (1 = no reduction, smaller hurts more).
        // A mod's Floor is a ratio of Home, useless when Base is 0 — the absolute floor is applied on read
        const float MinDefence = 0.1f;

        public void Init(StatSheet stat)
        {
            MaxHpStat = stat.Float(MobStats.MaxHp);
            DefStat = stat.Float(MobStats.Defence);
            InvStat = stat.Flag(MobStats.Flag.Invincible);
            MaxHpStat.OnChanged += MaxHpChanged;
        }
    }
}
