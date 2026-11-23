using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FireFlower.ScrapterText;

namespace FireFlower
{
    // Scratch's "Make a Block" window. Chain labels, number inputs, and boolean inputs to define the block's shape.
    // When editing, piece ids are kept so arguments of already-placed call blocks follow along
    public sealed class BlockMakerWindow : EditorWindow
    {
        static readonly Color BlockColor = new(0.86f, 0.32f, 0.45f);

        List<FunctionPiece> pieces;
        Action<List<FunctionPiece>> done;
        VisualElement preview;
        HelpBox error;

        public static void Open(IEnumerable<FunctionPiece> current, Action<List<FunctionPiece>> done)
        {
            var window = CreateInstance<BlockMakerWindow>();
            window.titleContent = new GUIContent(current == null ? T("Make a Block") : T("Edit Block"));
            window.pieces = current?.Select(p => new FunctionPiece { kind = p.kind, text = p.text, id = p.id }).ToList()
                            ?? new List<FunctionPiece> { New(FunctionPiece.Kind.Label, T("block name")) };
            window.done = done;
            window.minSize = new Vector2(560f, 240f);
            Ui.ShowCentered(window, window.minSize);
        }

        static FunctionPiece New(FunctionPiece.Kind kind, string text) => new() { kind = kind, text = text, id = Guid.NewGuid().ToString("N") };

        void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingTop = root.style.paddingBottom = root.style.paddingLeft = root.style.paddingRight = 10f;

            preview = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, alignItems = Align.Center, alignSelf = Align.Center,
                    minHeight = 44f, paddingLeft = 8f, paddingRight = 8f, marginBottom = 12f, backgroundColor = BlockColor,
                },
            };
            root.Add(preview);
            RefreshPreview();

            var options = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            options.Add(Option(T("Add an input") + "\n" + T("number"), FunctionPiece.Kind.Number, T("number")));
            options.Add(Option(T("Add an input") + "\n" + T("boolean"), FunctionPiece.Kind.Bool, T("boolean")));
            options.Add(Option(T("Add a label"), FunctionPiece.Kind.Label, T("label")));
            root.Add(options);

            error = new HelpBox("", HelpBoxMessageType.Error) { style = { display = DisplayStyle.None } };
            root.Add(error);
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 8f } };
            buttons.Add(new Button(Close) { text = T("Cancel") });
            buttons.Add(new Button(Confirm) { text = T("OK") });
            root.Add(buttons);
        }

        Button Option(string text, FunctionPiece.Kind kind, string defaultText) =>
            new(() =>
            {
                pieces.Add(New(kind, defaultText));
                RefreshPreview();
            })
            { text = text, style = { flexGrow = 1f, height = 56f, whiteSpace = WhiteSpace.Normal } };

        // Labels appear as plain text on the block, numbers as round slots, booleans as angular slots
        void RefreshPreview()
        {
            preview.Clear();
            foreach (FunctionPiece piece in pieces)
            {
                var field = new TextField { value = piece.text };
                field.RegisterValueChangedCallback(evt => piece.text = evt.newValue);
                VisualElement input = field.Q(className: TextField.inputUssClassName);
                input.style.minWidth = 50f;
                if (piece.kind == FunctionPiece.Kind.Label)
                {
                    input.style.backgroundColor = Color.clear;
                    input.style.color = Color.white;
                }
                else
                {
                    float radius = piece.kind == FunctionPiece.Kind.Number ? 10f : 0f;
                    input.style.borderTopLeftRadius = input.style.borderTopRightRadius = input.style.borderBottomLeftRadius = input.style.borderBottomRightRadius = radius;
                    input.style.backgroundColor = new Color(0.72f, 0.2f, 0.33f);
                    input.style.color = Color.white;
                }

                var remove = new Button(() =>
                {
                    pieces.Remove(piece);
                    RefreshPreview();
                })
                { text = "×", tooltip = T("Delete") };
                remove.SetEnabled(pieces.Count > 1);

                var group = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 4f } };
                group.Add(field);
                group.Add(remove);
                preview.Add(group);
            }
        }

        void Confirm()
        {
            foreach (FunctionPiece piece in pieces) piece.text = piece.text?.Trim() ?? "";
            List<FunctionPiece> result = pieces.Where(p => p.kind != FunctionPiece.Kind.Label || p.text.Length > 0).ToList();
            string message = !result.Any(p => p.kind == FunctionPiece.Kind.Label) ? T("A block needs at least one label.")
                : result.Any(p => p.text.Length == 0) ? T("Enter a name for each input.") : null;
            if (message != null)
            {
                error.text = message;
                error.style.display = DisplayStyle.Flex;
                return;
            }
            Close();
            done(result);
        }
    }
}
