using UnityEditor;
using UnityEngine;

// Hazır etkileşim objesi prefab'ı: trigger collider + Interactable + görünür küp.
public static class InteractablePrefabMaker
{
    [MenuItem("Tools/Subtitles/Etkileşim Objesi Prefabı Oluştur")]
    public static void Create()
    {
        const string folder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var root = new GameObject("Interactable");
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.2f, 1.2f, 1.2f);
        root.AddComponent<Interactable>();

        // Görünür kısım, collider'ı kaldırılır ki oyuncuyu engellemesin
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Interactable.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"Etkileşim objesi prefabı oluşturuldu: {path}");
    }
}
