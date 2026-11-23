using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    [Serializable]
    public class FeedbackBind : Feedback
    {
        [Tooltip("Name the caller uses")]
        public string key;
    }

    [Serializable]
    public class FeedbackTable
    {
        [SerializeField] FeedbackBind[] binds;

        Dictionary<string, FeedbackBind> map;

        public FeedbackBind Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            BuildMap();
            return map.TryGetValue(key, out FeedbackBind bind) ? bind : null;
        }

        public IFeedbackHandle Play(string key, Vector3 position, GameObject target = null, Transform parent = null, Vector2 direction = default)
            => Find(key)?.Play(position, target, parent, direction);

        public void Invalidate() => map = null;

        void BuildMap()
        {
            if (map != null) return;

            map = new Dictionary<string, FeedbackBind>();
            if (binds == null) return;

            foreach (FeedbackBind bind in binds)
            {
                if (bind == null || string.IsNullOrEmpty(bind.key)) continue;
                if (!map.TryAdd(bind.key, bind))
                    Debug.LogError($"FeedbackTable: duplicate key '{bind.key}'");
            }
        }
    }
}
