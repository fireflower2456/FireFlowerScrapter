using UnityEngine;

namespace FireFlower
{
    // Base for pixel effects (PixelCanvas) attached to a parent (a blade, etc.).
    // A rotating parent stretched only horizontally skews its children's drawing, so on start this moves under the parent's parent.
    // Hides when the original parent is disabled (OnOwnerHidden) and is destroyed along with it
    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class DetachedPixelFX : MonoBehaviour
    {
        [SerializeField, Min(1)] protected int pixelsPerUnit = 32;

        protected PixelCanvas canvas;
        Transform owner;
        bool owned;

        protected virtual void Awake()
        {
            canvas = new PixelCanvas(GetComponent<SpriteRenderer>(), pixelsPerUnit);
            canvas.Hide();
            owner = transform.parent;
            owned = owner != null;
            if (owned) transform.SetParent(owner.parent, true);
        }

        // Whether drawing is allowed this frame. false if the original parent is disabled or gone
        protected bool OwnerShown()
        {
            if (!owned) return true;
            if (owner == null)
            {
                Destroy(gameObject);
                return false;
            }
            if (owner.gameObject.activeInHierarchy) return true;
            OnOwnerHidden();
            canvas.Hide();
            return false;
        }

        protected abstract void OnOwnerHidden();

        // Dot radius for thickness width (world) (1 pixel if 0)
        protected int Radius(float width) => Mathf.Max(0, Mathf.RoundToInt((width * pixelsPerUnit - 1f) * 0.5f));
    }
}
