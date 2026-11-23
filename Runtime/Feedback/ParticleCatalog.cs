using UnityEngine;
using FireFlower;

namespace FireFlower
{
    [CreateAssetMenu(fileName = "ParticleCatalog", menuName = "Catalog/ParticleCatalog")]
    public class ParticleCatalog : ScriptableObjectCatalog<ParticleItem>
    {
        protected override bool UseId => false;
        protected override bool UseKey => true;
    }
}
