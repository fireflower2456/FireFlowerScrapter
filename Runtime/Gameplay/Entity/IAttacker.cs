using System;
using UnityEngine;

namespace FireFlower
{
    // Info while an attack is being computed. Changing Damage in OnAttacking affects only this attack
    public sealed class AttackInfo : IScrapEvent
    {
        public readonly IHitAble Target;
        public Component Other => Target as Component;
        public float Damage;
        public AttackInfo(IHitAble target, float damage) { Target = target; Damage = damage; }
    }

    // Kill info. Changing Coins in OnKill affects only the currency dropped this time
    public sealed class KillInfo : IScrapEvent
    {
        public readonly IHitAble Victim;
        public Component Other => Victim as Component;
        public int Coins;
        public KillInfo(IHitAble victim, int coins) { Victim = victim; Coins = coins; }
    }

    public interface IAttacker
    {
        public Team Team { get; }
        public Action AttackPerformed {  get; set; }
        public Action<IHitAble> OnAttackHit { get; set; }
        // Just before a normal attack's damage applies (before crits)
        public Action<AttackInfo> OnAttacking { get; set; }
        // Target killed by my attack (damage over time included). Called before currency drops. Determined by the victim's HitBody.LastHit.Instigator
        public Action<KillInfo> OnKill { get; set; }

        // Rolls a crit and increases dmg if it lands. Never crits by default
        public bool RollCrit(ref float dmg) => false;

        // Aim point. The mouse for the player, the blackboard target for monsters
        public Vector2 AimPoint { get; }

        // Rotation from from toward AimPoint (0° is up)
        public Quaternion AimFrom(Vector2 from)
        {
            Vector2 dir = AimPoint - from;
            if (dir.sqrMagnitude < 0.000001f) return Quaternion.identity;
            return Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        }

        public bool Attack(IHitAble hit,float dmg,DamageType type = DamageType.Normal)
        {
            if (hit.Team== Team && type != DamageType.Recovery) return false;

            if (type == DamageType.Normal && OnAttacking != null)
            {
                var info = new AttackInfo(hit, dmg);
                OnAttacking(info);
                dmg = info.Damage;
            }

            // Drone attacks also go through the owner, so they are caught here
            bool crit = type == DamageType.Normal && RollCrit(ref dmg);

            if (hit.Invincible || !hit.Hit(dmg, type, this, crit)) return false;
            OnAttackHit?.Invoke(hit);
            return true;
        }
    }
}
