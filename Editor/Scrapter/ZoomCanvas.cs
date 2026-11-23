using System;
using UnityEngine;
using UnityEngine.UIElements;
using FireFlower;

namespace FireFlower
{
    // A canvas that zooms with the wheel (the point under the pointer stays put) and pans by left/right-dragging empty space or middle-dragging anywhere.
    // Comes with a grid and −/zoom/+/1:1 buttons. Put content in Layer. Styled by the canvas/zoom classes in ScrapWeaverWindow.uss
    public sealed class ZoomCanvas
    {
        const float MinZoom = 0.25f;
        const float MaxZoom = 2f;
        const float ZoomFactor = 1.1f;
        const float GridSpacing = 20f;
        const int GridMajorEvery = 5;
        static readonly Color GridMinor = new(0.16f, 0.16f, 0.16f);
        static readonly Color GridMajor = new(0.11f, 0.11f, 0.11f);

        public readonly VisualElement Viewport;
        public readonly VisualElement Layer;
        readonly VisualElement grid;
        readonly Label zoomLabel;
        readonly float startZoom;
        float zoom;
        Vector2 pan;

        public float Zoom => zoom;

        // isBlock: a left-drag starting on this element doesn't pan (left to block dragging)
        public ZoomCanvas(Func<VisualElement, bool> isBlock, float startZoom = 1f)
        {
            this.startZoom = zoom = startZoom;
            Viewport = Ui.El("canvas-viewport");
            grid = Ui.El("grid-layer");
            grid.generateVisualContent += DrawGrid;
            Viewport.Add(grid);
            Layer = Ui.El("canvas-layer");
            Viewport.Add(Layer);

            VisualElement controls = Ui.El("zoom-controls");
            controls.Add(Ui.With(new Button(() => SetZoom(zoom / ZoomFactor)) { text = "−" }, "zoom-button"));
            zoomLabel = Ui.Label("", "zoom-label");
            controls.Add(zoomLabel);
            controls.Add(Ui.With(new Button(() => SetZoom(zoom * ZoomFactor)) { text = "+" }, "zoom-button"));
            controls.Add(Ui.With(new Button(Reset) { text = "1:1" }, "zoom-button", "zoom-reset"));
            Viewport.Add(controls);

            Vector2 panStart = default, pointerStart = default;
            Viewport.AddManipulator(new DragManipulator(
                p => { pointerStart = p; panStart = pan; },
                p => { pan = panStart + (p - pointerStart); Apply(); },
                threshold: 0f,
                accept: evt => evt.button == (int)MouseButton.MiddleMouse || !Inside(evt.target as VisualElement, isBlock),
                buttons: new[] { MouseButton.LeftMouse, MouseButton.MiddleMouse, MouseButton.RightMouse }));
            Viewport.RegisterCallback<WheelEvent>(evt =>
            {
                SetZoom(evt.delta.y > 0f ? zoom / ZoomFactor : zoom * ZoomFactor, evt.mousePosition);
                evt.StopPropagation();
            });
            Apply();
        }

        public void Reset()
        {
            zoom = startZoom;
            pan = Vector2.zero;
            Apply();
        }

        void SetZoom(float value, Vector2? worldPivot = null)
        {
            float next = Mathf.Clamp(value, MinZoom, MaxZoom);
            if (Mathf.Approximately(next, zoom)) return;
            Vector2 pivot = Viewport.WorldToLocal(worldPivot ?? Viewport.worldBound.center);
            pan = pivot - (pivot - pan) * (next / zoom);
            zoom = next;
            Apply();
        }

        void Apply()
        {
            Layer.style.translate = new StyleTranslate(new Translate(pan.x, pan.y, 0f));
            Layer.style.scale = new StyleScale(new Scale(new Vector3(zoom, zoom, 1f)));
            zoomLabel.text = $"{Mathf.RoundToInt(zoom * 100f)}%";
            grid.MarkDirtyRepaint();
        }

        bool Inside(VisualElement element, Func<VisualElement, bool> isBlock)
        {
            for (; element != null && element != Viewport; element = element.parent)
                if (isBlock(element)) return true;
            return false;
        }

        void DrawGrid(MeshGenerationContext context)
        {
            Rect rect = grid.contentRect;
            float spacing = GridSpacing * zoom;
            if (spacing < 4f) return;
            Painter2D painter = context.painter2D;
            painter.lineWidth = 1f;
            foreach (bool major in new[] { false, true })
            {
                painter.strokeColor = major ? GridMajor : GridMinor;
                painter.BeginPath();
                foreach (bool vertical in new[] { true, false })
                {
                    float offset = vertical ? pan.x : pan.y;
                    float length = vertical ? rect.width : rect.height;
                    for (int i = Mathf.FloorToInt(-offset / spacing); i * spacing + offset <= length; i++)
                    {
                        if ((i % GridMajorEvery == 0) != major) continue;
                        float p = Mathf.Round(i * spacing + offset) + 0.5f;
                        painter.MoveTo(vertical ? new Vector2(p, 0f) : new Vector2(0f, p));
                        painter.LineTo(vertical ? new Vector2(p, rect.height) : new Vector2(rect.width, p));
                    }
                }
                painter.Stroke();
            }
        }
    }
}
