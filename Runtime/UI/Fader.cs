using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

namespace FireFlower
{
    // Interpolates alpha over time on the CanvasGroup, else the Image, else the SpriteRenderer.
    // Uses unscaled time so it works while paused (Time.timeScale = 0).
    [DisallowMultipleComponent]
    public class Fader : MonoBehaviour
    {
        CanvasGroup cg;
        Image img;
        SpriteRenderer sr;
        Coroutine routine;

        [SerializeField][Range(0f, 1f)] float baseAlpha = 1f;
        [SerializeField, Min(0f)] float duration = 0.25f;

        bool cached;

        protected virtual void Awake() => Cache();   // So subclasses can call base.Awake()

        // Screens created inactive get Fade called before Awake
        void Cache()
        {
            if (cached) return;
            cached = true;
            cg = GetComponent<CanvasGroup>();
            img = GetComponent<Image>();
            sr = GetComponent<SpriteRenderer>();
        }
        // Negative time uses the inspector's duration
        public void Fade(bool show, Action onComplete = null, float time = -1f) // Callback run right after completion
        {
            Cache();
            if (routine != null) StopCoroutine(routine);

            if (show && !gameObject.activeSelf)
            {
                SetAlpha(0f);              // Prevents flicker
                gameObject.SetActive(true);
            }

            if (!gameObject.activeInHierarchy)   // Guards against an inactive parent
            {
                SetAlpha(show ? baseAlpha : 0f);
                routine = null;
                onComplete?.Invoke(); 
                return;
            }

            routine = StartCoroutine(FadeRoutine(show, onComplete, time < 0f ? duration : time));
        }

        IEnumerator FadeRoutine(bool show, Action onComplete, float dur)
        {
            float from = GetAlpha();
            float target = show ? baseAlpha : 0f;

            for (float elapsed = 0f; elapsed < dur; )
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, target, elapsed / dur));
                yield return null;
            }
            SetAlpha(target);
            if (!show)
                gameObject.SetActive(false);

            routine = null;
            onComplete?.Invoke(); 
        }

        float GetAlpha()
        {
            if (cg != null) return cg.alpha;
            if (img != null) return img.color.a;
            if (sr != null) return sr.color.a;
            return 1f;
        }

        void SetAlpha(float a)
        {
            if (cg != null)
            {
                cg.alpha = a;
                return;
            }
            if (img != null)
            {
                Color c = img.color;
                c.a = a;
                img.color = c;
                return;
            }
            if (sr != null)
            {
                Color c = sr.color;
                c.a = a;
                sr.color = c;
            }
        }
    }
}
