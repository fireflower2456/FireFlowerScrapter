using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // Places white copies of parts offset by thickness pixels up/down/left/right behind all parts, so multi-piece bodies get one outline around the whole silhouette
    public class SilhouetteOutline : MonoBehaviour
    {
        [SerializeField] SpriteRenderer[] parts;
        [Tooltip("FlashWhite")]
        [SerializeField] Material material;
        [SerializeField] Color color = Color.white;
        [SerializeField, Min(1)] int thickness = 1;

        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly Vector2[] Directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        readonly List<(SpriteRenderer source, SpriteRenderer copy)> copies = new();
        bool shown;

        public void Show()
        {
            if (copies.Count == 0) Build();
            shown = true;
            Sync();
        }

        public void Hide()
        {
            shown = false;
            foreach (var (_, copy) in copies)
                if (copy != null) copy.enabled = false;
        }

        void OnDisable() => Hide();

        void LateUpdate()
        {
            if (shown) Sync();
        }

        void Build()
        {
            int back = int.MaxValue;
            foreach (SpriteRenderer part in parts)
                if (part != null) back = Mathf.Min(back, part.sortingOrder);

            var block = new MaterialPropertyBlock();
            foreach (SpriteRenderer part in parts)
            {
                if (part == null || part.sprite == null) continue;
                float pixel = thickness / part.sprite.pixelsPerUnit;
                foreach (Vector2 direction in Directions)
                {
                    var copy = new GameObject("Outline").AddComponent<SpriteRenderer>();
                    copy.transform.SetParent(part.transform, false);
                    copy.transform.localPosition = direction * pixel;
                    copy.sharedMaterial = material;
                    copy.sortingLayerID = part.sortingLayerID;
                    copy.sortingOrder = back - 1;
                    copy.color = color;
                    copy.GetPropertyBlock(block);
                    block.SetFloat(FlashAmountId, 1f);
                    copy.SetPropertyBlock(block);
                    copy.enabled = false;
                    copies.Add((part, copy));
                }
            }
        }

        void Sync()
        {
            foreach (var (source, copy) in copies)
            {
                if (source == null || copy == null) continue;
                copy.sprite = source.sprite;
                copy.flipX = source.flipX;
                copy.flipY = source.flipY;
                copy.enabled = source.enabled && source.gameObject.activeInHierarchy;
            }
        }
    }
}
