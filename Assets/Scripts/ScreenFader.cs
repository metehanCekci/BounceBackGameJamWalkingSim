using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tüm ekranı kaplayan siyah perde. Sahneler arası kalır (DontDestroyOnLoad), böylece eski sahnede
// kararan ekran yeni sahnede açılır ve karanlıkta çalan ses sahne değişince kesilmez.
// Sahneye koymak zorunlu değil: ScreenFader.Instance ilk kullanıldığında kendini oluşturur.
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader instance;

    public static ScreenFader Instance
    {
        get
        {
            if (instance == null)
                new GameObject("ScreenFader").AddComponent<ScreenFader>();
            return instance;
        }
    }

    // Yeni sahnenin ilk kareleri (ışık pozlaması, shader yüklemesi) karanlıkta geçsin
    private const float settleTimeAfterLoad = 0.3f;

    // Sahne alanlarında varsayılan: build listesinde şu anki sahneden sonraki sahne
    public const int NextScene = -1;

    public bool IsTransitioning { get; private set; }

    // Sayaç gibi şeyler perdeyi oluşturmadan geçiş sürüyor mu diye bakabilsin
    public static bool IsBusy => instance != null && instance.IsTransitioning;

    private CanvasGroup canvasGroup;
    private AudioSource audioSource;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        // Altyazıların (100) altında kalır: kararmış ekranda konuşma olursa okunabilsin
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform black = (RectTransform)new GameObject("Black", typeof(RectTransform)).transform;
        black.SetParent(transform, false);
        black.anchorMin = Vector2.zero;
        black.anchorMax = Vector2.one;
        black.offsetMin = Vector2.zero;
        black.offsetMax = Vector2.zero;
        Image image = black.gameObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        // Dünya susturulduğunda (AudioListener.pause) perdenin sesi yine çalsın
        audioSource.ignoreListenerPause = true;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // Ekranı karart, karanlıkta sesi çal ve bekle, sonraki sahneyi yükle, orada ekranı aç.
    // buildIndex: File > Build Profiles > Scene List'teki sıra; NextScene (-1) şu anki sahneden sonrakini yükler,
    // son sahnedeysek en baştaki sahneye (ana menü) döner.
    public void TransitionToScene(int buildIndex, float fadeOutTime, float fadeInTime, AudioClip sound = null, float soundVolume = 1f, float blackTime = 0f)
    {
        if (IsTransitioning) return;
        if (buildIndex < 0)
            buildIndex = (SceneManager.GetActiveScene().buildIndex + 1) % SceneManager.sceneCountInBuildSettings;
        StartCoroutine(TransitionRoutine(buildIndex, fadeOutTime, fadeInTime, sound, soundVolume, blackTime));
    }

    private IEnumerator TransitionRoutine(int buildIndex, float fadeOutTime, float fadeInTime, AudioClip sound, float soundVolume, float blackTime)
    {
        IsTransitioning = true;
        yield return Fade(1f, fadeOutTime);

        if (sound != null)
            audioSource.PlayOneShot(sound, soundVolume);
        yield return new WaitForSecondsRealtime(blackTime);

        yield return LoadAndReveal(buildIndex, fadeInTime);
    }

    // Ekranı karart, karanlıkta biraz bekle, sesi çal ve ses BİTİNCE sonraki sahneyi yükle.
    // silenceWorld: ekran kararınca siren, müzik, adım sesi vb. her şey susar, sadece bu ses duyulur.
    public void TransitionAfterSound(int buildIndex, float fadeOutTime, float delayBeforeSound, AudioClip sound, float soundVolume, float fadeInTime, bool silenceWorld = false)
    {
        if (IsTransitioning) return;
        if (buildIndex < 0)
            buildIndex = (SceneManager.GetActiveScene().buildIndex + 1) % SceneManager.sceneCountInBuildSettings;
        StartCoroutine(AfterSoundRoutine(buildIndex, fadeOutTime, delayBeforeSound, sound, soundVolume, fadeInTime, silenceWorld));
    }

    private IEnumerator AfterSoundRoutine(int buildIndex, float fadeOutTime, float delayBeforeSound, AudioClip sound, float soundVolume, float fadeInTime, bool silenceWorld)
    {
        IsTransitioning = true;
        yield return Fade(1f, fadeOutTime);

        if (silenceWorld)
        {
            // Sahnedeki sesler duraklar (yeni sahneyle birlikte silinir), kalıcı müzik tamamen kesilir
            AudioListener.pause = true;
            if (AudioManager.Instance != null)
                AudioManager.Instance.StopAll();
        }
        yield return new WaitForSecondsRealtime(delayBeforeSound);

        if (sound != null)
        {
            audioSource.PlayOneShot(sound, soundVolume);
            yield return new WaitForSecondsRealtime(sound.length);
        }

        yield return LoadAndReveal(buildIndex, fadeInTime);
    }

    private IEnumerator LoadAndReveal(int buildIndex, float fadeInTime)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"ScreenFader: build listesinde {buildIndex} numaralı sahne yok ({SceneManager.sceneCountInBuildSettings} sahne var). File > Build Profiles > Scene List'e bak.");
            // Ekran siyah kalır: sahne geçişi yarıda açılıp oyunu ortada bırakmasın
            IsTransitioning = false;
            yield break;
        }

        yield return SceneManager.LoadSceneAsync(buildIndex);
        AudioListener.pause = false;
        yield return new WaitForSecondsRealtime(settleTimeAfterLoad);
        yield return Fade(0f, fadeInTime);
        IsTransitioning = false;
    }

    // Ekranı anında karart, sesi çal, karanlıkta işi yap (ör. oyuncuyu başa al), sonra ekranı aç
    public void CutToBlack(System.Action onBlack, float blackTime, float fadeInTime, AudioClip sound = null, float soundVolume = 1f)
    {
        if (IsTransitioning) return;
        StartCoroutine(CutRoutine(onBlack, blackTime, fadeInTime, sound, soundVolume));
    }

    private IEnumerator CutRoutine(System.Action onBlack, float blackTime, float fadeInTime, AudioClip sound, float soundVolume)
    {
        IsTransitioning = true;
        canvasGroup.alpha = 1f;
        if (sound != null)
            audioSource.PlayOneShot(sound, soundVolume);
        onBlack?.Invoke();

        yield return new WaitForSecondsRealtime(blackTime);
        yield return Fade(0f, fadeInTime);
        IsTransitioning = false;
    }

    // Sahne yüklemeden sadece karart (ör. sonraki sahne henüz yoksa bölüm sonu)
    public void FadeOut(float duration)
    {
        if (IsTransitioning) return;
        StopAllCoroutines();
        StartCoroutine(Fade(1f, duration));
    }

    // Siyahtan başlayarak ekranı aç (ör. giriş cutscene'i). Sahne geçişi zaten açıyorsa karışmaz.
    public void FadeIn(float duration)
    {
        if (IsTransitioning) return;
        StopAllCoroutines();
        canvasGroup.alpha = 1f;
        StartCoroutine(Fade(0f, duration));
    }

    // Oyun duraklatılmış olsa da (timeScale 0) perde çalışsın
    private IEnumerator Fade(float target, float duration)
    {
        float start = canvasGroup.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        canvasGroup.alpha = target;
    }
}
