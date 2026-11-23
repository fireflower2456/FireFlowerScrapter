using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireFlower
{
    /// <summary>
    /// Object field labelled [ID] name that expands to edit the entry in place.
    /// [0], [Fire], or [0|Fire] for both, depending on what the entry has.
    /// </summary>
    [CustomPropertyDrawer(typeof(CatalogEntryAttribute))]
    public class CatalogEntryDrawer : PropertyDrawer
    {
        static float Line => EditorGUIUtility.singleLineHeight;
        static float Gap => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = Line;
            if (!property.isExpanded || property.objectReferenceValue == null) return height;
            var entry = new SerializedObject(property.objectReferenceValue);
            for (SerializedProperty child = FirstChild(entry); child != null; child = NextChild(child))
                height += Gap + EditorGUI.GetPropertyHeight(child, true);
            return height;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            label = Label(property, label);
            var line = new Rect(rect.x, rect.y, rect.width, Line);
            if (property.objectReferenceValue == null)
            {
                EditorGUI.PropertyField(line, property, label);
                return;
            }

            var value = new Rect(line.x + EditorGUIUtility.labelWidth + 2f, line.y, line.width - EditorGUIUtility.labelWidth - 2f, Line);
            EditorGUI.PropertyField(value, property, GUIContent.none);
            property.isExpanded = EditorGUI.Foldout(new Rect(line.x, line.y, EditorGUIUtility.labelWidth, Line), property.isExpanded, label, true);
            if (!property.isExpanded || property.objectReferenceValue == null) return;

            var entry = new SerializedObject(property.objectReferenceValue);
            entry.Update();
            EditorGUI.indentLevel++;
            float y = line.yMax;
            for (SerializedProperty child = FirstChild(entry); child != null; child = NextChild(child))
            {
                float height = EditorGUI.GetPropertyHeight(child, true);
                y += Gap;
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, height), child, true);
                y += height;
            }
            EditorGUI.indentLevel--;
            entry.ApplyModifiedProperties();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            var field = new PropertyField(property, Label(property, new GUIContent(property.displayName)).text);
            var details = new Foldout { text = "Details", value = property.isExpanded };
            details.style.marginLeft = 15f;
            details.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != details) return;
                property.isExpanded = evt.newValue;
                property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            });
            root.Add(field);
            root.Add(details);

            void Rebuild(SerializedProperty changed)
            {
                field.label = Label(changed, new GUIContent(changed.displayName)).text;
                details.Clear();
                Object entry = changed.objectReferenceValue;
                details.style.display = entry != null ? DisplayStyle.Flex : DisplayStyle.None;
                if (entry != null) details.Add(new InspectorElement(entry));
            }
            Rebuild(property);
            root.TrackPropertyValue(property, Rebuild);
            return root;
        }

        static GUIContent Label(SerializedProperty property, GUIContent fallback)
        {
            Object entry = property.objectReferenceValue;
            string id = entry is ICatalogItem item ? item.Id.ToString() : null;
            string key = entry is IKeyedCatalogItem keyed ? keyed.Key : null;
            string tag = string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key) ? id ?? key : $"{id}|{key}";
            return string.IsNullOrEmpty(tag) ? fallback : new GUIContent($"[{tag}] {entry.name}");
        }

        static SerializedProperty FirstChild(SerializedObject entry)
        {
            SerializedProperty it = entry.GetIterator();
            return it.NextVisible(true) ? Skip(it) : null;
        }

        static SerializedProperty NextChild(SerializedProperty it) => it.NextVisible(false) ? Skip(it) : null;

        static SerializedProperty Skip(SerializedProperty it) =>
            it.propertyPath == "m_Script" ? NextChild(it) : it;
    }
}
