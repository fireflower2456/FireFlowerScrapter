using UnityEngine;

namespace FireFlower
{
    [RequireComponent(typeof(RectTransform))]
    public class LinearUIMove : MonoBehaviour
    {
        // Reference for SpaceLocal coordinates. Assigned by whoever owns the full-screen UI panel
        public static RectTransform Space { get; set; }

        public enum LinearUIHoverMode { SpaceLocal, AnchorOrigin, RestOffset }
        [SerializeField] bool restOnDisable = true;
        public bool IsMoving { get; set; }

        public RectTransform rect { get; private set; }

        protected Vector2 restAnchored;
        protected Vector3 restWorld;
        protected Vector2 velocity;

        public LinearUIHoverMode hoverMode = LinearUIHoverMode.RestOffset;
        public Vector2 hoverPos;
        [Range(0.00001f, 1f)] public float Smoothess = 0.1f;

        protected virtual void Awake()
        {
            rect = GetComponent<RectTransform>();
            restAnchored = rect.anchoredPosition;
            restWorld = rect.position;
        }

        protected virtual void OnDisable()
        {
            if (restOnDisable)
            {
                IsMoving = false;
                ResetToRest();
            }
        }
        protected virtual void LateUpdate()
        {
            rect.anchoredPosition = Vector2.SmoothDamp(
                rect.anchoredPosition, IsMoving ? HoverAnchored() : restAnchored, ref velocity, Smoothess,
                Mathf.Infinity, Time.unscaledDeltaTime);
        }

        protected void ResetToRest()
        {
            if (hoverMode == LinearUIHoverMode.SpaceLocal)
                rect.position = restWorld;
            else
                rect.anchoredPosition = restAnchored;

            velocity = Vector2.zero;
        }

        Vector2 HoverAnchored() => hoverMode switch
        {
            LinearUIHoverMode.RestOffset => restAnchored + hoverPos,
            LinearUIHoverMode.AnchorOrigin => hoverPos,
            LinearUIHoverMode.SpaceLocal => WorldToAnchored(
                Space.TransformPoint(hoverPos)),
            _ => restAnchored,
        };

        Vector2 WorldToAnchored(Vector3 worldPos)
        {
            var parentRect = rect.parent as RectTransform;
            if (!parentRect) return worldPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null,
                out Vector2 localPoint);

            Rect parentR = parentRect.rect;
            Vector2 anchorCenter = new Vector2(
                Mathf.Lerp(parentR.xMin, parentR.xMax, (rect.anchorMin.x + rect.anchorMax.x) * 0.5f),
                Mathf.Lerp(parentR.yMin, parentR.yMax, (rect.anchorMin.y + rect.anchorMax.y) * 0.5f));

            return localPoint - anchorCenter;
        }
    }
}
