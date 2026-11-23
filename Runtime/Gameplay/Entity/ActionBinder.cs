using UnityEngine;

namespace FireFlower
{
    // Per-key action table. If FeedbackBinder is "what you see", this is "what actually happens". Attach to the root of drone and monster prefabs
    public class ActionBinder : MonoBehaviour
    {
        [SerializeField] ActionBind[] actions;

        public void Run(string key, ActionContext ctx) => ActionBind.Run(actions, key, ctx);
    }
}
