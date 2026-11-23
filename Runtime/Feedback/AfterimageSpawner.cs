using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    [Serializable]
    public class AfterimageSettings
    {
        [Tooltip("Solid tint of the afterimage.")]
        [SerializeField] Color tint = Color.cyan;
        [Tooltip("Afterimage material. If empty, uses the source sprite's material at start.")]
        [SerializeField] Material material;
        [Tooltip("Alpha at spawn (0-1). Multiplied by the source's transparency.")]
        [SerializeField, Range(0f, 1f)] float startAlpha = 0.5f;
        [Tooltip("Fade time of one afterimage (seconds).")]
        [SerializeField, Min(0.01f)] float lifetime = 0.3f;
        [Tooltip("Spawn interval (seconds).")]
        [SerializeField, Min(0.001f)] float spawnInterval = 0.04f;
        [Tooltip("If above 0, spawns every this distance instead of by time. Keeps spacing even during fast moves like dashes.")]
        [SerializeField, Min(0f)] float spacing;
        [Tooltip("Sorting order offset relative to the source.")]
        [SerializeField] int sortingOrderOffset = -1;

        public Color Tint => tint;
        public Material Material => material;
        public float StartAlpha => startAlpha;
        public float Lifetime => lifetime;
        public float SpawnInterval => spawnInterval;
        public float Spacing => spacing;
        public int SortingOrderOffset => sortingOrderOffset;

    }

    public static class AfterimageSpawner
    {
        sealed class Emission
        {
            public AfterimageSettings settings;
            public SpriteRenderer source;
            public Material material;
            public float timer;
            public float spacing;
            public float remainder;
            public Vector3 previousPosition;
        }

        static readonly Dictionary<GameObject, Emission> emissions = new();
        static readonly List<GameObject> expired = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            emissions.Clear();
            expired.Clear();
        }

        public static void Begin(GameObject target, AfterimageSettings settings)
        {
            if (target == null || !target.activeInHierarchy || settings == null) return;
            if (!emissions.TryGetValue(target, out var emission))
            {
                if (!target.TryGetComponent<SpriteRenderer>(out var source)) return;
                emission = new Emission { source = source };
                emissions.Add(target, emission);
            }

            emission.settings = settings;
            emission.material = settings.Material != null ? settings.Material : emission.source.sharedMaterial;
            emission.timer = 0f;
            emission.spacing = settings.Spacing;
            emission.remainder = 0f;
            emission.previousPosition = target.transform.position;
            SpawnGhost(emission, emission.previousPosition);
        }

        public static void EndOn(GameObject target)
        {
            if (target != null) emissions.Remove(target);
        }

        public static void Tick(float deltaTime)
        {
            foreach (var pair in emissions)
            {
                var emission = pair.Value;
                if (pair.Key == null || emission.source == null || !pair.Key.activeInHierarchy)
                {
                    expired.Add(pair.Key);
                    continue;
                }

                Vector3 now = emission.source.transform.position;
                if (emission.spacing > 0f) EmitByDistance(emission, now);
                else EmitByTime(emission, deltaTime, now);
                emission.previousPosition = now;
            }
            foreach (var target in expired) emissions.Remove(target);
            expired.Clear();
        }

        static void EmitByDistance(Emission emission, Vector3 now)
        {
            Vector3 delta = now - emission.previousPosition;
            float distance = delta.magnitude;
            if (distance <= 0f) return;

            Vector3 direction = delta / distance;
            float nextDistance = emission.spacing - emission.remainder;
            while (nextDistance <= distance)
            {
                SpawnGhost(emission, emission.previousPosition + direction * nextDistance);
                nextDistance += emission.spacing;
            }
            emission.remainder = distance - (nextDistance - emission.spacing);
        }

        static void EmitByTime(Emission emission, float deltaTime, Vector3 now)
        {
            emission.timer += deltaTime;
            float interval = Mathf.Max(0.001f, emission.settings.SpawnInterval);

            while (emission.timer >= interval)
            {
                emission.timer -= interval;
                float t = deltaTime > 0f ? 1f - emission.timer / deltaTime : 1f;
                SpawnGhost(emission, Vector3.Lerp(emission.previousPosition, now, t));
            }
        }

        static void SpawnGhost(Emission emission, Vector3 position)
        {
            if (emission.source == null || emission.source.sprite == null) return;
            var settings = emission.settings;
            AfterimageGhost.Get().Play(emission.source, emission.material, settings.Tint,
                settings.StartAlpha * emission.source.color.a, settings.Lifetime, settings.SortingOrderOffset, position);
        }
    }
}
