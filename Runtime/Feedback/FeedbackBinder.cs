using UnityEngine;

namespace FireFlower
{
    public class FeedbackBinder : MonoBehaviour
    {
        [SerializeField] FeedbackTable feedbacks = new();

        public IFeedbackHandle Play(string key) => Play(key, transform.position);

        public IFeedbackHandle Play(string key, Vector3 position, GameObject target = null, Transform parent = null, Vector2 direction = default)
            => feedbacks.Play(key, position, target != null ? target : gameObject, parent, direction);

        public FeedbackBind Find(string key) => feedbacks.Find(key);

        void OnValidate() => feedbacks.Invalidate();
    }
}
