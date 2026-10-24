using UnityEditor;
using UnityEngine;

// Hazır bir Cutscene Trigger prefab'ı oluşturur: trigger collider + CutsceneTrigger scripti.
public static class CutscenePrefabMaker
{
    [MenuItem("Tools/Subtitles/Cutscene Trigger Prefabı Oluştur")]
    public static void Create()
    {
        const string folder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var go = new GameObject("CutsceneTrigger");
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3f, 2f, 3f);
        go.AddComponent<CutsceneTrigger>();

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/CutsceneTrigger.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"Cutscene trigger prefabı oluşturuldu: {path}");
    }
}
