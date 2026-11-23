using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireFlower
{
    // [EnumName] field: type "Type.Value" or pick with ▾. Unknown values turn red.
    // If a sibling socket of the same block is a dot path ending in an enum (e.g. (…) . bodyType), only that enum's values are offered. Otherwise only enums used by component members
    [CustomPropertyDrawer(typeof(ScrapWeaver.EnumNameAttribute))]
    sealed class EnumNameDrawer : PropertyDrawer
    {
        static readonly Color Invalid = new(1f, 0.45f, 0.45f);
        // The value of a SerializeReference field. Null for lists and plain fields (reading those throws)
        static object RefOf(SerializedProperty p) => p != null && p.propertyType == SerializedPropertyType.ManagedReference ? p.managedReferenceValue : null;
        static readonly Type[] Frequent = { typeof(RigidbodyType2D), typeof(ForceMode2D), typeof(RigidbodyInterpolation2D) };
        static List<Type> componentEnums;

        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            SerializedObject so = prop.serializedObject;
            string propertyPath = prop.propertyPath;
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var text = Ui.With(new TextField { style = { minWidth = 120 } }, "pick-text");
            text.BindProperty(prop);
            text.textEdition.placeholder = "Type.Value";
            var pick = Ui.With(new Button { text = "▾", style = { marginLeft = 2 } }, "unity-base-popup-field__input");
            row.Add(text);
            row.Add(pick);

            void Refresh()
            {
                bool ok = MemberPath.TryEnum(text.value, out _);
                (text.Q(className: "unity-text-field__input") ?? text).style.color = ok || string.IsNullOrEmpty(text.value) ? new StyleColor(StyleKeyword.Null) : new StyleColor(Invalid);
            }

            void Set(string value)
            {
                so.Update();
                so.FindProperty(propertyPath).stringValue = value;
                so.ApplyModifiedProperties();
            }

            void AddValues(GenericMenu menu, Type type, string folder)
            {
                foreach (string name in Enum.GetNames(type))
                    menu.AddItem(new GUIContent(folder + name), $"{type.Name}.{name}" == text.value, () => Set($"{type.Name}.{name}"));
            }

            void Open()
            {
                var menu = new GenericMenu();
                if (ContextType(so, propertyPath) is Type context) AddValues(menu, context, "");
                else
                {
                    foreach (Type t in Frequent) AddValues(menu, t, t.Name + "/");
                    menu.AddSeparator("");
                    foreach (Type t in ComponentEnums().Where(t => Array.IndexOf(Frequent, t) < 0)) AddValues(menu, t, Group(t) + t.Name + "/");
                }
                menu.DropDown(text.worldBound);
            }

            pick.clicked += Open;
            text.RegisterValueChangedCallback(_ => Refresh());
            row.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            Refresh();
            return row;
        }

        // In the block holding this enum value, the enum a sibling dot path ends in
        static Type ContextType(SerializedObject so, string propertyPath)
        {
            int dot = propertyPath.LastIndexOf('.');
            if (dot < 0) return null;
            string valuePath = propertyPath[..dot];
            string ownerPath = valuePath;
            do
            {
                int cut = ownerPath.LastIndexOf('.');
                if (cut < 0) return null;
                ownerPath = ownerPath[..cut];
            } while (ownerPath.EndsWith(".Array") || ownerPath.EndsWith("Array"));
            object mine = RefOf(so.FindProperty(valuePath));
            if (RefOf(so.FindProperty(ownerPath)) is not object owner) return null;
            for (Type t = owner.GetType(); t != null && t != typeof(object); t = t.BaseType)
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (f.GetValue(owner) is MemberValue member && !ReferenceEquals(member, mine) && member.EndType is { IsEnum: true } type)
                        return type;
            return null;
        }

        // Enums used by component members: the ones a dot block can reach
        static List<Type> ComponentEnums() => componentEnums ??= MemberPath.Components.Values
            .SelectMany(MemberPath.Members).Select(MemberPath.TypeOf).Where(t => t.IsEnum && (t.IsPublic || t.IsNestedPublic))
            .Distinct().OrderBy(t => t.Name).ToList();

        static string Group(Type type)
        {
            if (string.IsNullOrEmpty(type.Namespace)) return "Game/";
            int dot = type.Namespace.IndexOf('.');
            return dot < 0 ? type.Namespace + "/" : $"{type.Namespace[..dot]}/";
        }
    }
}
