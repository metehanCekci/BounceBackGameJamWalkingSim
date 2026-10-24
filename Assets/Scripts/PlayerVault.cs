using System;
using UnityEngine;

// Rainbow Six Siege tarzı engel atlama: önündeki alçak engele bakıp zıplama tuşuna basınca
// karakter fizikten çıkar, hazır bir yol boyunca ince engelin üstünden atlar ya da kalın engelin üstüne tırmanır.
// Tetikleyen PlayerMovement'tır (zıplama tuşu), bu script sadece algılar ve hareketi oynatır.
[RequireComponent(typeof(Rigidbody))]
public class PlayerVault : MonoBehaviour
{
    [Header("Algılama")]
    public LayerMask vaultableLayers;
    public float detectDistance = 0.5f;   // Collider yüzeyinden engele olan en fazla mesafe
    public float minVaultHeight = 0.3f;   // Ayak hizasına göre engel yüksekliği
    public float maxVaultHeight = 1.0f;
    public float maxVaultDepth = 0.8f;    // Bundan kalın engellerin üstünden atlanmaz, üstüne çıkılır
    [Range(0f, 90f)] public float maxApproachAngle = 50f; // Engele bundan daha çapraz bakarken atlanmaz

    [Header("Hareket")]
    public float vaultDuration = 0.6f;
    public float climbDuration = 0.75f;
    public float clearance = 0.12f;        // Engelin üstünden ne kadar boşlukla geçilir
    public float landingDistance = 0.35f;  // Engelin arka yüzünden ne kadar öteye inilir
    public float maxDropHeight = 2.5f;     // Arkası bundan derinse yere kadar biz taşımayız, fizik düşürür

    [Header("Debug")]
    public bool drawGizmos = true;

    public bool IsVaulting { get; private set; }
    public bool IsClimbing { get; private set; } // Atlama değil tırmanma
    public float Progress { get; private set; }  // 0-1, kamera efektleri okur

    public event Action<bool> OnVaultStarted; // true = tırmanma
    public event Action OnVaultEnded;

    private Rigidbody rb;
    private Collider col;

    // Yol ayak noktası (collider'ın alt orta noktası) üzerinden hesaplanır
    private Vector3 startFeet, overFeet, landFeet;
    private Vector3 feetToPivot;
    private Vector3 exitVelocity;
    private float duration, timer, riseFraction, clearFraction;
    private bool hasPath;
    private CollisionDetectionMode savedCollisionMode;

    void Reset()
    {
        vaultableLayers = LayerMask.GetMask("Vaultable");
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    void OnDisable()
    {
        if (IsVaulting)
            EndVault();
    }

    public bool TryStartVault()
    {
        if (IsVaulting) return false;

        Bounds bounds = col.bounds;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float height = bounds.size.y;
        Vector3 feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        // 1. Önümüzde engel var mı? Diz hizasından ileri bak
        Vector3 kneeOrigin = feet + Vector3.up * (minVaultHeight * 0.8f);
        if (!Physics.Raycast(kneeOrigin, forward, out RaycastHit wallHit, radius + detectDistance, vaultableLayers, QueryTriggerInteraction.Ignore))
            return false;

        // Dik bir yüzey olmalı (rampaya vault yapılmaz), engele dik doğrultuda atlarız
        if (Mathf.Abs(wallHit.normal.y) > 0.3f) return false;
        Vector3 dir = -Vector3.ProjectOnPlane(wallHit.normal, Vector3.up).normalized;
        if (Vector3.Angle(forward, dir) > maxApproachAngle) return false;

        // 2. En yüksek atlama hizasında da bir şeye çarpıyorsak engel çok yüksek (ya da üstü kapalı)
        float distToWall = Vector3.Dot(wallHit.point - feet, dir);
        if (Physics.Raycast(feet + Vector3.up * (maxVaultHeight + 0.05f), dir, distToWall + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;

        // 3. Engelin üst yüzeyini bul
        Vector3 topOrigin = wallHit.point + dir * 0.05f;
        topOrigin.y = feet.y + maxVaultHeight + 0.05f;
        if (!Physics.Raycast(topOrigin, Vector3.down, out RaycastHit topHit, maxVaultHeight + 0.05f, vaultableLayers, QueryTriggerInteraction.Ignore))
            return false;
        if (topHit.normal.y < 0.7f) return false;

        float topY = topHit.point.y;
        if (topY - feet.y < minVaultHeight) return false;

        Vector3 nearEdge = new Vector3(wallHit.point.x, topY, wallHit.point.z);

        // 4. Engel ne kadar kalın? Arkadan engele doğru bak; ışın engelin içinden başlarsa çarpmaz, yani kalındır
        Vector3 farProbe = nearEdge + dir * (maxVaultDepth + 0.05f) + Vector3.down * 0.05f;
        bool isThin = Physics.Raycast(farProbe, -dir, out RaycastHit backHit, maxVaultDepth + 0.05f, vaultableLayers, QueryTriggerInteraction.Ignore);

        if (isThin)
        {
            float depth = Vector3.Dot(backHit.point - nearEdge, dir);
            Vector3 farEdge = nearEdge + dir * depth;
            float peakY = topY + clearance;

            Vector3 land = farEdge + dir * (radius + landingDistance);
            land.y = feet.y; // Arkada zemin yoksa başladığımız seviyede bırakıp fiziğe devrederiz
            if (Physics.Raycast(new Vector3(land.x, topY + 0.1f, land.z), Vector3.down, out RaycastHit groundHit, maxDropHeight + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                land.y = groundHit.point.y;

            // Engelin üstünde eğilmiş kadar, indiğimiz yerde tam boy yer olmalı
            Vector3 overNear = new Vector3(nearEdge.x, peakY, nearEdge.z);
            Vector3 overFar = new Vector3(farEdge.x, peakY, farEdge.z);
            if (!HasRoom(overNear, height * 0.6f, radius) || !HasRoom(overFar, height * 0.6f, radius) || !HasRoom(land, height, radius))
                return false;

            startFeet = feet;
            overFeet = overNear;
            landFeet = land;
            duration = vaultDuration;
            riseFraction = 0.35f;

            // Engelin arka kenarını vücutça geçene kadar aşağı inmeyiz
            Vector3 path = Vector3.ProjectOnPlane(land - feet, Vector3.up);
            Vector3 toClear = Vector3.ProjectOnPlane(farEdge + dir * (radius * 0.8f) - feet, Vector3.up);
            float clearAt = path.sqrMagnitude > 0.0001f ? Vector3.Dot(toClear, path) / path.sqrMagnitude : 1f;
            clearFraction = Mathf.Clamp(clearAt, riseFraction, 0.85f);

            // Çıkışta yoldaki hız korunur, hareket kesik kesik bitmesin
            float dropTime = (1f - clearFraction) * duration;
            exitVelocity = path / duration + Vector3.down * (2f * (peakY - land.y) / dropTime);
            IsClimbing = false;
        }
        else
        {
            Vector3 over = nearEdge - dir * (radius * 0.9f);
            over.y = topY + clearance * 0.5f;
            Vector3 land = nearEdge + dir * (radius + 0.1f);

            if (!HasRoom(over, height, radius) || !HasRoom(land, height, radius))
                return false;

            startFeet = feet;
            overFeet = over;
            landFeet = land;
            duration = climbDuration;
            riseFraction = 0.55f;
            exitVelocity = Vector3.zero;
            IsClimbing = true;
        }

        // Yolu ayak noktasına göre kurduk, pivotu aynı farkla taşıyacağız
        feetToPivot = transform.position - feet;
        startFeet = rb.position - feetToPivot;

        savedCollisionMode = rb.collisionDetectionMode;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // Kinematik gövde sadece bunu destekliyor
        rb.isKinematic = true;

        timer = 0f;
        Progress = 0f;
        IsVaulting = true;
        hasPath = true;
        OnVaultStarted?.Invoke(IsClimbing);
        return true;
    }

    void FixedUpdate()
    {
        if (!IsVaulting) return;

        // Son konuma bir önceki adımda gittik, şimdi fiziği geri verebiliriz
        if (timer >= duration)
        {
            EndVault();
            return;
        }

        timer += Time.fixedDeltaTime;
        Progress = Mathf.Clamp01(timer / duration);
        rb.MovePosition(EvaluatePath(Progress) + feetToPivot);
    }

    private void EndVault()
    {
        RestorePhysics();
        rb.linearVelocity = exitVelocity;
        Progress = 1f;
        OnVaultEnded?.Invoke();
    }

    // Ölüp başa dönerken yarım kalan atlamayı iniş efekti/sesi olmadan bitir
    public void Cancel()
    {
        if (!IsVaulting) return;
        RestorePhysics();
        Progress = 0f;
    }

    private void RestorePhysics()
    {
        rb.isKinematic = false;
        rb.collisionDetectionMode = savedCollisionMode;
        IsVaulting = false;
    }

    private Vector3 EvaluatePath(float t)
    {
        if (IsClimbing)
        {
            // Tırmanma: önce engelin yüzünde yukarı çekil, sonra üstüne doğru öne geç
            if (t < riseFraction)
            {
                float u = t / riseFraction;
                Vector3 p = Vector3.Lerp(startFeet, overFeet, u);
                p.y = Mathf.Lerp(startFeet.y, overFeet.y, Mathf.SmoothStep(0f, 1f, u));
                return p;
            }
            float s = (t - riseFraction) / (1f - riseFraction);
            Vector3 q = Vector3.Lerp(overFeet, landFeet, Mathf.SmoothStep(0f, 1f, s));
            q.y = Mathf.Lerp(overFeet.y, landFeet.y, s);
            return q;
        }

        // Atlama: ileri sabit hızla gideriz (momentum), dikeyde hızlı kalkış, engelin üstünde süzülme, düşüş
        Vector3 pos = Vector3.Lerp(startFeet, landFeet, t);
        float peakY = overFeet.y;
        if (t < riseFraction)
        {
            float u = t / riseFraction;
            pos.y = Mathf.Lerp(startFeet.y, peakY, 1f - (1f - u) * (1f - u));
        }
        else if (t < clearFraction)
        {
            pos.y = peakY;
        }
        else
        {
            float k = (t - clearFraction) / (1f - clearFraction);
            pos.y = Mathf.Lerp(peakY, landFeet.y, k * k);
        }
        return pos;
    }

    private bool HasRoom(Vector3 feetPos, float bodyHeight, float radius)
    {
        float r = radius * 0.95f;
        Vector3 bottom = feetPos + Vector3.up * (r + 0.02f);
        Vector3 top = feetPos + Vector3.up * Mathf.Max(bodyHeight - r, r + 0.02f);
        foreach (Collider other in Physics.OverlapCapsule(bottom, top, r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (other != col) return false;
        }
        return true;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || !hasPath) return;

        // Son atlamanın ayak yolu: ayar yaparken Scene penceresinden bakılır
        Gizmos.color = IsClimbing ? Color.yellow : Color.green;
        Vector3 prev = EvaluatePath(0f);
        for (int i = 1; i <= 20; i++)
        {
            Vector3 next = EvaluatePath(i / 20f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
        Gizmos.DrawWireSphere(landFeet, 0.05f);
    }
}
