using System.Collections;
using UnityEngine;

// Müzik ve çuval arama sesi. Sahneler arası kalır (DontDestroyOnLoad).
// Her sahneye aynı prefab'ı koy: müzik aynıysa kesilmeden devam eder, farklıysa yumuşakça geçer.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Müzik (sürekli döngüde)")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.4f;
    public float musicFadeTime = 1.5f;

    [Header("Ortam Sesleri (sürekli döngüde)")]
    public AudioClip birds;
    [Range(0f, 1f)] public float birdsVolume = 0.3f;
    public AudioClip wind;
    [Range(0f, 1f)] public float windVolume = 0.25f;

    [Header("Çuval Arama")]
    public AudioClip searchSound;   // E basılı tutulunca baştan çalar, yarıda bırakılırsa kesilir
    [Range(0f, 1f)] public float searchVolume = 1f;

    private AudioSource musicA;
    private AudioSource musicB;
    private AudioSource search;
    private AudioSource birdsSource;
    private AudioSource windSource;
    private bool usingA = true;
    private Coroutine fade;

    private AudioSource ActiveMusic => usingA ? musicA : musicB;
    public AudioClip CurrentMusic => ActiveMusic != null ? ActiveMusic.clip : null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Yeni sahnedeki kopya: müziği farklıysa eskisine geçiş yaptır, kendini sil
            if (music != null)
                Instance.PlayMusic(music);
            Instance.SetAmbience(birds, birdsVolume, wind, windVolume);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        musicA = CreateSource(true);
        musicB = CreateSource(true);
        search = CreateSource(false);
        birdsSource = CreateSource(true);
        windSource = CreateSource(true);

        if (music != null)
            PlayMusic(music);
        SetAmbience(birds, birdsVolume, wind, windVolume);
    }

    // Yeni sahnede farklı ortam sesi seçildiyse ona geçer, aynıysa kesmeden devam eder
    public void SetAmbience(AudioClip birdsClip, float birdsVol, AudioClip windClip, float windVol)
    {
        PlayLoop(birdsSource, birdsClip, birdsVol);
        PlayLoop(windSource, windClip, windVol);
    }

    private static void PlayLoop(AudioSource source, AudioClip clip, float volume)
    {
        source.volume = volume;
        if (clip == null)
        {
            source.Stop();
            return;
        }
        if (source.clip == clip && source.isPlaying) return;

        source.clip = clip;
        source.Play();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void StartSearchSound()
    {
        if (Instance == null || Instance.searchSound == null) return;
        Instance.search.clip = Instance.searchSound;
        Instance.search.volume = Instance.searchVolume;
        Instance.search.Play();
    }

    public static void StopSearchSound()
    {
        if (Instance != null)
            Instance.search.Stop();
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || clip == CurrentMusic) return;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Crossfade(clip));
    }

    private IEnumerator Crossfade(AudioClip clip)
    {
        AudioSource from = ActiveMusic;
        AudioSource to = usingA ? musicB : musicA;
        usingA = !usingA;

        to.clip = clip;
        to.volume = 0f;
        to.Play();

        float startVolume = from.volume;
        float t = 0f;
        while (t < musicFadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / musicFadeTime);
            to.volume = musicVolume * k;
            from.volume = startVolume * (1f - k);
            yield return null;
        }

        to.volume = musicVolume;
        from.Stop();
        fade = null;
    }

    private AudioSource CreateSource(bool looping)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = looping;
        source.spatialBlend = 0f;
        return source;
    }
}
