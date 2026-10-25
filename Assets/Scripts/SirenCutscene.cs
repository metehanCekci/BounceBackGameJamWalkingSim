using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Parkur sahnesi girişi: ekran açılırken karakter yavaşça yürür, bir süre sonra siren çalmaya başlar,
// karakter durur ve kamera sirene dönüp yakınlaşır, sonra karakter o yöne koşmaya başlar ve kontrol oyuncuya geçer.
// Sahnedeki sirenler ve bomba sayaçları bu cutscene sireni başlatana kadar bekletilir.
public class SirenCutscene : MonoBehaviour
{
    [Header("Açılış")]
    public float fadeDuration = 2f;
    public bool cinematicBars = true;

    [Header("Yürüme")]
    [Range(0.1f, 1f)] public float walkSpeed = 0.4f;
    public float walkBeforeSiren = 3f;     // Siren çalmadan önce kaç saniye yürünsün

    [Header("Siren")]
    public bool controlSirens = true;      // Sahnedeki tüm sirenler ve bomba sayaçları siren anına kadar bekler
    public float reactionDelay = 1f;       // Siren başladıktan sonra karakterin durup bakmasına kadar geçen süre
    public UnityEvent onSirenStart;

    [Header("Odak & Zoom")]
    public Transform focusTarget;          // Boşsa en yakın siren
    public float zoomFOV = 30f;            // Küçük = daha yakın
    public float zoomSpeed = 3f;
    public float focusDuration = 3f;

    [Header("Satırlar (odak sırasında, her birinde ses ve metin)")]
    public SubtitleLine[] lines;
    public bool waitForSubtitles = true;   // Konuşma bitmeden koşmaya başlama

    [Header("Koşma")]
    public float runDuration = 1.5f;       // Kontrol verilmeden önce kendiliğinden koşma süresi

    [Header("Bitince")]
    public UnityEvent onFinished;

    void Awake()
    {
        if (!controlSirens) return;

        // Diğer objelerin Start'ından önce: sirenler ve sayaçlar kendiliğinden başlamasın
        foreach (AlarmSiren siren in FindObjectsByType<AlarmSiren>(FindObjectsSortMode.None))
            siren.startActive = false;
        foreach (BombTimer timer in FindObjectsByType<BombTimer>(FindObjectsSortMode.None))
            timer.startRunning = false;
    }

    IEnumerator Start()
    {
        PlayerInputManager input = FindAnyObjectByType<PlayerInputManager>();
        RealisticPlayerCamera cam = FindAnyObjectByType<RealisticPlayerCamera>();

        // 1. Ekran açılır, bantlar zaten orada, karakter yürür
        if (input != null)
        {
            input.InputLocked = true;
            input.ScriptedMove = new Vector2(0f, walkSpeed);
        }
        if (cinematicBars) CinematicBars.ShowInstant();
        ScreenFader.Instance.FadeIn(fadeDuration);

        yield return WaitClamped(walkBeforeSiren);

        // 2. Siren başlar, yürümeye devam
        StartSirens();
        yield return WaitClamped(reactionDelay);

        // 3. Karakter durur, kamera sirene döner ve yakınlaşır
        if (input != null) input.ScriptedMove = null;
        Transform target = focusTarget != null ? focusTarget : FindNearestSiren(cam);
        if (cam != null)
            cam.SetFovOverride(zoomFOV, zoomSpeed);
        if (lines != null && lines.Length > 0)
            Subtitles.Say(lines);

        float t = 0f;
        while (t < focusDuration || (waitForSubtitles && Subtitles.IsPlaying))
        {
            if (cam != null && target != null)
                cam.SetFocus(target.position);
            t += Mathf.Min(Time.deltaTime, 1f / 30f);
            yield return null;
        }

        // 4. Zoom açılır, karakter sirene doğru koşmaya başlar
        if (cam != null)
        {
            cam.ClearFovOverride();
            cam.ClearFocus();
        }
        if (input != null)
        {
            input.ScriptedMove = new Vector2(0f, 1f);
            input.ScriptedSprint = true;
        }

        yield return WaitClamped(runDuration);

        // 5. Kontrol oyuncuda
        if (input != null)
        {
            input.ScriptedMove = null;
            input.ScriptedSprint = false;
            input.InputLocked = false;
        }
        if (cinematicBars) CinematicBars.Hide();
        onFinished.Invoke();
    }

    private void StartSirens()
    {
        if (controlSirens)
        {
            foreach (AlarmSiren siren in FindObjectsByType<AlarmSiren>(FindObjectsSortMode.None))
                siren.Activate();
            foreach (BombTimer timer in FindObjectsByType<BombTimer>(FindObjectsSortMode.None))
                timer.StartTimer();
        }
        onSirenStart.Invoke();
    }

    private static Transform FindNearestSiren(RealisticPlayerCamera cam)
    {
        Vector3 from = cam != null ? cam.transform.position : Vector3.zero;
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (AlarmSiren siren in FindObjectsByType<AlarmSiren>(FindObjectsSortMode.None))
        {
            float d = (siren.transform.position - from).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = siren.transform;
            }
        }
        return best;
    }

    private static IEnumerator WaitClamped(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Mathf.Min(Time.deltaTime, 1f / 30f);
            yield return null;
        }
    }
}
