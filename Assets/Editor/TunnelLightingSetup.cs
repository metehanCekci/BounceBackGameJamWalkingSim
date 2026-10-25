using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Tünel gibi kapalı yerlerde lambalar görünmüyordu: HDRP'de gökyüzünün ortam ışığı duvarları bilmez,
// tünelin içi dışarısı kadar aydınlık kalır ve pozlama gündüzde takılır; 600 lümenlik lamba kaybolur.
// Seçili duvarları saran bir alan Volume'u kurar: içerideyken ortam ışığı kısılır ve pozlama karanlığa uyar.
// İçerideki zayıf point/spot ışıkları da görünür güce çeker. Tekrar çalıştırmak güvenli.
public static class TunnelLightingSetup
{
    const string ProfilePath = "Assets/Settings/TunnelInterior.asset";
    const string VolumeName = "Tunnel Interior Volume";
    const float MinLumen = 5000f;
    const float MinRange = 12f;

    [MenuItem("Tools/Level/Tünel İçi Işığını Kur (Seçili Duvarlar)")]
    static void Setup()
    {
        if (!TryGetSelectionBounds(out Bounds bounds))
        {
            EditorUtility.DisplayDialog("Tünel Işığı", "Önce Hierarchy'de tünelin duvarlarını seç (hepsini).", "Tamam");
            return;
        }

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
            profile = VolumeProfileFactory.CreateVolumeProfileAtPath(ProfilePath);

        // İçeride gökyüzü ışığı ve yansımalar neredeyse sıfır: tüneli lambalar aydınlatsın
        var indirect = Get<IndirectLightingController>(profile);
        indirect.indirectDiffuseLightingMultiplier.Override(0.02f);
        indirect.reflectionLightingMultiplier.Override(0.05f);

        // Dışarıdaki sis gökyüzü renginde ve duvarları bilmez; tünelin içini parlak pusla doldurmasın
        Get<Fog>(profile).enabled.Override(false);

        // Güneş gölgesi varsayılan 150 m'de biter; uzun tünelin ötesinde tavan güneşi kesmez, tünelin dibi güneş alır
        Get<HDShadowSettings>(profile).maxShadowDistance.Override(Mathf.Max(150f, bounds.size.magnitude));

        // Pozlama karanlığa uyabilsin ama gece kadar açılmasın. Sahnedeki alaca karanlık profilinin
        // "en az EV 9" sınırını içeride ezer; o sınır tünelde lambaları simsiyah bırakıyordu
        var exposure = Get<Exposure>(profile);
        exposure.mode.Override(ExposureMode.AutomaticHistogram);
        exposure.compensation.Override(0f);
        exposure.limitMin.Override(3f);
        exposure.limitMax.Override(14f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject go = GameObject.Find(VolumeName);
        if (go == null)
        {
            go = new GameObject(VolumeName);
            Undo.RegisterCreatedObjectUndo(go, "Tünel Işığı");
        }
        Undo.RecordObject(go.transform, "Tünel Işığı");
        go.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        // Default katmanında kalmalı: kameranın Volume Layer Mask'i sadece Default'u dinliyor,
        // başka katmandaki Volume hiç uygulanmaz
        go.layer = 0;

        BoxCollider box = go.GetComponent<BoxCollider>();
        if (box == null)
            box = Undo.AddComponent<BoxCollider>(go);
        Undo.RecordObject(box, "Tünel Işığı");
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = bounds.size;

        Volume volume = go.GetComponent<Volume>();
        if (volume == null)
            volume = Undo.AddComponent<Volume>(go);
        Undo.RecordObject(volume, "Tünel Işığı");
        volume.isGlobal = false;
        volume.blendDistance = 3f; // Girişte birden değil yavaşça kararsın
        volume.priority = 20f;
        volume.sharedProfile = profile;

        int boosted = BoostLights(bounds);

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeObject = go;
        if (boosted == 0)
            Debug.LogWarning("Tünel ışığı kuruldu ama tünelin içinde point/spot ışık bulunamadı. Işık eklediysen sahne kaydedilmemiş olabilir ya da ışık tünelin dışında kalmış.");
        else
            Debug.Log($"Tünel ışığı kuruldu: {boosted} ışık ayarlandı. Sahneyi kaydetmeyi unutma.");
    }

    static bool TryGetSelectionBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (GameObject selected in Selection.gameObjects)
        {
            foreach (Renderer r in selected.GetComponentsInChildren<Renderer>())
            {
                if (any) bounds.Encapsulate(r.bounds);
                else { bounds = r.bounds; any = true; }
            }
        }
        return any;
    }

    static int BoostLights(Bounds tunnel)
    {
        tunnel.Expand(2f);
        int count = 0;
        foreach (Light light in Object.FindObjectsByType<Light>())
        {
            if (light.type != LightType.Point && light.type != LightType.Spot) continue;
            if (!tunnel.Contains(light.transform.position)) continue;

            Undo.RecordObject(light, "Tünel Işığı");
            // Baked ışık, ışık bake edilmeden hiçbir şeyi aydınlatmaz
            if (light.lightmapBakeType == LightmapBakeType.Baked)
            {
                light.lightmapBakeType = LightmapBakeType.Realtime;
                Debug.Log($"'{light.name}' Baked moddaydı, bake yapılmadığı için hiçbir şey aydınlatmıyordu; Realtime yaptım.", light);
            }

            // Zayıfsa görünür güce çek; daha güçlü ayarlanmışsa dokunma
            if (light.lightUnit == LightUnit.Candela)
                light.intensity = Mathf.Max(light.intensity, MinLumen / (4f * Mathf.PI));
            else
            {
                if (light.lightUnit != LightUnit.Lumen)
                    light.lightUnit = LightUnit.Lumen;
                light.intensity = Mathf.Max(light.intensity, MinLumen);
            }
            light.range = Mathf.Max(light.range, MinRange);
            light.enabled = true;
            if (light.transform.localScale != Vector3.one)
            {
                // Işığa ölçek gerekmez; bir modelin içinden çıkarılınca kalan 100 gibi değerler kafa karıştırır
                Undo.RecordObject(light.transform, "Tünel Işığı");
                light.transform.localScale = Vector3.one;
            }
            FreeTrappedLight(light);
            Debug.Log($"'{light.name}': {light.intensity:0} {light.lightUnit}, menzil {light.range:0} m, gölge {light.shadows}, mod {light.lightmapBakeType}.", light);
            count++;
        }
        return count;
    }

    // Gölgesi açık ışık kapalı bir modelin (ör. camlı lamba) içindeyse, model ışığı her yönden gölgeler
    // ve dışarı hiçbir şey çıkmaz. Küçük modellerin gölge vermesini kapatır, büyükler için uyarır.
    static void FreeTrappedLight(Light light)
    {
        if (light.shadows == LightShadows.None) return;

        Vector3 p = light.transform.position;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>())
        {
            if (r.shadowCastingMode == ShadowCastingMode.Off || !r.bounds.Contains(p)) continue;

            if (r.bounds.size.magnitude > 3f)
            {
                // Duvar gibi büyük bir şeyin içinde: gölgesini kapatmak tünelin gölgelerini bozar
                Debug.LogWarning($"'{light.name}' ışığı '{r.name}' objesinin içinde kalmış; ışığı onun dışına, tünelin boşluğuna taşı.", light);
                continue;
            }

            Undo.RecordObject(r, "Tünel Işığı");
            r.shadowCastingMode = ShadowCastingMode.Off;
            Debug.Log($"'{r.name}' artık gölge vermiyor: '{light.name}' ışığını içinde hapsediyordu.", r);
        }
    }

    static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
            component = VolumeProfileFactory.CreateVolumeComponent<T>(profile, false, false);
        component.active = true;
        EditorUtility.SetDirty(component);
        return component;
    }
}
