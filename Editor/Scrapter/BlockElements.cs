using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireFlower
{
    public static class Ui
    {
        // Shows an enum socket as a pill dropdown inside the sentence. Value only, no label
        public static VisualElement Dropdown(SerializedObject so, SerializedProperty property)
        {
            string path = property.propertyPath;
            var choices = new List<string>(property.enumDisplayNames);
            var popup = With(new PopupField<string>(choices, Mathf.Clamp(property.enumValueIndex, 0, choices.Count - 1)), "title-dropdown");
            popup.RegisterValueChangedCallback(evt =>
            {
                so.FindProperty(path).enumValueIndex = choices.IndexOf(evt.newValue);
                so.ApplyModifiedProperties();
            });
            popup.TrackPropertyValue(property, p => popup.SetValueWithoutNotify(choices[Mathf.Clamp(p.enumValueIndex, 0, choices.Count - 1)]));
            return popup;
        }

        public static string EnumText(object value)
        {
            if (value == null) return "";
            FieldInfo member = value.GetType().GetField(value.ToString());
            return member?.GetCustomAttribute<InspectorNameAttribute>()?.displayName ?? ObjectNames.NicifyVariableName(value.ToString());
        }

        public static VisualElement El(params string[] classes) => With(new VisualElement(), classes);

        public static Label Label(string text, params string[] classes) => With(new Label(text), classes);

        public static T With<T>(T element, params string[] classes) where T : VisualElement
        {
            foreach (string c in classes) element.AddToClassList(c);
            return element;
        }

        // Shows the popup as a modal centered on the window that opened it (or the main editor window)
        public static void ShowCentered(EditorWindow popup, Vector2 size)
        {
            Rect host = EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.position : EditorGUIUtility.GetMainWindowPosition();
            popup.position = new Rect(host.center - size * 0.5f, size);
            popup.ShowModalUtility();
        }
    }

    // Element that draws an outline from a point list built from (width, height). Pass a separate stroke point list to split fill and stroke
    public sealed class PathElement : VisualElement
    {
        readonly Func<float, float, Vector2[]> fillPoints;
        readonly Func<float, float, Vector2[]> strokePoints;
        readonly Color fill;
        readonly Color stroke;
        readonly float lineWidth;

        public PathElement(Func<float, float, Vector2[]> fillPoints, Color fill, Color stroke, float lineWidth = 1f,
            Func<float, float, Vector2[]> strokePoints = null)
        {
            this.fillPoints = fillPoints;
            this.strokePoints = strokePoints;
            this.fill = fill;
            this.stroke = stroke;
            this.lineWidth = lineWidth;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        void Draw(MeshGenerationContext context)
        {
            float w = layout.width, h = layout.height;
            if (w <= 0f || h <= 0f) return;
            Painter2D p = context.painter2D;
            p.lineWidth = lineWidth;
            p.lineJoin = LineJoin.Miter;
            p.fillColor = fill;
            p.strokeColor = stroke;

            Trace(p, fillPoints(w, h), true);
            p.Fill();
            if (strokePoints == null) p.Stroke();
            else
            {
                Trace(p, strokePoints(w, h), false);
                p.Stroke();
            }
        }

        static void Trace(Painter2D p, Vector2[] points, bool close)
        {
            p.BeginPath();
            p.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) p.LineTo(points[i]);
            if (close) p.ClosePath();
        }
    }

    // Generic drag: starts past a threshold, ends on release, cancels when the pointer is captured away.
    // Modifier keys are ignored (activators need an exact modifier match, so holding Alt or Shift would never start a drag). Put modifier rules in accept
    public sealed class DragManipulator : PointerManipulator
    {
        readonly Func<PointerDownEvent, bool> accept;
        readonly Action<Vector2> begin;
        readonly Action<Vector2> move;
        readonly Action end;
        readonly Action cancel;
        readonly float threshold;
        readonly MouseButton[] buttons;
        Vector2 start;
        int pointerId = -1;
        bool dragging;

        public DragManipulator(Action<Vector2> begin, Action<Vector2> move, Action end = null, Action cancel = null,
            float threshold = 4f, Func<PointerDownEvent, bool> accept = null, params MouseButton[] buttons)
        {
            this.begin = begin;
            this.move = move;
            this.end = end;
            this.cancel = cancel;
            this.threshold = threshold;
            this.accept = accept;
            this.buttons = buttons.Length > 0 ? buttons : new[] { MouseButton.LeftMouse };
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        void OnDown(PointerDownEvent evt)
        {
            if (Array.IndexOf(buttons, (MouseButton)evt.button) < 0 || (accept != null && !accept(evt))) return;
            // If the last drag's release was missed (released outside the window, etc.), drop the leftover state so this drag can start
            dragging = false;
            pointerId = evt.pointerId;
            start = evt.position;
            target.CapturePointer(pointerId);
            if (threshold <= 0f) Begin();
            evt.StopPropagation();
        }

        void OnMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != pointerId) return;
            if (!dragging && Vector2.Distance(start, evt.position) >= threshold) Begin();
            if (dragging) move(evt.position);
            evt.StopPropagation();
        }

        void OnUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointerId) return;
            bool finish = dragging;
            Reset();
            if (finish) end?.Invoke();
            evt.StopPropagation();
        }

        void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            bool wasDragging = dragging;
            pointerId = -1;
            dragging = false;
            if (wasDragging) cancel?.Invoke();
        }

        void Begin()
        {
            dragging = true;
            begin(start);
        }

        void Reset()
        {
            int id = pointerId;
            pointerId = -1;
            dragging = false;
            if (target.HasPointerCapture(id)) target.ReleasePointer(id);
        }
    }
}
