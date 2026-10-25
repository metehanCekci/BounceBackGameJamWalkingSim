using UnityEngine;

// Sahnedeki bir olaydan müziği değiştirmek için (ör. siren başlayınca gerilim müziği).
// AudioManager sahneler arası kaldığı için sahne olayları doğrudan ona bağlanamaz; bu bileşen aracılık eder.
// Hiç AudioManager yoksa (sahne doğrudan açıldıysa) bir tane oluşturur.
public class MusicChange : MonoBehaviour
{
    public AudioClip music;
    public bool playOnStart = false;       // Açıksa sahne açılınca çalar, kapalıysa bir olaydan Play() çağır

    void Start()
    {
        if (playOnStart)
            Play();
    }

    public void Play()
    {
        if (music == null) return;

        if (AudioManager.Instance == null)
            new GameObject("AudioManager").AddComponent<AudioManager>();

        AudioManager.Instance.PlayMusic(music);
    }
}
