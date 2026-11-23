using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FireFlower.ScrapWeaverTypes;
using static FireFlower.ScrapterText;

namespace FireFlower
{
    // Palette and canvas: drawing blocks, drag and drop, data editing
    public abstract partial class ScrapterWindow
    {
        // Snap distance (screen px). A shown spot holds up to Sticky, and a new spot must be more than Preference closer to take over
        protected const float SnapRadius = 48f;
        protected const float StickyRadius = 68f;
        protected const float StickyPreference = 8f;
        protected static readonly Vector2 ScriptsPosition = new(60f, 50f);
        // Right of the script list (.scripts-node width 280)
        protected static readonly Vector2 RootPosition = new(370f, 50f);
        protected static readonly Color OutlineColor = new(0.06f, 0.06f, 0.06f);
        protected static readonly FieldInfo PartsField = typeof(ScrapSet).GetField(nameof(ScrapSet.parts));
        protected static readonly FieldInfo DefinitionsField = typeof(ScrapSet).GetField(nameof(ScrapSet.definitions));
        protected static readonly FieldInfo LooseBlocksField = typeof(LooseBlock).GetField(nameof(LooseBlock.blocks));

        // Loose block stacks left on the canvas (a single block for sensing blocks). Not runtime data; kept per asset by this window
        [Serializable]
        protected sealed class LooseBlock { public ScriptableObject item; public Vector2 position; [SerializeReference] public List<object> blocks = new(); }

        // A place for blocks = one [SerializeReference] field of some object. The root is the parts of the asset's ScrapSet
        // If Fixed >= 0, it is a single-block socket covering only that array element (like a call block's argument)
        protected sealed class Slot
        {
            public object Owner; public FieldInfo Field; public Type Accepts; public BlockShape Shape; public bool Multiple; public int Fixed = -1;
            public bool Same(Slot other) => other != null && ReferenceEquals(Owner, other.Owner) && Field == other.Field && Fixed == other.Fixed;
            public bool IsLoose => Owner is LooseBlock;
            public static Slot Of(object owner, FieldInfo field) => new()
                { Owner = owner, Field = field, Accepts = ElementType(field.FieldType), Shape = ShapeOf(ElementType(field.FieldType)), Multiple = IsCollection(field.FieldType) };

            public static Slot Element(object owner, FieldInfo field, int index)
            {
                Slot slot = Of(owner, field);
                slot.Multiple = false;
                slot.Fixed = index;
                return slot;
            }

            public object Get()
            {
                if (Owner == null) return null;
                object value = Field.GetValue(Owner);
                if (Fixed < 0) return value;
                return value is IList list && Fixed < list.Count ? list[Fixed] : null;
            }

            public void Set(object block)
            {
                if (Fixed < 0) Field.SetValue(Owner, block);
                else if (Field.GetValue(Owner) is IList list && Fixed < list.Count) list[Fixed] = block;
            }
        }

        // Swap: the filled socket's block itself is the Zone; dropping replaces it and moves the old block aside on the canvas
        protected sealed class DropTarget { public Slot Slot; public int Index; public bool Replace, Swap; public VisualElement Zone; }

        // Block is null when newly taken from the palette. Blocks = Block plus the blocks below dragged with it.
        // Spots: each drop spot in canvas coordinates. Marker: the translucent shadow inserted into an effect stack
        protected sealed class DragState
        {
            public Type Type; public Func<object> Create; public object Block; public List<object> Blocks = new(); public Slot Source; public int Index;
            public List<VisualElement> SourceViews = new(); public VisualElement Preview, Marker; public Vector2 Grab, Pointer; public DropTarget Hovered; public bool OverPalette, OverCanvas;
            public Dictionary<DropTarget, Rect> Spots;
            public IVisualElementScheduledItem Ticker;
            public bool WholeLoose => Source?.Owner is LooseBlock loose && Index == 0 && Blocks.Count == loose.blocks.Count;
            public bool IsHome(DropTarget target) => Block != null && target.Slot.Same(Source) && target.Index == Index && !WholeLoose;
        }

        [SerializeField] protected List<LooseBlock> looseBlocks = new();
        protected readonly List<DropTarget> dropTargets = new();
        protected bool buildingPreview;
        protected bool altDrag;
        protected DragState drag;
        // View and path of each block drawn on the canvas
        protected readonly Dictionary<object, (VisualElement view, string path)> blockViews = new(FieldPath.ReferenceComparer.Instance);

        protected void RememberBlock(object block, VisualElement view, string path)
        {
            blockViews[block] = (view, path);
            view.EnableInClassList("block-selected", selected.Contains(block));
        }

        // Innermost (smallest) block under the pointer
        protected object BlockAt(Vector2 pointer)
        {
            if (!canvasViewport.worldBound.Contains(pointer)) return null;
            object best = null;
            float bestArea = float.MaxValue;
            foreach (var pair in blockViews)
            {
                Rect r = pair.Value.view.worldBound;
                if (pair.Value.view.panel == null || !r.Contains(pointer) || r.width * r.height >= bestArea) continue;
                best = pair.Key;
                bestArea = r.width * r.height;
            }
            return best;
        }

        // Text (LocalizedString) field. Read-only by default
        protected virtual VisualElement LocalizedField(FieldInfo field, object block, string path)
        {
            var text = block != null ? field.GetValue(block) as UnityEngine.Localization.LocalizedString : null;
            string current = LocalizedText.Read(text);
            return Ui.Label(current.Length > 0 ? current : FieldLabel(field), "title-field-placeholder");
        }

        // New key when splitting a copied block's text
        protected virtual string NextTextKey() => $"{CanvasAsset.name}/{GUID.Generate().ToString()[..8]}";

        // ───────────── Palette ─────────────

        protected void RefreshPalette()
        {
            if (paletteScroll == null) return;
            paletteScroll.Clear();
            categoryRail.Clear();
            string query = searchField.value?.Trim() ?? string.Empty;

            foreach (string category in Categories)
            {
                if (!ShowsCategory(category)) continue;
                var types = All.Where(b => b.Category == category && ShowsBlock(b) && Loaded(b.Type) && !MadeByButton.Contains(b.Type) && !IsBranch(b.Type) && !IsFamilyVariant(b.Type) && (query.Length == 0 || b.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderBy(b => b.Shape).ThenBy(b => b.Name).ToList();
                Action<string> section = category switch
                {
                    VariableCategory => AddVariablePalette,
                    MyBlockCategory => AddFunctionPalette,
                    _ => null,
                };
                if (types.Count == 0 && section == null) continue;

                Label header = Ui.Label(T(category), "category-header");
                paletteScroll.Add(header);
                section?.Invoke(query);
                // If a category holds several socket kinds (sensing: trigger, condition, value), split them under subheaders
                var groups = types.GroupBy(b => b.Base).ToList();
                foreach (var group in groups)
                {
                    if (groups.Count > 1) paletteScroll.Add(Ui.Label(DisplayOf(group.Key), "category-subheader"));
                    foreach (BlockType blockType in group) paletteScroll.Add(PaletteBlock(blockType.Type));
                }

                VisualElement rail = Ui.El("rail-button");
                rail.Add(Ui.El("rail-dot"));
                rail[0].style.backgroundColor = CategoryColor(category);
                rail.Add(Ui.Label(T(category), "rail-label"));
                rail.RegisterCallback<ClickEvent>(_ => paletteScroll.ScrollTo(header));
                categoryRail.Add(rail);
            }
        }

        // Module blocks show only when the edited data has loaded that module
        bool Loaded(Type type) => type.GetCustomAttribute<ScrapWeaver.ModuleAttribute>() is not ScrapWeaver.ModuleAttribute module || (Modules?.Contains(module.Name) ?? false);

        protected static IEnumerable<string> AllModules() =>
            TypeCache.GetTypesWithAttribute<ScrapWeaver.ModuleAttribute>().Select(t => t.GetCustomAttribute<ScrapWeaver.ModuleAttribute>().Name).Distinct().OrderBy(n => n);

        // Variables category header: [Make a Variable] and a pill per variable, grouped under scope subheaders (global, local, object, persistent)
        protected void AddVariablePalette(string query)
        {
            paletteScroll.Add(Ui.With(new Button(CreateVariable) { text = T("Make a Variable") }, "palette-button"));
            AddBuiltinVariables(query);
            VariableScope? shown = null;
            foreach (var (key, scope, type, component) in DeclaredVariables(CanvasTarget).OrderBy(v => Array.IndexOf(CreateScopes, v.scope)).ThenBy(v => Array.FindIndex(CreateTypes, k => k.type == v.type && k.component == v.component)))
            {
                if (query.Length > 0 && key.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (shown != scope) paletteScroll.Add(Ui.Label($"{Ui.EnumText(scope)} variables", "category-subheader"));
                shown = scope;
                VisualElement pill = type switch
                {
                    VariableType.Vector2 => PaletteBlock(typeof(Vector2Variable), () => new Vector2Variable(key, scope)),
                    VariableType.Vector3 => PaletteBlock(typeof(Vector3Variable), () => new Vector3Variable(key, scope)),
                    VariableType.Transform => PaletteBlock(typeof(TransformVariable), () => new TransformVariable(key, scope)),
                    VariableType.Int => PaletteBlock(typeof(IntVariable), () => new IntVariable(key, scope)),
                    VariableType.Bool => PaletteBlock(typeof(BoolVariable), () => new BoolVariable(key, scope)),
                    VariableType.Component => PaletteBlock(typeof(ComponentVariable), () => new ComponentVariable(key, scope, component)),
                    _ => PaletteBlock(typeof(VariableValue), () => new VariableValue(key, scope)),
                };
                pill.tooltip = $"{Ui.EnumText(scope)} {TypeTag(type, component)} variable";
                var row = Ui.El("variable-row");
                row.Add(pill);
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    if (scope == VariableScope.Local) evt.menu.AppendAction(T("Rename"), _ => RenameVariable(key, scope, type));
                    evt.menu.AppendAction(T("Delete"), _ => DeleteVariable(key, scope));
                }));
                paletteScroll.Add(row);
            }
        }

        // Built-in variables: filled automatically while the script runs. Cannot be created or deleted.
        // this = the running object, other = the other side of the event that started the line, item = the current "for each" element
        void AddBuiltinVariables(string query)
        {
            var builtins = new (string name, VariableType type, Type block, Func<object> create)[]
            {
                ("this", VariableType.Transform, typeof(ThisValue), () => new ThisValue()),
                ("other", VariableType.Transform, typeof(OtherValue), () => new OtherValue()),
                ("Time.time", VariableType.Number, typeof(TimeValue), () => new TimeValue(TimeValue.Kind.Game)),
                ("Time.deltaTime", VariableType.Number, typeof(TimeValue), () => new TimeValue(TimeValue.Kind.Frame)),
            };
            bool header = false;
            foreach (var (name, type, block, create) in builtins)
            {
                if (query.Length > 0 && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!header) paletteScroll.Add(Ui.Label(T("Built-in variables"), "category-subheader"));
                header = true;
                VisualElement pill = PaletteBlock(block, create);
                pill.tooltip = T("Built-in {0} variable", Ui.EnumText(type));
                // Type tag shown semi-transparent on the block, like variable sockets
                VisualElement title = pill.Q(className: "node-title") ?? pill;
                title.Add(Ui.Label(TypeTag(type), "variable-type"));
                paletteScroll.Add(pill);
            }
        }

        // Blocks made by a category header button or a definition hat. Not shown in the normal list
        protected static readonly HashSet<Type> MadeByButton = new()
        {
            typeof(ThisValue), typeof(OtherValue), typeof(ElementValue), typeof(TimeValue),
            typeof(VariableValue), typeof(IntVariable), typeof(BoolVariable), typeof(Vector2Variable), typeof(Vector3Variable), typeof(TransformVariable), typeof(ComponentVariable), typeof(FunctionBlock), typeof(CallBlock), typeof(ArgumentValue), typeof(ArgumentCondition),
        };


        // My Blocks category header: [Make a Block] and a call block per defined function
        protected void AddFunctionPalette(string query)
        {
            paletteScroll.Add(Ui.With(new Button(CreateFunction) { text = T("Make a Block") }, "palette-button"));
            foreach (FunctionBlock function in Functions())
            {
                if (query.Length > 0 && FunctionText(function).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                paletteScroll.Add(PaletteBlock(typeof(CallBlock), () => NewCall(function)));
            }
        }

        protected VisualElement PaletteBlock(Type type) => PaletteBlock(type, () => Activator.CreateInstance(type));

        // Shown in the palette with the same shape as on the canvas
        protected VisualElement PaletteBlock(Type type, Func<object> create)
        {
            VisualElement view = Preview(() => BlockView(create(), null, -1, null, null));
            view.tooltip = type.FullName;
            view.AddToClassList("palette-block");
            view.AddManipulator(new DragManipulator(p => BeginDrag(type, null, null, -1, null, p, view, new Vector2(20f, 12f), create),
                UpdateDrag, CompleteDrag, CancelDrag));
            return view;
        }

        // View-only block with no drop zones or drag handle
        protected VisualElement Preview(Func<VisualElement> build)
        {
            bool was = buildingPreview;
            buildingPreview = true;
            try { return build(); }
            finally { buildingPreview = was; }
        }

        // ───────────── Canvas ─────────────

        protected void RefreshCanvas()
        {
            if (canvasLayer == null) return;
            canvasLayer.Unbind();
            canvasLayer.Clear();
            dropTargets.Clear();
            blockViews.Clear();
            OnCanvasCleared();
            if (serializedItem == null || WorkingSet == null)
            {
                canvasLayer.Add(Ui.With(new HelpBox(T(EmptyHelp),HelpBoxMessageType.Info), "canvas-help"));
                AfterCanvasRefresh();
                return;
            }

            serializedItem.Update();
            looseBlocks.RemoveAll(l => l.blocks == null || l.blocks.Count == 0 || l.blocks.Contains(null));
            serializedWindow.Update();
            AddRootNodes();

            // Definition hats (function, buff) each sit on their own outside the root stacks
            ScrapSet set = DefinitionSet;
            string definitionsPath = $"{DefinitionSetPath}.{DefinitionsField.Name}";
            for (int i = 0; i < (set.definitions?.Length ?? 0); i++)
            {
                DefinitionBlock definition = set.definitions[i];
                if (definition == null) continue;
                VisualElement holder = Ui.El("loose-stack");
                holder.style.left = definition.position.x;
                holder.style.top = definition.position.y;
                holder.Add(BlockView(definition, Slot.Of(set, DefinitionsField), i, $"{definitionsPath}.Array.data[{i}]", serializedItem));
                canvasLayer.Add(holder);
            }
            // Editing a value field inside a block also marks unsaved changes
            canvasLayer.TrackSerializedObjectValue(serializedItem, _ => SetDirty(true));

            for (int i = 0; i < looseBlocks.Count; i++)
            {
                LooseBlock loose = looseBlocks[i];
                if (loose.item != CanvasAsset) continue;
                Slot slot = LooseSlot(loose);
                string path = $"{nameof(looseBlocks)}.Array.data[{i}].{nameof(LooseBlock.blocks)}";
                VisualElement holder = Ui.El("loose-stack");
                holder.style.left = loose.position.x;
                holder.style.top = loose.position.y;
                // Effect stacks accept more blocks above and below
                holder.Add(slot.Shape == BlockShape.Part ? BuildSequence(slot, path, serializedWindow) : BlockView(loose.blocks[0], slot, 0, $"{path}.Array.data[0]", serializedWindow));
                canvasLayer.Add(holder);
            }
            AfterCanvasRefresh();
        }

        protected static Slot LooseSlot(LooseBlock loose)
        {
            BlockShape shape = ShapeOfBlock(loose.blocks[0].GetType());
            return new Slot { Owner = loose, Field = LooseBlocksField, Accepts = shape == BlockShape.Part ? typeof(ScrapPart) : typeof(object), Shape = shape, Multiple = true };
        }

        protected LooseBlock NewLoose(Vector2 position, IEnumerable<object> blocks) => new() { item = CanvasAsset, position = position, blocks = blocks.ToList() };

        // Any asset holding effects; its ScrapSet field becomes the canvas root
        protected static FieldInfo ScrapSetField(Type type) =>
            type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(f => f.FieldType == typeof(ScrapSet));

        // What the canvas edits: the working copy, or a directly edited target given by the mode
        protected UnityEngine.Object CanvasTarget => working != null ? working : serializedItem?.targetObject;

        // Effect set the canvas edits and its serialized path. Defaults to the target's single ScrapSet field; for assets with several, the subclass window picks
        protected virtual ScrapSet WorkingSet => CanvasTarget is UnityEngine.Object target ? ScrapSetField(target.GetType())?.GetValue(target) as ScrapSet : null;
        protected virtual string SetPath => ScrapSetField(CanvasTarget.GetType()).Name;
        // Set holding My Blocks and buff definitions. A subclass window overrides it to share across sets
        protected virtual ScrapSet DefinitionSet => WorkingSet;
        // Set declaring local variables, and the blocks that can use them
        protected virtual ScrapSet VariableSet => WorkingSet;
        protected virtual object LocalVariableUsers => WorkingSet?.All;
        protected virtual string DefinitionSetPath => SetPath;

        // Places the root node on the canvas. Windows laying out several effect sets on one canvas (mob HFSM) override this
        protected virtual void AddRootNodes()
        {
            if (ShowScripts) canvasLayer.Add(ScriptsNode());
            canvasLayer.Add(RootNode());
        }

        protected VisualElement RootNode() => RootNode(WorkingSet, SetPath, RootTitle(), RootPosition, DecorateRootTitle);

        protected VisualElement RootNode(ScrapSet set, string setPath, string titleText, Vector2 position, Action<VisualElement> decorate)
        {
            var slot = Slot.Of(set, PartsField);
            VisualElement root = Ui.El("node", "root-node");
            root.style.left = position.x;
            root.style.top = position.y;

            // One round-topped head like a Scratch hat; blocks attach below it (not wrapped)
            VisualElement title = Ui.El("node-title", "root-hat");
            title.style.backgroundColor = RootColor;
            title.Add(Ui.Label(titleText, "node-name"));
            decorate?.Invoke(title);
            title.Add(Ui.With(new PathElement(
                (w, h) => new[] { Vector2.zero, new Vector2(w, 0f), new Vector2(w * 0.5f, h) }, RootColor, OutlineColor, 1f,
                (w, h) => new[] { new Vector2(0f, 0.5f), new Vector2(w * 0.5f, h - 0.5f), new Vector2(w, 0.5f) }) { pickingMode = PickingMode.Ignore },
                "node-connector"));
            root.Insert(0, title);
            root.Insert(0, BuildSequence(slot, $"{setPath}.{PartsField.Name}", serializedItem));
            return root;
        }

        // Effects written in C# instead of blocks. Started together with the root stacks.
        // The canvas is zoomed/panned so IMGUI drawers (SubclassSelector) get no input; draw UI Toolkit fields directly
        protected VisualElement ScriptsNode()
        {
            SerializedProperty scripts = serializedItem.FindProperty($"{SetPath}.{nameof(ScrapSet.scripts)}");
            VisualElement list = Ui.El("scripts-list");
            for (int i = 0; i < scripts.arraySize; i++) list.Add(ScriptRow(scripts, i));
            list.Add(Ui.With(new Button(() => EditScripts(() =>
            {
                scripts.arraySize++;
                scripts.GetArrayElementAtIndex(scripts.arraySize - 1).managedReferenceValue = null;
            })) { text = T("Add Script") }, "scripts-add"));
            list.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            VisualElement node = Ui.El("node", "root-node", "scripts-node");
            node.style.left = ScriptsPosition.x;
            node.style.top = ScriptsPosition.y;
            VisualElement title = Ui.El("node-title");
            title.style.backgroundColor = ModuleColor;
            title.Add(Ui.Label(T("Scripts"), "node-name"));
            node.Insert(0, title);
            node.Insert(0, Mouth(ModuleColor, list));
            node.Insert(0, Colored(Ui.El("node-foot"), ModuleColor));
            return node;
        }

        // Head: class picker + remove. Body: that script's fields
        protected VisualElement ScriptRow(SerializedProperty scripts, int index)
        {
            SerializedProperty element = scripts.GetArrayElementAtIndex(index);
            List<Type> types = TypeCache.GetTypesDerivedFrom<ScrapScript>().Where(t => !t.IsAbstract).OrderBy(t => t.Name).ToList();
            List<string> names = types.Select(t => t.Name).ToList();
            Type current = element.managedReferenceValue?.GetType();

            var pick = new DropdownField(names, current != null ? names.IndexOf(current.Name) : -1);
            pick.AddToClassList("scripts-pick");
            pick.RegisterValueChangedCallback(evt => EditScripts(() =>
                element.managedReferenceValue = Activator.CreateInstance(types[names.IndexOf(evt.newValue)])));
            var remove = new Button(() => EditScripts(() =>
            {
                element.managedReferenceValue = null;
                scripts.DeleteArrayElementAtIndex(index);
            })) { text = "×" };

            VisualElement head = Ui.El("scripts-head");
            head.Add(pick);
            head.Add(remove);
            VisualElement row = Ui.El("node-fields", "scripts-row");
            row.Add(head);

            SerializedProperty end = element.GetEndProperty();
            SerializedProperty child = element.Copy();
            for (bool enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
                row.Add(BoundField(serializedItem, child.Copy(), child.displayName, "node-property"));
            return row;
        }

        protected void EditScripts(Action edit)
        {
            serializedItem.Update();
            edit();
            serializedItem.ApplyModifiedProperties();
            SetDirty(true);
            RefreshCanvas();
        }

        protected static VisualElement Colored(VisualElement element, Color color)
        {
            element.style.backgroundColor = color;
            return element;
        }


        protected static VisualElement Mouth(Color color, VisualElement content)
        {
            VisualElement mouth = Ui.El("node-mouth");
            mouth.Add(Colored(Ui.El("node-arm"), color));
            mouth.Add(Ui.With(content, "node-mouth-content"));
            return mouth;
        }

        // Effect stacks are added in reverse with column-reverse. Visual order stays top to bottom,
        // but draw order flips so the upper block's V notch draws over the block or foot below
        protected static void Put(VisualElement container, VisualElement child, bool socket)
        {
            if (socket) container.Add(child);
            else container.Insert(0, child);
        }

        protected VisualElement BuildSequence(Slot slot, string path, SerializedObject so)
        {
            List<object> blocks = Blocks(slot);
            if (slot.Shape != BlockShape.Part) return BuildSocketRow(slot, path, so, blocks);

            VisualElement sequence = Ui.El("block-sequence");
            Put(sequence, DropZone(slot, 0, false, blocks.Count == 0), false);
            for (int i = 0; i < blocks.Count; i++)
            {
                string elementPath = path == null ? null : $"{path}.Array.data[{i}]";
                Put(sequence, blocks[i] == null ? DropZone(slot, i, true, true) : BlockView(blocks[i], slot, i, elementPath, so), false);
                if (blocks[i] == null || !IsCap(blocks[i].GetType())) Put(sequence, DropZone(slot, i + 1, false, false), false);
            }
            return sequence;
        }

        // Socket array: chained inside one shape like Scratch's 'and' block: [A] and [B] and [empty].
        // A trailing empty socket is always shown; dropping there appends
        protected VisualElement BuildSocketRow(Slot slot, string path, SerializedObject so, List<object> blocks)
        {
            Color color = ShapeColor(slot.Shape) * 0.7f;
            color.a = 1f;
            VisualElement row = blocks.Count == 0 ? Ui.El("socket-row") : Ui.With(Shape(slot.Shape, color, false), "socket-row", "socket-join");
            for (int i = 0; i < blocks.Count; i++)
            {
                row.Add(DropZone(slot, i, false, false));
                row.Add(blocks[i] == null ? DropZone(slot, i, true, true) : Occupied(BlockView(blocks[i], slot, i, path == null ? null : $"{path}.Array.data[{i}]", so), slot, i));
            }
            row.Add(DropZone(slot, blocks.Count, false, true));
            return row;
        }

        protected VisualElement BuildSingle(Slot slot, string path, SerializedObject so)
        {
            bool socket = slot.Shape != BlockShape.Part;
            VisualElement holder = Ui.El(socket ? "socket-row" : "block-sequence");
            object block = slot.Get();
            Put(holder, block == null ? DropZone(slot, 0, true, true) : Occupied(BlockView(block, slot, 0, path, so), slot, 0), socket);
            return holder;
        }

        // Filled socket: dropping on the block replaces it and moves the old block aside
        protected VisualElement Occupied(VisualElement view, Slot slot, int index)
        {
            if (!buildingPreview) dropTargets.Add(new DropTarget { Slot = slot, Index = index, Replace = true, Swap = true, Zone = view });
            return view;
        }

        // With a null path, draws only the shape without value fields (palette, and preview while dragging from it)
        protected VisualElement BlockView(object block, Slot slot, int index, string path, SerializedObject so)
        {
            VisualElement view = block is ScrapPart part ? PartView(part, slot, index, path, so) : SensingView(block, slot, index, path, so);
            if (path != null && block != null && !buildingPreview) RememberBlock(block, view, path);
            return view;
        }

        // Effect node: colored title row + dark field body; with child sockets, a left arm and foot wrap the children
        protected VisualElement PartView(ScrapPart block, Slot slot, int index, string path, SerializedObject so)
        {
            Type type = block.GetType();
            Color color = ColorOf(type);
            FieldInfo[] mouths = PartFields(type);

            VisualElement node = Ui.El("node", "part-node");
            if (block is DefinitionBlock) node.AddToClassList("hat-node");
            VisualElement head = Ui.El("node-head");
            head.Add(Colored(TitleRow(type, block, path, so), color));
            VisualElement fields = Fields(type, path, so);
            if (fields != null) head.Add(fields);
            MakeDraggable(head, type, block, slot, index, path, node);
            node.Insert(0, head);

            for (int i = 0; i < mouths.Length; i++)
            {
                if (IsBranchList(mouths[i]))
                {
                    BranchMouths(node, block, mouths[i], path, so, color);
                    continue;
                }
                // Label the middle bar only with two or more mouths (like Scratch's 'else')
                if (i > 0)
                {
                    VisualElement bar = Colored(Ui.El("node-bar"), color);
                    bar.Add(Ui.Label(FieldLabel(mouths[i]), "node-bar-name"));
                    node.Insert(0, bar);
                }
                node.Insert(0, Mouth(color, SlotView(block, mouths[i], path, so)));
            }
            if (mouths.Length > 0) node.Insert(0, Colored(Ui.El("node-foot"), color));

            // Marks continuation to the next block by pulling the bottom edge into a V, colored like the lowest part (foot, body or title row)
            if (block is not DefinitionBlock && !IsCap(type))
            {
                Color bottom = mouths.Length == 0 && fields != null ? BodyColor : color;
                node.Add(Ui.With(new PathElement(
                    (w, h) => new[] { Vector2.zero, new Vector2(w, 0f), new Vector2(w * 0.5f, h) }, bottom, OutlineColor, 1f,
                    (w, h) => new[] { new Vector2(0f, 0.5f), new Vector2(w * 0.5f, h - 0.5f), new Vector2(w, 0.5f) }) { pickingMode = PickingMode.Ignore },
                    "node-connector"));
            }
            // Covers just the arm area so the head/bar bottom line doesn't cross the arm. Head and bars draw after the arm
            if (mouths.Length > 0)
                foreach (VisualElement piece in node.Children().Where(c => c.ClassListContains("node-head") || c.ClassListContains("node-bar")).ToList())
                    piece.Add(Colored(Ui.El("arm-joint"), color));
            head.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                foreach (VisualElement piece in node.Children())
                    if (piece.ClassListContains("node-bar") || piece.ClassListContains("node-foot")) piece.style.minWidth = head.resolvedStyle.width;
            });
            return node;
        }

        // Branch list: each branch has a header bar (branch fields like text or weight, plus −) and one mouth. The bottom bar's + adds a branch.
        // Branches themselves can't be dragged; only the blocks inside move
        protected void BranchMouths(VisualElement node, ScrapPart block, FieldInfo field, string path, SerializedObject so, Color color)
        {
            Slot slot = Slot.Of(block, field);
            List<object> branches = Blocks(slot);
            for (int j = 0; j < branches.Count; j++)
            {
                if (branches[j] is not ScrapPart branch) continue;
                string branchPath = path == null ? null : $"{path}.{field.Name}.Array.data[{j}]";
                VisualElement bar = Ui.With(Colored(Ui.El("node-bar"), color), "branch-bar");
                bar.Add(TitleRow(branch.GetType(), branch, branchPath, so));
                if (path != null)
                {
                    int index = j;
                    bar.Add(BranchButton("−", T("Remove this branch"), () => Edit("Remove Branch", () =>
                    {
                        List<object> rest = Blocks(slot);
                        rest.RemoveAt(index);
                        Write(slot, rest);
                    })));
                    bar.RegisterCallback<PointerDownEvent>(_ => OnPicked(branch));
                    RememberBlock(branch, bar, branchPath);
                }
                node.Insert(0, bar);
                node.Insert(0, Mouth(color, SlotView(branch, PartFields(branch.GetType())[0], branchPath, so)));
            }
            VisualElement add = Ui.With(Colored(Ui.El("node-bar"), color), "branch-bar");
            if (path != null)
                add.Add(BranchButton(T("+ branch"), T("Add a branch"), () => Edit("Add Branch", () =>
                    Write(slot, Blocks(slot).Append(Activator.CreateInstance(slot.Accepts)).ToList()))));
            node.Insert(0, add);
        }

        protected static Button BranchButton(string text, string tooltip, Action click)
        {
            var button = Ui.With(new Button(click) { text = text, tooltip = tooltip }, "branch-button");
            button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            return button;
        }

        // Sensing block: the shape is the kind. Like Scratch, name and sockets on one line, then value fields
        protected VisualElement SensingView(object block, Slot slot, int index, string path, SerializedObject so)
        {
            Type type = block.GetType();
            VisualElement shape = Ui.With(Shape(ShapeOfBlock(type), ColorOf(type), false), "node", "sensing-node");
            VisualElement title = TitleRow(type, block, path, so);
            // Only when standing alone (palette, loose on the canvas). A value in a socket already shows its type by where it sits
            if ((slot == null || slot.IsLoose) && ConstantTag(type) is string tag) title.Add(Ui.Label(tag, "variable-type"));
            MakeDraggable(shape, type, block, slot, index, path, shape);
            shape.Add(title);
            VisualElement fields = Fields(type, path, so);
            if (fields != null) shape.Add(fields);
            return shape;
        }

        // Shapes of sensing blocks and empty sockets: condition = hexagon, event = tag pointed on the right, value = rectangle
        // Pill: a quarter circle at each corner. Semicircles only up to one line's height. Taller multi-line blocks just get rounded corners so the first line's sockets stay inside the curve
        const float MaxPillRadius = 13f;
        static Vector2[] Pill(float w, float h)
        {
            const int Steps = 6;
            float r = Mathf.Min(MaxPillRadius, h * 0.5f, w * 0.5f);
            var points = new List<Vector2>((Steps + 1) * 4);
            // The four corners clockwise: top right → bottom right → bottom left → top left
            void Corner(float cx, float cy, float from)
            {
                for (int k = 0; k <= Steps; k++)
                {
                    float a = from + Mathf.PI * 0.5f * k / Steps;
                    points.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
            }
            Corner(w - r, r, -Mathf.PI * 0.5f);
            Corner(w - r, h - r, 0f);
            Corner(r, h - r, Mathf.PI * 0.5f);
            Corner(r, r, Mathf.PI);
            return points.ToArray();
        }

        protected static VisualElement Shape(BlockShape shape, Color color, bool hollow)
        {
            var element = new PathElement((w, h) =>
                {
                    float e = Mathf.Min(10f, h * 0.5f);
                    return shape switch
                    {
                        BlockShape.Condition => new[] { new Vector2(e, 0), new Vector2(w - e, 0), new Vector2(w, e), new Vector2(w, h - e), new Vector2(w - e, h), new Vector2(e, h), new Vector2(0, h - e), new Vector2(0, e) },
                        BlockShape.Event => new[] { Vector2.zero, new Vector2(w - e, 0), new Vector2(w, e), new Vector2(w, h - e), new Vector2(w - e, h), new Vector2(0, h) },
                        _ => Pill(w, h),
                    };
                },
                hollow ? new Color(0f, 0f, 0f, 0.3f) : color,
                hollow ? new Color(color.r, color.g, color.b, 0.9f) : new Color(0.07f, 0.07f, 0.07f),
                hollow ? 1.5f : 1f);
            return Ui.With(element, "shape-" + shape.ToString().ToLowerInvariant());
        }

        // The title row reads as a sentence: text, socket and value field pieces from [ScrapWeaver.Display] laid out in order
        protected VisualElement TitleRow(Type type, object block, string path, SerializedObject so)
        {
            VisualElement row = Ui.El("node-title");
            switch (block)
            {
                case FunctionBlock function:
                    FunctionTitle(row, function);
                    return row;
                case CallBlock call:
                    CallTitle(row, call, path, so);
                    return row;
                case ArgumentValue value:
                    row.Add(Ui.Label(ArgumentText(value.id), "node-name"));
                    return row;
                case ArgumentCondition condition:
                    row.Add(Ui.Label(ArgumentText(condition.id), "node-name"));
                    return row;
                case ElementValue element:
                    row.Add(Ui.Label(ElementText(element.source), "node-name"));
                    return row;
            }
            foreach (TitlePart part in TitleParts(type))
            {
                if (part.Field == null)
                {
                    row.Add(Ui.Label(part.Text, "node-name"));
                    continue;
                }
                // A definition's name is looked up by name from its users, so it isn't edited here
                if (block is DefinitionBlock && part.Field.FieldType == typeof(string))
                {
                    row.Add(Ui.Label(part.Field.GetValue(block) as string, "title-field-placeholder"));
                    continue;
                }
                VisualElement slot = IsReference(part.Field)
                    ? Ui.With(SlotView(block, part.Field, path, so), "socket-slot")
                    : TitleField(part.Field, block, path, so, TitleParts(type).Count == 1);
                slot.RegisterCallback<PointerDownEvent>(evt => { if (!OnFieldChrome(evt.target as VisualElement, slot)) evt.StopPropagation(); });
                // Labels ignore the pointer so grabbing one drags the block instead of scrubbing the number
                if (!IsReference(part.Field)) slot.RegisterCallback<GeometryChangedEvent>(_ => slot.Query(className: "unity-base-field__label").ForEach(label => label.pickingMode = PickingMode.Ignore));
                row.Add(slot);
            }
            // A block that passes an object to its inner line: an (element) pill on the header, dragged out like a My Block input
            if (block is IElementSource source && path != null && !string.IsNullOrEmpty(source.ElementId))
            {
                string id = source.ElementId;
                VisualElement pill = PaletteBlock(typeof(ElementValue), () => new ElementValue(id));
                pill.RemoveFromClassList("palette-block");
                VisualElement holder = Ui.El("argument-pill");
                holder.Add(pill);
                holder.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                row.Add(holder);
            }
            return row;
        }

        // As in Scratch, only the input box edits; grabbing a value field's label or empty space drags the block
        protected static bool OnFieldChrome(VisualElement element, VisualElement stop)
        {
            for (; element != null && element != stop; element = element.parent)
            {
                if (element is IMGUIContainer || element is Button || element.ClassListContains("unity-base-field__input") || element.ClassListContains("unity-foldout__toggle")) return false;
                if (element.ClassListContains("unity-base-field")) return true;
            }
            return false;
        }

        // Plain value field placed on the title row. In previews (no path) only its name shows, in an empty socket shape.
        // Enums are in-sentence dropdowns, so value only with no label. Also drop the label when the block has a single value field (e.g. a number)
        // Constant blocks (number, vector, true/false, enum value) have no words, so they show their type semi-transparent, like variable pills
        static string ConstantTag(Type type) =>
            type == typeof(NumberValue) ? "float"
                : type == typeof(Vector3Value) ? "Vector3"
            : type == typeof(BoolCondition) ? "bool"
            : type == typeof(EnumValue) ? "enum"
            : null;

        // An empty socket's gray text: the type it takes
        static string SocketTypeName(Type accepts) =>
            typeof(ValueSource).IsAssignableFrom(accepts) ? "float"
            : typeof(ScrapCondition).IsAssignableFrom(accepts) ? "bool"
            : DisplayOf(accepts);

        protected VisualElement TitleField(FieldInfo field, object block, string path, SerializedObject so, bool alone = false)
        {
            if (field.FieldType == typeof(UnityEngine.Localization.LocalizedString)) return LocalizedField(field, block, path);
            if (CustomTitleField(field, block) is VisualElement custom) return custom;
            if (field.FieldType.IsEnum)
                return path == null
                    ? Ui.Label(Ui.EnumText(block != null ? field.GetValue(block) : Enum.GetValues(field.FieldType).GetValue(0)) + "  ▾", "title-dropdown-placeholder")
                    : Ui.Dropdown(so, so.FindProperty($"{path}.{field.Name}"));
            return path == null
                ? Ui.Label(block != null && field.GetValue(block) is string text && text.Length > 0 ? text
                    : field.IsDefined(typeof(ScrapWeaver.EnumNameAttribute)) ? "Type.Value"
                    : alone && block != null ? field.GetValue(block)?.ToString() : FieldLabel(field), "title-field-placeholder")
                : BoundField(so, so.FindProperty($"{path}.{field.Name}"), alone ? "" : FieldLabel(field), "title-field");
        }

        protected static PropertyField BoundField(SerializedObject so, SerializedProperty property, string label, string className)
        {
            var field = Ui.With(new PropertyField(property, label), className);
            field.Bind(so);
            return field;
        }

        protected VisualElement SlotView(object block, FieldInfo field, string path, SerializedObject so)
        {
            Slot slot = Slot.Of(block, field);
            string childPath = path == null ? null : $"{path}.{field.Name}";
            return slot.Multiple ? BuildSequence(slot, childPath, so) : BuildSingle(slot, childPath, so);
        }

        // Plain value fields in the block body, excluding [SerializeReference] fields and those on the title row
        protected VisualElement Fields(Type type, string path, SerializedObject so)
        {
            if (path == null || !InlineFields) return null;
            VisualElement fields = Ui.El("node-fields");
            foreach (FieldInfo field in ScrapWeaverTypes.Fields(type))
            {
                if (IsReference(field) || InTitle(type, field) || so.FindProperty($"{path}.{field.Name}") is not SerializedProperty property) continue;
                fields.Add(BoundField(so, property, FieldLabel(field), "node-property"));
            }
            if (fields.childCount == 0) return null;
            fields.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            return fields;
        }

        // Grab the head to drag; right-click for the block menu
        protected void MakeDraggable(VisualElement handle, Type type, object block, Slot slot, int index, string path, VisualElement view)
        {
            if (buildingPreview) return;
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                altDrag = evt.altKey;
                OnPicked(block);
            }, TrickleDown.TrickleDown);
            handle.AddManipulator(new DragManipulator(p => BeginDrag(type, block, slot, index, path, p, view, p - view.worldBound.position),
                UpdateDrag, CompleteDrag, CancelDrag));
            if (slot == null) return;
            handle.RegisterCallback<PointerEnterEvent>(_ => PointAt(slot, index, block));
            handle.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                // Right-click on a value field is left to the field's own menu (copy/paste)
                if (IsInsideClass(evt.target as VisualElement, "node-fields", handle)) return;
                if (block is FunctionBlock function) evt.menu.AppendAction(T("Edit"), _ => EditFunction(function));
                else AddClipboardMenu(slot, index, block, evt);
                evt.menu.AppendAction(T("Delete"), _ => Delete(slot, index, block));
                evt.StopPropagation();
            }));
        }

        // An empty socket shows the accepted kind's name (trigger, condition, value) faintly inside its shape outline
        protected VisualElement DropZone(Slot slot, int index, bool replace, bool placeholder)
        {
            VisualElement zone = placeholder && slot.Shape != BlockShape.Part ? EmptySocket(slot)
                : placeholder ? Ui.El("drop-zone-empty") : Ui.El();
            zone.AddToClassList("drop-zone");
            if (!buildingPreview)
            {
                dropTargets.Add(new DropTarget { Slot = slot, Index = index, Replace = replace, Zone = zone });
                if (placeholder) TrackEmptyZone(zone, slot, index);
            }
            return zone;
        }

        protected static VisualElement EmptySocket(Slot slot)
        {
            VisualElement socket = Ui.With(Shape(slot.Shape, ShapeColor(slot.Shape), true), "socket-empty");
            socket.Add(Ui.Label(SocketTypeName(slot.Accepts), "socket-empty-label"));
            return socket;
        }

        // ───────────── Data editing ─────────────

        protected static List<object> Blocks(Slot slot) =>
            slot.Owner != null && slot.Field.GetValue(slot.Owner) is IList list ? list.Cast<object>().ToList() : new List<object>();

        protected static object Occupant(Slot slot, int index) =>
            slot.Multiple ? Blocks(slot).ElementAtOrDefault(index) : slot.Get();

        protected static void Write(Slot slot, List<object> blocks)
        {
            Type fieldType = slot.Field.FieldType;
            IList collection = fieldType.IsArray ? Array.CreateInstance(fieldType.GetElementType(), blocks.Count) : (IList)Activator.CreateInstance(fieldType);
            for (int i = 0; i < blocks.Count; i++)
                if (fieldType.IsArray) collection[i] = blocks[i];
                else collection.Add(blocks[i]);
            slot.Field.SetValue(slot.Owner, collection);
        }

        // Removes count blocks from index. Removes a loose canvas stack once it is empty
        protected List<object> TakeOut(Slot slot, int index, int count = 1)
        {
            if (!slot.Multiple)
            {
                object single = slot.Get();
                slot.Set(null);
                return new List<object> { single };
            }
            List<object> blocks = Blocks(slot);
            List<object> taken = blocks.GetRange(index, count);
            blocks.RemoveRange(index, count);
            Write(slot, blocks);
            if (slot.Owner is LooseBlock loose && blocks.Count == 0) looseBlocks.Remove(loose);
            return taken;
        }

        protected static void PutIn(Slot slot, int index, bool replace, IList<object> blocks)
        {
            if (!slot.Multiple)
            {
                slot.Set(blocks[0]);
                return;
            }
            List<object> list = Blocks(slot);
            if (replace && index < list.Count) list.RemoveAt(index);
            list.InsertRange(Mathf.Clamp(index, 0, list.Count), blocks);
            Write(slot, list);
        }

        // Detaches the block set from the whole tree. Call block argument sockets are only cleared, since the slot is the input
        protected static void Detach(object owner, ISet<object> targets)
        {
            if (owner == null) return;
            foreach (FieldInfo field in ScrapWeaverTypes.Fields(owner.GetType()).Where(IsReference))
            {
                object value = field.GetValue(owner);
                if (value is IList list)
                {
                    if (owner is CallBlock)
                    {
                        for (int i = 0; i < list.Count; i++)
                            if (list[i] != null && targets.Contains(list[i])) list[i] = null;
                    }
                    else if (list.Cast<object>().Any(targets.Contains))
                        Write(Slot.Of(owner, field), list.Cast<object>().Where(b => !targets.Contains(b)).ToList());
                    foreach (object child in (IList)field.GetValue(owner)) Detach(child, targets);
                }
                else if (value != null && targets.Contains(value)) field.SetValue(owner, null);
                else Detach(value, targets);
            }
        }

        protected void DeleteBlocks(IEnumerable<object> blocks)
        {
            var targets = new HashSet<object>(blocks, FieldPath.ReferenceComparer.Instance);
            if (targets.Count == 0) return;
            Edit("Delete Block", () =>
            {
                Detach(WorkingSet, targets);
                foreach (LooseBlock loose in looseBlocks.Where(l => l.item == CanvasAsset))
                {
                    loose.blocks.RemoveAll(targets.Contains);
                    foreach (object block in loose.blocks) Detach(block, targets);
                }
                looseBlocks.RemoveAll(l => l.blocks.Count == 0);
            });
        }

        protected void Edit(string undoName, Action edit)
        {
            if (CanvasTarget is not UnityEngine.Object target) return;
            Undo.RegisterCompleteObjectUndo(new[] { target, this }, undoName);
            edit();
            // Drone skills edit the prefab directly, without a copy
            if (working == null) EditorUtility.SetDirty(target);
            SetDirty(true);
            serializedItem.Update();
            serializedWindow.Update();
            RefreshCanvas();
        }

        // Prevents putting a block into itself or a descendant socket
        protected static bool Contains(object block, object target)
        {
            if (ReferenceEquals(block, target)) return true;
            if (block == null) return false;
            foreach (FieldInfo field in PartFields(block.GetType()).Concat(SocketFields(block.GetType())))
            {
                object value = field.GetValue(block);
                if (value is IList list ? list.Cast<object>().Any(child => Contains(child, target)) : Contains(value, target)) return true;
            }
            return false;
        }

        // ───────────── Variables ─────────────

        // For a copy edited by an open window, the set that window shows (the selected state for multi-state HFSMs)
        protected static ScrapSet SetOf(UnityEngine.Object target) =>
            target is ScrapterWindow window ? window.VariableSet
            : OpenWindows.FirstOrDefault(w => w.working != null && w.working == target) is ScrapterWindow editing ? editing.VariableSet
            : target != null ? ScrapSetField(target.GetType())?.GetValue(target) as ScrapSet : null;

        static IEnumerable<ScrapSet> SetsOf(UnityEngine.Object target) =>
            target is IScrapSets many ? many.Sets : SetOf(target) is ScrapSet set ? new[] { set } : Enumerable.Empty<ScrapSet>();

        // Selectable variables: that effect's local variables first, then global and persistent
        public static IEnumerable<(string key, VariableScope scope, VariableType type, string component)> DeclaredVariables(UnityEngine.Object target)
        {
            ScrapSet set = SetOf(target);
            foreach (string key in set?.variables ?? Enumerable.Empty<string>()) yield return (key, VariableScope.Local, VariableType.Number, null);
            foreach (VariableDecl d in set?.typedVariables ?? Enumerable.Empty<VariableDecl>()) yield return (d.name, VariableScope.Local, d.type, d.component);
            foreach (var shared in ScrapWeaverVariables.All) yield return shared;
        }

        // Uses the local variables of the set containing the socket. With several sets (HFSM states) on one canvas, each uses its own
        public static IEnumerable<(string key, VariableScope scope, VariableType type, string component)> DeclaredVariables(SerializedObject so, string propertyPath)
        {
            for (int dot = propertyPath.LastIndexOf('.'); dot > 0; dot = propertyPath.LastIndexOf('.', dot - 1))
            {
                SerializedProperty set = so.FindProperty(propertyPath[..dot]);
                if (set == null || set.type != nameof(ScrapSet)) continue;
                SerializedProperty names = set.FindPropertyRelative(nameof(ScrapSet.variables));
                for (int i = 0; i < names.arraySize; i++) yield return (names.GetArrayElementAtIndex(i).stringValue, VariableScope.Local, VariableType.Number, null);
                SerializedProperty typed = set.FindPropertyRelative(nameof(ScrapSet.typedVariables));
                for (int i = 0; i < typed.arraySize; i++)
                {
                    SerializedProperty d = typed.GetArrayElementAtIndex(i);
                    yield return (d.FindPropertyRelative(nameof(VariableDecl.name)).stringValue, VariableScope.Local, (VariableType)d.FindPropertyRelative(nameof(VariableDecl.type)).enumValueIndex,
                    d.FindPropertyRelative(nameof(VariableDecl.component)).stringValue);
                }
                foreach (var shared in ScrapWeaverVariables.All) yield return shared;
                yield break;
            }
            foreach (var declared in DeclaredVariables(so.targetObject)) yield return declared;
        }

        protected static readonly VariableScope[] CreateScopes = { VariableScope.Local, VariableScope.Self, VariableScope.Global, VariableScope.Persistent };
        // Types offered by Make a Variable. Only common components; add names here as needed
        static readonly (VariableType type, string component)[] CreateTypes =
        {
            (VariableType.Number, null), (VariableType.Int, null), (VariableType.Bool, null), (VariableType.Vector2, null), (VariableType.Vector3, null), (VariableType.Transform, null),
            (VariableType.Component, nameof(Rigidbody2D)), (VariableType.Component, nameof(Collider2D)), (VariableType.Component, nameof(SpriteRenderer)),
            (VariableType.Component, nameof(Animator)), (VariableType.Component, nameof(AudioSource)),
        };

        // Type name shown on palette pills and variable sockets (float, int, bool, Vector2, Vector3, Transform, component name)
        public static string TypeTag(VariableType type, string component = null) => type == VariableType.Component ? component : Ui.EnumText(type);

        protected void CreateVariable()
        {
            if (VariableSet == null) return;
            NameWindow.Open(T("New Variable"), T("New variable name:"),
                new[] { T("For this effect only (local)"), T("For all effects on this object (object)"), T("For all effects (global)"), T("Kept in saves (persistent)") },
                CreateTypes.Select(k => TypeTag(k.type, k.component)).ToArray(),
                (name, choice, kind) => name.Length == 0 ? T("Enter a name.")
                    : DeclaredVariables(CanvasTarget).Any(v => v.key == name) ? T("A variable with that name already exists.")
                    : CreateScopes[choice] == VariableScope.Persistent && CreateTypes[kind].type != VariableType.Number ? T("Persistent variables must be float.") : null,
                (name, choice, kind) =>
                {
                    VariableScope scope = CreateScopes[choice];
                    var (type, component) = CreateTypes[kind];
                    if (scope != VariableScope.Local) ScrapWeaverVariables.Add(name, scope, type, component);
                    else if (type == VariableType.Number) Edit("Make a Variable", () => VariableSet.variables.Add(name));
                    else Edit("Make a Variable", () => VariableSet.typedVariables.Add(new VariableDecl(name, type, component)));
                    RefreshPalette();
                });
        }

        // Local variables only. Renames the declaration and the blocks using it in this effect
        protected void RenameVariable(string key, VariableScope scope, VariableType type)
        {
            NameWindow.Open(T("Rename Variable"), T("New name for '{0}':", key), null,
                name => name.Length == 0 ? T("Enter a name.") : name != key && DeclaredVariables(CanvasTarget).Any(v => v.key == name) ? T("A variable with that name already exists.") : null,
                (name, _) =>
                {
                    if (name == key) return;
                    Edit("Rename Variable", () =>
                    {
                        int at = VariableSet.variables.IndexOf(key);
                        if (at >= 0) VariableSet.variables[at] = name;
                        for (int i = 0; i < VariableSet.typedVariables.Count; i++)
                            if (VariableSet.typedVariables[i].name == key)
                        {
                            VariableDecl d = VariableSet.typedVariables[i];
                            d.name = name;
                            VariableSet.typedVariables[i] = d;
                        }
                        foreach (object found in Walk(LocalVariableUsers))
                            foreach (FieldInfo field in ScrapWeaverTypes.Fields(found.GetType()))
                                if (field.GetCustomAttribute<ScrapWeaver.VariableKeyAttribute>() is { } attr && (string)field.GetValue(found) == key
                                    && (VariableScope)field.DeclaringType.GetField(attr.ScopeField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(found) == scope)
                                    field.SetValue(found, name);
                    });
                    OnVariableRenamed(key, name);
                    RefreshPalette();
                });
        }

        protected virtual void OnVariableRenamed(string from, string to) { }

        protected void DeleteVariable(string key, VariableScope scope)
        {
            List<string> users = scope != VariableScope.Local ? SharedVariableUsers(key, scope).ToList()
                : VariableUses(LocalVariableUsers).Contains((key, scope)) ? new List<string> { CanvasAsset.name } : new List<string>();
            if (users.Count > 0)
            {
                EditorUtility.DisplayDialog(T("Delete Variable"), T("Can't delete '{0}': blocks are using it.", key) + "\n" + string.Join("\n", users), T("OK"));
                return;
            }
            if (scope != VariableScope.Local) ScrapWeaverVariables.Remove(key, scope);
            else Edit("Delete Variable", () =>
            {
                VariableSet.variables.Remove(key);
                VariableSet.typedVariables.RemoveAll(d => d.name == key);
            });
            RefreshPalette();
        }

        // Assets using global/persistent variables. Open ones are checked via their unsaved copy
        protected IEnumerable<string> SharedVariableUsers(string key, VariableScope scope)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (SetsOf(asset == item && working != null ? working : asset).Any(set => VariableUses(set?.All).Contains((key, scope))))
                    yield return asset.name;
            }
        }

        // Collects every [VariableKey] field in the block tree
        protected static IEnumerable<(string key, VariableScope scope)> VariableUses(object block) =>
            from found in Walk(block)
            from field in ScrapWeaverTypes.Fields(found.GetType())
            let attr = field.GetCustomAttribute<ScrapWeaver.VariableKeyAttribute>()
            where attr != null
            select ((string)field.GetValue(found),
                (VariableScope)field.DeclaringType.GetField(attr.ScopeField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(found));

        // The block and every block nested in it
        protected static IEnumerable<object> Walk(object block)
        {
            if (block is IList list) return list.Cast<object>().SelectMany(Walk);
            if (block == null) return Enumerable.Empty<object>();
            return ScrapWeaverTypes.Fields(block.GetType()).Where(IsReference).SelectMany(f => Walk(f.GetValue(block))).Prepend(block);
        }

        // ───────────── Definitions (My Blocks) ─────────────

        protected IEnumerable<FunctionBlock> Functions() => DefinitionSet?.definitions?.OfType<FunctionBlock>() ?? Enumerable.Empty<FunctionBlock>();

        protected static string FunctionText(FunctionBlock function) => string.Join(" ", function.pieces.Select(p => p.text));

        // An (element) pill's name comes from the block that gives it. Shown as missing if that block is gone
        protected string ElementText(string id) =>
            CanvasBlocks().SelectMany(Walk).OfType<IElementSource>().FirstOrDefault(e => e.ElementId == id)?.ElementName ?? T("(missing element)");

        protected string ArgumentText(string id) => Functions().SelectMany(f => f.pieces).FirstOrDefault(p => p.id == id)?.text ?? T("(missing input)");

        // Every block on this screen: effect data (definitions included) and loose canvas blocks
        protected IEnumerable<object> CanvasBlocks() =>
            (WorkingSet?.All ?? Array.Empty<ScrapPart>()).Cast<object>()
            .Concat(looseBlocks.Where(l => l.item == CanvasAsset).SelectMany(l => l.blocks));

        protected static CallBlock NewCall(FunctionBlock function) => new()
        {
            function = function.id,
            numbers = new ValueSource[function.pieces.Count(p => p.kind == FunctionPiece.Kind.Number)],
            conditions = new ScrapCondition[function.pieces.Count(p => p.kind == FunctionPiece.Kind.Bool)],
        };

        // New definition hats go near the center of the visible canvas
        protected Vector2 NewDefinitionPosition() => canvasLayer.WorldToLocal(canvasViewport.worldBound.center) - new Vector2(120f, 40f);

        protected void AddDefinition(DefinitionBlock definition) =>
            DefinitionSet.definitions = (DefinitionSet.definitions ?? Array.Empty<DefinitionBlock>()).Append(definition).ToArray();

        protected void CreateFunction()
        {
            if (WorkingSet == null) return;
            Vector2 at = NewDefinitionPosition();
            BlockMakerWindow.Open(null, pieces =>
            {
                Edit("Make a Block", () => AddDefinition(new FunctionBlock { id = Guid.NewGuid().ToString("N"), pieces = pieces, position = at }));
                RefreshPalette();
            });
        }

        protected void EditFunction(FunctionBlock function) =>
            BlockMakerWindow.Open(function.pieces, pieces =>
            {
                Edit("Edit Block", () => Reshape(function, pieces));
                RefreshPalette();
            });

        // Re-matches call block arguments to input ids after a reshape. Arguments of removed inputs are dropped
        protected void Reshape(FunctionBlock function, List<FunctionPiece> pieces)
        {
            static List<string> Ids(IEnumerable<FunctionPiece> from, FunctionPiece.Kind kind) => from.Where(p => p.kind == kind).Select(p => p.id).ToList();
            static T[] Remap<T>(T[] args, List<string> from, List<string> to) where T : class =>
                to.Select(id => from.IndexOf(id) is int i && i >= 0 && i < args.Length ? args[i] : null).ToArray();

            List<string> oldNumbers = Ids(function.pieces, FunctionPiece.Kind.Number), oldConditions = Ids(function.pieces, FunctionPiece.Kind.Bool);
            List<string> newNumbers = Ids(pieces, FunctionPiece.Kind.Number), newConditions = Ids(pieces, FunctionPiece.Kind.Bool);
            foreach (CallBlock call in CanvasBlocks().SelectMany(Walk).OfType<CallBlock>().Where(c => c.function == function.id).ToList())
            {
                call.numbers = Remap(call.numbers ?? Array.Empty<ValueSource>(), oldNumbers, newNumbers);
                call.conditions = Remap(call.conditions ?? Array.Empty<ScrapCondition>(), oldConditions, newConditions);
            }
            function.pieces = pieces;
        }

        // A definition isn't deleted while blocks still use it
        protected void Delete(Slot slot, int index, object block)
        {
            if (block is DefinitionBlock definition && DefinitionInUse(definition) is string reason)
            {
                EditorUtility.DisplayDialog(T("Delete Definition"), reason, T("OK"));
                RefreshCanvas();
                return;
            }
            Edit("Delete Block", () => TakeOut(slot, index));
            if (block is DefinitionBlock) RefreshPalette();
        }

        protected string DefinitionInUse(DefinitionBlock definition)
        {
            List<object> blocks = CanvasBlocks().Where(b => !ReferenceEquals(b, definition)).SelectMany(Walk).ToList();
            return definition switch
            {
                FunctionBlock function when blocks.OfType<CallBlock>().Any(c => c.function == function.id) => T("Can't delete: this block is still called somewhere."),
                _ => null,
            };
        }

        // Definition hat: "define" followed by the block's shape. Drag input pills out to use them in the body
        protected void FunctionTitle(VisualElement row, FunctionBlock function)
        {
            row.Add(Ui.Label(T("define"), "node-name"));
            VisualElement shape = Ui.El("function-shape");
            foreach (FunctionPiece piece in function.pieces)
            {
                if (piece.kind == FunctionPiece.Kind.Label)
                {
                    shape.Add(Ui.Label(piece.text, "node-name"));
                    continue;
                }
                string id = piece.id;
                VisualElement pill = piece.kind == FunctionPiece.Kind.Number
                    ? PaletteBlock(typeof(ArgumentValue), () => new ArgumentValue { id = id })
                    : PaletteBlock(typeof(ArgumentCondition), () => new ArgumentCondition { id = id });
                pill.RemoveFromClassList("palette-block");
                VisualElement holder = Ui.El("argument-pill");
                holder.Add(pill);
                holder.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                shape.Add(holder);
            }
            row.Add(shape);
        }

        protected static readonly FieldInfo CallNumbersField = typeof(CallBlock).GetField(nameof(CallBlock.numbers));
        protected static readonly FieldInfo CallConditionsField = typeof(CallBlock).GetField(nameof(CallBlock.conditions));

        // Call block: lays out labels and argument sockets following the definition's shape
        protected void CallTitle(VisualElement row, CallBlock call, string path, SerializedObject so)
        {
            FunctionBlock function = Functions().FirstOrDefault(f => f.id == call.function);
            if (function == null)
            {
                row.Add(Ui.Label(T("(missing block)"), "node-name"));
                return;
            }
            int number = 0, condition = 0;
            foreach (FunctionPiece piece in function.pieces)
            {
                if (piece.kind == FunctionPiece.Kind.Label)
                {
                    row.Add(Ui.Label(piece.text, "node-name"));
                    continue;
                }
                bool isNumber = piece.kind == FunctionPiece.Kind.Number;
                FieldInfo field = isNumber ? CallNumbersField : CallConditionsField;
                int index = isNumber ? number++ : condition++;
                string childPath = path == null ? null : $"{path}.{field.Name}.Array.data[{index}]";
                VisualElement socket = Ui.With(BuildSingle(Slot.Element(call, field, index), childPath, so), "socket-slot");
                socket.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                row.Add(socket);
            }
        }

        // ───────────── Drag and drop ─────────────

        protected void BeginDrag(Type type, object block, Slot source, int index, string path, Vector2 pointer, VisualElement sourceView, Vector2 grab, Func<object> create = null)
        {
            if (serializedItem == null) return;
            EndDrag();
            drag = new DragState
            {
                Type = type, Create = create ?? (() => Activator.CreateInstance(type)), Block = block, Source = source, Index = index,
                Grab = block != null ? grab / zoom : grab,
            };
            // Grabbing in an effect stack drags the blocks below too. Alt-grab takes just that block. Definition hats stand alone, so only one
            if (block != null)
            {
                int count = source != null && source.Multiple && source.Shape == BlockShape.Part && !altDrag && block is not DefinitionBlock ? Blocks(source).Count - index : 1;
                drag.Blocks = source != null && source.Multiple ? Blocks(source).GetRange(index, count) : new List<object> { block };
                foreach (object moved in drag.Blocks)
                    if (blockViews.TryGetValue(moved, out var entry)) drag.SourceViews.Add(entry.view);
                if (drag.SourceViews.Count == 0) drag.SourceViews.Add(sourceView);
            }

            // The dragged block follows the pointer in its original shape
            SerializedObject so = source != null && source.IsLoose ? serializedWindow : serializedItem;
            drag.Preview = Ui.El("drag-preview");
            drag.Preview.Add(Preview(() => DragPreview(path, so)));
            drag.Preview.pickingMode = PickingMode.Ignore;
            drag.Preview.style.scale = new StyleScale(new Scale(new Vector3(zoom, zoom, 1f)));
            rootVisualElement.Add(drag.Preview);

            // Like Scratch, the grabbed blocks leave the stack and it closes up. A value taken out of a single socket leaves an empty socket
            foreach (VisualElement view in drag.SourceViews) view.AddToClassList("block-drag-source");
            if (block != null && source is { Multiple: false } && source.Shape != BlockShape.Part) drag.SourceViews[0].parent?.Add(EmptySocket(source));
            if (ShapeOfBlock(type) == BlockShape.Part && !typeof(DefinitionBlock).IsAssignableFrom(type))
            {
                drag.Marker = Ui.With(Preview(() => DragPreview(null, so)), "insertion-marker");
                drag.Marker.pickingMode = PickingMode.Ignore;
            }
            if (drag.SourceViews.Count == 0) TakeSpots();
            drag.Ticker = canvasViewport.schedule.Execute(DragTick).Every(16);
            UpdateDrag(pointer);
        }

        // Drop spots are measured once, after the grabbed blocks left and the stack closed.
        // The inserted shadow pushes blocks down but the spots stay put, so the target doesn't flicker between neighbours
        protected void TakeSpots()
        {
            drag.Spots = new Dictionary<DropTarget, Rect>();
            foreach (DropTarget target in dropTargets)
                if (target.Zone.panel != null && CanDrop(target)) drag.Spots[target] = canvasLayer.WorldToLocal(target.Zone.worldBound);
        }

        protected void DragTick()
        {
            if (drag == null) return;
            // Measure once the removed blocks are reflected on screen (style and layout)
            if (drag.Spots == null && drag.SourceViews.All(v => v.resolvedStyle.position == Position.Absolute))
            {
                TakeSpots();
                UpdateDrag(drag.Pointer);
            }
            ScrollAtEdge();
        }

        // Blocks dragged along are the next elements in the stack path
        protected VisualElement DragPreview(string path, SerializedObject so)
        {
            if (drag.Block == null) return BlockView(drag.Create(), drag.Source, drag.Index, null, so);
            if (drag.Blocks.Count == 1) return BlockView(drag.Block, drag.Source, drag.Index, path, so);
            string sequencePath = path?.Substring(0, path.LastIndexOf(".Array.data[", StringComparison.Ordinal));
            VisualElement stack = Ui.El("block-sequence");
            for (int i = 0; i < drag.Blocks.Count; i++)
                Put(stack, BlockView(drag.Blocks[i], drag.Source, drag.Index + i, sequencePath == null ? null : $"{sequencePath}.Array.data[{drag.Index + i}]", so), false);
            return stack;
        }

        protected const float EdgeScrollBand = 32f;
        protected const float EdgeScrollSpeed = 14f;

        // While dragging, scrolls the canvas when the pointer enters an edge band
        protected void ScrollAtEdge()
        {
            if (drag == null || !drag.OverCanvas) return;
            Rect r = canvasViewport.worldBound;
            static float Push(float p, float low, float high) =>
                p < low + EdgeScrollBand ? EdgeScrollSpeed * (1f - (p - low) / EdgeScrollBand)
                : p > high - EdgeScrollBand ? -EdgeScrollSpeed * (1f - (high - p) / EdgeScrollBand)
                : 0f;
            var delta = new Vector2(Push(drag.Pointer.x, r.xMin, r.xMax), Push(drag.Pointer.y, r.yMin, r.yMax));
            if (delta == Vector2.zero) return;
            pan += delta;
            ApplyView();
            UpdateDrag(drag.Pointer);
        }

        // A filled socket's Zone is the block itself, so the empty-socket (height-changing) class must not be used
        protected static string ActiveClass(DropTarget target) => target.Swap ? "drop-swap-active" : "drop-zone-active";

        protected Vector2 PreviewTopLeft(Vector2 pointer) => pointer - drag.Grab * zoom;

        protected void UpdateDrag(Vector2 pointer)
        {
            if (drag == null) return;
            drag.Pointer = pointer;
            Vector2 local = rootVisualElement.WorldToLocal(PreviewTopLeft(pointer));
            drag.Preview.style.translate = new StyleTranslate(new Translate(local.x, local.y, 0f));
            drag.OverPalette = palettePane.worldBound.Contains(pointer);
            drag.OverCanvas = canvasViewport.worldBound.Contains(pointer);
            palettePane.EnableInClassList("trash-active", drag.OverPalette && drag.Block != null);

            // Snaps when the dragged block's top-left corner nears a socket. On overlap (a socket inside a filled block), the smaller wins.
            // A shown spot holds a little farther out and isn't given up to a neighbour at about the same distance (as in Scratch)
            DropTarget next = null;
            if (!drag.OverPalette && drag.Spots != null)
            {
                Vector2 snap = canvasLayer.WorldToLocal(PreviewTopLeft(pointer) + new Vector2(8f, 6f) * zoom);
                float best = float.MaxValue;
                float bestArea = float.MaxValue;
                foreach (var (target, r) in drag.Spots)
                {
                    bool current = target == drag.Hovered;
                    float distance = new Vector2(Mathf.Max(r.xMin - snap.x, 0f, snap.x - r.xMax), Mathf.Max(r.yMin - snap.y, 0f, snap.y - r.yMax)).magnitude * zoom;
                    if (distance > (current ? StickyRadius : SnapRadius)) continue;
                    float score = current ? Mathf.Max(0f, distance - StickyPreference) : distance;
                    float area = r.width * r.height;
                    if (score > best || (score == best && area >= bestArea)) continue;
                    best = score;
                    bestArea = area;
                    next = target;
                }
            }
            if (next == drag.Hovered) return;
            Highlight(drag.Hovered, false);
            drag.Hovered = next;
            Highlight(next, true);
        }

        // An effect-stack spot gets a translucent shadow of the dragged blocks that pushes the blocks below down. Sockets and filled slots just light up
        protected void Highlight(DropTarget target, bool on)
        {
            if (target == null) return;
            if (drag.Marker == null || target.Swap)
            {
                target.Zone.EnableInClassList(ActiveClass(target), on);
                return;
            }
            if (on) target.Zone.Add(drag.Marker);
            else drag.Marker.RemoveFromHierarchy();
            target.Zone.EnableInClassList("drop-zone-marker", on);
        }

        protected bool CanDrop(DropTarget target)
        {
            // Definition hats are never inserted; they just move on the canvas
            if (typeof(DefinitionBlock).IsAssignableFrom(drag.Type)) return false;
            // Dropping back home leaves things as they were. A whole loose stack doesn't snap to itself; it just moves
            if (drag.Block != null && target.Slot.Same(drag.Source))
            {
                if (drag.WholeLoose) return false;
                if (drag.IsHome(target)) return true;
                if (target.Slot.Multiple && !target.Replace && target.Index > drag.Index && target.Index <= drag.Index + drag.Blocks.Count) return false;
            }
            // Cap blocks only at the end of a stack
            if (target.Slot.Multiple && !target.Replace && IsCap(drag.Block == null ? drag.Type : drag.Blocks[^1].GetType())
                && target.Index < Blocks(target.Slot).Count - (target.Slot.Same(drag.Source) ? drag.Blocks.Count : 0)) return false;
            if (drag.Block == null) return FitFor(drag.Type, target.Slot.Accepts) != null;
            if (!drag.Blocks.All(b => target.Slot.Accepts.IsAssignableFrom(b.GetType()))) return false;
            if (drag.Blocks.Any(b => Contains(b, target.Slot.Owner))) return false;
            // Multiple blocks are only inserted between stack blocks
            if (drag.Blocks.Count > 1 && (!target.Slot.Multiple || target.Replace)) return false;
            return !target.Swap || !ReferenceEquals(Occupant(target.Slot, target.Index), drag.Block);
        }

        protected void CompleteDrag()
        {
            DragState d = drag;
            if (d == null) return;
            Vector2 canvasPosition = canvasLayer.WorldToLocal(PreviewTopLeft(d.Pointer));
            EndDrag();

            if (d.OverPalette)
            {
                if (d.Blocks.Count > 1) Edit("Delete Block", () => TakeOut(d.Source, d.Index, d.Blocks.Count));
                else if (d.Block != null) Delete(d.Source, d.Index, d.Block);
                else RefreshCanvas();
            }
            else if (d.Hovered != null && d.IsHome(d.Hovered)) RefreshCanvas();
            else if (d.Hovered != null)
            {
                DropTarget target = d.Hovered;
                Rect zone = target.Zone.worldBound;
                Vector2 ejectPosition = canvasLayer.WorldToLocal(new Vector2(zone.xMax + 12f, zone.yMax + 8f));
                Edit(d.Block == null ? "Add Block" : "Move Block", () =>
                {
                    int index = target.Index;
                    List<object> moving;
                    if (d.Block != null)
                    {
                        moving = TakeOut(d.Source, d.Index, d.Blocks.Count);
                        if (target.Slot.Same(d.Source) && target.Slot.Multiple && d.Index < index) index -= moving.Count;
                    }
                    else
                    {
                        Type fit = FitFor(d.Type, target.Slot.Accepts);
                        moving = new List<object> { fit == d.Type ? d.Create() : Activator.CreateInstance(fit) };
                    }
                    object displaced = target.Swap ? Occupant(target.Slot, index) : null;
                    PutIn(target.Slot, index, target.Replace, moving);
                    if (displaced != null) looseBlocks.Add(NewLoose(ejectPosition, new[] { displaced }));
                });
            }
            else if (d.OverCanvas)
            {
                Edit("Drop Block", () =>
                {
                    if (d.Block is DefinitionBlock definition) definition.position = canvasPosition;
                    else if (d.Source?.Owner is LooseBlock loose && d.Index == 0 && d.Blocks.Count == loose.blocks.Count) loose.position = canvasPosition;
                    else looseBlocks.Add(NewLoose(canvasPosition, d.Block != null ? TakeOut(d.Source, d.Index, d.Blocks.Count) : new List<object> { d.Create() }));
                });
            }
            else RefreshCanvas();
        }

        protected void CancelDrag()
        {
            EndDrag();
            RefreshCanvas();
        }

        protected void EndDrag()
        {
            if (drag == null) return;
            Highlight(drag.Hovered, false);
            palettePane.RemoveFromClassList("trash-active");
            foreach (VisualElement view in drag.SourceViews) view.RemoveFromClassList("block-drag-source");
            drag.Ticker?.Pause();
            drag.Preview.RemoveFromHierarchy();
            drag = null;
        }
    }
}
