using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FireFlower.ScrapterText;

namespace FireFlower
{
    // [MemberPath] field: type the path, or type a dot (or ▾) to pick the next segment. Invalid paths turn red
    // The menu lists members of the preceding value's (root socket's) type. Whether the block is a number, Vector2 or Vector3 decides which members can end the path
    [CustomPropertyDrawer(typeof(ScrapWeaver.MemberPathAttribute))]
    sealed class MemberPathDrawer : PropertyDrawer
    {
        static readonly Color Invalid = new(1f, 0.45f, 0.45f);
        // The value of a SerializeReference field. Null for lists and plain fields (reading those throws)
        static object RefOf(SerializedProperty p) => p != null && p.propertyType == SerializedPropertyType.ManagedReference ? p.managedReferenceValue : null;

        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            SerializedObject so = prop.serializedObject;
            string propertyPath = prop.propertyPath;
            int last = propertyPath.LastIndexOf('.');
            string ownerPath = last < 0 ? null : propertyPath[..last];
            string rootPath = (last < 0 ? "" : propertyPath[..(last + 1)]) + ((ScrapWeaver.MemberPathAttribute)attribute).RootField;
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var text = Ui.With(new TextField { style = { minWidth = 120 } }, "pick-text");
            text.BindProperty(prop);
            var pick = Ui.With(new Button { text = "▾", style = { marginLeft = 2 } }, "unity-base-popup-field__input");
            row.Add(text);
            row.Add(pick);

            Type Start() => MemberPath.StartType(RefOf(so.FindProperty(rootPath)) as DataSource);
            object owner = ownerPath == null ? null : RefOf(so.FindProperty(ownerPath));
            Func<Type, bool> ends = owner switch
            {
                Vector2Source or Vector3Source => t => t == typeof(Vector2) || t == typeof(Vector3),
                _ => MemberPath.IsNumber,
            };
            string kind = owner switch { Vector2Source => "Vector2", Vector3Source => "Vector3", _ => "number" };

            void Refresh()
            {
                var resolved = MemberPath.Resolve(Start(), text.value, out string error);
                if (resolved != null && !ends(resolved.End)) error = T("{0} can't be used in a {1} socket", resolved.End.Name, kind);
                bool ok = error == null;
                (text.Q(className: "unity-text-field__input") ?? text).style.color = ok || string.IsNullOrEmpty(text.value) ? new StyleColor(StyleKeyword.Null) : new StyleColor(Invalid);
                text.tooltip = ok ? resolved.End.Name : error;
            }

            void Set(string value)
            {
                so.Update();
                so.FindProperty(propertyPath).stringValue = value;
                so.ApplyModifiedProperties();
            }

            // Picking a member that can end the path finishes it; a member that can be drilled into adds a dot and opens the next menu
            void Open()
            {
                string current = text.value ?? "";
                int dot = current.LastIndexOf('.');
                string prefix = dot < 0 ? "" : current[..dot] + ".";
                Type type = dot < 0 ? Start() : MemberPath.Resolve(Start(), current[..dot], out _)?.End;
                var menu = new GenericMenu();
                if (type != null)
                    foreach (var m in MemberPath.Members(type).OrderBy(m => m.Name))
                    {
                        Type t = MemberPath.TypeOf(m);
                        if (ends(t)) menu.AddItem(new GUIContent($"{m.Name}  ({t.Name})"), false, () => Set(prefix + m.Name));
                        else if (!t.IsPrimitive && !t.IsEnum && t != typeof(string) && MemberPath.Members(t).Any())
                            menu.AddItem(new GUIContent($"{m.Name}  ({t.Name})  ›"), false, () => Set(prefix + m.Name + "."));
                    }
                if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent(type == null ? T("Unknown type for the preceding value") : T("Nothing to pick")));
                menu.DropDown(text.worldBound);
            }

            pick.clicked += Open;
            text.RegisterValueChangedCallback(evt =>
            {
                Refresh();
                if (evt.newValue != null && evt.newValue.EndsWith(".") && (evt.previousValue?.Length ?? 0) < evt.newValue.Length) Open();
            });
            row.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            Refresh();
            return row;
        }
    }
}
