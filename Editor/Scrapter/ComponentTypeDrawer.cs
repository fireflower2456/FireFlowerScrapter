using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FireFlower.ScrapterText;

namespace FireFlower
{
    // [ComponentType] field: pick a component name. Common ones on top, game ones under "Game", the rest under their namespace
    [CustomPropertyDrawer(typeof(ScrapWeaver.ComponentTypeAttribute))]
    sealed class ComponentTypeDrawer : PropertyDrawer
    {
        static readonly Color Invalid = new(1f, 0.45f, 0.45f);
        static readonly Type[] Frequent =
        {
            typeof(Transform), typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer), typeof(Animator), typeof(AudioSource),
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            SerializedObject so = prop.serializedObject;
            string path = prop.propertyPath;
            var button = Ui.With(new Button(), "unity-base-popup-field__input");

            void Refresh()
            {
                string current = so.FindProperty(path)?.stringValue;
                button.text = (string.IsNullOrEmpty(current) ? T("(Component)") : current) + "  ▾";
                button.style.color = MemberPath.ComponentType(current) != null ? new StyleColor(StyleKeyword.Null) : new StyleColor(Invalid);
            }

            button.clicked += () =>
            {
                string current = so.FindProperty(path).stringValue;
                var menu = new GenericMenu();
                foreach (Type t in Frequent) Add(t);
                menu.AddSeparator("");
                foreach (var (_, t) in MemberPath.Components.Where(c => Array.IndexOf(Frequent, c.Value) < 0).OrderBy(c => c.Key)) Add(t);
                menu.DropDown(button.worldBound);

                void Add(Type t) => menu.AddItem(new GUIContent(Group(t) + t.Name), t.Name == current, () =>
                {
                    so.Update();
                    so.FindProperty(path).stringValue = t.Name;
                    so.ApplyModifiedProperties();
                });
            };
            button.TrackPropertyValue(prop, _ => Refresh());
            button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            Refresh();
            return button;
        }

        static string Group(Type type)
        {
            if (Array.IndexOf(Frequent, type) >= 0) return "";
            if (string.IsNullOrEmpty(type.Namespace)) return "Game/";
            int dot = type.Namespace.IndexOf('.');
            return dot < 0 ? type.Namespace + "/" : $"{type.Namespace[..dot]}/{type.Namespace.Replace('.', '·')}/";
        }
    }
}
