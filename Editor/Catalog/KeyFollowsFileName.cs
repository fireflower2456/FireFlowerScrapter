using UnityEditor;

namespace FireFlower
{
    class KeyFollowsFileName : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in moved)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) is not IKeyedCatalogItem keyed) continue;
                var asset = (UnityEngine.Object)keyed;
                if (keyed.Key == asset.name) continue;

                var so = new SerializedObject(asset);
                if (so.FindProperty("key") is not SerializedProperty key) continue;
                key.stringValue = asset.name;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
    }
}
