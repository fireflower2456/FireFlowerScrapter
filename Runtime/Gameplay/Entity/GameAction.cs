using System;
using UnityEngine;

namespace FireFlower
{
    // The "actual effect" attached to an event (damage, debuff, explosion, etc.). Feedback handles what is seen and heard.
    // Keyed like feedback; when the event happens, the source (IActionSource) invokes it with the same key
    [Serializable]
    public abstract class GameAction
    {
        // Does nothing if required info (target, etc.) is missing
        public abstract void Run(ActionContext ctx);
    }

    // Event info. Feedback's Play(key, position, target) plus the source and damage dealt. Target and damage may be absent
    public class ActionContext
    {
        public IActionSource Source;
        public Vector3 Position;
        public GameObject Target;
        public float Damage;                // Damage dealt this time (on hit)
        public DamageType DamageType;

        public IAttacker Attacker => Source.Attacker;
        public Team Team => Source.Attacker.Team;

        public ActionContext(IActionSource source, Vector3 position, GameObject target = null)
        {
            Source = source;
            Position = position;
            Target = target;
        }
    }

    // Multiple actions per key
    [Serializable]
    public class ActionBind
    {
        [Tooltip("Bullet: Spawn / Hit / Tick (interval per touched target) / Die · Explosion: Explode / Target · Saw: Hit · Chain: Attach / Tick / Snap / Detach · Monster charge: Hit")]
        public string key;
        [SerializeReference, SubclassSelector] public GameAction[] actions;

        public static void Run(ActionBind[] binds, string key, ActionContext ctx)
        {
            if (binds == null) return;
            foreach (ActionBind bind in binds)
            {
                if (bind == null || bind.key != key || bind.actions == null) continue;
                foreach (GameAction action in bind.actions) action?.Run(ctx);
            }
        }
    }
}
