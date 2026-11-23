using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FireFlower.ScrapterText;

namespace FireFlower
{
    // Window that asks for a name (new variable, new buff). If choices/kinds are given, one of each is picked.
    // Stays open if validate returns an error message
    public sealed class NameWindow : EditorWindow
    {
        string label;
        string[] choices;
        string[] kinds;
        Func<string, int, int, string> validate;
        Action<string, int, int> done;

        public static void Open(string title, string label, string[] choices, Func<string, string> validate, Action<string, int> done) =>
            Open(title, label, choices, null, (name, _, _) => validate(name), (name, choice, _) => done(name, choice));

        public static void Open(string title, string label, string[] choices, string[] kinds, Func<string, int, int, string> validate, Action<string, int, int> done)
        {
            var window = CreateInstance<NameWindow>();
            window.titleContent = new GUIContent(title);
            window.label = label;
            window.choices = choices ?? Array.Empty<string>();
            window.kinds = kinds ?? Array.Empty<string>();
            window.validate = validate;
            window.done = done;
            Vector2 size = new(360f, 110f + window.choices.Length * 20f + (window.kinds.Length > 0 ? 30f : 0f));
            window.minSize = window.maxSize = size;
            Ui.ShowCentered(window, size);
        }

        void CreateGUI()
        {
            var name = new TextField(label);
            var choice = new RadioButtonGroup(null, choices.ToList()) { value = 0 };
            var kind = new DropdownField(T("Type"), kinds.ToList(), 0);
            var error = new HelpBox("", HelpBoxMessageType.Error) { style = { display = DisplayStyle.None } };

            void Confirm()
            {
                string trimmed = name.value.Trim();
                if (validate(trimmed, choice.value, kind.index) is string message)
                {
                    error.text = message;
                    error.style.display = DisplayStyle.Flex;
                    return;
                }
                Close();
                done(trimmed, choice.value, kind.index);
            }

            name.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter) Confirm();
            });
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
            buttons.Add(new Button(Close) { text = T("Cancel") });
            buttons.Add(new Button(Confirm) { text = T("OK") });

            VisualElement root = rootVisualElement;
            root.style.paddingTop = root.style.paddingBottom = root.style.paddingLeft = root.style.paddingRight = 8f;
            root.Add(name);
            if (choices.Length > 0) root.Add(choice);
            if (kinds.Length > 0) root.Add(kind);
            root.Add(error);
            root.Add(buttons);
            name.schedule.Execute(() => name.Focus());
        }
    }
}
