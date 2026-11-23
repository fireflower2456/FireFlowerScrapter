using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    /// <summary>
    /// Runtime object pool keyed by prefab.
    /// No pre-registration needed: a prefab's pool is created on its first Get.
    /// Call Clear to drop everything when no longer needed.
    /// </summary>
    public static class ObjectPoolExtention
    {
        class Pool
        {
            public Transform root;          // Parent holding idle instances
            public int maxSize;
            public bool disposed;           // Disposed pool: returned instances are just destroyed
            public readonly Stack<Component> idle = new();
        }

        // Which pool an instance belongs to + its cached component, so Release skips GetComponent.
        struct Entry
        {
            public Pool pool;
            public Component comp;
        }

        const int DefaultMaxSize = 64;

        static readonly Dictionary<Component, Pool> pools = new();          // prefab -> pool
        static readonly Dictionary<System.Type, Pool> typePools = new();    // prefab-less pools (afterimages etc.)
        static readonly Dictionary<GameObject, Entry> owner = new();        // instance -> owner

        static Transform root;

        // With domain reload disabled, static state survives from the previous play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pools.Clear();
            typePools.Clear();
            owner.Clear();
            root = null;
        }

        static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject("ObjectPool");
                    Object.DontDestroyOnLoad(go);
                    root = go.transform;
                }
                return root;
            }
        }

        // ------------------------
        // Get
        // ------------------------

        /// <summary>Takes one from the prefab's pool. Creates the pool if missing and instantiates if empty.</summary>
        public static T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            Pool pool = GetOrCreate(prefab);
            Transform p = parent != null ? parent : pool.root;

            T inst = PopIdle<T>(pool);

            if (inst == null)
            {
                inst = Object.Instantiate(prefab, p);
                owner[inst.gameObject] = new Entry { pool = pool, comp = inst };
            }

            Transform t = inst.transform;
            t.SetParent(p, false);
            t.SetPositionAndRotation(position, rotation);
            inst.gameObject.SetActive(true);
            return inst;
        }

        /// <summary>For GameObject prefabs, when the component type is unknown (particles etc.).</summary>
        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            => Get(prefab.transform, position, rotation, parent).gameObject;

        /// <summary>
        /// Pool for prefab-less objects (built in code, like afterimages). One pool per type.
        /// When empty, creates one by adding T to a new GameObject.
        /// </summary>
        public static T Get<T>() where T : Component
        {
            Pool pool = GetOrCreateByType<T>();

            T inst = PopIdle<T>(pool);

            if (inst == null)
            {
                inst = new GameObject(typeof(T).Name).AddComponent<T>();
                inst.transform.SetParent(pool.root, false);
                owner[inst.gameObject] = new Entry { pool = pool, comp = inst };
            }

            inst.gameObject.SetActive(true);
            return inst;
        }

        // ------------------------
        // Release
        // ------------------------

        /// <summary>Returns to the pool. Non-pooled objects are destroyed, so this is always safe to call instead of Destroy.</summary>
        public static void Release(GameObject instance)
        {
            if (instance == null) return;

            // Returning an already-returned instance would duplicate it on the stack (e.g. a bullet hit twice in one frame)
            if (!instance.activeSelf) return;

            if (!owner.TryGetValue(instance, out Entry entry))
            {
                Object.Destroy(instance);
                return;
            }

            instance.SetActive(false);

            // Not reused if the pool is disposed or over its capacity
            if (entry.pool.disposed || entry.pool.idle.Count >= entry.pool.maxSize)
            {
                owner.Remove(instance);
                Object.Destroy(instance);
                return;
            }

            instance.transform.SetParent(entry.pool.root, false);
            entry.pool.idle.Push(entry.comp);
        }

        // ------------------------
        // Cleanup
        // ------------------------

        /// <summary>Disposes all pools. Call on stage transitions etc.</summary>
        public static void Clear()
        {
            foreach (var pool in pools.Values) Dispose(pool);
            foreach (var pool in typePools.Values) Dispose(pool);

            pools.Clear();
            typePools.Clear();
            owner.Clear();
        }

        static void Dispose(Pool pool)
        {
            pool.disposed = true;

            foreach (var idle in pool.idle)
                if (idle != null)
                {
                    owner.Remove(idle.gameObject);
                    Object.Destroy(idle.gameObject);
                }

            pool.idle.Clear();
            if (pool.root != null) Object.Destroy(pool.root.gameObject);
        }

        // Skip instances destroyed by scene changes etc. that may remain on the stack
        static T PopIdle<T>(Pool pool) where T : Component
        {
            T inst = null;
            while (inst == null && pool.idle.Count > 0)
                inst = pool.idle.Pop() as T;
            return inst;
        }

        static Pool GetOrCreate<T>(T prefab) where T : Component
        {
            if (pools.TryGetValue(prefab, out Pool pool))
                return pool;

            pool = NewPool(prefab.name);
            pools[prefab] = pool;
            return pool;
        }

        static Pool GetOrCreateByType<T>() where T : Component
        {
            if (typePools.TryGetValue(typeof(T), out Pool pool))
                return pool;

            pool = NewPool(typeof(T).Name);
            typePools[typeof(T)] = pool;
            return pool;
        }

        static Pool NewPool(string name)
        {
            var go = new GameObject($"Pool ({name})");
            go.transform.SetParent(Root, false);
            return new Pool { root = go.transform, maxSize = DefaultMaxSize };
        }
    }
}
