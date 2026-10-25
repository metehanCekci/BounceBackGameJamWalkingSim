using UnityEngine;

// Oyuncu alana girince karakter sinematik olarak içeri yürür (bantlar, kilitli kamera), ekran kararır,
// karanlıkta biraz beklenir, ses (patlama) çalar ve ses bitince sonraki sahne açılır.
// Bunker'ın tamamını kaplayan trigger collider'ın olduğu objeye eklenir.
[RequireComponent(typeof(Collider))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Sahne")]
    [Tooltip("Build listesindeki sıra. -1: bu sahneden sonraki sahne.")]
    public int nextSceneIndex = ScreenFader.NextScene;

    [Header("Kararma")]
    public float fadeOutTime = 1.5f;      // Ekranın kararma süresi
    public float delayBeforeSound = 1f;   // Ekran karardıktan sonra ses çalmadan önce bekleme
    public float fadeInTime = 2f;         // Yeni sahnede ekranın açılma süresi

    [Header("Ses")]
    public AudioClip explosionSound;      // Karanlıkta çalar; yeni sahne bu ses bitince yüklenir
    [Range(0f, 1f)] public float explosionVolume = 1f;
    public bool silenceOtherSounds = true; // Ekran kararınca siren, müzik vb. tamamen susar, sadece bu ses duyulur

    [Header("Sinematik Giriş")]
    public bool cinematicEntry = true;    // Kararırken bantlar gelir, kamera kilitlenir, karakter kendi kendine içeri yürür
    [Range(0.1f, 1f)] public float walkSpeed = 0.4f;

    private bool triggered;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        // Ölüp başa dönüş sürerken girersek geçişi harcamayalım
        if (triggered || ScreenFader.IsBusy) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        triggered = true;

        if (cinematicEntry)
        {
            // Kontrol bırakılmasın: kamera kilitli, karakter içeri yürür; yeni sahnede her şey sıfırlanır
            PlayerInputManager input = player.inputManager;
            if (input != null)
            {
                input.InputLocked = true;
                input.ScriptedSprint = false;
                input.ScriptedMove = new Vector2(0f, walkSpeed);
            }
            CinematicBars.Show();
        }

        ScreenFader.Instance.TransitionAfterSound(nextSceneIndex, fadeOutTime, delayBeforeSound, explosionSound, explosionVolume, fadeInTime, silenceOtherSounds);
    }
}
