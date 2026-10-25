using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Oyuncu bu alana girince durur, bir şey söyler, arkasını döner ve geldiği yöne geri itilir.
// Geri dönmemesi gereken yerlere koy (ör. çoktan geçilen yol). Her girişte tekrar çalışır.
// Open() çağrılınca engel kalkar; o zaman girişte geri itmek yerine onEnterWhenOpen çalışır
// (ör. görev bitince sahne geçişi). Close() ile engel geri gelir.
[RequireComponent(typeof(Collider))]
public class BoundaryTrigger : MonoBehaviour
{
    [Header("Durum")]
    public bool blocking = true;           // Kapalıyken geri iter, açıkken geçişe izin verir

    [Header("Engel Açıkken Girince")]
    public string nextScene;               // Doluysa ekran kararır ve bu sahne yüklenir (boşsa sadece kararır)
    public bool fadeOutWhenOpen = true;    // Kapalıysa sadece aşağıdaki olay çalışır
    public float fadeDuration = 1.5f;
    [Range(0.1f, 1f)] public float exitWalkSpeed = 0.4f; // Kararırken karakter kendi kendine yavaşça yürür
    public UnityEvent onEnterWhenOpen;

    [Header("Satırlar (her birinde ses ve metin)")]
    public SubtitleLine[] lines = { new SubtitleLine("I shouldn't go back there.") };

    [Header("Geri İtme")]
    public Transform returnPoint;          // Boşsa oyuncu girdiği yönün tersine itilir
    public float pauseTime = 0.6f;         // Konuşurken durma süresi
    public float pushDistance = 2.5f;
    public float pushDuration = 0.8f;
    public bool turnAround = true;         // Kamera geri dönülecek yöne çevrilir

    private bool busy;
    private bool openTriggered;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    // Inspector olaylarından çağrılabilir (ör. SearchSequence > On Completed)
    public void Open()
    {
        blocking = false;
        openTriggered = false;
    }

    public void Close()
    {
        blocking = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (busy) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        if (blocking)
        {
            StartCoroutine(PushBack(player));
        }
        else if (!openTriggered)
        {
            // Açıkken olay bir kez çalışır, sahne geçişi gibi şeyler iki kere tetiklenmesin
            openTriggered = true;
            onEnterWhenOpen.Invoke();
            if (fadeOutWhenOpen)
                StartCoroutine(FadeAndLoad(player));
        }
    }

    private IEnumerator FadeAndLoad(PlayerMovement player)
    {
        // Kontrol bırakılmasın: kamera kilitli, karakter yavaşça yürümeye devam eder, bantlar gelir
        PlayerInputManager input = player.inputManager;
        if (input != null)
        {
            input.InputLocked = true;
            input.ScriptedMove = new Vector2(0f, exitWalkSpeed);
        }
        CinematicBars.Show();

        // O an konuşulan bitsin, sonra karart
        yield return null;
        while (Subtitles.IsPlaying)
            yield return null;

        if (string.IsNullOrEmpty(nextScene))
            ScreenFader.Instance.FadeOut(fadeDuration);
        else
            ScreenFader.Instance.TransitionToScene(nextScene, fadeDuration, fadeDuration);
    }

    private IEnumerator PushBack(PlayerMovement player)
    {
        busy = true;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        PlayerInputManager input = player.inputManager;
        RealisticPlayerCamera cam = FindAnyObjectByType<RealisticPlayerCamera>();

        // Geri itme yönü: dönüş noktasına doğru ya da geliş yönünün tersi
        Vector3 dir;
        if (returnPoint != null)
        {
            dir = returnPoint.position - player.transform.position;
        }
        else
        {
            Vector3 v = rb.linearVelocity;
            dir = -new Vector3(v.x, 0f, v.z);
            if (dir.sqrMagnitude < 0.01f)
                dir = -player.transform.forward;
        }
        dir.y = 0f;
        dir.Normalize();

        if (input != null) input.InputLocked = true;
        CinematicBars.Show();
        if (lines != null && lines.Length > 0)
            Subtitles.Say(lines);

        yield return new WaitForSeconds(pauseTime);

        if (turnAround && cam != null)
            cam.SetFocus(cam.transform.position + dir * 5f);

        float speed = pushDistance / Mathf.Max(0.05f, pushDuration);
        float t = 0f;
        while (t < pushDuration)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(dir.x * speed, v.y, dir.z * speed);
        }

        yield return new WaitForSeconds(0.2f);

        if (cam != null) cam.ClearFocus();
        if (input != null) input.InputLocked = false;
        CinematicBars.Hide();

        // Hemen tekrar tetiklenmesin
        yield return new WaitForSeconds(0.5f);
        busy = false;
    }
}
