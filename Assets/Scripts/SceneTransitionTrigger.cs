using UnityEngine;

// Oyuncu alana girince ekran kararır, karanlıkta ses (patlama) çalar ve sonraki sahne açılır.
// Bunker'ın tamamını kaplayan trigger collider'ın olduğu objeye eklenir.
[RequireComponent(typeof(Collider))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Sahne")]
    public string nextSceneName = "ParkourMapReverse"; // Build listesinde olmalı

    [Header("Kararma")]
    public float fadeOutTime = 1.5f; // Ekranın kararma süresi
    public float blackTime = 3f;     // Ekran simsiyahken bekleme, patlama bu arada çalar
    public float fadeInTime = 2f;    // Yeni sahnede ekranın açılma süresi

    [Header("Sinematik Giriş")]
    public bool cinematicEntry = true;   // Kararırken bantlar gelir, kamera kilitlenir, karakter yavaşça içeri yürür
    [Range(0.1f, 1f)] public float walkSpeed = 0.4f;

    [Header("Ses")]
    public AudioClip explosionSound; // Ekran tamamen kararınca çalar, sahne değişince kesilmez
    [Range(0f, 1f)] public float explosionVolume = 1f;

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

        ScreenFader.Instance.TransitionToScene(nextSceneName, fadeOutTime, fadeInTime, explosionSound, explosionVolume, blackTime);
    }
}
