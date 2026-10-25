using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Uzaktaki siren kulesi prefab'ı: kafes kule + dönen kırmızı ikaz lambası + haritanın her yerinden duyulan siren + bomba sayacı.
// Kule basit parçalardan kurulur; istersen gövdeyi bir modelle değiştirip Siren objesini onun tepesine taşı.
public static class SirenTowerPrefabMaker
{
    const float Height = 14f;    // Platform yüksekliği; uzaktan görünsün
    const float BaseHalf = 1.6f; // Tabanda bacaklar arası yarım mesafe
    const float TopHalf = 0.9f;  // Tepede
    const int Levels = 4;        // Kafes kat sayısı

    const string SirenClipPath = "Assets/Audio/SirenLoop.wav";
    const string SirenFallbackPath = "Assets/Audio/Sİren.mp3";
    const string ExplosionClipPath = "Assets/Audio/BombExplode.mp3";

    static readonly Color LampColor = new Color(1f, 0.04f, 0.02f);

    [MenuItem("Tools/Level/Siren Kulesi Prefabı Oluştur")]
    public static void Create()
    {
        const string folder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        Material metal = GetOrCreateMaterial("SirenTower_Metal", m =>
        {
            m.SetColor("_BaseColor", new Color(0.3f, 0.28f, 0.26f)); // sRGB; koyu, paslı demir
            m.SetFloat("_Metallic", 0.6f);
            m.SetFloat("_Smoothness", 0.3f);
        });
        Material lampMaterial = GetOrCreateMaterial("SirenTower_Lamp", m =>
        {
            m.SetColor("_BaseColor", new Color(0.5f, 0.02f, 0.02f));
            m.SetFloat("_Smoothness", 0.85f);
            // Pozlamadan etkilenmesin: gündüz güneşin altında da parlak görünsün
            m.SetFloat("_EmissiveExposureWeight", 0f);
            HDMaterial.SetEmissiveColor(m, LampColor * 0.3f);
        });

        var root = new GameObject("SirenTower");

        // Kafes gövde: 4 eğik bacak, her katta kuşak ve çapraz destekler
        Transform body = CreateChild(root.transform, "Body", Vector3.zero);
        Vector3[] bottom = Corners(BaseHalf, 0f);
        Vector3[] top = Corners(TopHalf, Height);
        for (int i = 0; i < 4; i++)
            Beam(body, "Leg", bottom[i], top[i], 0.22f, metal);

        for (int level = 0; level < Levels; level++)
        {
            float t0 = level / (float)Levels;
            float t1 = (level + 1) / (float)Levels;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Vector3 a0 = Vector3.Lerp(bottom[i], top[i], t0);
                Vector3 b0 = Vector3.Lerp(bottom[j], top[j], t0);
                Vector3 a1 = Vector3.Lerp(bottom[i], top[i], t1);
                Vector3 b1 = Vector3.Lerp(bottom[j], top[j], t1);
                Beam(body, "Ring", a1, b1, 0.12f, metal);
                Beam(body, "Brace", a0, b1, 0.08f, metal);
                Beam(body, "Brace", b0, a1, 0.08f, metal);
            }
        }

        // Platform ve korkuluk
        float deckHalf = TopHalf + 0.4f;
        Part(body, "Platform", PrimitiveType.Cube, new Vector3(0f, Height, 0f), new Vector3(deckHalf * 2f, 0.15f, deckHalf * 2f), metal);
        Vector3[] deck = Corners(deckHalf - 0.05f, Height);
        Vector3[] rail = Corners(deckHalf - 0.05f, Height + 1f);
        for (int i = 0; i < 4; i++)
        {
            Beam(body, "RailPost", deck[i], rail[i], 0.06f, metal);
            Beam(body, "Rail", rail[i], rail[(i + 1) % 4], 0.05f, metal);
        }

        // Siren başlığı: sabit direk, üstünde dönen gövde (hoparlörler + lamba + spot ışıkları)
        Transform siren = CreateChild(root.transform, "Siren", new Vector3(0f, Height + 0.075f, 0f));
        Decor(siren, "Pole", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.16f, 0.6f, 0.16f), metal);

        Transform rotor = CreateChild(siren, "Rotor", new Vector3(0f, 1.35f, 0f));
        Decor(rotor, "Housing", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.6f, 0.15f, 0.6f), metal);
        Decor(rotor, "HornFront", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.55f), new Vector3(0.34f, 0.3f, 0.34f), metal, Quaternion.Euler(90f, 0f, 0f));
        Decor(rotor, "HornBack", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.55f), new Vector3(0.34f, 0.3f, 0.34f), metal, Quaternion.Euler(90f, 0f, 0f));
        // Gerçek boyundan büyük: uzaktan da birkaç piksel kaplasın
        GameObject lamp = Decor(rotor, "Lamp", PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f), Vector3.one * 0.55f, lampMaterial);

        // Arka arkaya iki ışık, hafif aşağı bakıp etrafı tarar
        CreateSpot(rotor, "LightFront", new Vector3(0f, 0.42f, 0f), Quaternion.Euler(12f, 0f, 0f), new Color(1f, 0.08f, 0.04f));
        CreateSpot(rotor, "LightBack", new Vector3(0f, 0.42f, 0f), Quaternion.Euler(12f, 180f, 0f), new Color(1f, 0.95f, 0.9f));

        AlarmSiren alarm = siren.gameObject.AddComponent<AlarmSiren>();
        alarm.rotor = rotor;
        alarm.lamp = lamp.GetComponent<Renderer>();
        alarm.primaryColor = LampColor;
        alarm.sirenSound = AssetDatabase.LoadAssetAtPath<AudioClip>(SirenClipPath);
        if (alarm.sirenSound == null)
            alarm.sirenSound = AssetDatabase.LoadAssetAtPath<AudioClip>(SirenFallbackPath);
        if (alarm.sirenSound == null)
            Debug.LogWarning("Siren sesi bulunamadı, Siren objesindeki AlarmSiren'e elle ata.");

        // AlarmSiren kendi ayarlıyor; sahne açılırken kaynak kendi kendine de çalmasın
        AudioSource audio = siren.GetComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.clip = alarm.sirenSound;
        audio.loop = true;
        audio.spatialBlend = 1f;

        BombTimer timer = root.AddComponent<BombTimer>();
        timer.explosionSound = AssetDatabase.LoadAssetAtPath<AudioClip>(ExplosionClipPath);

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/SirenTower.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"Siren kulesi prefabı oluşturuldu: {path}");
    }

    static Material GetOrCreateMaterial(string name, System.Action<Material> setup)
    {
        // Var olanı ezmiyoruz, elle ayarlanmış olabilir
        string path = $"Assets/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        mat = new Material(Shader.Find("HDRP/Lit"));
        setup(mat);
        HDMaterial.ValidateMaterial(mat);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void CreateSpot(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Color color)
    {
        Transform t = CreateChild(parent, name, localPosition);
        t.localRotation = localRotation;

        HDAdditionalLightData hdLight = t.gameObject.AddHDLight(LightType.Spot);
        Light light = t.GetComponent<Light>();
        light.color = color;
        // Gündüz güneşi çok güçlü (100.000 lux): yakında yeri kırmızıya boyayabilmesi için yüksek
        light.lightUnit = UnityEngine.Rendering.LightUnit.Candela;
        light.intensity = 2000000f;
        light.range = 150f;
        light.spotAngle = 35f;
        light.innerSpotAngle = 12f;
        light.shadows = LightShadows.None; // Performans; ışık lambanın içinden çıkıyor
        // Sahnede hacimsel sis varsa dönen kırmızı huzme görünür
        hdLight.affectsVolumetric = true;
        hdLight.volumetricDimmer = 1f;
    }

    static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    static Vector3[] Corners(float half, float y)
    {
        // Sırayla dönen köşeler: i ile i+1 komşu
        return new[]
        {
            new Vector3(-half, y, -half),
            new Vector3(half, y, -half),
            new Vector3(half, y, half),
            new Vector3(-half, y, half),
        };
    }

    // İki nokta arasına uzatılmış ince kutu (kafes çubuğu)
    static void Beam(Transform parent, string name, Vector3 from, Vector3 to, float thickness, Material mat)
    {
        Vector3 dir = to - from;
        Part(parent, name, PrimitiveType.Cube, (from + to) * 0.5f, new Vector3(thickness, dir.magnitude, thickness), mat,
            Quaternion.FromToRotation(Vector3.up, dir));
    }

    static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale,
        Material mat, Quaternion? localRotation = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation ?? Quaternion.identity;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // Tepedeki süs parçaları: oyuncu oraya çıkamaz, collider gereksiz
    static GameObject Decor(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale,
        Material mat, Quaternion? localRotation = null)
    {
        GameObject go = Part(parent, name, type, localPosition, localScale, mat, localRotation);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
}
