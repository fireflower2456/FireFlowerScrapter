using System.Linq;
using UnityEngine;
using FireFlower;

namespace FireFlower.Samples
{
    // Sample scene boot: assigns catalogs directly instead of by address, then runs Init → Boot on the scene's managers in bootOrder.
    // A real game calls CatalogStore.Load with Addressables addresses (type names) and uses its own boot order
    public class FeedbackSampleBooter : MonoBehaviour
    {
        [SerializeField] ParticleCatalog particles;

        void Awake()
        {
            CatalogStore.Set(particles);
            var managers = FindObjectsByType<ManagerBase>(FindObjectsSortMode.None).OrderBy(m => m.bootOrder).ToList();
            foreach (ManagerBase manager in managers) manager.Init();
            foreach (ManagerBase manager in managers) manager.Boot();
        }
    }
}
