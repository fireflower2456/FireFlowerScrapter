using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FireFlower
{
    // Handles any CatalogBase<T> the same way. Edits the serialized "items" field directly
    public static class CatalogEditing
    {
        static List<ScriptableObject> cache;
        static CatalogEditing() => EditorApplication.projectChanged += () => cache = null;

        // Every catalog asset in the project. Re-scanned when the project changes
        public static List<ScriptableObject> All() => cache ??= TypeCache.GetTypesDerivedFrom<ScriptableObject>()
            .Where(t => !t.IsAbstract && !t.IsGenericType && ItemType(t) != null)
            .SelectMany(t => AssetDatabase.FindAssets($"t:{t.Name}").Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(g))))
            .Where(o => o != null)
            .Distinct()
            .OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // The T of CatalogBase<T>. null if not a catalog
        public static Type ItemType(Type catalogType)
        {
            for (Type t = catalogType; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(CatalogBase<>)) return t.GetGenericArguments()[0];
            return null;
        }

        public static List<UnityEngine.Object> Items(ScriptableObject catalog)
        {
            SerializedProperty items = new SerializedObject(catalog).FindProperty("items");
            return Enumerable.Range(0, items.arraySize).Select(i => items.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
        }

        // Catalogs that can hold this asset
        public static List<ScriptableObject> For(UnityEngine.Object asset) =>
            asset == null ? new List<ScriptableObject>() : All().Where(c => ItemType(c.GetType()).IsInstanceOfType(asset)).ToList();

        // All assets of the catalog's entry type (listed or not)
        public static List<UnityEngine.Object> Candidates(ScriptableObject catalog)
        {
            Type type = ItemType(catalog.GetType());
            return AssetDatabase.FindAssets($"t:{type.Name}")
                .Select(g => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(g), type))
                .Where(o => o != null)
                .ToList();
        }

        // Creates a new asset in the folder holding most assets of that type, sets the next Id and the name as Key, and adds it to every catalog that can hold it.
        // Reusing a deleted Id would make old saves point at the wrong entry, so gaps are never filled: it takes the highest Id + 1
        public static ScriptableObject Create(Type type, string baseName, string fallbackFolder)
        {
            List<UnityEngine.Object> same = AssetDatabase.FindAssets($"t:{type.Name}")
                .Select(g => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(g), type))
                .Where(o => o != null && o.GetType() == type)
                .ToList();
            string folder = same
                .Select(o => System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(o))?.Replace('\\', '/'))
                .Where(f => !string.IsNullOrEmpty(f))
                .GroupBy(f => f).OrderByDescending(g => g.Count()).Select(g => g.Key)
                .FirstOrDefault() ?? fallbackFolder;
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.asset");

            var data = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(data, path);
            var so = new SerializedObject(data);
            if (so.FindProperty("id") is SerializedProperty id)
                id.intValue = same.OfType<ICatalogItem>().Select(o => o.Id).DefaultIfEmpty(0).Max() + 1;
            if (so.FindProperty("key") is SerializedProperty key) key.stringValue = System.IO.Path.GetFileNameWithoutExtension(path);
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (ScriptableObject catalog in For(data)) Add(catalog, data);
            AssetDatabase.SaveAssets();
            return data;
        }

        public enum Order { Id, Key, Name }

        // Empty slots go last
        public static void Sort(ScriptableObject catalog, Order order) => Edit(catalog, list =>
        {
            IEnumerable<UnityEngine.Object> present = list.Where(o => o != null);
            var sorted = (order == Order.Id
                ? present.OrderBy(o => o is ICatalogItem item ? item.Id : int.MaxValue)
                : present.OrderBy(o => order == Order.Key && o is IKeyedCatalogItem keyed ? keyed.Key ?? "" : o.name, StringComparer.OrdinalIgnoreCase)).ToList();
            sorted.AddRange(list.Where(o => o == null));
            list.Clear();
            list.AddRange(sorted);
        });

        public static bool ConfirmDelete(UnityEngine.Object asset) =>
            EditorUtility.DisplayDialog("Delete", $"Delete {asset.name}?\nIt will be removed from the catalogs and the asset file moved to the trash.", "Delete", "Cancel");

        // Removes it from every catalog and moves the asset to the trash. Also deletes localized strings only that asset used
        public static void Delete(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            foreach (ScriptableObject catalog in For(asset)) Remove(catalog, asset);
            LocalizationFollowsDelete.MoveAssetToTrash(path);
        }

        public static void RemoveEmpty(ScriptableObject catalog) => Edit(catalog, list => list.RemoveAll(o => o == null));

        public static void Add(ScriptableObject catalog, UnityEngine.Object asset) => Edit(catalog, list => { if (!list.Contains(asset)) list.Add(asset); });
        public static void Remove(ScriptableObject catalog, UnityEngine.Object asset) => Edit(catalog, list => list.RemoveAll(o => o == asset));

        static void Edit(ScriptableObject catalog, Action<List<UnityEngine.Object>> edit)
        {
            var so = new SerializedObject(catalog);
            SerializedProperty items = so.FindProperty("items");
            var list = Enumerable.Range(0, items.arraySize).Select(i => items.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
            edit(list);
            items.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        // Name shown in the list: [Id|Key] asset name (same rule as CatalogEntryDrawer)
        public static string Label(UnityEngine.Object entry)
        {
            if (entry == null) return "(empty)";
            string id = entry is ICatalogItem item ? item.Id.ToString() : null;
            string key = entry is IKeyedCatalogItem keyed && !string.IsNullOrEmpty(keyed.Key) ? keyed.Key : null;
            string tag = id != null && key != null ? $"{id}|{key}" : id ?? key;
            return tag != null ? $"[{tag}] {entry.name}" : entry.name;
        }
    }
}
