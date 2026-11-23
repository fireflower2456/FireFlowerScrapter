using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireFlower
{
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }
            Rect button = position;
            button.xMin += EditorGUIUtility.labelWidth + EditorGUIUtility.standardVerticalSpacing;
            button.height = EditorGUIUtility.singleLineHeight;
            // Drawn before the field so the click reaches the button, not the foldout under it
            if (EditorGUI.DropdownButton(button, new GUIContent(TypeLabel(property)), FocusType.Keyboard))
                ShowMenu(button, property.serializedObject, property.propertyPath);
            EditorGUI.PropertyField(position, property, label, true);
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var field = new PropertyField(property);
            if (property.propertyType != SerializedPropertyType.ManagedReference) return field;

            var root = new VisualElement();
            root.Add(field);
            var button = new Button { text = TypeLabel(property) };
            button.style.position = Position.Absolute;
            button.style.top = 0f;
            button.style.right = 0f;
            button.style.left = Length.Percent(40f);
            button.style.height = EditorGUIUtility.singleLineHeight;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;
            button.clicked += () => ShowMenu(button.worldBound, serializedObject, path);
            root.TrackPropertyValue(property, changed => button.text = TypeLabel(changed));
            root.Add(button);
            return root;
        }

        static string TypeLabel(SerializedProperty property)
        {
            string full = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full)) return "(None)";
            int cut = Math.Max(full.LastIndexOf('.'), Math.Max(full.LastIndexOf('/'), full.LastIndexOf(' ')));
            return full.Substring(cut + 1);
        }

        static void ShowMenu(Rect rect, SerializedObject serializedObject, string path)
        {
            SerializedProperty property = serializedObject.FindProperty(path);
            Type baseType = property != null ? FieldType(property) : null;
            if (baseType == null) return;
            new TypeDropdown(baseType, type => Assign(serializedObject, path, type)).Show(rect);
        }

        // Each target gets its own instance; assigning through a multi-object property would share one
        static void Assign(SerializedObject serializedObject, string path, Type type)
        {
            foreach (UnityEngine.Object target in serializedObject.targetObjects)
            {
                var single = new SerializedObject(target);
                SerializedProperty property = single.FindProperty(path);
                if (property == null) continue;
                property.managedReferenceValue = type == null ? null : Create(type);
                property.isExpanded = type != null;
                single.ApplyModifiedProperties();
            }
            serializedObject.Update();
        }

        static object Create(Type type) =>
            type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null
                ? Activator.CreateInstance(type, true)
                : System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);

        // managedReferenceFieldTypename is "Assembly Namespace.Type" (nested types use '/')
        static Type FieldType(SerializedProperty property)
        {
            string name = property.managedReferenceFieldTypename;
            int space = name.IndexOf(' ');
            if (space < 0) return null;
            return Type.GetType($"{name.Substring(space + 1).Replace('/', '+')}, {name.Substring(0, space)}");
        }

        static IEnumerable<Type> Candidates(Type baseType) =>
            TypeCache.GetTypesDerivedFrom(baseType).Append(baseType)
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters
                            && !typeof(UnityEngine.Object).IsAssignableFrom(t)
                            && t.IsDefined(typeof(SerializableAttribute), false))
                .Distinct()
                .OrderBy(t => t.Name, StringComparer.Ordinal);

        sealed class TypeDropdown : AdvancedDropdown
        {
            readonly Type baseType;
            readonly Action<Type> picked;

            public TypeDropdown(Type baseType, Action<Type> picked) : base(new AdvancedDropdownState())
            {
                this.baseType = baseType;
                this.picked = picked;
                minimumSize = new Vector2(220f, 300f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(baseType.Name);
                root.AddChild(new TypeItem(null));
                foreach (Type type in Candidates(baseType)) root.AddChild(new TypeItem(type));
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item) => picked(((TypeItem)item).type);
        }

        sealed class TypeItem : AdvancedDropdownItem
        {
            public readonly Type type;
            public TypeItem(Type type) : base(type == null ? "(None)" : type.Name) => this.type = type;
        }
    }
}
