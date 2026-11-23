using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using FireFlower;

namespace FireFlower
{
    // Lets editor text fields read and write Localization tables directly, and creates/links a table entry per field
    public static class LocalizedText
    {
        // Language the source text is written in. Separate from the runtime default language (en)
        public const string SourceLocale = "ko-KR";

        // If empty or its entry was deleted (e.g. unsaved removal), link anew with key; if it points to another key in the same table, rename it to key
        public static bool Link(LocalizedString text, string table, string key)
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(table);
            if (text == null || collection == null) return false;
            SharedTableData shared = collection.SharedData;
            SharedTableData.SharedTableEntry linked = Entry(text, out StringTableCollection current);

            // Pointing at another table means it was chosen on purpose
            Undo.RecordObject(shared, "Link Localized Text");
            if (linked == null) text.SetReference(collection.TableCollectionNameReference, (shared.GetEntry(key) ?? shared.AddKey(key)).Id);
            else if (current == collection && linked.Key != key && !shared.Contains(key)) shared.RenameKey(linked.Id, key);
            else return false;
            EditorUtility.SetDirty(shared);
            return true;
        }

        // Entry the field points to. null if empty or deleted
        public static SharedTableData.SharedTableEntry Entry(LocalizedString text, out StringTableCollection collection)
        {
            collection = text == null || text.IsEmpty ? null : LocalizationEditorSettings.GetStringTableCollection(text.TableReference);
            return collection?.SharedData.GetEntryFromReference(text.TableEntryReference);
        }

        // Reads and writes the text without a runtime lookup
        public static string Read(LocalizedString text, string locale = SourceLocale)
            => Entry(text, out StringTableCollection collection) is { } entry && collection.GetTable(locale) is StringTable table
                ? table.GetEntry(entry.Id)?.Value ?? string.Empty
                : string.Empty;

        public static void Write(LocalizedString text, string value, string locale)
        {
            if (Entry(text, out StringTableCollection collection) is not { } entry || collection.GetTable(locale) is not StringTable table) return;
            Undo.RecordObject(table, "Edit Localized Text");
            table.AddEntry(entry.Id, value);
            EditorUtility.SetDirty(table);
        }

        // Text before editing. Restored if the editor discards without saving
        [Serializable]
        public struct Original
        {
            public string table;
            public long id;
            public string locale;
            public bool existed;
            public string value;

            public bool Same(Original other) => table == other.table && id == other.id && locale == other.locale;
        }

        public static Original? Capture(LocalizedString text, string locale)
        {
            if (Entry(text, out StringTableCollection collection) is not { } entry || collection.GetTable(locale) is not StringTable table) return null;
            StringTableEntry current = table.GetEntry(entry.Id);
            return new Original { table = collection.TableCollectionName, id = entry.Id, locale = locale, existed = current != null, value = current?.Value };
        }

        public static void Restore(Original original)
        {
            if (LocalizationEditorSettings.GetStringTableCollection(original.table)?.GetTable(original.locale) is not StringTable table) return;
            if (original.existed) table.AddEntry(original.id, original.value);
            else table.RemoveEntry(original.id);
            EditorUtility.SetDirty(table);
        }

        // Visits every text field in the object. picked = field chosen from a list ([Speaker]), inBlock = inside an effect block
        public static void Each(object root, Action<LocalizedString, bool, bool> visit, HashSet<object> visited = null)
        {
            visited ??= new HashSet<object>(FieldPath.ReferenceComparer.Instance);
            if (root is UnityEngine.Object) Fields(root, visit, visited, false);
            else Walk(root, visit, visited, false);
        }

        static void Walk(object node, Action<LocalizedString, bool, bool> visit, HashSet<object> visited, bool inBlock)
        {
            if (node == null || node is string || node is UnityEngine.Object || node is Delegate) return;
            Type type = node.GetType();
            if (type.IsPrimitive || type.IsEnum || type.IsPointer) return;
            if (!type.IsValueType && !visited.Add(node)) return;
            if (node is IList list)
            {
                foreach (object element in list) Walk(element, visit, visited, inBlock);
                return;
            }
            Fields(node, visit, visited, inBlock || node is ScrapPart);
        }

        static void Fields(object node, Action<LocalizedString, bool, bool> visit, HashSet<object> visited, bool inBlock)
        {
            for (Type t = node.GetType(); t != null && t != typeof(object) && t != typeof(UnityEngine.ScriptableObject) && t != typeof(UnityEngine.MonoBehaviour); t = t.BaseType)
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType.IsPointer) continue;
                    object value = field.GetValue(node);
                    if (value is LocalizedString text)
                    {
                        if (!text.IsEmpty) visit(text, field.IsDefined(typeof(ScrapWeaver.SpeakerAttribute), true), inBlock);
                    }
                    else Walk(value, visit, visited, inBlock);
                }
        }

        // Copies the text to a new key in every language and points only this field at it. Keys come from nextKey until a free one appears
        public static void Fork(LocalizedString text, System.Func<string> nextKey)
        {
            if (Entry(text, out StringTableCollection collection) is not { } entry) return;
            SharedTableData shared = collection.SharedData;
            Undo.RecordObjects(collection.StringTables.Cast<UnityEngine.Object>().Append(shared).ToArray(), "Copy Localized Text");
            string key;
            do key = nextKey(); while (shared.Contains(key));
            long id = shared.AddKey(key).Id;
            foreach (StringTable table in collection.StringTables)
            {
                if (table.GetEntry(entry.Id) is { } source) table.AddEntry(id, source.Value).IsSmart = source.IsSmart;
                EditorUtility.SetDirty(table);
            }
            text.SetReference(collection.TableCollectionNameReference, id);
            EditorUtility.SetDirty(shared);
        }

        // Removing a field also deletes its entry in every language. Undo restores it
        public static void Remove(LocalizedString text)
        {
            if (Entry(text, out StringTableCollection collection) is not { } entry) return;
            Undo.RecordObjects(collection.StringTables.Cast<UnityEngine.Object>().Append(collection.SharedData).ToArray(), "Remove Localized Text");
            collection.RemoveEntry(entry.Id);
            EditorUtility.SetDirty(collection.SharedData);
            foreach (StringTable table in collection.StringTables) EditorUtility.SetDirty(table);
        }
    }
}
