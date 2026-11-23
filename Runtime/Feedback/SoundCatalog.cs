using UnityEngine;
using FireFlower;

namespace FireFlower
{
    [CreateAssetMenu(fileName = "SoundCatalog", menuName = "Catalog/SoundCatalog")]
    public class SoundCatalog : ScriptableObjectCatalog<SoundItem>
    {
        protected override bool UseId => false;
        protected override bool UseKey => true;
    }
}
