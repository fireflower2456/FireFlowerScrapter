using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace FireFlower
{
    // Deletes the localized strings an asset used only when the asset is deleted through an editor (catalog info, etc.). Deleting it in the Project window leaves the strings.
    // Collects the strings before deleting and, after deleting, removes only those no longer used anywhere in the project — strings shared elsewhere stay.
    // The block editor also hands over the strings it handled after save/discard
    public static class LocalizationFollowsDelete
    {
        [System.Serializable]
        public struct TextRef
        {
            public string table;
            public long id;
            public TextRef(string table, long id) { this.table = table; this.id = id; }
        }

        static readonly HashSet<(StringTableCollection table, long id)> pending = new();
        static bool scheduled;

        // Assets go to the trash (recoverable from the OS trash)
        public static void MoveAssetToTrash(string path)
        {
            IEnumerable<string> paths = AssetDatabase.IsValidFolder(path)
                ? AssetDatabase.FindAssets("", new[] { path }).Select(AssetDatabase.GUIDToAssetPath)
                : new[] { path };
            foreach (string assetPath in paths)
                Collect(assetPath);
            AssetDatabase.MoveAssetToTrash(path);

            if (pending.Count > 0 && !scheduled)
            {
                scheduled = true;
                EditorApplication.delayCall += RemoveUnused;
            }
        }

        // Only looks at what is saved to files — the caller must finish saving (or discarding) first
        public static void RemoveIfUnused(IEnumerable<TextRef> texts)
        {
            foreach (TextRef text in texts)
                if (LocalizationEditorSettings.GetStringTableCollection(text.table) is { } table) pending.Add((table, text.id));
            if (pending.Count == 0 || scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += RemoveUnused;
        }

        static void Collect(string path)
        {
            if (path.EndsWith(".unity")) return;

            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj == null) continue;
                SerializedProperty it = new SerializedObject(obj).GetIterator();
                while (it.Next(true))
                {
                    if (it.type != nameof(LocalizedString)) continue;

                    string tableName = it.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue;
                    if (string.IsNullOrEmpty(tableName)) continue;
                    // Stored in assets as "GUID:..."
                    TableReference reference = tableName.StartsWith("GUID:") ? System.Guid.Parse(tableName.Substring(5)) : tableName;
                    StringTableCollection table = LocalizationEditorSettings.GetStringTableCollection(reference);
                    if (table == null) continue;

                    long id = it.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue;
                    if (id == 0) id = table.SharedData.GetId(it.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue);
                    if (id != 0) pending.Add((table, id));
                }
            }
        }

        static void RemoveUnused()
        {
            scheduled = false;
            List<string> files = Directory.EnumerateFiles("Assets", "*.*", SearchOption.AllDirectories).ToList();
            string[] yaml = files.Where(f => f.EndsWith(".asset") || f.EndsWith(".prefab") || f.EndsWith(".unity")).Select(File.ReadAllText).ToArray();
            string[] code = files.Where(f => f.EndsWith(".cs")).Select(File.ReadAllText).ToArray();

            foreach (var group in pending.GroupBy(p => p.table))
            {
                StringTableCollection table = group.Key;
                string guidRef = "GUID:" + table.SharedData.TableCollectionNameGuid.ToString("N");
                string nameRef = "m_TableCollectionName: " + table.TableCollectionName;
                var removed = new List<string>();

                foreach ((_, long id) in group)
                {
                    var entry = table.SharedData.GetEntry(id);
                    if (entry == null) continue;

                    // Match to the end of the line so one number doesn't match the prefix of another
                    string idLine = "m_KeyId: " + id;
                    bool used = yaml.Any(t => (t.Contains(idLine + "\n") || t.Contains(idLine + "\r")) && (t.Contains(guidRef) || t.Contains(nameRef)))
                        || code.Any(t => t.Contains($"\"{entry.Key}\""));
                    if (used) continue;

                    // So the strings come back if a deleted block is restored after saving
                    if (removed.Count == 0) Undo.RecordObjects(table.StringTables.Cast<Object>().Append(table.SharedData).ToArray(), "Remove Unused Localized Text");
                    table.RemoveEntry(id);
                    removed.Add(entry.Key);
                }

                if (removed.Count == 0) continue;
                EditorUtility.SetDirty(table.SharedData);
                foreach (var stringTable in table.StringTables) EditorUtility.SetDirty(stringTable);
                Debug.Log($"[{table.TableCollectionName}] Removed unused localized strings: {string.Join(", ", removed)}");
            }

            pending.Clear();
            AssetDatabase.SaveAssets();
        }
    }
}
