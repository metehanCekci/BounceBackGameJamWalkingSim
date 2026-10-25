using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Oyuncu alana girince kamera bir noktaya odaklanır: hareket ve bakış kilitlenir,
// sinematik bantlar kayar, istenirse bir metin oynar. Süre bitince kontrol geri verilir.
[RequireComponent(typeof(Collider))]
public class FocusTrigger : MonoBehaviour
{
    [Header("Odak")]
    public Transform focusTarget;          // Kameranın bakacağı obje
    public float duration = 3f;            // Odakta kalma süresi
    public bool cinematicBars = true;

    [Header("Satırlar (isteğe bağlı, her birinde ses ve metin)")]
    public SubtitleLine[] lines;
    public bool waitForSubtitles = true;   // Süre bitse de konuşma bitmeden bırakma

    [Header("Diğer")]
    public bool playOnce = true;
    public UnityEvent onFinished;

    private bool played;
    private bool running;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (running || (playOnce && played) || focusTarget == null) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null)
            StartCoroutine(Run(player));
    }

    private IEnumerator Run(PlayerMovement player)
    {
        running = true;
        played = true;

        PlayerInputManager input = player.inputManager;
        RealisticPlayerCamera cam = FindAnyObjectByType<RealisticPlayerCamera>();

        if (input != null) input.InputLocked = true;
        if (cinematicBars) CinematicBars.Show();
        if (lines != null && lines.Length > 0)
            Subtitles.Say(lines);

        float t = 0f;
        while (t < duration || (waitForSubtitles && Subtitles.IsPlaying))
        {
            // Hedef hareket ediyorsa kamera onu takip etsin
            if (cam != null && focusTarget != null)
                cam.SetFocus(focusTarget.position);
            t += Time.deltaTime;
            yield return null;
        }

        if (cam != null) cam.ClearFocus();
        if (input != null) input.InputLocked = false;
        if (cinematicBars) CinematicBars.Hide();

        running = false;
        onFinished.Invoke();
    }
}
