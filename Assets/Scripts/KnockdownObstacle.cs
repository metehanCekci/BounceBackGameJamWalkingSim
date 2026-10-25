using UnityEngine;

// Bu bileşeni taşıyan objeye çarpınca oyuncu geriye savrulup sırtüstü düşer, biraz yerde kalır ve kalkar.
// Normal (katı) collider da trigger collider da olur. Prefab'a ekleyip kopyalayarak kullan.
[RequireComponent(typeof(Collider))]
public class KnockdownObstacle : MonoBehaviour
{
    [Header("Düşüş")]
    public float instantPushback = 0.5f;   // Çarptığı an anında geriye atılma mesafesi (metre)
    public float pushSpeed = 4f;           // Ardından geriye savrulma hızı
    public float fallTime = 0.35f;         // Yere düşme süresi
    public float lieTime = 1.2f;           // Yerde kalma süresi
    public float getUpTime = 0.9f;         // Kalkma süresi

    [Header("Ses (isteğe bağlı)")]
    public AudioClip impactSound;
    [Range(0f, 1f)] public float soundVolume = 1f;

    void OnCollisionEnter(Collision collision)
    {
        TryKnockdown(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        TryKnockdown(other);
    }

    private void TryKnockdown(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        // Oyuncuda yoksa düşme bileşeni otomatik eklenir, kurulum gerekmesin
        PlayerKnockdown knockdown = player.GetComponent<PlayerKnockdown>();
        if (knockdown == null)
            knockdown = player.gameObject.AddComponent<PlayerKnockdown>();

        knockdown.Knockdown(this);
    }
}
