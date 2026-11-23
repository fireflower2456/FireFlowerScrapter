using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FireFlower.ScrapterText;

namespace FireFlower
{
    // [VariableKey] socket: picks only declared variables of that socket's kind. Picking also sets the sibling scope socket (and the component-name socket for Component variables)
    [CustomPropertyDrawer(typeof(ScrapWeaver.VariableKeyAttribute))]
    sealed class VariableKeyDrawer : PropertyDrawer
    {
        static readonly Color Invalid = new(1f, 0.45f, 0.45f);

        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            SerializedObject so = prop.serializedObject;
            string path = prop.propertyPath;
            int dot = path.LastIndexOf('.');
            var attr = (ScrapWeaver.VariableKeyAttribute)attribute;
            string parent = dot < 0 ? "" : path[..(dot + 1)];
            string scopePath = parent + attr.ScopeField;
            string componentPath = attr.ComponentField == null ? null : parent + attr.ComponentField;
            var button = Ui.With(new Button(), "unity-base-popup-field__input");
            var typeLabel = new Label { pickingMode = PickingMode.Ignore, style = { opacity = 0.55f, color = Color.white, marginLeft = 4, marginRight = 2 } };

            string Component() => componentPath == null ? null : so.FindProperty(componentPath)?.stringValue;

            void Refresh()
            {
                string current = so.FindProperty(path)?.stringValue;
                bool declared = ScrapterWindow.DeclaredVariables(so, path).Any(v => v.key == current && v.type == attr.Type && (componentPath == null || v.component == Component()));
                button.text = (string.IsNullOrEmpty(current) ? T("(no variable)") : current) + "  ▾";
                button.style.color = declared ? new StyleColor(StyleKeyword.Null) : new StyleColor(Invalid);
                typeLabel.text = ScrapterWindow.TypeTag(attr.Type, Component());
            }

            button.clicked += () =>
            {
                string current = so.FindProperty(path).stringValue;
                var menu = new GenericMenu();
                foreach (var (key, scope, _, component) in ScrapterWindow.DeclaredVariables(so, path).Where(v => v.type == attr.Type))
                    menu.AddItem(new GUIContent(componentPath == null ? key : $"{key}  ({component})"), key == current, () =>
                    {
                        so.Update();
                        so.FindProperty(path).stringValue = key;
                        so.FindProperty(scopePath).enumValueIndex = (int)scope;
                        if (componentPath != null) so.FindProperty(componentPath).stringValue = component;
                        so.ApplyModifiedProperties();
                    });
                menu.ShowAsContext();
            };
            button.TrackPropertyValue(prop, _ => Refresh());
            Refresh();
            // The type goes outside the picker, semi-transparent over the block
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(button);
            row.Add(typeLabel);
            return row;
        }
    }
}
