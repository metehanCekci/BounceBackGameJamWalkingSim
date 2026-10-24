using UnityEditor;
using UnityEngine;

// Her sahneye konacak AudioManager prefab'ını oluşturur.
public static class AudioPrefabMaker
{
    [MenuItem("Tools/Audio/AudioManager Prefabı Oluştur")]
    public static void Create()
    {
        const string folder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/AudioManager.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"AudioManager prefabı oluşturuldu: {path}");
    }
}
