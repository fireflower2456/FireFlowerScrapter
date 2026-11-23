using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    public interface ICatalogItem
    {
        int Id { get; }
    }

    public interface IKeyedCatalogItem
    {
        string Key { get; }
    }

    /// <summary>
    /// Expandable with an [ID] name header. Drawer: CatalogEntryDrawer.
    /// </summary>
    public class CatalogEntryAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// SO catalog base (class). Lookup by both id and key;
    /// each catalog chooses what to index by with UseId/UseKey.
    /// </summary>
    public abstract class CatalogBase<T> : ScriptableObject
        where T : class
    {
        //CatalogEntry — shown in the Inspector with an [ID] name header and edited in place when expanded
        [SerializeField, CatalogEntry]
        protected List<T> items = new();

        protected virtual bool UseId => true;
        protected virtual bool UseKey => false;

        Dictionary<int, T> idMap = null;
        Dictionary<string, T> keyMap = null;
        public IReadOnlyList<T> Items => items;

        public T Get(int id) => TryGet(id, out var item) ? item : null;

        public bool TryGet(int id, out T item)
        {
            BuildMap();

            return idMap.TryGetValue(id, out item);
        }

        // The compiler can't catch typos, so print the key name when missing
        public T Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            BuildMap();
            if (keyMap.TryGetValue(key, out var item)) return item;

            Debug.LogWarning($"{name}: no entry '{key}'", this);
            return null;
        }

        public bool TryGet(string key, out T item)
        {
            BuildMap();

            if (string.IsNullOrEmpty(key))
            {
                item = null;
                return false;
            }
            return keyMap.TryGetValue(key, out item);
        }

        /// <summary>Sorts by the index key. Empty slots go to the end.</summary>
        void SortItems()
        {
            items.Sort((a, b) =>
            {
                if (a == null || b == null) return (a == null).CompareTo(b == null);

                if (UseId) return IdOf(a).CompareTo(IdOf(b));
                return string.CompareOrdinal(KeyOf(a), KeyOf(b));
            });
        }

        static int IdOf(T item)
        {
            return item is ICatalogItem byId ? byId.Id : int.MaxValue;
        }

        static string KeyOf(T item)
        {
            return item is IKeyedCatalogItem byKey ? byKey.Key : null;
        }

        // Touching the list in the Inspector keeps it sorted by the index key
        protected virtual void OnValidate()
        {
            SortItems();
            Refresh();
        }
        protected virtual void Awake()
        {
            Refresh();
        }

        void Refresh()
        {
            idMap = null;
            keyMap = null;
            BuildMap();
        }
        void BuildMap()
        {
            if (idMap != null)
                return;
            idMap = new Dictionary<int, T>();
            keyMap = new Dictionary<string, T>();
            foreach (var item in items)
            {
                // T has a class constraint, so == null is a reference compare — destroyed assets (fake null) are only caught by comparing as Object
                if (item == null || (item is Object obj && obj == null))
                {
                    Debug.LogWarning(
                        $"{typeof(T).Name} is null. Fill it in.",
                        this
                    );
                    continue;
                }

                if (UseId) AddById(item);
                if (UseKey) AddByKey(item);
            }
        }

        void AddById(T item)
        {
            if (item is not ICatalogItem byId)
            {
                Debug.LogError(
                    $"{typeof(T).Name} Catalog : entry without an id (not ICatalogItem)",
                    item as Object
                );
                return;
            }

            if (!idMap.TryAdd(byId.Id, item))
            {
                Debug.LogError(
                    $"{typeof(T).Name} Catalog : duplicate ID {byId.Id}",
                    item as Object
                );
            }
        }

        void AddByKey(T item)
        {
            if (item is not IKeyedCatalogItem byKey)
            {
                Debug.LogError(
                    $"{typeof(T).Name} Catalog : entry without a key (not IKeyedCatalogItem)",
                    item as Object
                );
                return;
            }

            if (string.IsNullOrEmpty(byKey.Key))
            {
                Debug.LogError(
                    $"{typeof(T).Name} Catalog : empty key",
                    item as Object
                );
                return;
            }

            if (!keyMap.TryAdd(byKey.Key, item))
            {
                Debug.LogError(
                    $"{typeof(T).Name} Catalog : duplicate key '{byKey.Key}'",
                    item as Object
                );
            }
        }
    }

    public abstract class ScriptableObjectCatalog<T> : CatalogBase<T>
        where T : ScriptableObject
    {
    }
}
