using System;
using System.Collections;
using UnityEngine;

namespace FireFlower
{
    // Turns on the target's silhouette outline (SilhouetteOutline). Duration 0 = until stopped
    [Serializable] public class OutlineFeedback : FeedbackBase
    {
        [SerializeField, Min(0f), Tooltip("How long it stays on (seconds). 0 = until stopped")] float duration = 0.3f;

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
        {
            GameObject owner = target != null ? target : parent != null ? parent.gameObject : null;
            SilhouetteOutline outline = owner != null ? owner.GetComponentInChildren<SilhouetteOutline>() : null;
            if (outline == null) return null;
            outline.Show();
            var handle = new Handle(outline);
            if (duration > 0f) VFXManager.Instance.StartCoroutine(StopAfter(handle, duration));
            return handle;
        }

        static IEnumerator StopAfter(Handle handle, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            handle.Stop();
        }

        sealed class Handle : IFeedbackHandle
        {
            SilhouetteOutline outline;
            public Handle(SilhouetteOutline outline) => this.outline = outline;

            public void Stop()
            {
                if (outline != null) outline.Hide();
                outline = null;
            }
        }
    }
}
