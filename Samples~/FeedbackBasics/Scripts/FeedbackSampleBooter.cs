using System.Linq;
using UnityEngine;
using FireFlower;

namespace FireFlower.Samples
{
    // Sample scene boot: runs Init → Boot on the scene's managers in bootOrder. A real game uses its own boot order
    public class FeedbackSampleBooter : MonoBehaviour
    {
        void Awake()
        {
            var managers = FindObjectsByType<ManagerBase>(FindObjectsSortMode.None).OrderBy(m => m.bootOrder).ToList();
            foreach (ManagerBase manager in managers) manager.Init();
            foreach (ManagerBase manager in managers) manager.Boot();
        }
    }
}
