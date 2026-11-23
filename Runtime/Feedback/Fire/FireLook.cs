using UnityEngine;
using UnityEngine.Rendering;

namespace FireFlower
{
    // How heat fire (HeatField) flows, cools, is colored, and glows. Each fire keeps its own copy and just tweaks the values
    [CreateAssetMenu(fileName = "FireLook", menuName = "VFX/FireLook")]
    public class FireLook : ScriptableObject
    {
        [Header("Color")]
        [Tooltip("Hottest first. When heat passes the threshold at the same index in heatAbove, that color is used")]
        public Color[] palette =
        {
            new(1f, 1f, 0.9f), new(1f, 0.9f, 0.35f), new(1f, 0.55f, 0.1f), new(0.85f, 0.2f, 0.05f), new(0.35f, 0.08f, 0.05f),
        };
        public float[] heatAbove = { 0.82f, 0.64f, 0.44f, 0.25f, 0.1f };
        [Range(0f, 1f), Tooltip("Overall fire opacity. Fixed regardless of color")] public float opacity = 0.5f;

        [Header("Motion")]
        [Tooltip("Cooling per cell of flow. Higher means shorter flames")] public float cooling = 0.06f;
        [Min(0f), Tooltip("Extra cooling per cell away from the fire's source line. Higher makes edges die out faster")] public float edgeCooling = 0.02f;
        [Tooltip("Cooling noise density: along the flow, and perpendicular to it. A smaller flow value gives longer tongues of flame")] public Vector2 noiseScale = new(0.05f, 0.3f);
        [Range(0f, 1f), Tooltip("For sideways-flowing fire, cells that fully cooled heat rises per cell of flow. Hot heat barely rises")] public float lift = 0.6f;
        [Min(0f), Tooltip("Sideways sway strength while flowing. Higher makes the fire thicker and wavier")] public float turbulence = 2f;
        [Min(1f), Tooltip("Heat spread steps per second")] public float simFps = 45f;
        [Min(1f), Tooltip("Redraws per second. Holds each frame briefly, like hand-drawn animation")] public float drawFps = 12f;

        [Header("Glow")]
        [Tooltip("Material that multiplies color by brightness to draw brighter than 1")] public Material material;
        [Tooltip("Bloom volume applied while fire exists. Its threshold is above 1, so only areas the material draws brighter than 1 bloom")] public VolumeProfile bloom;
        [Min(0f)] public float bloomIn = 0.1f, bloomOut = 0.3f;
    }
}
