using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireFlower
{
    // URP chromatic aberration caps at 1, so this screen effect splits further. Intensity is a fraction of screen width
    [Serializable, VolumeComponentMenu("Escape Bin/RGB Split")]
    public sealed class RGBSplit : VolumeComponent
    {
        public MinFloatParameter amount = new(0f, 0f);

        static readonly int AmountId = Shader.PropertyToID("_RGBSplitAmount");

        public static void Apply()
        {
            var split = VolumeManager.instance.stack?.GetComponent<RGBSplit>();
            Shader.SetGlobalFloat(AmountId, split != null ? split.amount.value : 0f);
        }
    }
}
