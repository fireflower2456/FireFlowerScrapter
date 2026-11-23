using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using static FireFlower.ScrapterText;

namespace FireFlower
{
    // Shared base for windows that assemble an asset's ScrapSet out of blocks. Child windows decide what to open and which blocks to show
    // (e.g. items/buffs, CinematicScraperWindow: dialogue/cutscenes).
    // Effects (ScrapPart) are nodes that wrap children in a C shape; other [SerializeReference]s (TriggerEvent, conditions, values)
    // become 'sensing' blocks that snap into matching-shape sockets inside nodes. The canvas is in .Canvas, copy/paste in .Clipboard.
    // item is the source asset, working is the editing copy — working overwrites item only on save (button / Ctrl+S)
    public abstract partial class ScrapterWindow : EditorWindow
    {
        protected const string Folder = "Packages/com.fireflower.scrapter/Editor/Scrapter/";
        const float MinZoom = 0.25f;
        const float MaxZoom = 2f;
        const float ZoomFactor = 1.1f;
        const float GridSpacing = 20f;
        const int GridMajorEvery = 5;
        static readonly Color GridMinor = new(0.16f, 0.16f, 0.16f);
        static readonly Color GridMajor = new(0.11f, 0.11f, 0.11f);

        [SerializeField] protected ScriptableObject item;
        [SerializeField] protected ScriptableObject working;
        [SerializeField] protected bool dirty;
        [SerializeField] float zoom = 1f;
        protected float Zoom => zoom;
        [SerializeField] Vector2 pan;
        [SerializeField] float paletteWidth = 300f;
        [SerializeField] List<LocalizationFollowsDelete.TextRef> texts = new();
        [SerializeField] protected List<LocalizedText.Original> originals = new();
        protected SerializedObject serializedItem;
        protected SerializedObject serializedWindow;

        protected Label paneTitle;
        protected Button saveButton;
        protected VisualElement palettePane;
        protected VisualElement categoryRail;
        protected ScrollView paletteScroll;
        protected VisualElement canvasViewport;
        protected VisualElement gridLayer;
        protected VisualElement canvasLayer;
        protected Label zoomLabel;
        protected TextField searchField;
        protected ScrollView itemPane;

        protected abstract string WindowName { get; }
        // Variable/buff pickers look here to find the set of the copy being edited
        static readonly List<ScrapterWindow> OpenWindows = new();

        // ───────────── Filled in by child windows ─────────────

        // Left side of the top bar (pickers, create buttons)
        protected virtual void BuildToolbar(VisualElement host) { }
        // Extra views attached after the window is built (preview, scene view, etc.)
        protected virtual void OnGUIBuilt() { }
        // Categories and blocks shown in the palette
        protected virtual bool ShowsCategory(string category) => true;
        protected virtual bool ShowsBlock(BlockType block) => true;
        protected virtual ICollection<string> Modules => null;
        // Canvas hint when nothing is open
        protected virtual string EmptyHelp => "Pick an asset to edit with blocks.";
        // Whether to show a list of effects written in C# instead of blocks
        protected virtual bool ShowScripts => true;
        // Whether to expand value fields in the block body. Off leaves only the title-row sockets (windows with a separate edit pane)
        protected virtual bool InlineFields => true;
        // When the root line runs
        protected virtual string RootTitle() => T("when {0} starts", working.name);
        protected virtual void DecorateRootTitle(VisualElement title) { }
        // When a block, branch or hat is clicked. null means nothing selected
        protected virtual void OnPicked(object block) { }
        protected virtual void AfterCanvasRefresh() { }
        // Special socket in the title row (summary text instead of a value, etc.). null means the default socket
        protected virtual VisualElement CustomTitleField(System.Reflection.FieldInfo field, object block) => null;
        // Right pane
        protected virtual void RefreshSidePane() { }
        protected virtual void RefreshChoices() { }
        // What the canvas edits: the copy by default. Child windows supply targets edited directly without a copy (prefab parts, etc.)
        protected virtual UnityEngine.Object DirectTarget => null;
        // Which view loose blocks on the canvas are tied to
        protected virtual ScriptableObject CanvasAsset => item;
        protected virtual void OnLoading(ScriptableObject selected) { }
        protected virtual void OnSelected(ScriptableObject selected) { }
        protected virtual void OnBinding() { }
        protected virtual void SaveDirect() { }
        protected virtual void AfterUndo() { }
        protected virtual void OnViewChanged() { }
        // Right before the canvas redraws. Forgets marks attached to blocks
        protected virtual void OnCanvasCleared() { }
        protected virtual void OnClosing() { }
        protected virtual void PingTarget()
        {
            if (CanvasAsset != null) EditorGUIUtility.PingObject(Selection.activeObject = CanvasAsset);
        }

        [Shortcut("ScrapWeaver/Save", typeof(ScrapterWindow), KeyCode.S, ShortcutModifiers.Action)]
        static void SaveShortcut(ShortcutArguments args) => ((ScrapterWindow)args.context).Save();

        public void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder + "ScrapWeaverWindow.uxml");
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(Folder + "ScrapWeaverWindow.uss");
            if (tree == null || style == null)
            {
                rootVisualElement.Add(new HelpBox(T("Could not find the ScrapWeaver UXML or USS."), HelpBoxMessageType.Error));
                return;
            }
            tree.CloneTree(rootVisualElement);
            ScrapterText.Translate(rootVisualElement);
            rootVisualElement.styleSheets.Add(style);
            serializedWindow = new SerializedObject(this);

            T Q<T>(string name) where T : VisualElement => rootVisualElement.Q<T>(name);
            palettePane = Q<VisualElement>("palettePane");
            categoryRail = Q<VisualElement>("categoryRail");
            paletteScroll = Q<ScrollView>("paletteScroll");
            canvasViewport = Q<VisualElement>("canvasViewport");
            gridLayer = Q<VisualElement>("gridLayer");
            canvasLayer = Q<VisualElement>("canvasLayer");
            zoomLabel = Q<Label>("zoomLabel");
            searchField = Q<TextField>("searchField");
            itemPane = Q<ScrollView>("itemPane");
            saveButton = Q<Button>("saveButton");
            paneTitle = Q<Label>("paneTitle");

            palettePane.style.width = paletteWidth;
            float widthStart = 0f, resizeStart = 0f;
            Q<VisualElement>("paletteResizer").AddManipulator(new DragManipulator(
                p => { resizeStart = p.x; widthStart = palettePane.resolvedStyle.width; },
                p => palettePane.style.width = paletteWidth = Mathf.Clamp(widthStart + p.x - resizeStart, 250f, 900f),
                threshold: 0f));

            AddLanguagePicker(saveButton);
            BuildToolbar(Q<VisualElement>("toolbarHost"));
            OnGUIBuilt();

            searchField.RegisterValueChangedCallback(_ => RefreshPalette());
            saveButton.clicked += Save;
            Q<Button>("pingButton").clicked += PingTarget;
            Q<Button>("clearButton").clicked += ClearEffects;
            Q<Button>("zoomInButton").clicked += () => SetZoom(zoom * ZoomFactor);
            Q<Button>("zoomOutButton").clicked += () => SetZoom(zoom / ZoomFactor);
            Q<Button>("zoomResetButton").clicked += () => { zoom = 1f; pan = Vector2.zero; ApplyView(); };

            // Dragging empty canvas with the left/right button, or anywhere with the middle button, pans the view. Shift+left is box select
            Vector2 panStart = default, pointerStart = default;
            canvasViewport.AddManipulator(new DragManipulator(
                p => { pointerStart = p; panStart = pan; },
                p => { pan = panStart + (p - pointerStart); ApplyView(); },
                threshold: 0f,
                accept: evt => evt.button == (int)MouseButton.MiddleMouse
                               || (!(evt.shiftKey && evt.button == (int)MouseButton.LeftMouse) && !IsInsideClass(evt.target as VisualElement, "node", canvasViewport)),
                buttons: new[] { MouseButton.LeftMouse, MouseButton.MiddleMouse, MouseButton.RightMouse }));
            canvasViewport.RegisterCallback<WheelEvent>(evt =>
            {
                SetZoom(evt.delta.y > 0f ? zoom / ZoomFactor : zoom * ZoomFactor, evt.mousePosition);
                evt.StopPropagation();
            });
            gridLayer.generateVisualContent += DrawGrid;
            RegisterClipboard();

            RefreshPalette();
            ApplyView();
            // Keep using the in-progress copy even when reopened after a compile
            if (working == null || item == null) Load(item);
            else BindWorking();

            Undo.undoRedoPerformed += OnUndoRedo;
            ScrapterText.Changed -= Rebuild;
            ScrapterText.Changed += Rebuild;
            if (!OpenWindows.Contains(this)) OpenWindows.Add(this);
        }

        // Language changed: build the whole window again. The editing copy is kept
        void Rebuild()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            rootVisualElement.Clear();
            // Only our sheet: the root also carries Unity's default editor sheet (fonts)
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(Folder + "ScrapWeaverWindow.uss");
            if (style != null) rootVisualElement.styleSheets.Remove(style);
            CreateGUI();
            SetDirty(dirty);
        }

        void AddLanguagePicker(VisualElement before)
        {
            var picker = Ui.With(new DropdownField(ScrapterText.Languages, ScrapterText.Current), "language-picker");
            // Events are pooled: read the value now, not inside the delayed call
            picker.RegisterValueChangedCallback(evt =>
            {
                string picked = evt.newValue;
                EditorApplication.delayCall += () => ScrapterText.Current = picked;
            });
            before.parent.Insert(before.parent.IndexOf(before), picker);
        }

        void OnDisable()
        {
            ScrapterText.Changed -= Rebuild;
            OpenWindows.Remove(this);
            OnClosing();
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnDestroy()
        {
            if (working != null) DestroyImmediate(working);
            RestoreTexts();
            TidyTexts();
        }

        void OnUndoRedo()
        {
            serializedItem?.Update();
            serializedWindow?.Update();
            // Also called by other windows' undo, so decide by whether the copy actually differs from the source
            if (working != null) SetDirty(originals.Count > 0 || EditorJsonUtility.ToJson(working) != EditorJsonUtility.ToJson(item));
            RefreshCanvas();
            AfterUndo();
        }

        // ───────────── Load · Save ─────────────

        protected void SelectItem(ScriptableObject selected)
        {
            if (selected == item) return;
            if (dirty)
            {
                int choice = EditorUtility.DisplayDialogComplex(T("Unsaved Changes"), T("{0} has unsaved changes.", CanvasAsset.name), T("Save"), T("Cancel"), T("Discard"));
                if (choice == 1)
                {
                    RefreshChoices();
                    return;
                }
                if (choice == 0) Save();
            }
            Load(selected);
            OnSelected(selected);
        }

        protected void Load(ScriptableObject selected)
        {
            OnLoading(selected);
            if (working != null) DestroyImmediate(working);
            RestoreTexts();
            TidyTexts();
            item = selected;
            working = null;
            if (item != null)
            {
                working = Instantiate(item);
                working.name = item.name;
                working.hideFlags = HideFlags.DontSave;
            }
            dirty = false;
            BindWorking();
            RememberTexts();
        }

        protected void BindWorking()
        {
            OnBinding();
            serializedItem = working != null ? new SerializedObject(working)
                : DirectTarget is UnityEngine.Object direct ? new SerializedObject(direct)
                : null;
            RefreshChoices();
            SetDirty(dirty);
            RefreshSidePane();
            RefreshCanvas();
            // The local variable list differs per opened target
            RefreshPalette();
        }

        protected void SetDirty(bool value)
        {
            dirty = value;
            hasUnsavedChanges = value;
            saveChangesMessage = CanvasAsset == null ? null : T("The effects of {0} have unsaved changes.", CanvasAsset.name);
            titleContent = new GUIContent(value ? T(WindowName) + "*" : T(WindowName));
            saveButton?.SetEnabled(value);
        }

        protected void Save()
        {
            SaveDirect();
            if (item == null || working == null) return;
            if (SplitSharedTexts()) SetDirty(true);
            if (!dirty) return;
            EditorUtility.CopySerialized(working, item);
            item.name = working.name;
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssetIfDirty(item);
            AssetDatabase.SaveAssets();   // also the tables holding edited dialogue lines
            RenameToKey();
            SetDirty(false);
            originals.Clear();
            TidyTexts();
            RememberTexts();
        }

        // Text keys opened or created in this window. After save or discard, remove from the table those no longer used anywhere in the project
        // (so deleted blocks and dialogue closed without saving don't pile up as junk)
        protected void RememberText(LocalizedString text)
        {
            if (LocalizedText.Entry(text, out var collection) is { } entry) texts.Add(new(collection.TableCollectionName, entry.Id));
        }

        protected void RememberTexts()
        {
            var visited = new HashSet<object>(FieldPath.ReferenceComparer.Instance);
            foreach (object block in CanvasBlocks().ToList()) EachText(block, RememberText, visited);
        }

        // Dialogue lines are written to the table immediately, so keep the text from the first edit and restore it on discard
        protected void KeepOriginal(LocalizedString text, string locale)
        {
            if (LocalizedText.Capture(text, locale) is { } original && !originals.Any(o => o.Same(original))) originals.Add(original);
        }

        protected void RestoreTexts()
        {
            foreach (LocalizedText.Original original in originals) LocalizedText.Restore(original);
            if (originals.Count > 0) AssetDatabase.SaveAssets();
            originals.Clear();
        }

        protected void TidyTexts()
        {
            if (texts.Count == 0) return;
            LocalizationFollowsDelete.RemoveIfUnused(texts);
            texts.Clear();
        }

        // The asset file name follows Key (so new items/buffs don't stay as NewItem n)
        void RenameToKey()
        {
            string key = (item as IKeyedCatalogItem)?.Key;
            if (string.IsNullOrWhiteSpace(key) || item.name == key) return;
            string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(item), key);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"Could not rename the asset to Key ({key}): {error}", item);
                return;
            }
            working.name = item.name;
            RefreshChoices();
        }

        public override void SaveChanges()
        {
            Save();
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            SetDirty(false);
            base.DiscardChanges();
        }

        void ClearEffects()
        {
            ScrapSet set = WorkingSet;
            if (set?.parts == null || set.parts.Length == 0) return;
            if (!EditorUtility.DisplayDialog(T("Clear Effects"), T("Delete all {0} effects of {1}?", set.parts.Length, CanvasAsset.name), T("Clear"), T("Cancel"))) return;
            Edit("Clear All Effects", () => set.parts = Array.Empty<ScrapPart>());
        }

        // The first entry is 'None'. Keep the current value even if it dropped out of the list (just deleted, etc.)
        protected static void RefreshChoices<T>(PopupField<T> field, IEnumerable<T> all, T current) where T : class
        {
            if (field == null) return;
            var choices = new List<T> { null };
            choices.AddRange(all);
            if (current != null && !choices.Contains(current)) choices.Add(current);
            field.choices = choices;
            field.SetValueWithoutNotify(current);
        }

        protected static List<T> FindAll<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null)
            .ToList();

        // ───────────── Zoom · Pan · Grid ─────────────

        // Applies pan and zoom together in one frame without a scroll view — the point under the pointer stays put
        void SetZoom(float value, Vector2? worldPivot = null)
        {
            float next = Mathf.Clamp(value, MinZoom, MaxZoom);
            if (Mathf.Approximately(next, zoom)) return;
            Vector2 pivot = canvasViewport.WorldToLocal(worldPivot ?? canvasViewport.worldBound.center);
            pan = pivot - (pivot - pan) * (next / zoom);
            zoom = next;
            ApplyView();
        }

        protected void ApplyView()
        {
            canvasLayer.style.translate = new StyleTranslate(new Translate(pan.x, pan.y, 0f));
            canvasLayer.style.scale = new StyleScale(new Scale(new Vector3(zoom, zoom, 1f)));
            zoomLabel.text = $"{Mathf.RoundToInt(zoom * 100f)}%";
            gridLayer.MarkDirtyRepaint();
            OnViewChanged();
        }

        void DrawGrid(MeshGenerationContext context)
        {
            Rect rect = gridLayer.contentRect;
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

        protected static bool IsInsideClass(VisualElement element, string className, VisualElement stop)
        {
            for (; element != null && element != stop; element = element.parent)
                if (element.ClassListContains(className)) return true;
            return false;
        }
    }
}
