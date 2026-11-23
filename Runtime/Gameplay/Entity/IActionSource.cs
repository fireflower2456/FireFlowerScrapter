using UnityEngine;

namespace FireFlower
{
    // The source of an event (bullet, drone, mob, explosion). Implemented by whatever holds the owner and base damage (a bullet's own damage, for bullets)
    public interface IActionSource
    {
        IAttacker Attacker { get; }
        float Power { get; }
        void Raise(string key, ActionContext ctx);
    }

    public static class ActionSourceExtensions
    {
        public static void Raise(this IActionSource source, string key, Vector3 position, GameObject target = null) =>
            source.Raise(key, new ActionContext(source, position, target));

        // Attacks, and raises the key event if it lands
        public static bool Hit(this IActionSource source, string key, IHitAble target, float damage, DamageType type = DamageType.Normal)
        {
            if (!source.Attacker.Attack(target, damage, type)) return false;
            GameObject go = ((Component)target).gameObject;
            source.Raise(key, new ActionContext(source, go.transform.position, go) { Damage = damage, DamageType = type });
            return true;
        }
    }
}
