using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using FireFlower;

namespace FireFlower
{
    public class SoundManager : MonoBehaviour
    {
        public enum Channel { Master, BGM, SFX }

        public static SoundManager Instance { get; private set; }
        const string Mixer = "SoundMixer";
        const float PanStrength = 0.6f;
        const float OffscreenRange = 0.5f;
        const float BgmFade = 1f;

        SoundCatalog catalog;

        AudioMixer mixer;
        AudioMixerGroup sfxGroup;
        AudioMixerGroup bgmGroup;
        AudioSource[] bgm;
        int bgmSlot;
        string bgmKey;
        Coroutine bgmRoutine;
        readonly float[] volumes = { 1f, 1f, 1f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public static void Create(SoundCatalog catalog)
        {
            if (Instance != null) return;

            var go = new GameObject(nameof(SoundManager));
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<SoundManager>();
            Instance.catalog = catalog;
        }

        void Awake()
        {
            mixer = Resources.Load<AudioMixer>(Mixer);
            sfxGroup = mixer.FindMatchingGroups(nameof(Channel.SFX))[0];
            bgmGroup = mixer.FindMatchingGroups(nameof(Channel.BGM))[0];
            bgm = new[] { NewBgmSource(), NewBgmSource() };
        }

        // The mixer ignores SetFloat during Awake
        void Start() => ApplyVolumes();

        AudioSource NewBgmSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = bgmGroup;
            source.playOnAwake = false;
            source.loop = true;
            return source;
        }

        #region SFX
        public SoundVoice Play(string key, Vector3? position = null)
        {
            SoundItem item = catalog != null ? catalog.Get(key) : null;
            AudioClip clip = item != null ? item.PickClip() : null;
            if (clip == null) return null;

            float pan = 0f, fade = 1f;
            if (item.world && position.HasValue && !TryPlace(position.Value, out pan, out fade)) return null;

            SoundVoice voice = ObjectPoolExtention.Get<SoundVoice>();
            voice.Play(sfxGroup, clip, item.volume * fade, 1f + Random.Range(-item.pitchJitter, item.pitchJitter), pan, item.loop);
            return voice;
        }

        bool TryPlace(Vector3 position, out float pan, out float fade)
        {
            pan = 0f;
            fade = 1f;
            Camera cam = Camera.main;
            if (cam == null) return true;

            Vector3 view = cam.WorldToViewportPoint(position);
            pan = Mathf.Clamp((view.x - 0.5f) * 2f, -1f, 1f) * PanStrength;
            float outside = Mathf.Max(Mathf.Abs(view.x - 0.5f), Mathf.Abs(view.y - 0.5f)) - 0.5f;
            fade = 1f - Mathf.Clamp01(outside / OffscreenRange);
            return fade > 0f;
        }
        #endregion SFX

        #region BGM
        public void PlayBgm(string key)
        {
            if (key == bgmKey) return;

            SoundItem item = catalog != null ? catalog.Get(key) : null;
            AudioClip clip = item != null ? item.PickClip() : null;
            if (clip == null) return;

            bgmKey = key;
            bgmSlot = 1 - bgmSlot;
            AudioSource next = bgm[bgmSlot];
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            Crossfade(next, bgm[1 - bgmSlot], item.volume);
        }

        public void StopBgm()
        {
            if (bgmKey == null) return;

            bgmKey = null;
            Crossfade(null, bgm[bgmSlot], 0f);
        }

        void Crossfade(AudioSource fadeIn, AudioSource fadeOut, float volume)
        {
            if (bgmRoutine != null) StopCoroutine(bgmRoutine);
            bgmRoutine = StartCoroutine(CrossfadeRoutine(fadeIn, fadeOut, volume));
        }

        IEnumerator CrossfadeRoutine(AudioSource fadeIn, AudioSource fadeOut, float volume)
        {
            float outStart = fadeOut.volume;
            for (float t = 0f; t < BgmFade; t += Time.unscaledDeltaTime)
            {
                float k = t / BgmFade;
                if (fadeIn != null) fadeIn.volume = volume * k;
                fadeOut.volume = outStart * (1f - k);
                yield return null;
            }

            if (fadeIn != null) fadeIn.volume = volume;
            fadeOut.Stop();
            bgmRoutine = null;
        }

        #endregion BGM

        #region Volume
        public void SetVolume(Channel channel, float value)
            => mixer.SetFloat(channel.ToString(), Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);

        // 0–1. Stored and applied again in Start
        public void SetVolumes(float master, float bgmVolume, float sfx)
        {
            volumes[0] = master;
            volumes[1] = bgmVolume;
            volumes[2] = sfx;
            ApplyVolumes();
        }

        void ApplyVolumes()
        {
            SetVolume(Channel.Master, volumes[0]);
            SetVolume(Channel.BGM, volumes[1]);
            SetVolume(Channel.SFX, volumes[2]);
        }
        #endregion Volume
    }
}
