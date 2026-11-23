using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using static FireFlower.ScrapWeaverTypes;
using static FireFlower.ScrapterText;

namespace FireFlower
{
    // Block copy, paste, and delete: right-click menu, Ctrl+C / Ctrl+V / Delete, Shift+drag box selection.
    // Shortcuts act on box-selected blocks if any, otherwise on the block (or empty slot) the pointer was last over.
    // Each paste is a full deep copy, so pasted blocks never share [SerializeReference]s
    public abstract partial class ScrapterWindow
    {
        protected static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        protected static readonly List<object> clipboard = new();

        protected Slot focusSlot;
        protected int focusIndex;
        protected object focusBlock;
        protected Vector2 pointerOnCanvas;
        protected Vector2 pointerWorld = new(float.NaN, float.NaN);

        protected readonly HashSet<object> selected = new(FieldPath.ReferenceComparer.Instance);
        protected VisualElement marquee;

        protected void RegisterClipboard()
        {
            // Clicking the canvas takes focus so Copy/Paste commands come to this window
            canvasViewport.focusable = true;
            canvasViewport.RegisterCallback<PointerDownEvent>(evt =>
            {
                canvasViewport.Focus();
                if (!evt.shiftKey) ClearSelection();
            }, TrickleDown.TrickleDown);
            canvasViewport.RegisterCallback<PointerMoveEvent>(evt =>
            {
                pointerWorld = evt.position;
                pointerOnCanvas = canvasLayer.WorldToLocal(evt.position);
            }, TrickleDown.TrickleDown);
            canvasViewport.RegisterCallback<PointerLeaveEvent>(_ => pointerWorld = new Vector2(float.NaN, float.NaN));
            rootVisualElement.RegisterCallback<ValidateCommandEvent>(evt =>
            {
                if (IsBlockCommand(evt.commandName) && !Typing()) evt.StopPropagation();
            });
            rootVisualElement.RegisterCallback<ExecuteCommandEvent>(evt =>
            {
                if (Typing() || !IsBlockCommand(evt.commandName)) return;
                switch (evt.commandName)
                {
                    case "Copy" when Targets(focusBlock) is { Count: > 0 } copied:
                        Copy(copied);
                        break;
                    case "Paste":
                        Paste(focusSlot, focusBlock != null ? focusIndex + 1 : focusIndex);
                        break;
                    // Delete only the block directly under the pointer now (not the last one passed over)
                    case "SoftDelete" or "Delete" when Targets(BlockAt(pointerWorld)) is { Count: > 0 } deleted:
                        DeleteBlocks(deleted);
                        ClearSelection();
                        break;
                    default:
                        return;
                }
                evt.StopPropagation();
            });

            marquee = Ui.El("selection-marquee");
            marquee.pickingMode = PickingMode.Ignore;
            marquee.style.display = DisplayStyle.None;
            canvasViewport.Add(marquee);
            Vector2 from = default, to = default;
            canvasViewport.AddManipulator(new DragManipulator(
                p => { from = to = p; ShowMarquee(from, to); },
                p => { to = p; ShowMarquee(from, to); },
                () => { marquee.style.display = DisplayStyle.None; SelectInside(WorldRect(from, to)); },
                () => marquee.style.display = DisplayStyle.None,
                threshold: 0f,
                accept: evt => evt.shiftKey && evt.button == (int)MouseButton.LeftMouse && !IsInsideClass(evt.target as VisualElement, "node", canvasViewport)));
        }

        protected static bool IsBlockCommand(string name) => name is "Copy" or "Paste" or "SoftDelete" or "Delete";

        // While typing in a value field, leave copy/paste to that field
        protected bool Typing()
        {
            for (var e = rootVisualElement.panel?.focusController?.focusedElement as VisualElement; e != null; e = e.parent)
                if (e is TextElement || e.ClassListContains("unity-base-field")) return true;
            return false;
        }

        protected void PointAt(Slot slot, int index, object block)
        {
            focusSlot = slot;
            focusIndex = index;
            focusBlock = block;
        }

        // Shortcut targets: on-screen selected blocks in draw order, else the block under the pointer
        protected List<object> Targets(object pointed)
        {
            List<object> picked = blockViews.Keys.Where(selected.Contains).ToList();
            if (picked.Count > 0) return picked;
            return pointed != null && pointed is not DefinitionBlock ? new List<object> { pointed } : new List<object>();
        }

        // ───── Box selection ─────

        protected static Rect WorldRect(Vector2 a, Vector2 b) => Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        protected void ShowMarquee(Vector2 a, Vector2 b)
        {
            Rect r = WorldRect(canvasViewport.WorldToLocal(a), canvasViewport.WorldToLocal(b));
            marquee.style.display = DisplayStyle.Flex;
            marquee.style.left = r.x;
            marquee.style.top = r.y;
            marquee.style.width = r.width;
            marquee.style.height = r.height;
        }

        // Blocks fully inside the rectangle. Blocks nested in a selected block go with the outer one, so they are excluded
        protected void SelectInside(Rect world)
        {
            selected.Clear();
            foreach (var pair in blockViews)
            {
                Rect r = pair.Value.view.worldBound;
                if (pair.Key is not DefinitionBlock && pair.Value.view.panel != null && world.Contains(r.min) && world.Contains(r.max)) selected.Add(pair.Key);
            }
            selected.ExceptWith(selected.Where(inner => selected.Any(outer => !ReferenceEquals(outer, inner) && Contains(outer, inner))).ToList());
            ApplySelection();
        }

        protected void ClearSelection()
        {
            if (selected.Count == 0) return;
            selected.Clear();
            ApplySelection();
        }

        protected void ApplySelection()
        {
            foreach (var pair in blockViews) pair.Value.view.EnableInClassList("block-selected", selected.Contains(pair.Key));
        }

        // ───── Copy · Paste ─────

        // Right-click menu on a block header
        protected void AddClipboardMenu(Slot slot, int index, object block, ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction(T("Copy") + "  Ctrl+C", _ => Copy(new List<object> { block }));
            evt.menu.AppendAction(T("Paste") + "  Ctrl+V", _ => Paste(slot, index + 1), _ => CanPaste(slot) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        // Empty slots also get pointer tracking and a paste menu
        protected void TrackEmptyZone(VisualElement zone, Slot slot, int index)
        {
            zone.RegisterCallback<PointerEnterEvent>(_ => PointAt(slot, index, null));
            zone.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction(T("Paste") + "  Ctrl+V", _ => Paste(slot, index), _ => CanPaste(slot) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.StopPropagation();
            }));
        }

        protected static void Copy(IEnumerable<object> blocks)
        {
            clipboard.Clear();
            clipboard.AddRange(blocks.Select(DeepClone));
        }

        protected static bool CanPaste(Slot slot) => clipboard.Count > 0 && (slot.IsLoose || Fits(slot, clipboard));

        // Next to a sensing block lying on the canvas, place separately instead of plugging in
        protected static bool Fits(Slot slot, List<object> blocks) =>
            !(slot.IsLoose && slot.Shape != BlockShape.Part)
            && (slot.Multiple || blocks.Count == 1)
            && blocks.All(b => b is not DefinitionBlock && slot.Accepts.IsAssignableFrom(b.GetType()));

        // If it fits: list slots insert at index, single slots are replaced. If not, place it loose on the canvas
        protected void Paste(Slot slot, int index)
        {
            if (clipboard.Count == 0 || WorkingSet == null) return;
            Vector2 at = slot?.Owner is LooseBlock loose ? loose.position + new Vector2(24f, 24f) : pointerOnCanvas;
            Edit("Paste Blocks", () =>
            {
                List<object> copies = clipboard.Select(DeepClone).ToList();
                foreach (object copy in copies) EachText(copy, text =>
                {
                    LocalizedText.Fork(text, NextTextKey);
                    RememberText(text);
                });
                if (slot != null && Fits(slot, copies)) PutIn(slot, slot.Multiple ? index : 0, !slot.Multiple, copies);
                else PlaceLoose(copies, at);
            });
        }

        // Effect blocks go in one stack; mixed blocks are laid out one below another
        protected void PlaceLoose(List<object> blocks, Vector2 at)
        {
            if (blocks.All(b => b is ScrapPart))
            {
                looseBlocks.Add(NewLoose(at, blocks));
                return;
            }
            for (int i = 0; i < blocks.Count; i++) looseBlocks.Add(NewLoose(at + new Vector2(0f, 36f * i), new[] { blocks[i] }));
        }

        // Text fields owned by the block (excluding pick-from-list fields like speaker). Each text field needs its own key — with a shared key, editing one changes the other
        protected static void EachText(object node, Action<LocalizedString> visit, HashSet<object> visited = null) =>
            LocalizedText.Each(node, (text, picked, _) => { if (!picked) visit(text); }, visited);

        // Before saving: if blocks on this screen point to the same text key, split the later ones onto new keys (e.g. duplicates from older pastes).
        // Fields outside blocks (pick fields like item description headers) are shared on purpose, so they are skipped
        protected bool SplitSharedTexts()
        {
            var seen = new HashSet<(string, long)>();
            var visited = new HashSet<object>(FieldPath.ReferenceComparer.Instance);
            bool split = false;
            foreach (object block in CanvasBlocks().ToList()) EachText(block, text =>
            {
                if (LocalizedText.Entry(text, out var collection) is not { } entry) return;
                if (seen.Add((collection.TableCollectionName, entry.Id))) return;
                LocalizedText.Fork(text, NextTextKey);
                RememberText(text);
                split = true;
                Debug.Log($"[{working.name}] Split text that shared a key: {entry.Key} → {LocalizedText.Entry(text, out _)?.Key}");
            }, visited);
            return split;
        }

        // Deep-copies values, arrays, lists, and nested classes. Unity objects (sprites, assets) and strings are referenced as-is
        protected static object DeepClone(object source)
        {
            if (source == null) return null;
            Type type = source.GetType();
            if (type.IsPrimitive || type.IsEnum || source is string || source is UnityEngine.Object || source is Delegate) return source;
            if (source is Array array)
            {
                Array copy = Array.CreateInstance(type.GetElementType(), array.Length);
                for (int i = 0; i < array.Length; i++) copy.SetValue(DeepClone(array.GetValue(i)), i);
                return copy;
            }
            if (source is IList list && type.IsGenericType)
            {
                var copy = (IList)Activator.CreateInstance(type);
                foreach (object element in list) copy.Add(DeepClone(element));
                return copy;
            }
            object clone = MemberwiseCloneMethod.Invoke(source, null);
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                        field.SetValue(clone, DeepClone(field.GetValue(source)));
            return clone;
        }
    }
}
