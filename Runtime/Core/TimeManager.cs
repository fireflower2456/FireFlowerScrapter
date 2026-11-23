using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    public class TimeManager : ManagerBase<TimeManager>
    {
        public sealed class TimeHandle
        {
            public StatMod Mod;
            public bool Active = true;

            public TimeHandle(float scale)
                => Mod = new StatMod(StatOp.Mul, Mathf.Max(0f, scale) * 100f);
        }

        // Keep equal to Project Settings > Fixed Timestep.
        // Deriving it from Time.fixedDeltaTime would pick up the reduced value if the scene changes during slow-mo,
        // making physics permanently faster. Only write it here, never read it back
        [SerializeField, Min(0.001f)] float baseFixedDeltaTime = 0.02f;
        float appliedScale = 1f;
        bool isRunning;
        FloatStat scaleStat;
        Coroutine fadeRoutine;
        TimeHandle hitStopHandle;
        float hitStopEnd;
        readonly HashSet<TimeHandle> activeHandles = new();

        public override void Boot()
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = null;
            hitStopHandle = null;
            foreach (var handle in activeHandles) handle.Active = false;
            activeHandles.Clear();

            scaleStat = new FloatStat(1f);
            appliedScale = scaleStat.Value;
            IsRunning = true;
        }

        // Only controls whether it runs. The handle API changes the scale while running.
        public bool IsRunning
        {
            get => isRunning;
            set
            {
                isRunning = value;
                ApplyTimeScale();
            }
        }

        // Omitting seconds applies the final scale immediately.
        public TimeHandle AddPlayTimeMod(float scale, float seconds = 0f)
        {
            var handle = new TimeHandle(scale);
            activeHandles.Add(handle);
            scaleStat.Add(handle.Mod);
            FadeToTarget(seconds);
            return handle;
        }

        // Updates only this handle's scale, combined with other slow-motion and hit-stop scales.
        public void SetPlayTimeMod(TimeHandle handle, float scale)
        {
            if (handle == null || !handle.Active || !activeHandles.Contains(handle)) return;
            float value = Mathf.Max(0f, scale) * 100f;
            if (handle.Mod.Value == value) return;
            scaleStat.Remove(handle.Mod);
            handle.Mod = new StatMod(StatOp.Mul, value);
            scaleStat.Add(handle.Mod);
            FadeToTarget(0f);
        }

        // Removes only this handle's modifier and returns to the new final value.
        public void RemovePlayTimeMod(TimeHandle handle, float seconds = 0f)
        {
            if (handle == null || !handle.Active || !activeHandles.Remove(handle)) return;
            handle.Active = false;
            scaleStat.Remove(handle.Mod);
            FadeToTarget(seconds);
        }

        public void HitStop(float seconds)
        {
            CancelHitStop();
            if (seconds <= 0f) return;

            hitStopHandle = AddPlayTimeMod(0f);
            hitStopEnd = Time.realtimeSinceStartup + seconds;
        }

        // Waiting in a coroutine would not resume after an editor pause, leaving time stuck at 0
        void Update()
        {
            if (hitStopHandle != null && Time.realtimeSinceStartup >= hitStopEnd) CancelHitStop();
        }

        void CancelHitStop()
        {
            TimeHandle handle = hitStopHandle;
            hitStopHandle = null;
            RemovePlayTimeMod(handle);
        }

        void FadeToTarget(float seconds)
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            float target = Mathf.Max(0f, scaleStat.Value);
            if (seconds <= 0f)
            {
                appliedScale = target;
                ApplyTimeScale();
                return;
            }
            fadeRoutine = StartCoroutine(FadeScale(appliedScale, target, seconds));
        }

        IEnumerator FadeScale(float start, float target, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                appliedScale = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / seconds));
                ApplyTimeScale();
                yield return null;
            }

            appliedScale = target;
            fadeRoutine = null;
            ApplyTimeScale();
        }

        void ApplyTimeScale()
        {
            if (!IsRunning)
            {
                Time.timeScale = 0f;
                return;
            }

            Time.timeScale = appliedScale;
            if (appliedScale > 0f)
                Time.fixedDeltaTime = appliedScale * baseFixedDeltaTime;
        }
    }
}
