using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

// Tam ekran video (mp4) oynatır. Sahne açılınca, trigger'a girince ya da bir olaydan Play() ile başlar.
// Oynarken oyuncu kilitlenir, istenirse tuşla geçilebilir; bitince olay tetiklenir ya da sonraki sahne yüklenir.
// Ekran, render texture ve video oynatıcı kendiliğinden oluşturulur, kurulum gerekmez.
public class VideoCutscene : MonoBehaviour
{
    [Header("Video")]
    public VideoClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    public bool preload = true;            // Sahne açılınca arka planda hazırlanır, başlarken takılmaz
    public float blackBeforePlay = 0.5f;   // Oynamadan önce siyah ekranda bekleme (video bu arada tam hazır olur)
    public float videoFadeIn = 1f;         // Video başlarken siyahtan açılma süresi (ses de birlikte yükselir), 0: tak diye gelir
    public Color backgroundColor = Color.black; // Video gelmeden önceki zemin (önceki sahne beyaza kararıyorsa beyaz yap)

    [Header("Başlatma")]
    public bool playOnStart = false;       // Sahne açılınca oynar
    public bool playOnTriggerEnter = true; // Bu objede trigger collider varsa oyuncu girince oynar

    [Header("Oynarken")]
    public bool lockPlayer = true;
    public bool stopGameMusic = true;      // Video başlarken çalan oyun müziği kısılıp durur
    public bool skippable = true;          // Boşluk / Enter / Esc ile geçilebilir
    public float skipDelay = 1f;           // Yanlışlıkla geçilmesin diye başta bekleme
    public bool pauseWorldRendering = true; // Video oynarken arkadaki dünya çizilmez (görünmüyor zaten, takılmayı azaltır)

    [Header("Bitince")]
    public bool loadNextScene = false;     // Açıksa video biter bitmez ekran siyah kalır ve sonraki sahne yüklenir
    [Tooltip("Build listesindeki sıra. -1: bu sahneden sonraki sahne.")]
    public int nextSceneIndex = ScreenFader.NextScene;
    public float fadeInTime = 1.5f;        // Yeni sahnenin açılma süresi
    public UnityEvent onFinished;

    public bool IsPlaying { get; private set; }

    private bool played;
    private bool preparing;
    private GameObject screen;
    private Canvas canvas;
    private RawImage image;
    private RenderTexture texture;
    private VideoPlayer player;

    void Start()
    {
        if (preload && clip != null)
            StartCoroutine(Prepare());
        if (playOnStart)
            Play();
    }

    void OnDestroy()
    {
        Cleanup();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!playOnTriggerEnter || played) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        Play();
    }

    public void Play()
    {
        if (played || clip == null) return;
        played = true;
        StartCoroutine(Run());
    }

    // Ekranı gizli kurar ve videoyu hazırlar (çözücü açılır, ilk kareler yüklenir)
    private IEnumerator Prepare()
    {
        if (screen == null)
            Build();
        if (player.isPrepared || preparing)
        {
            while (!player.isPrepared)
                yield return null;
            yield break;
        }

        preparing = true;
        player.Prepare();
        while (!player.isPrepared)
            yield return null;

        // Isınma: kısa bir an sessizce oynatıp durdur, çözücü ve ilk kareler gerçekten hazır olsun
        AudioSource audio = player.GetTargetAudioSource(0);
        if (audio != null) audio.mute = true;
        player.Play();
        while (player.frame < 2)
            yield return null;
        player.Pause();
        player.frame = 0;
        if (audio != null) audio.mute = false;
        preparing = false;
    }

    private IEnumerator Run()
    {
        IsPlaying = true;
        PlayerInputManager input = lockPlayer ? FindAnyObjectByType<PlayerInputManager>() : null;
        if (input != null) input.InputLocked = true;
        Subtitles.Clear();
        // Oyunun müziği videonun sesine binmesin
        if (stopGameMusic && AudioManager.Instance != null)
            AudioManager.Instance.FadeOutMusic(0.5f);

        // Siyah zemin hemen gelir; video hazırlanırken ve kısa bir süre daha siyah kalır
        if (screen == null)
            Build();
        canvas.enabled = true;
        image.enabled = false;

        yield return Prepare();
        yield return new WaitForSecondsRealtime(blackBeforePlay);

        bool finished = false;
        player.loopPointReached += _ => finished = true;

        // Görüntü ve ses sıfırdan başlar, ilk kare gelince birlikte açılır
        AudioSource audio = player.GetTargetAudioSource(0);
        image.color = new Color(1f, 1f, 1f, 0f);
        if (audio != null) audio.volume = 0f;

        player.Play();
        while (player.frame < 1 && !finished)
            yield return null;
        image.enabled = true;
        StartCoroutine(FadeInVideo(audio));

        Camera worldCamera = Camera.main;
        int savedMask = worldCamera != null ? worldCamera.cullingMask : 0;
        if (pauseWorldRendering && worldCamera != null)
            worldCamera.cullingMask = 0;

        float t = 0f;
        while (!finished)
        {
            t += Time.unscaledDeltaTime;
            if (skippable && t > skipDelay && SkipPressed())
                break;
            yield return null;
        }

        player.Stop();
        if (pauseWorldRendering && worldCamera != null)
            worldCamera.cullingMask = savedMask;

        // Sahne geçişi varsa video kapanınca oyun bir an bile görünmesin: ekran önce simsiyah olur
        if (loadNextScene)
            ScreenFader.Instance.FadeOut(0f);

        Cleanup();
        IsPlaying = false;

        if (loadNextScene)
        {
            ScreenFader.Instance.TransitionToScene(nextSceneIndex, 0f, fadeInTime);
            yield break;
        }

        if (input != null) input.InputLocked = false;
        onFinished.Invoke();
    }

    private IEnumerator FadeInVideo(AudioSource audio)
    {
        float t = 0f;
        while (t < videoFadeIn && image != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / videoFadeIn);
            image.color = new Color(1f, 1f, 1f, k);
            if (audio != null) audio.volume = volume * k;
            yield return null;
        }
        if (image != null) image.color = Color.white;
        if (audio != null) audio.volume = volume;
    }

    private static bool SkipPressed()
    {
        Keyboard k = Keyboard.current;
        if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame))
            return true;
        return Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
    }

    // Her şeyin (altyazı, bantlar dahil) üstünde siyah zemin + oranı korunan video; başta gizli
    private void Build()
    {
        texture = new RenderTexture((int)clip.width, (int)clip.height, 0);
        texture.Create();

        screen = new GameObject("VideoCutsceneScreen");
        canvas = screen.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvas.enabled = false;

        RectTransform background = CreateFullScreen("Background", screen.transform);
        background.gameObject.AddComponent<Image>().color = backgroundColor;

        RectTransform videoRect = CreateFullScreen("Video", screen.transform);
        image = videoRect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;

        AspectRatioFitter fitter = videoRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = clip.height > 0 ? clip.width / (float)clip.height : 16f / 9f;

        player = screen.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.clip = clip;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = texture;
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;

        // Ses AudioSource üzerinden çalar: "Direct" modda video yavaş çözülünce ses tamponu taşıp kopuyordu
        AudioSource audioSource = screen.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.controlledAudioTrackCount = 1;
        player.EnableAudioTrack(0, true);
        player.SetTargetAudioSource(0, audioSource);
    }

    private void Cleanup()
    {
        if (screen != null)
            Destroy(screen);
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
        screen = null;
        texture = null;
        player = null;
    }

    private static RectTransform CreateFullScreen(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
}
