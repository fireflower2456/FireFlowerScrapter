using UnityEngine;

namespace FireFlower
{
    // Project layer constants. Take them from here instead of setting masks in each prefab's Inspector.
    // Name-based, so they don't break if the layer order changes in ProjectSettings.
    public static class Layers
    {
        public static readonly LayerMask Ground = LayerMask.GetMask("Ground");
        // Platforms you can jump up through. Only stood on; doesn't block bullets or sight
        public static readonly LayerMask Platform = LayerMask.GetMask("Platform");
        public static readonly LayerMask Floor = LayerMask.GetMask("Ground", "Platform");   // things you can stand on
        public static readonly LayerMask Player = LayerMask.GetMask("Player");
        public static readonly LayerMask Enemy = LayerMask.GetMask("Enemy");
        public static readonly LayerMask Object = LayerMask.GetMask("Object");
        // Layers only separate kinds (mob, object, bullet, terrain). Friend/foe is Team — mobs still use two layers, Player/Enemy, so they're grouped here
        public static readonly LayerMask Mob = LayerMask.GetMask("Player", "Enemy");
        public static readonly LayerMask Wall = LayerMask.GetMask("Ground", "Object");   // things that block sight

        // Instead of bit ops. Use as Layers.Ground.Has(go).
        public static bool Has(this LayerMask mask, GameObject go) => (mask.value & (1 << go.layer)) != 0;
    }
}
