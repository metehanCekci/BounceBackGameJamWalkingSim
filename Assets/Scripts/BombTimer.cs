using UnityEngine;
using UnityEngine.SceneManagement;

// Haritada fazla kalırsak bomba düşer: süre dolunca ekran anında kararır, patlama sesi çalar
// ve oyuncu beklemeden başa döner. Bunker'a girip sahne geçişi başlarsa sayaç durur.
public class BombTimer : MonoBehaviour
{
    public float timeLimit = 120f;               // Saniye; ParkourMap'te bunker kuş uçuşu ~320 m, tam depar bile ~67 sn
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float explosionVolume = 1f;
    public float blackTime = 0.15f;              // Karanlıkta kalma, oyun hızlı olsun diye kısa
    public float fadeInTime = 0.35f;             // Başa dönünce ekranın açılma süresi
    public Transform spawnPoint;                 // Boşsa oyuncunun sahne başındaki yeri kullanılır
    public bool reloadScene = false;             // Sahnede sıfırlanması gereken şeyler varsa aç (yükleme kadar sürer)

    public float TimeLeft { get; private set; }  // Ekranda geri sayım göstermek istersen buradan oku

    private PlayerMovement player;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool exploding;

    void Start()
    {
        TimeLeft = timeLimit;
        player = FindAnyObjectByType<PlayerMovement>();

        Transform start = spawnPoint != null ? spawnPoint : (player != null ? player.transform : null);
        if (start != null)
        {
            startPosition = start.position;
            // Oyuncu sadece sağa sola döner, eğik bir spawn noktası onu yatırmasın
            startRotation = Quaternion.Euler(0f, start.eulerAngles.y, 0f);
        }
    }

    void Update()
    {
        // Bunker geçişi ya da önceki patlama sürerken saymayız
        if (exploding || ScreenFader.IsBusy) return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft > 0f) return;

        TimeLeft = 0f;
        exploding = true;
        if (reloadScene || player == null)
            ScreenFader.Instance.TransitionToScene(SceneManager.GetActiveScene().name, 0f, fadeInTime, explosionSound, explosionVolume, blackTime);
        else
            ScreenFader.Instance.CutToBlack(Respawn, blackTime, fadeInTime, explosionSound, explosionVolume);
    }

    private void Respawn()
    {
        player.Teleport(startPosition, startRotation);
        TimeLeft = timeLimit;
        exploding = false;
    }
}
