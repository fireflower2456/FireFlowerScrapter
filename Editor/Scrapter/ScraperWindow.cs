using UnityEditor;
using UnityEditor.Callbacks;
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
            var fieldHost = Ui.With(new VisualElement(), "item-field-host");
            fieldHost.Add(itemField);
            host.Add(fieldHost);
            host.Add(new Button(Create) { text = T("New"), style = { marginLeft = 6 } });
        }

        protected override void RefreshChoices() => itemField?.SetValueWithoutNotify(item);

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
