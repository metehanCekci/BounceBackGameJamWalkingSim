using UnityEngine;
using UnityEngine.Events;

// Oyuncu alana girince ya da çıkınca Inspector'dan bağlanan olayları çalıştırır.
// Arama grubunu, kapıyı, objektifi ya da başka bir şeyi başlatmak için kullanılır.
[RequireComponent(typeof(Collider))]
public class EventTrigger : MonoBehaviour
{
    public bool playOnce = true;

    public UnityEvent onEnter;
    public UnityEvent onExit;

    private bool entered;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (playOnce && entered) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        entered = true;
        onEnter.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        onExit.Invoke();
    }
}
