using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Açık sahneyi alaca karanlığa çevirir: güneş ufkun hemen üstünde, güneşe göre hesaplanan gerçek gökyüzü,
// karanlığı geri açmasın diye sınırlanmış pozlama ve siren huzmeleri havada görünsün diye hafif hacimsel sis.
// Proje genelindeki varsayılan profile dokunmaz (ana menü ve diğer sahneler etkilenmesin), sahneye kendi Volume'unu koyar.
// Tekrar çalıştırmak güvenli: aynı profil ve Volume güncellenir.
public static class DuskLightingSetup
{
    const string ProfilePath = "Assets/Settings/ParkourMapDusk.asset";
    const string VolumeName = "Dusk Volume";
    const float SunElevation = 4f; // Derece; ufkun hemen üstü, güneş hâlâ biraz parlıyor

    [MenuItem("Tools/Level/Alaca Karanlık Kur (Açık Sahne)")]
    static void Setup()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
            profile = VolumeProfileFactory.CreateVolumeProfileAtPath(ProfilePath);

        // Hazır gündüz resmi (HDRI) yerine güneşin konumuna göre hesaplanan atmosfer: güneş alçalınca gökyüzü kendisi kızarır
        var environment = Get<VisualEnvironment>(profile);
        environment.skyType.Override((int)SkyType.PhysicallyBased);
        environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

        // Gökyüzü parlaklığı buradan: eksi değer gökyüzünü koyulaştırır
        Get<PhysicallyBasedSky>(profile).exposure.Override(0f);

        // Varsayılan Automatic pozlama sahne kararınca kamerayı açıp her şeyi gündüze çeviriyordu;
        // aralığı sınırlayıp biraz kısık tutuyoruz
        var exposure = Get<Exposure>(profile);
        exposure.mode.Override(ExposureMode.AutomaticHistogram);
        exposure.compensation.Override(-1f);
        exposure.limitMin.Override(9f);
        exposure.limitMax.Override(13f);

        // Hafif pus + hacimsel sis: dönen siren huzmeleri havada görünsün.
        // Başlangıçla kule arası ~230 m; daha yoğun sis kulenin silüetini yutar
        var fog = Get<Fog>(profile);
        fog.enabled.Override(true);
        fog.meanFreePath.Override(350f);
        fog.baseHeight.Override(5f);
        fog.maximumHeight.Override(60f);
        fog.enableVolumetricFog.Override(true);
        fog.depthExtent.Override(120f);

        // Geceye yaklaşan hafif mavi ton; bloom ile siren lambası uzaktan parlasın
        Get<WhiteBalance>(profile).temperature.Override(-10f);
        Get<Bloom>(profile).intensity.Override(0.3f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        Volume volume = FindVolume(profile);
        if (volume == null)
        {
            var go = new GameObject(VolumeName);
            Undo.RegisterCreatedObjectUndo(go, "Alaca Karanlık");
            volume = go.AddComponent<Volume>();
        }
        Undo.RecordObject(volume, "Alaca Karanlık");
        volume.isGlobal = true;
        volume.priority = 10f; // Proje varsayılanının üstünde, siren renk efektinin (50) altında
        volume.sharedProfile = profile;

        Light sun = FindSun();
        if (sun != null)
            PlaceSun(sun);
        else
            Debug.LogWarning("Alaca Karanlık: sahnede Directional Light yok, güneşi elle ekle.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeObject = volume.gameObject;
        Debug.Log($"Alaca karanlık kuruldu: {ProfilePath}. Sahneyi kaydetmeyi unutma.");
    }

    static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
            component = VolumeProfileFactory.CreateVolumeComponent<T>(profile, false, false);
        component.active = true;
        EditorUtility.SetDirty(component);
        return component;
    }

    static Volume FindVolume(VolumeProfile profile)
    {
        foreach (Volume v in Object.FindObjectsByType<Volume>())
        {
            if (v.sharedProfile == profile || v.name == VolumeName)
                return v;
        }
        return null;
    }

    static Light FindSun()
    {
        Light sun = null;
        foreach (Light light in Object.FindObjectsByType<Light>())
        {
            if (light.type == LightType.Directional && (sun == null || light.intensity > sun.intensity))
                sun = light;
        }
        return sun;
    }

    static void PlaceSun(Light sun)
    {
        // Güneş, başlangıçtan sirene (yoksa bunker'a) bakınca arkada kalsın:
        // kule gün batımının karşısındaki koyu gökyüzünde kırmızı yanıp söner
        float yaw = sun.transform.eulerAngles.y;
        PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>();
        Component target = Object.FindAnyObjectByType<AlarmSiren>();
        if (target == null)
            target = Object.FindAnyObjectByType<SceneTransitionTrigger>();
        if (player != null && target != null)
        {
            Vector3 dir = target.transform.position - player.transform.position;
            if (new Vector2(dir.x, dir.z).sqrMagnitude > 1f)
                yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        Undo.RecordObject(sun.transform, "Alaca Karanlık");
        sun.transform.rotation = Quaternion.Euler(SunElevation, yaw, 0f);

        // Işığın atmosferden geçerken kızarıp zayıflaması gökyüzüyle etkileşime bağlı
        if (sun.TryGetComponent(out HDAdditionalLightData hdSun))
        {
            Undo.RecordObject(hdSun, "Alaca Karanlık");
            hdSun.interactsWithSky = true;
        }
    }
}
