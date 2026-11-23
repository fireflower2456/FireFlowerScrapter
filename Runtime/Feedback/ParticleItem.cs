using UnityEngine;
using FireFlower;

namespace FireFlower
{
    [CreateAssetMenu(fileName = "ParticleItem", menuName = "Particle/ParticleItem")]
    public class ParticleItem : ScriptableObject, IKeyedCatalogItem
    {
        [Tooltip("Name code/data use to call this particle. Must be unique within the catalog")]
        [SerializeField] string key;
        public GameObject prefab;

        public string Key => key;
    }
}
