using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Bunker modelinin malzemelerini HDRP/Lit ile kurar.
// Model 3ds Max'ten İspanyolca isimli malzemelerle geldi ve .mtl dosyası yok, Unity texture'ları kendiliğinden bağlayamıyor.
// Menü: Tools > Bunker > Malzemeleri Kur (tekrar çalıştırmak güvenli, var olan malzemeleri günceller)
public static class BunkerMaterialSetup
{
    const string Folder = "Assets/Models/Bunker";
    const string ModelPath = Folder + "/bunker_model.obj";
    const string MaterialFolder = Folder + "/Materials";

    struct Entry
    {
        public string sourceKey;    // Modeldeki malzeme adında geçen parça
        public string materialName; // Bizim oluşturduğumuz malzeme
        public string baseMap, normalMap, maskMap;

        public Entry(string sourceKey, string materialName, string baseMap, string normalMap, string maskMap)
        {
            this.sourceKey = sourceKey;
            this.materialName = materialName;
            this.baseMap = baseMap;
            this.normalMap = normalMap;
            this.maskMap = maskMap;
        }
    }

    // Mask Map'ler (R metallic, G AO, B detail, A smoothness) orijinal Glossiness/MetallicSmoothness dokularından üretildi
    static readonly Entry[] Entries =
    {
        // Bunker_mio = "benim bunkerim", ana gövde (beton, metal değil)
        new Entry("bunker_mio", "Bunker_Govde", "Bunker_mio_DefaultMaterial_Diffuse", "Bunker_mio_DefaultMaterial_Normal", "Bunker_mio_MaskMap"),
        // biga (viga) central = orta kiriş; tamamen metal, renk dokusu yok, rengi yansıma haritası taşıyor
        new Entry("biga_central", "Bunker_Kiris", "biga_central_DefaultMaterial_Reflection", "biga_central_DefaultMaterial_Normal", "biga_central_MaskMap"),
        // cerradura = kilit; kiriş gibi tamamen metal
        new Entry("cerradura_central", "Bunker_Kilit", "cerradura_central_DefaultMaterial_Reflecti", "cerradura_central_DefaultMaterial_Normal", "cerradura_central_MaskMap"),
        // puerta = kapı; Unity Standard formatında gelmişti, metal ve boyalı kısımlar karışık
        new Entry("puerta", "Bunker_Kapi", "puerta_DefaultMaterial_AlbedoTransparency", "puerta_DefaultMaterial_Normal", "puerta_MaskMap"),
        // tubos y cables = borular ve kablolar (koyu, metal değil)
        new Entry("tubos_cables", "Bunker_Borular", "tubos_cables_DefaultMaterial_Diffuse", "tubos_cables_DefaultMaterial_Normal", "tubos_cables_MaskMap"),
    };

    [MenuItem("Tools/Bunker/Malzemeleri Kur")]
    static void Setup()
    {
        Shader lit = Shader.Find("HDRP/Lit");
        if (lit == null)
        {
            Debug.LogError("Bunker: HDRP/Lit shader bulunamadı.");
            return;
        }

        // 1. Doku ayarları: normal haritaları Normal Map olmalı, mask haritaları renk değil veri (sRGB kapalı)
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (Entry e in Entries)
            {
                ConfigureTexture(e.normalMap, TextureImporterType.NormalMap, false);
                ConfigureTexture(e.maskMap, TextureImporterType.Default, false);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // 2. Malzemeler
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder(Folder, "Materials");

        var materials = new Dictionary<string, Material>();
        foreach (Entry e in Entries)
        {
            string path = $"{MaterialFolder}/{e.materialName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew)
                mat = new Material(lit);
            else
                mat.shader = lit;

            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseColorMap", LoadTexture(e.baseMap));
            mat.SetTexture("_NormalMap", LoadTexture(e.normalMap));
            mat.SetFloat("_NormalScale", 1f);
            mat.SetTexture("_MaskMap", LoadTexture(e.maskMap));
            // Mask haritası takılıyken metallic/smoothness/AO bu aralıklarla okunur, dokudaki değer aynen kullanılsın
            mat.SetFloat("_MetallicRemapMin", 0f);
            mat.SetFloat("_MetallicRemapMax", 1f);
            mat.SetFloat("_SmoothnessRemapMin", 0f);
            mat.SetFloat("_SmoothnessRemapMax", 1f);
            mat.SetFloat("_AORemapMin", 0f);
            mat.SetFloat("_AORemapMax", 1f);
            HDMaterial.ValidateMaterial(mat); // HDRP keyword'lerini takılı dokulara göre açar

            if (isNew)
                AssetDatabase.CreateAsset(mat, path);
            else
                EditorUtility.SetDirty(mat);
            materials[e.sourceKey] = mat;
        }
        AssetDatabase.SaveAssets();

        // 3. Modelin kendi malzeme yuvalarını bizim malzemelere bağla; model sahneye her konduğunda doğru gelsin
        var model = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (model == null)
        {
            Debug.LogError($"Bunker: model bulunamadı ({ModelPath}).");
            return;
        }

        // Unity'nin modelden çıkardığı malzeme adları; .obj içindeki adlar da eklenir ki tekrar çalıştırınca da bulunsun
        var sourceNames = new HashSet<string>(AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>().Select(m => m.name));
        foreach (string line in File.ReadLines(ModelPath))
        {
            if (line.StartsWith("usemtl "))
                sourceNames.Add(line.Substring(7).Trim());
        }

        int remapped = 0;
        foreach (string name in sourceNames)
        {
            Entry match = Entries.FirstOrDefault(e => name.ToLowerInvariant().Contains(e.sourceKey));
            if (match.sourceKey == null)
            {
                Debug.LogWarning($"Bunker: modeldeki '{name}' malzemesi için eşleşme yok, elle atanması gerekiyor.");
                continue;
            }
            model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), materials[match.sourceKey]);
            remapped++;
        }
        model.SaveAndReimport();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ModelPath);
        Debug.Log($"Bunker: {Entries.Length} malzeme kuruldu, modeldeki {remapped} malzeme yuvası bağlandı ({string.Join(", ", sourceNames)}).");
    }

    static string TexturePath(string name) => $"{Folder}/{name}.png";

    static Texture2D LoadTexture(string name)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(name));
        if (tex == null)
            Debug.LogWarning($"Bunker: doku bulunamadı ({TexturePath(name)}).");
        return tex;
    }

    static void ConfigureTexture(string name, TextureImporterType type, bool sRGB)
    {
        var importer = AssetImporter.GetAtPath(TexturePath(name)) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Bunker: doku bulunamadı ({TexturePath(name)}).");
            return;
        }
        if (importer.textureType == type && importer.sRGBTexture == sRGB)
            return;

        importer.textureType = type;
        importer.sRGBTexture = sRGB;
        importer.SaveAndReimport();
    }
}
