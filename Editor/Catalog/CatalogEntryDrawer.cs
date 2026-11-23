using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;

namespace FireFlower
{
    /// <summary>
    /// Same as Expandable, but the label becomes [ID] name. Includes everything the entry has —
    /// [0], [Fire], or [0|Fire] for both. Expanding and drawing child fields is all done by base.
    /// </summary>
    [CustomPropertyDrawer(typeof(CatalogEntryAttribute))]
    public class CatalogEntryDrawer : ExpandablePropertyDrawer
    {
        protected override void OnGUI_Internal(
            Rect rect,
            SerializedProperty property,
            GUIContent label
        )
        {
            Object entry = property.objectReferenceValue;

            string id = entry is ICatalogItem item ? item.Id.ToString() : null;
            string key = entry is IKeyedCatalogItem keyed ? keyed.Key : null;

            string tag = string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key)
                ? id ?? key
                : $"{id}|{key}";

            if (!string.IsNullOrEmpty(tag))
                label = new GUIContent($"[{tag}] {entry.name}");

            base.OnGUI_Internal(rect, property, label);
        }
    }
}
