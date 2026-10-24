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
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        triggered = true;
        ScreenFader.Instance.TransitionToScene(nextSceneName, fadeOutTime, fadeInTime, explosionSound, explosionVolume, blackTime);
    }
}
