using System.Linq;
using UnityEditor;
using UnityEngine;

// Sahne düzeni için iki yardımcı: çakışan objeleri bulur, seçilenleri zemine oturtur.
public static class LevelAudit
{
    [MenuItem("Tools/Level/Üst Üste Binen Objeleri Bul")]
    public static void FindOverlaps()
    {
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy)
            .Where(r => r.GetComponentInParent<Terrain>() == null)
            .ToList();

        int count = 0;
        for (int i = 0; i < renderers.Count; i++)
        {
            for (int j = i + 1; j < renderers.Count; j++)
            {
                MeshRenderer a = renderers[i];
                MeshRenderer b = renderers[j];

                // Parent-child ilişkisi olanlar zaten birlikte duruyor, atla
                if (a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform))
                    continue;

                if (!a.bounds.Intersects(b.bounds))
                    continue;

                // Sadece dokunma değil, gerçek örtüşme olsun diye küçük payları yok say
                Vector3 overlap = Vector3.Min(a.bounds.max, b.bounds.max) - Vector3.Max(a.bounds.min, b.bounds.min);
                if (overlap.x < 0.05f || overlap.y < 0.05f || overlap.z < 0.05f)
                    continue;

                count++;
                Debug.Log($"Çakışma: {a.name} <-> {b.name} | örtüşme: {overlap}", a.gameObject);
            }
        }

        Debug.Log($"Toplam çakışan çift: {count} (incelenen renderer: {renderers.Count}). Not: Bu kontrol kutu (AABB) bazlı, eğik objelerde yanlış alarm olabilir.");
    }

    [MenuItem("Tools/Level/Seçilileri Yere Oturt")]
    public static void DropSelectedToGround()
    {
        int moved = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            Renderer r = go.GetComponentInChildren<Renderer>();
            if (r == null)
                continue;

            Bounds b = r.bounds;
            Vector3 origin = new Vector3(b.center.x, b.max.y + 50f, b.center.z);

            // Kendi çocuklarına çarpan ışınları yok say, en yakın zemin temasını al
            RaycastHit? ground = Physics.RaycastAll(origin, Vector3.down, 200f)
                .Where(h => !h.transform.IsChildOf(go.transform))
                .OrderBy(h => h.distance)
                .Cast<RaycastHit?>()
                .FirstOrDefault();

            if (ground == null)
            {
                Debug.LogWarning($"{go.name}: altında zemin bulunamadı, atlandı.", go);
                continue;
            }

            float delta = ground.Value.point.y - b.min.y;
            Undo.RecordObject(go.transform, "Yere Oturt");
            go.transform.position += Vector3.up * delta;
            moved++;
        }

        Debug.Log($"Yere oturtulan obje sayısı: {moved}");
    }

    [MenuItem("Tools/Level/Seçilileri Izgaraya Diz")]
    public static void ArrangeSelectedInGrid()
    {
        const float Gap = 1f; // Objeler arası boşluk (metre)

        GameObject[] objects = Selection.gameObjects.OrderBy(g => g.name).ToArray();
        if (objects.Length == 0)
            return;

        float maxFootprint = 0f;
        Vector3 centroid = Vector3.zero;
        Bounds[] bounds = new Bounds[objects.Length];

        for (int i = 0; i < objects.Length; i++)
        {
            bounds[i] = GetBounds(objects[i]);
            maxFootprint = Mathf.Max(maxFootprint, Mathf.Max(bounds[i].size.x, bounds[i].size.z));
            centroid += objects[i].transform.position;
        }
        centroid /= objects.Length;

        float cell = maxFootprint + Gap;
        int cols = Mathf.CeilToInt(Mathf.Sqrt(objects.Length));
        int rows = Mathf.CeilToInt(objects.Length / (float)cols);
        Vector3 gridOrigin = centroid - new Vector3((cols - 1) * cell * 0.5f, 0f, (rows - 1) * cell * 0.5f);

        for (int i = 0; i < objects.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;
            Vector3 target = gridOrigin + new Vector3(col * cell, 0f, row * cell);

            // Sadece yatay taşı, yükseklik "Yere Oturt" ile ayarlanır
            Vector3 delta = target - bounds[i].center;
            delta.y = 0f;

            Undo.RecordObject(objects[i].transform, "Izgaraya Diz");
            objects[i].transform.position += delta;
        }

        Debug.Log($"{objects.Length} obje ızgaraya dizildi. Şimdi Seçilileri Yere Oturt'u çalıştır.");
    }

    private static Bounds GetBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
            b.Encapsulate(r.bounds);
        return b;
    }
}
