using UnityEngine;

namespace FireFlower
{
    // Runs a block script (ScrapAsset) on this object. Starts from the top when enabled, stops when disabled (reverting what the blocks changed).
    // The blocks' (this) is this object
    [AddComponentMenu("FireFlower/Scrap Player")]
    public sealed class ScrapPlayer : MonoBehaviour
    {
        [SerializeField] ScrapAsset scrap;
        ScrapRunner runner;

        // Changing it restarts from the top while enabled
        public ScrapAsset Scrap
        {
            get => scrap;
            set
            {
                scrap = value;
                if (isActiveAndEnabled) Restart();
            }
        }

        public void Restart()
        {
            runner?.Stop();
            runner = scrap != null ? scrap.blocks.RunWith(this, this) : null;
        }

        void OnEnable() => Restart();
        void Update() => runner?.Tick();

        void OnDisable()
        {
            runner?.Stop();
            runner = null;
        }
    }
}
