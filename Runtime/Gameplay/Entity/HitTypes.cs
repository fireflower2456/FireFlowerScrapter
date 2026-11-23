
namespace FireFlower
{
    public enum DamageType
    {
        Normal = 0,
        True = 1,
        Recovery = 2,
        Environment = 3,   // Fixed damage with no attacker (self-damage, etc.). Treated like damage over time
    }

    public readonly struct HitInfo : IScrapEvent
    {
        public UnityEngine.Component Other => Instigator as UnityEngine.Component;
        public readonly IAttacker Instigator;
        public readonly float Amount;
        public readonly bool IsCrit;
        public readonly DamageType Type;
        // Came in as damage over time
        public readonly bool Dot;

        public HitInfo(IAttacker instigator, float amount, DamageType type, bool dot = false, bool isCrit = false)
        {
            Instigator = instigator;
            Amount = amount;
            Type = type;
            Dot = dot;
            IsCrit = isCrit;
        }

        public bool IsHeal => Type == DamageType.Recovery;
        public bool IsDot => Dot || Type == DamageType.Environment;
    }
}
