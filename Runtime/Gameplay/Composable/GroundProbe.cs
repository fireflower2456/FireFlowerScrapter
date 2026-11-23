using System;
using UnityEngine;

namespace FireFlower
{
    // Sweeps below and to the sides with overlap boxes. A query, not a collision callback, so it can be called from any state or motion.
    // Box sizes differ per mob, so the container (controller) holds one set and lends it to the states.
    [Serializable]
    public class GroundProbe
    {
        [SerializeField] LayerMask groundMask;
        [SerializeField] Vector2 footSize = new(0.7f, 0.15f);
        [SerializeField] Vector2 footOffset = new(0f, -0.5f);
        [SerializeField] Vector2 sideSize = new(0.15f, 0.7f);
        [SerializeField] Vector2 sideOffset = new(0.5f, 0f);
        [Tooltip("Rising faster than this counts as airborne")]
        [SerializeField] float risingSpeed = 0.1f;

        Transform origin;
        Rigidbody2D body;

        LayerMask Mask => groundMask.value == 0 ? Layers.Wall : groundMask;

        public void Init(Transform origin, Rigidbody2D body)
        {
            this.origin = origin;
            this.body = body;
        }

        // Platforms are only stood on — not treated as side walls
        public bool HasSupport => Overlap(footOffset, footSize, Mask | Layers.Platform);
        public bool IsGrounded => (body == null || body.linearVelocity.y <= risingSpeed) && HasSupport;

        // The sign of direction picks which side to check
        public bool WallAt(float direction) =>
            Overlap(new Vector2(sideOffset.x * Mathf.Sign(direction), sideOffset.y), sideSize, Mask);

        bool Overlap(Vector2 offset, Vector2 size, LayerMask mask) =>
            origin != null && Physics2D.OverlapBox((Vector2)origin.position + offset, size, 0f, mask) != null;

        public void DrawGizmos(Transform at)
        {
            if (at == null) return;
            var previous = Gizmos.color;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube((Vector2)at.position + footOffset, footSize);
            Gizmos.DrawWireCube((Vector2)at.position + sideOffset, sideSize);
            Gizmos.DrawWireCube((Vector2)at.position + new Vector2(-sideOffset.x, sideOffset.y), sideSize);
            Gizmos.color = previous;
        }
    }
}
