using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ParkourMap'in kopyasını ParkourMapReverse adıyla oluşturur ve iki sahneyi de build listesine ekler.
// Bunker'a girince geçilen dönüş sahnesi bu kopyadan düzenlenir.
public static class ReverseSceneMaker
{
    const string SourcePath = "Assets/Scenes/ParkourMap.unity";
    const string TargetPath = "Assets/Scenes/ParkourMapReverse.unity";

    [MenuItem("Tools/Level/Ters Sahneyi Oluştur (ParkourMap Kopyası)")]
    static void Create()
    {
        // Açık sahnedeki kaydedilmemiş değişiklikler kopyaya da girsin
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetPath) != null)
        {
            // Üzerinde çalışılmış olabilir, ezmiyoruz
            Debug.LogWarning($"{TargetPath} zaten var, üzerine yazılmadı. Baştan oluşturmak istersen önce onu sil.");
        }
        else if (!AssetDatabase.CopyAsset(SourcePath, TargetPath))
        {
            Debug.LogError($"Sahne kopyalanamadı: {SourcePath}");
            return;
        }

        AddToBuildSettings(SourcePath);
        AddToBuildSettings(TargetPath);

        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetPath));
        Debug.Log($"Ters sahne hazır: {TargetPath}. ParkourMap ve ParkourMapReverse build listesinde.");
    }

    static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;

        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
