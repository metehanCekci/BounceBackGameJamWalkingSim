using System.Collections;
using UnityEngine;

// Oyuncuyu geriye savurup sırtüstü düşürür, yerde bekletir ve kaldırır.
// KnockdownObstacle'a ilk çarpışta oyuncuya otomatik eklenir.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerKnockdown : MonoBehaviour
{
    public bool IsDown { get; private set; }

    private PlayerMovement movement;
    private Rigidbody rb;
    private RealisticPlayerCamera cam;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody>();
        cam = GetComponentInChildren<RealisticPlayerCamera>();
        if (cam == null)
            cam = FindAnyObjectByType<RealisticPlayerCamera>();
    }

    public void Knockdown(KnockdownObstacle source)
    {
        if (IsDown) return;
        StartCoroutine(Run(source));
    }

    private IEnumerator Run(KnockdownObstacle s)
    {
        IsDown = true;

        PlayerInputManager input = movement.inputManager;
        if (input != null)
        {
            input.InputLocked = true;
            input.ScriptedMove = null;
            input.ScriptedSprint = false;
        }

        if (s.impactSound != null)
            AudioSource.PlayClipAtPoint(s.impactSound, transform.position, s.soundVolume);

        // Geriye savrulurken sırtüstü düş
        Vector3 dir = -transform.forward;
        dir.y = 0f;
        dir.Normalize();

        // Çarpma anında kısa ve sert geri itilme (ışınlanma değil, fizikle kayar; duvara çarparsa durur)
        const float burstTime = 0.1f;
        float burstSpeed = s.instantPushback / burstTime;
        float t = 0f;
        while (t < burstTime && s.instantPushback > 0f)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
            if (cam != null) cam.SetKnockdown(0.15f * (t / burstTime)); // Darbeyle kafa hafif geri gider
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(dir.x * burstSpeed, v.y, dir.z * burstSpeed);
        }

        t = 0f;
        while (t < s.fallTime)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
            float k = Mathf.Clamp01(t / s.fallTime);

            // Başta yavaş, sonra hızlanarak yere (darbedeki hafif geri gidişten devam eder)
            float startAmount = s.instantPushback > 0f ? 0.15f : 0f;
            if (cam != null) cam.SetKnockdown(Mathf.Lerp(startAmount, 1f, k * k));
            float push = s.pushSpeed * (1f - k);
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(dir.x * push, v.y, dir.z * push);
        }
        if (cam != null) cam.SetKnockdown(1f);

        yield return new WaitForSeconds(s.lieTime);

        // Yavaşça kalk
        t = 0f;
        while (t < s.getUpTime)
        {
            t += Time.deltaTime;
            if (cam != null) cam.SetKnockdown(1f - Mathf.SmoothStep(0f, 1f, t / s.getUpTime));
            yield return null;
        }
        if (cam != null) cam.SetKnockdown(0f);

        if (input != null) input.InputLocked = false;

        // Kalkar kalkmaz aynı engele tekrar düşmesin
        yield return new WaitForSeconds(0.5f);
        IsDown = false;
    }
}
