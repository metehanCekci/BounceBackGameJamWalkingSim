using UnityEngine;

// Oyuncu bu objenin trigger alanına girince altyazıları oynatır.
// Kullanım: boş obje oluştur > Box Collider ekle > bu scripti ekle > satırları yaz.
[RequireComponent(typeof(Collider))]
public class SubtitleTrigger : MonoBehaviour
{
    public SubtitleLine[] lines;
    public bool playOnce = true;
    public bool interruptCurrent = false; // Açıksa o an konuşulanı kesip bunu başlatır

    private bool played;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (played && playOnce) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        played = true;
        if (interruptCurrent)
            Subtitles.Clear();
        Subtitles.Say(lines);
    }
}
