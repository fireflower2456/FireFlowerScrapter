using UnityEngine;

namespace FireFlower
{
    // For targets whose body sprite is one of several children, tells which one it is
    public interface IMainSprite
    {
        SpriteRenderer MainSprite { get; }
    }

    // Read by particles that scale with damage
    public interface ILastDamage
    {
        float? LastDamage { get; }
    }

    public static class FeedbackTarget
    {
        public static SpriteRenderer MainSprite(GameObject go, bool includeInactive)
        {
            if (go.TryGetComponent(out IMainSprite owner) && owner.MainSprite != null) return owner.MainSprite;
            return go.GetComponentInChildren<SpriteRenderer>(includeInactive);
        }
    }
}
