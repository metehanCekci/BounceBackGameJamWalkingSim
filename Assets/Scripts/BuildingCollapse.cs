using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Oyuncu bu trigger'a girince bina önce titrer, sonra tabanından devrilip yola düşer ve yolu tıkar.
// Bu bileşeni yolun üstüne koyduğun trigger objesine ekle, Building'e yıkılacak binayı sürükle.
// Bina varsayılan olarak bu trigger'ın olduğu yere doğru devrilir.
[RequireComponent(typeof(Collider))]
public class BuildingCollapse : MonoBehaviour
{
    [Header("Bina")]
    public Transform building;             // Yıkılacak bina (kökü)
    public Transform fallTowards;          // Boşsa bu trigger'ın olduğu yere doğru devrilir
    public float fallAngle = 85f;          // Devrilme açısı (90 = tam yatar)
    public bool triggerOnEnter = true;     // Kapalıysa sadece Collapse() çağrılınca yıkılır (ör. başka bir cutscene yönetiyorsa)

    [Header("Zamanlama")]
    public float rumbleTime = 2f;          // Devrilmeden önce titreme süresi
    public float rumbleAmount = 1.5f;      // Titreme şiddeti (derece)
    public float preFallLean = 5f;         // Titrerken düşeceği yöne doğru eğilme (düşeceği belli olsun)
    public float fallTime = 2.2f;          // Devrilme süresi

    [Header("Ezilme")]
    public bool killPlayer = true;         // Bina oyuncunun üstüne düşerse bölüm baştan yüklenir
    public float deathFadeOut = 0.3f;
    public float deathFadeIn = 1f;

    [Header("Kamera")]
    public bool lockCamera = true;         // Yıkılma boyunca kamera binaya kilitlenir
    public bool cinematicBars = true;
    public float holdAfterImpact = 1.5f;   // Çarptıktan sonra kilidin açılmasına kadar bekleme
    [Range(0f, 1f)] public float impactShake = 1f;
    public float impactShakeBoost = 4f;    // Çarpma sarsıntısı normal sarsıntının kaç katı sert

    [Header("Ses & Efekt (boşsa kodla üretilenler kullanılır)")]
    public AudioClip rumbleSound;
    public AudioClip impactSound;
    [Range(0f, 1f)] public float volume = 1f;
    public GameObject dustEffect;          // Çarpma noktasında oluşturulur (toz/duman prefab'ı)
    public bool generatedFx = true;        // Kapalıysa boş alanlar için hiçbir şey çalmaz

    [Header("Çarptıktan Sonra Satırlar (her birinde ses ve metin)")]
    public SubtitleLine[] lines;

    [Header("Bitince")]
    public UnityEvent onCollapsed;

    private bool triggered;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!triggerOnEnter || triggered || building == null) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        Collapse();
    }

    // Başka bir olaydan da çağrılabilir (ör. siren cutscene'i)
    public void Collapse()
    {
        if (triggered || building == null) return;
        triggered = true;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        PlayerInputManager input = FindAnyObjectByType<PlayerInputManager>();
        RealisticPlayerCamera cam = FindAnyObjectByType<RealisticPlayerCamera>();
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        Collider playerCollider = player != null ? player.GetComponent<Collider>() : null;

        Bounds bounds = GetBounds(building);
        // Ezilme binanın collider'ına değil kendi şekline bakar: collider'ı olmayan ya da sadece
        // trigger'ı olan binalar da öldürsün. Döndürülmeden önce binanın kendi ekseninde ölçülür.
        Bounds localShape = GetLocalBounds(building);
        Vector3 target = fallTowards != null ? fallTowards.position : transform.position;
        Vector3 fallDir = target - bounds.center;
        fallDir.y = 0f;
        if (fallDir.sqrMagnitude < 0.01f) fallDir = building.forward;
        fallDir.Normalize();

        // Bina, devrileceği taraftaki taban kenarı etrafında döner
        Vector3 extents = bounds.extents;
        float edge = Mathf.Abs(fallDir.x) * extents.x + Mathf.Abs(fallDir.z) * extents.z;
        Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + fallDir * edge;
        Vector3 axis = Vector3.Cross(Vector3.up, fallDir);
        Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

        if (lockCamera && input != null) input.InputLocked = true;
        if (lockCamera && cinematicBars) CinematicBars.Show();

        // Gürleme titremeden devrilmenin sonuna kadar sürer
        if (rumbleSound != null)
            PlayAt(rumbleSound, bounds.center);
        else if (generatedFx)
            ProceduralFx.PlayClip(ProceduralFx.Rumble(rumbleTime + fallTime), bounds.center, volume);

        // 1. Titreme: giderek sertleşir, bina düşeceği yöne eğilir, tabanından ve tepesinden toz dökülür
        Quaternion startRot = building.rotation;
        Vector3 startPos = building.position;
        Vector3 baseArea = new Vector3(extents.x * 2f, 0f, extents.z * 2f);
        float nextDust = 0f;
        float t = 0f;
        while (t < rumbleTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / rumbleTime);
            float strength = rumbleAmount * k;
            float lean = preFallLean * k * k;

            building.SetPositionAndRotation(startPos + Random.insideUnitSphere * strength * 0.04f, startRot);
            building.RotateAround(pivot, axis, lean + Random.Range(-strength, strength));

            if (generatedFx && dustEffect == null && t >= nextDust)
            {
                nextDust = t + 0.4f;
                ProceduralFx.SpawnDust(pivot, baseArea, 0.15f + 0.2f * k);
                ProceduralFx.SpawnDust(RotatePoint(top, pivot, axis, lean), baseArea * 0.5f, 0.1f);
            }

            if (cam != null)
            {
                cam.AddShake(Time.deltaTime * (0.5f + 1.5f * k), 1f + 1.5f * k);
                if (lockCamera) cam.SetFocus(bounds.center);
            }
            yield return null;
        }
        building.SetPositionAndRotation(startPos, startRot);
        building.RotateAround(pivot, axis, preFallLean);

        // Taban kırılırken toz
        if (dustEffect == null && generatedFx)
            ProceduralFx.SpawnDust(pivot, baseArea, 0.5f);

        // 2. Devrilme: eğik halinden başlar, düştükçe hızlanır.
        // Katı collider'lar düşerken kapalı: açık kalırsa fizik oyuncuyu kenara iter ve ezilmez.
        // Çarpınca geri açılır, yıkılan bina yolu kapatsın
        var solidColliders = new List<Collider>();
        foreach (Collider c in building.GetComponentsInChildren<Collider>())
        {
            if (c.enabled && !c.isTrigger)
            {
                c.enabled = false;
                solidColliders.Add(c);
            }
        }

        float applied = preFallLean;
        t = 0f;
        while (t < fallTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fallTime);
            float angle = Mathf.Lerp(preFallLean, fallAngle, k * k * k);
            if (cam != null) cam.AddShake(Time.deltaTime * 0.8f, 2f);
            building.RotateAround(pivot, axis, angle - applied);
            applied = angle;

            // Bina oyuncuya değdiyse ezildi: ekran kararır, bölüm baştan
            if (killPlayer && k > 0.3f && IsCrushing(playerCollider, localShape))
            {
                ProceduralFx.PlayClip(impactSound != null ? impactSound : ProceduralFx.Impact(), player.transform.position, volume);
                if (cam != null) cam.AddShake(1f, impactShakeBoost);
                ScreenFader.Instance.TransitionToScene(SceneManager.GetActiveScene().buildIndex, deathFadeOut, deathFadeIn);
                yield break;
            }

            if (cam != null && lockCamera)
                cam.SetFocus(RotatePoint(top, pivot, axis, applied * 0.6f));
            yield return null;
        }

        foreach (Collider c in solidColliders)
            c.enabled = true;

        // 3. Çarpma
        Vector3 impactPoint = RotatePoint(top, pivot, axis, fallAngle);
        impactPoint.y = bounds.min.y;
        Vector3 impactCenter = Vector3.Lerp(pivot, impactPoint, 0.5f);
        if (impactSound != null)
            PlayAt(impactSound, impactPoint);
        else if (generatedFx)
            ProceduralFx.PlayClip(ProceduralFx.Impact(), impactCenter, volume);

        if (cam != null) cam.AddShake(impactShake, impactShakeBoost);

        if (dustEffect != null)
        {
            Instantiate(dustEffect, impactCenter, Quaternion.identity);
        }
        else if (generatedFx)
        {
            // Toz, yere yatan binanın boyu boyunca yayılır
            float height = bounds.size.y;
            Vector3 lying = fallDir * height + axis * (extents.x + extents.z);
            ProceduralFx.SpawnDust(impactCenter, new Vector3(Mathf.Abs(lying.x), 0f, Mathf.Abs(lying.z)), 1f);
        }
        if (lines != null && lines.Length > 0)
            Subtitles.Say(lines);

        // Yer bir süre sarsılmaya devam eder, giderek söner
        t = 0f;
        while (t < holdAfterImpact)
        {
            t += Time.deltaTime;
            if (cam != null) cam.AddShake(Time.deltaTime * 2f * (1f - t / holdAfterImpact), impactShakeBoost);
            yield return null;
        }

        if (cam != null) cam.ClearFocus();
        if (lockCamera && input != null) input.InputLocked = false;
        if (lockCamera && cinematicBars) CinematicBars.Hide();
        onCollapsed.Invoke();
    }

    private bool IsCrushing(Collider playerCollider, Bounds localShape)
    {
        if (playerCollider == null) return false;

        // Oyuncunun ayak, gövde ve baş noktalarından biri dönen binanın içine girdiyse ezildi
        Bounds pb = playerCollider.bounds;
        float inset = Mathf.Min(0.3f, pb.extents.y * 0.5f);
        Vector3[] points =
        {
            new Vector3(pb.center.x, pb.min.y + inset, pb.center.z),
            pb.center,
            new Vector3(pb.center.x, pb.max.y - inset, pb.center.z),
        };
        foreach (Vector3 p in points)
        {
            if (localShape.Contains(building.InverseTransformPoint(p)))
                return true;
        }
        return false;
    }

    // Binanın görünen parçalarının, binanın kendi ekseninde ölçülmüş sınırları
    private static Bounds GetLocalBounds(Transform root)
    {
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        bool any = false;
        Bounds result = new Bounds();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
            Bounds mb = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (any) result.Encapsulate(p);
                else { result = new Bounds(p, Vector3.zero); any = true; }
            }
        }
        return result;
    }

    private void PlayAt(AudioClip clip, Vector3 position)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    private static Vector3 RotatePoint(Vector3 point, Vector3 pivot, Vector3 axis, float angle)
    {
        return pivot + Quaternion.AngleAxis(angle, axis) * (point - pivot);
    }

    public static Bounds GetBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(root.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
            b.Encapsulate(r.bounds);
        return b;
    }
}
