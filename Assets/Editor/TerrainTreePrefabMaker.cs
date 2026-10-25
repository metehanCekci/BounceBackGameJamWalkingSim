using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Terrain, ağaç olarak eklenen prefab'ın kök objesindeki konumu, dönmeyi ve ölçeği yok sayar. Modeli yatık gelip
// prefab'da kökü döndürülerek dikleştirilmiş objeler (ör. mısır) bu yüzden terrain'de yatık çıkar.
// Bu araç seçili prefab'ın göründüğü halini mesh'in kendisine gömer, pivotu tabana alır ve kökü
// dönmesiz/ölçeksiz bir "_Terrain" prefab'ı yapar. Orijinal prefab'a dokunmaz; tekrar çalıştırınca aynı dosyaları günceller.
public static class TerrainTreePrefabMaker
{
    [MenuItem("Tools/Level/Terrain Ağacı Prefabı Oluştur (Seçili Prefab)")]
    static void Create()
    {
        var source = Selection.activeObject as GameObject;
        string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : null;
        if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith(".prefab"))
        {
            EditorUtility.DisplayDialog("Terrain Ağacı", "Önce Project penceresinde bir prefab seç.", "Tamam");
            return;
        }

        // Read/Write kapalı modellerin noktaları okunamayabilir; geçici açıp iş bitince eski haline getiriyoruz
        var madeReadable = new List<ModelImporter>();
        try
        {
            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh != null)
                    MakeReadable(filter.sharedMesh, madeReadable);
            }
            // Model yeniden import edilince ondan türeyen prefab da yenilenir; eski referanslar bayatlamasın
            if (madeReadable.Count > 0)
                source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

            var renderers = new List<MeshRenderer>();
            foreach (MeshRenderer renderer in source.GetComponentsInChildren<MeshRenderer>())
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    renderers.Add(renderer);
            }
            if (renderers.Count == 0)
            {
                EditorUtility.DisplayDialog("Terrain Ağacı", "Prefab'da mesh bulunamadı.", "Tamam");
                return;
            }

            // Kökün konumu hariç her şey (dönme, ölçek, alt objelerin yerleşimi) mesh'e gömülür
            Matrix4x4 toTree = Matrix4x4.Translate(-source.transform.position);
            var parts = new List<CombineInstance>();
            var materials = new List<Material>();
            var temporary = new List<Mesh>();
            foreach (MeshRenderer renderer in renderers)
            {
                Mesh baked = Bake(renderer.GetComponent<MeshFilter>().sharedMesh, toTree * renderer.transform.localToWorldMatrix);
                temporary.Add(baked);
                for (int i = 0; i < baked.subMeshCount; i++)
                {
                    parts.Add(new CombineInstance { mesh = baked, subMeshIndex = i });
                    materials.Add(renderer.sharedMaterials[Mathf.Min(i, renderer.sharedMaterials.Length - 1)]);
                }
            }

            var mesh = new Mesh { name = source.name + "_Terrain", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), false, false);
            foreach (Mesh t in temporary)
                Object.DestroyImmediate(t);
            MoveBaseToOrigin(mesh);

            string folder = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            Mesh meshAsset = SaveMesh(mesh, $"{folder}/{source.name}_TerrainMesh.asset");

            var tree = new GameObject(source.name + "_Terrain");
            tree.AddComponent<MeshFilter>().sharedMesh = meshAsset;
            MeshRenderer treeRenderer = tree.AddComponent<MeshRenderer>();
            treeRenderer.sharedMaterials = materials.ToArray();
            treeRenderer.shadowCastingMode = renderers[0].shadowCastingMode;

            // Aynı yola kaydetmek prefab'ı günceller; terrain'deki bağlantı kopmaz
            string prefabPath = $"{folder}/{source.name}_Terrain.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tree, prefabPath);
            Object.DestroyImmediate(tree);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Terrain ağacı prefabı hazır: {prefabPath}. Terrain > Paint Trees > Edit Trees'ten eski prefab'ın yerine bunu seç.");
        }
        finally
        {
            foreach (ModelImporter importer in madeReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
    }

    static void MakeReadable(Mesh mesh, List<ModelImporter> madeReadable)
    {
        if (mesh.isReadable) return;
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) as ModelImporter;
        if (importer == null || madeReadable.Contains(importer)) return;

        importer.isReadable = true;
        importer.SaveAndReimport();
        madeReadable.Add(importer);
    }

    static Mesh Bake(Mesh source, Matrix4x4 matrix)
    {
        Mesh mesh = Object.Instantiate(source);
        // Orantısız ölçekte normaller ters-transpoz matrisle döner, yoksa ışık yanlış düşer
        Matrix4x4 normalMatrix = matrix.inverse.transpose;
        bool mirrored = matrix.determinant < 0f;

        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);

        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
            normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;

        Vector4[] tangents = mesh.tangents;
        for (int i = 0; i < tangents.Length; i++)
        {
            Vector3 t = matrix.MultiplyVector(tangents[i]).normalized;
            tangents[i] = new Vector4(t.x, t.y, t.z, mirrored ? -tangents[i].w : tangents[i].w);
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.tangents = tangents;

        // Aynalanan (eksi ölçekli) objede yüzler içe döner; üçgen sırasını çeviriyoruz
        if (mirrored)
        {
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] triangles = mesh.GetTriangles(s);
                System.Array.Reverse(triangles);
                mesh.SetTriangles(triangles, s);
            }
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    static void MoveBaseToOrigin(Mesh mesh)
    {
        // Terrain objeyi pivotundan yere oturtur: pivot tabanda ve ortada olsun ki ne gömülsün ne havada kalsın
        Bounds bounds = mesh.bounds;
        Vector3 offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] -= offset;
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
    }

    static Mesh SaveMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // Var olanın içeriğini değiştiriyoruz ki ona bağlı prefab ve terrain kopmasın
        EditorUtility.CopySerialized(mesh, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        Object.DestroyImmediate(mesh);
        return existing;
    }
}
