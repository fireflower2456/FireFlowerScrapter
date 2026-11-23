using UnityEngine;

namespace FireFlower
{
    // Attaches at an anchor relative to the target sprite's bounds plus a world offset.
    // anchor: normalized -1~1 coordinates. (0,0) = center, (0,1) = above the head, (0,-1) = at the feet, (±1,0) = left/right edge
    public class ParticleOffset : ParticleBase
    {
        [SerializeField] Vector2 anchor = new(0f, 1f);
        [SerializeField] Vector2 offset = new(0f, 0.3f);
        [SerializeField] bool followFlip = true;

        protected override void OnInit()
        {
            SpriteRenderer target = FindTargetSprite();
            if (target == null) return;

            Vector2 a = anchor;
            Vector2 o = offset;
            if (followFlip && target.flipX)
            {
                a.x = -a.x;
                o.x = -o.x;
            }

            Bounds b = target.bounds;
            Vector3 pos = b.center + Vector3.Scale(b.extents, a) + (Vector3)o;
            pos.z = transform.position.z;
            transform.position = pos;
        }
    }
}
