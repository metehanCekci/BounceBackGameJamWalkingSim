using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Sahne açılışında kısa giriş: ekran siyahtan açılırken sinematik bantlar gelir, karakter kendi kendine yürümeye başlar,
// biraz sonra satırlarını söyler ve konuşma bitene kadar yürümeye devam eder. Sonra kontrol oyuncuya geçer.
// Sahnede herhangi bir objeye ekle.
public class IntroCutscene : MonoBehaviour
{
    [Header("Açılış")]
    public float fadeDuration = 2f;        // Ekranın siyahtan açılma süresi
    public bool cinematicBars = true;

    [Header("Otomatik Yürüme")]
    [Range(0.1f, 1f)] public float walkSpeed = 0.6f;   // 1 = normal yürüme hızı

    [Header("Satırlar (her birinde ses ve metin)")]
    public SubtitleLine[] lines;
    public float delayBeforeLines = 1.5f;  // Bantlar geldikten sonra konuşmadan önce bekleme
    public float walkAfterLines = 0.5f;    // Konuşma bitince durmadan önce biraz daha yürü

    [Header("Bitince")]
    public UnityEvent onFinished;

    IEnumerator Start()
    {
        PlayerInputManager input = FindAnyObjectByType<PlayerInputManager>();

        if (input != null)
        {
            input.InputLocked = true;
            input.ScriptedMove = new Vector2(0f, walkSpeed);
        }

        // Bantlar siyahın içinde zaten hazır olsun, ekran açılınca orada görünsünler
        if (cinematicBars) CinematicBars.ShowInstant();
        ScreenFader.Instance.FadeIn(fadeDuration);

        // Takılan uzun kareler süreyi yemesin
        yield return WaitClamped(delayBeforeLines);

        if (lines != null && lines.Length > 0)
        {
            Subtitles.Say(lines);
            yield return null;
            while (Subtitles.IsPlaying)
                yield return null;
        }

        yield return WaitClamped(walkAfterLines);

        if (input != null)
        {
            input.ScriptedMove = null;
            input.InputLocked = false;
        }
        if (cinematicBars) CinematicBars.Hide();
        onFinished.Invoke();
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
