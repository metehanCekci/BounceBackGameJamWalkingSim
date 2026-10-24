using UnityEngine;
using UnityEngine.Events;

// Oyuncu alana girince altyazı satırlarını sırayla oynatır, bitince onFinished olayını tetikler.
// Sonraki objektif sistemi bu olaya bağlanabilir.
[RequireComponent(typeof(Collider))]
public class CutsceneTrigger : MonoBehaviour
{
    [Header("Satırlar")]
    public SubtitleLine[] lines;
    public bool playOnce = true;
    public bool interruptCurrent = false;   // Açıksa o an konuşulanı kesip bunu başlatır

    [Header("Bitince")]
    public UnityEvent onFinished;

    private bool played;
    private bool waiting;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnDisable()
    {
        if (waiting && SubtitleManager.Instance != null)
            SubtitleManager.Instance.OnQueueFinished -= HandleFinished;
        waiting = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (played && playOnce) return;
        if (lines == null || lines.Length == 0) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        played = true;
        if (interruptCurrent)
            Subtitles.Clear();

        Subtitles.Say(lines);

        if (!waiting)
        {
            waiting = true;
            SubtitleManager.Instance.OnQueueFinished += HandleFinished;
        }
    }

    private void HandleFinished()
    {
        SubtitleManager.Instance.OnQueueFinished -= HandleFinished;
        waiting = false;
        onFinished.Invoke();
    }
}
