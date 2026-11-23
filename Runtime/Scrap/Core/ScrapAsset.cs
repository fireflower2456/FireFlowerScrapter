using UnityEngine;

namespace FireFlower
{
    // Asset holding only a block script. Created and edited in the Scraper (Tools/FireFlower/Scraper)
    [CreateAssetMenu(menuName = "FireFlower/Scrap", fileName = "NewScrap")]
    public class ScrapAsset : ScriptableObject
    {
        public ScrapSet blocks = new();

        // self is the blocks' (this). It can be null. Stop the returned runner when done
        public ScrapRunner Run(Component self = null) => blocks.Run(self);
    }
}
