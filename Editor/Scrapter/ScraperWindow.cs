using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FireFlower.ScrapterText;

namespace FireFlower
{
    // Generic Scraper: edits any ScriptableObject with a ScrapSet field as blocks. Block script assets (ScrapAsset) are also created here.
    // For a game-specific window, inherit ScrapterWindow instead of this one
    public sealed class ScraperWindow : ScrapterWindow
    {
        ObjectField itemField;

        protected override string WindowName => "Scraper";
        protected override string EmptyHelp => "Pick an asset to edit as blocks, or create a block script with [New].";

        [MenuItem("Tools/FireFlower/Scraper")]
        public static void Open() => Show(Selection.activeObject as ScriptableObject);

        public static void Show(ScriptableObject source)
        {
            var window = GetWindow<ScraperWindow>();
            window.minSize = new Vector2(900f, 500f);
            if (CanOpen(source)) window.SelectItem(source);
        }

        [MenuItem("Assets/Open as Blocks", true)]
        static bool CanOpenSelection() => CanOpen(Selection.activeObject as ScriptableObject);

        [MenuItem("Assets/Open as Blocks", priority = 20)]
        static void OpenSelection() => Open();

        [OnOpenAsset]
        static bool OnOpenAsset(int instanceId, int line)
        {
            if (EditorUtility.EntityIdToObject(instanceId) is not ScrapAsset asset) return false;
            Show(asset);
            return true;
        }

        static bool CanOpen(ScriptableObject source) => source != null && ScrapSetField(source.GetType()) != null;

        protected override void BuildToolbar(VisualElement host)
        {
            itemField = Ui.With(new ObjectField { objectType = typeof(ScriptableObject), allowSceneObjects = false, tooltip = T("An asset with a ScrapSet field") }, "item-object-field");
            itemField.RegisterValueChangedCallback(evt =>
            {
                var picked = evt.newValue as ScriptableObject;
                if (picked != null && !CanOpen(picked))
                {
                    Debug.LogWarning($"{picked.name} has no ScrapSet field, so it cannot be opened as blocks.", picked);
                    itemField.SetValueWithoutNotify(item);
                    return;
                }
                SelectItem(picked);
            });
            // Unity's picker lists every ScriptableObject; swap its button for one that lists only assets that open as blocks
            itemField.Q(className: ObjectField.selectorUssClassName).style.display = DisplayStyle.None;
            itemField.style.flexGrow = 1f;
            var pick = new Button { text = "▾", tooltip = T("An asset with a ScrapSet field"), style = { marginLeft = 2, marginRight = 0, width = 20 } };
            pick.clicked += () => new AssetDropdown(OpenableAssets(), picked => itemField.value = picked).Show(pick.worldBound);
            var fieldHost = Ui.With(new VisualElement(), "item-field-host");
            fieldHost.style.flexDirection = FlexDirection.Row;
            fieldHost.Add(itemField);
            fieldHost.Add(pick);
            host.Add(fieldHost);
            host.Add(new Button(Create) { text = T("New"), style = { marginLeft = 6 } });
        }

        protected override void RefreshChoices() => itemField?.SetValueWithoutNotify(item);

        static List<ScriptableObject> OpenableAssets()
        {
            var guids = new HashSet<string>();
            var found = new List<ScriptableObject>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (type.IsAbstract || ScrapSetField(type) == null) continue;
                foreach (string guid in AssetDatabase.FindAssets($"t:{type.Name}"))
                    if (guids.Add(guid) && AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is ScriptableObject asset && CanOpen(asset))
                        found.Add(asset);
            }
            return found;
        }

        // Grouped by type when more than one kind of asset opens as blocks
        sealed class AssetDropdown : AdvancedDropdown
        {
            readonly List<ScriptableObject> assets;
            readonly Action<ScriptableObject> picked;

            public AssetDropdown(List<ScriptableObject> assets, Action<ScriptableObject> picked) : base(new AdvancedDropdownState())
            {
                this.assets = assets;
                this.picked = picked;
                minimumSize = new Vector2(260f, 320f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(T("Block assets"));
                var byType = assets.GroupBy(a => a.GetType()).OrderBy(g => g.Key.Name, StringComparer.Ordinal).ToList();
                foreach (var group in byType)
                {
                    AdvancedDropdownItem parent = root;
                    if (byType.Count > 1) root.AddChild(parent = new AdvancedDropdownItem(group.Key.Name));
                    foreach (ScriptableObject asset in group.OrderBy(a => a.name, StringComparer.OrdinalIgnoreCase))
                        parent.AddChild(new AssetItem(asset));
                }
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is AssetItem chosen) picked(chosen.asset);
            }
        }

        sealed class AssetItem : AdvancedDropdownItem
        {
            public readonly ScriptableObject asset;
            public AssetItem(ScriptableObject asset) : base(asset.name)
            {
                this.asset = asset;
                icon = AssetPreview.GetMiniThumbnail(asset);
            }
        }

        void Create()
        {
            string folder = item != null ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(item)) : "Assets";
            string path = EditorUtility.SaveFilePanelInProject(T("New Block Script"), "NewScrap", "asset", T("Where to save the block script asset"), folder);
            if (string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance<ScrapAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            SelectItem(asset);
        }
    }
}
