using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // Counts seconds in 1 ms buckets for optimization
    public static class FixedWait
    {
        static readonly Dictionary<float, WaitForSeconds> Pool = new();

        public static WaitForSeconds Get(float seconds)
        {
            if (seconds < 0f)
                seconds = 0f;
            float key = Mathf.Round(seconds * 1000f) / 1000f;
            if (!Pool.TryGetValue(key, out var w))
            {
                w = new WaitForSeconds(key);
                Pool.Add(key, w);
            }
            return w;
        }
    }
}
