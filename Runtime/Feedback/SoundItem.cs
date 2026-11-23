using UnityEngine;
using FireFlower;

namespace FireFlower
{
    [CreateAssetMenu(fileName = "SoundItem", menuName = "Sound/SoundItem")]
    public class SoundItem : ScriptableObject, IKeyedCatalogItem
    {
        [SerializeField] string key;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 0.5f)] public float pitchJitter;
        public bool loop;
        public bool world;

        public string Key => key;
        public AudioClip PickClip() => clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];
    }
}
