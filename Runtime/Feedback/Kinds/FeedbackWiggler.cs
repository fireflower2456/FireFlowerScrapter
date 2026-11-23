using System.Collections;
using UnityEngine;

namespace FireFlower
{
    // Added by WiggleFeedback. Re-triggering mid-wiggle still uses the original position as the base
    public class FeedbackWiggler : MonoBehaviour
    {
        Vector3 origin;
        Coroutine routine;

        public void Play(float duration, float amplitude, int swings, bool realtime)
        {
            if (routine != null) StopCoroutine(routine);
            else origin = transform.localPosition;
            routine = StartCoroutine(Wiggle(duration, amplitude, swings, realtime));
        }

        public void Stop()
        {
            if (routine == null) return;
            StopCoroutine(routine);
            routine = null;
            transform.localPosition = origin;
        }

        void OnDisable() => Stop();

        IEnumerator Wiggle(float duration, float amplitude, int swings, bool realtime)
        {
            for (float t = 0f; t < duration; t += realtime ? Time.unscaledDeltaTime : Time.deltaTime)
            {
                float progress = t / duration;
                float offset = amplitude * (1f - progress) * Mathf.Sin(progress * swings * 2f * Mathf.PI);
                transform.localPosition = origin + new Vector3(offset, 0f, 0f);
                yield return null;
            }
            transform.localPosition = origin;
            routine = null;
        }
    }
}
