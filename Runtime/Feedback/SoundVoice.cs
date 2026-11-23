using UnityEngine;
using UnityEngine.Audio;
using FireFlower;

namespace FireFlower
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundVoice : MonoBehaviour, IFeedbackHandle
    {
        AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
        }

        public void Play(AudioMixerGroup group, AudioClip clip, float volume, float pitch, float pan, bool loop)
        {
            source.outputAudioMixerGroup = group;
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.panStereo = pan;
            source.loop = loop;
            source.Play();
        }

        void Update()
        {
            if (!source.isPlaying) ObjectPoolExtention.Release(gameObject);
        }

        public void Stop() => ObjectPoolExtention.Release(gameObject);
    }
}
