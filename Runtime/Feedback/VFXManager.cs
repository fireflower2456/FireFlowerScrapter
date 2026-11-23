using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using FireFlower;

namespace FireFlower
{
    public class VFXManager : ManagerBase<VFXManager>
    {
        ParticleCatalog Catalog => CatalogStore.Get<ParticleCatalog>();

        [SerializeField] Material flashMaterial;
        [SerializeField, Min(0f)] float flashDuration = 0.05f;
        [SerializeField] DamageParticle damagePrefab;
        [SerializeField] CinemachineBasicMultiChannelPerlin perlin;
        [SerializeField] float shakeFrequency = 2f;
        float shakeTimeRemaining;
        float shakeDuration;
        float shakeStrength;
        Coroutine shakeRoutine;
        DampingHold dampingHold;
        Vector3 baseDamping;
        bool baseLookahead;
        Image screenFlash;
        Coroutine screenFlashRoutine;
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        readonly Dictionary<SpriteRenderer, FlashState> flashes = new();
        void Update()
        {
            AfterimageSpawner.Tick(Time.deltaTime);
        }

        void LateUpdate()
        {
            RGBSplit.Apply();
        }

        void OnDisable()
        {
            AfterimageSpawner.Clear();
            foreach (var pair in flashes)
            {
                if (pair.Value.routine != null) StopCoroutine(pair.Value.routine);
                FlashState.RestoreFlash(pair.Key, pair.Value);
            }
            flashes.Clear();
        }

        // If direction is given, rotates right to face it
        public ParticleCore PlayParticle(string key, Vector3 position, Transform parents = null,
            GameObject target = null, Vector2 direction = default, float amount = 1f)
        {
            ParticleItem item = Catalog.Get(key);
            if (item == null) return null;

            Quaternion rotation = direction == Vector2.zero
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            GameObject spawned = ObjectPoolExtention.Get(item.prefab, position, rotation, parents);

            if (target == null && parents != null) target = parents.gameObject;

            if (!spawned.TryGetComponent(out ParticleCore core)) core = spawned.AddComponent<ParticleCore>();

            core.Init(target, amount);
            return core;
        }
        public void ShowDamage(float damage, Transform target, Color color, float scale = 1f)
        {
            var particle = ObjectPoolExtention.Get(damagePrefab, target.position, Quaternion.identity);
            particle.Show(damage, color, scale);
        }
        public void Shake(float duration, float strength = 1f)
        {
            shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, duration);
            shakeDuration = Mathf.Max(shakeDuration, duration);
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeRoutine ??= StartCoroutine(ShakeRoutine());
        }
        // Changes camera damping and disables look-ahead until the handle stops. When overlapping the latest wins; the first one holds the original values
        public IFeedbackHandle HoldCameraDamping(Vector3 damping)
        {
            var composer = perlin.GetComponent<CinemachinePositionComposer>();
            if (dampingHold == null)
            {
                baseDamping = composer.Damping;
                baseLookahead = composer.Lookahead.Enabled;
            }
            dampingHold = new DampingHold(this);
            composer.Damping = damping;
            composer.Lookahead.Enabled = false;
            return dampingHold;
        }

        void ReleaseCameraDamping(DampingHold released)
        {
            if (released != dampingHold) return;
            dampingHold = null;
            var composer = perlin.GetComponent<CinemachinePositionComposer>();
            composer.Damping = baseDamping;
            composer.Lookahead.Enabled = baseLookahead;
        }

        // Covers the whole screen with color, then clears it over duration. Drawn above the world and below the HUD
        public void FlashScreen(Color color, float duration)
        {
            if (screenFlash == null) screenFlash = CreateScreenFlash();
            if (screenFlashRoutine != null) StopCoroutine(screenFlashRoutine);
            screenFlashRoutine = StartCoroutine(ScreenFlashRoutine(color, duration));
        }

        Image CreateScreenFlash()
        {
            var canvas = new GameObject("ScreenFlash").AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;
            var image = new GameObject("Flash").AddComponent<Image>();
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.enabled = false;
            return image;
        }

        IEnumerator ScreenFlashRoutine(Color color, float duration)
        {
            screenFlash.enabled = true;
            for (float t = 0f; t < duration;)
            {
                Color c = color;
                c.a *= 1f - t / duration;
                screenFlash.color = c;
                yield return null;
                if (TimeManager.Instance.IsRunning) t += Time.unscaledDeltaTime;
            }
            screenFlash.enabled = false;
            screenFlashRoutine = null;
        }

        // Calling the same profile again restarts that volume from the beginning
        public void PulseVolume(VolumeProfile profile, float duration, AnimationCurve weight, float priority)
        {
            if (!volumePulses.TryGetValue(profile, out var pulse))
            {
                var volume = new GameObject("Pulse_" + profile.name).AddComponent<Volume>();
                volume.transform.SetParent(transform, false);
                volume.isGlobal = true;
                volume.sharedProfile = profile;
                volume.weight = 0f;
                pulse = new VolumePulse { volume = volume };
                volumePulses.Add(profile, pulse);
            }
            if (pulse.routine != null) StopCoroutine(pulse.routine);
            pulse.volume.priority = priority;
            pulse.routine = StartCoroutine(VolumePulseRoutine(pulse, duration, weight));
        }

        // Ramps in over rampTime and holds until stopped, then clears over fadeTime. If several hold the same profile, it clears when the last one stops
        public IFeedbackHandle HoldVolume(VolumeProfile profile, float rampTime, float fadeTime, float priority)
        {
            if (!volumeHolds.TryGetValue(profile, out var hold))
            {
                var volume = new GameObject("Hold_" + profile.name).AddComponent<Volume>();
                volume.transform.SetParent(transform, false);
                volume.isGlobal = true;
                volume.sharedProfile = profile;
                volume.weight = 0f;
                hold = new VolumeHold(this) { volume = volume };
                volumeHolds.Add(profile, hold);
            }
            hold.volume.priority = priority;
            hold.holders++;
            hold.Run(VolumeHoldRoutine(hold, rampTime, 1f));
            hold.fadeTime = fadeTime;
            return new VolumeTicket(hold);
        }

        IEnumerator VolumeHoldRoutine(VolumeHold hold, float time, float to)
        {
            float from = hold.volume.weight;
            for (float t = 0f; t < time;)
            {
                hold.volume.weight = Mathf.Lerp(from, to, t / time);
                yield return null;
                if (TimeManager.Instance.IsRunning) t += Time.unscaledDeltaTime;
            }
            hold.volume.weight = to;
            hold.routine = null;
        }

        readonly Dictionary<VolumeProfile, VolumeHold> volumeHolds = new();

        sealed class VolumeHold
        {
            readonly VFXManager owner;
            public Volume volume;
            public Coroutine routine;
            public float fadeTime;
            public int holders;

            public VolumeHold(VFXManager owner) => this.owner = owner;

            public void Run(IEnumerator body)
            {
                if (routine != null) owner.StopCoroutine(routine);
                routine = owner.StartCoroutine(body);
            }

            public void Release()
            {
                if (--holders > 0) return;
                holders = 0;
                if (owner != null && volume != null) Run(owner.VolumeHoldRoutine(this, fadeTime, 0f));
            }
        }

        sealed class VolumeTicket : IFeedbackHandle
        {
            VolumeHold hold;

            public VolumeTicket(VolumeHold hold) => this.hold = hold;

            public void Stop()
            {
                hold?.Release();
                hold = null;
            }
        }

        sealed class VolumePulse
        {
            public Volume volume;
            public Coroutine routine;
        }

        readonly Dictionary<VolumeProfile, VolumePulse> volumePulses = new();

        // Must keep running during hit stop, so counts in real time, but stops when paused by a menu etc.
        IEnumerator VolumePulseRoutine(VolumePulse pulse, float duration, AnimationCurve weight)
        {
            for (float t = 0f; t < duration;)
            {
                pulse.volume.weight = Mathf.Clamp01(weight.Evaluate(t / duration));
                yield return null;
                if (TimeManager.Instance.IsRunning) t += Time.unscaledDeltaTime;
            }
            pulse.volume.weight = Mathf.Clamp01(weight.Evaluate(1f));
            pulse.routine = null;
        }

        sealed class DampingHold : IFeedbackHandle
        {
            readonly VFXManager owner;

            public DampingHold(VFXManager owner) => this.owner = owner;

            public void Stop()
            {
                if (owner != null) owner.ReleaseCameraDamping(this);
            }
        }

        // Until stopped, turns white at strength for flash seconds every interval (real time)
        public IFeedbackHandle BlinkWhite(SpriteRenderer sr, float interval, float flash, float strength)
        {
            if (!isActiveAndEnabled || sr == null) return null;
            var blink = new WhiteBlink(this);
            blink.routine = StartCoroutine(BlinkRoutine(sr, interval, flash, strength));
            return blink;
        }

        IEnumerator BlinkRoutine(SpriteRenderer sr, float interval, float flash, float strength)
        {
            while (sr != null)
            {
                FlashWhite(sr, flash, true, strength, false);
                for (float t = 0f; t < interval;)
                {
                    yield return null;
                    if (TimeManager.Instance.IsRunning) t += Time.unscaledDeltaTime;
                }
            }
        }

        sealed class WhiteBlink : IFeedbackHandle
        {
            readonly VFXManager owner;
            public Coroutine routine;

            public WhiteBlink(VFXManager owner) => this.owner = owner;

            public void Stop()
            {
                if (owner != null && routine != null) owner.StopCoroutine(routine);
                routine = null;
            }
        }

        public void FlashWhite(SpriteRenderer sr, bool realTime = false) => FlashWhite(sr, flashDuration, realTime);

        // The material before the white flash, if flashing. So copying the look (afterimages, etc.) doesn't copy the flash
        public Material BaseMaterial(SpriteRenderer sr) => flashes.TryGetValue(sr, out var flash) ? flash.originalMaterial : sr.sharedMaterial;

        // Covers in white at strength. With fade it clears over duration; otherwise it stays and snaps back at the end.
        // With realTime, counts in real time regardless of hit stop or slow motion
        public void FlashWhite(SpriteRenderer sr, float duration, bool realTime, float strength = 1f, bool fade = true)
        {
            if (!isActiveAndEnabled || sr == null || !sr.gameObject.activeInHierarchy || flashMaterial == null)
                return;
            if (!flashes.TryGetValue(sr, out var flash))
            {
                flash = new FlashState { originalMaterial = sr.sharedMaterial, flashMaterial = flashMaterial };
                sr.GetPropertyBlock(flash.originalProperties);
                flashes.Add(sr, flash);
            }

            if (flash.routine != null) StopCoroutine(flash.routine);
            sr.sharedMaterial = flash.flashMaterial;
            sr.GetPropertyBlock(flash.properties);
            flash.properties.SetFloat(FlashAmountId, strength);
            sr.SetPropertyBlock(flash.properties);
            flash.routine = StartCoroutine(FlashRoutine(sr, flash, Mathf.Max(0f, duration), realTime, strength, fade));
        }
        IEnumerator ShakeRoutine()
        {
            perlin.FrequencyGain = shakeFrequency;
            while (shakeTimeRemaining > 0f)
            {
                shakeTimeRemaining -= Time.unscaledDeltaTime;
                perlin.AmplitudeGain = shakeStrength * (shakeTimeRemaining / shakeDuration);
                yield return null;
            }
            perlin.AmplitudeGain = 0f;
            shakeTimeRemaining = 0f;
            shakeDuration = 0f;
            shakeStrength = 0f;
            shakeRoutine = null;
        }

        IEnumerator FlashRoutine(SpriteRenderer sr, FlashState flash, float duration, bool realTime, float strength, bool fade)
        {
            for (float t = 0f; t < duration;)
            {
                yield return null;
                if (sr == null || !sr.gameObject.activeInHierarchy || sr.sharedMaterial != flash.flashMaterial)
                    break;
                if (!realTime) t += Time.deltaTime;
                else if (TimeManager.Instance.IsRunning) t += Time.unscaledDeltaTime;

                if (!fade) continue;
                float amount = strength * (1f - Mathf.Clamp01(t / duration));
                sr.GetPropertyBlock(flash.properties);
                flash.properties.SetFloat(FlashAmountId, amount);
                sr.SetPropertyBlock(flash.properties);
            }
            FlashState.RestoreFlash(sr, flash);
            flashes.Remove(sr);
        }

    }

    public sealed class FlashState
    {
        public Material originalMaterial;
        public Material flashMaterial;
        public readonly MaterialPropertyBlock originalProperties = new();
        public readonly MaterialPropertyBlock properties = new();
        public Coroutine routine;
        public static void RestoreFlash(SpriteRenderer sr, FlashState flash)
        {
            if (sr == null) return;
            if (sr.sharedMaterial == flash.flashMaterial)
                sr.sharedMaterial = flash.originalMaterial;
            sr.SetPropertyBlock(flash.originalProperties.isEmpty ? null : flash.originalProperties);
        }
    }
}
