using System;
using FireFlower;

namespace FireFlower
{
    public interface IHitAble : ILastDamage
    {
        float? ILastDamage.LastDamage => Body.LastHit?.Amount;
        StatSheet Stat { get;}
        Team Team { get;}
        HitBody Body { get;}
        Action<HitInfo> OnHit => Body.OnHit;
        Action OnHpChanged => Body.OnHpChanged;
        Action OnDie => Body.OnDie;
        float MaxHp => Body.MaxHp;
        float Defence => Body.Def;
        bool Invincible => Body.Inv;
        float Hp => Body.Hp;
        float Shield => Body.Shield;

        bool Hit(float dmg, DamageType type, IAttacker attacker, bool crit = false)
        {
            if (Body.IsDead) return false;
            Apply(Count(dmg, type, Defence), type, attacker, false, crit);
            return true;
        }

        // Damage over time. Ignores defense
        bool HitDot(float dmg, IAttacker from)
        {
            if (Body.IsDead) return false;
            Apply(dmg, DamageType.True, from, true);
            return true;
        }

        private void Apply(float amount, DamageType type, IAttacker instigator, bool dot, bool crit = false)
        {
            var info = new HitInfo(instigator, amount, type, dot, crit);
            Body.TakeHit(info);
            OnHit?.Invoke(info);
        }

        static float Count(float dmg, DamageType type, float defence) => type switch
        {
            DamageType.Recovery => -dmg,
            DamageType.True or DamageType.Environment => dmg,
            _ => dmg / defence,
        };
    }
}
