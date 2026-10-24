using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Ekranın üstünde, ortadan harf harf yazılan ve konuşurken ses çıkaran altyazı kutusu.
// Sahneye koymak zorunlu değil: Subtitles.Say ilk çağrıldığında kendini oluşturur.
// Ayarları değiştirmek istersen sahneye boş obje koyup bu scripti ekle.
public class SubtitleManager : MonoBehaviour
{
    public static SubtitleManager Instance { get; private set; }

    [Header("Görünüm")]
    public Font font;                        // Boşsa Resources/SubtitleFont kullanılır
    public int fontSize = 38;
    public int speakerFontSize = 28;
    public Color boxColor = new Color(0f, 0f, 0f, 0.55f);
    public float topMargin = 40f;
    public float boxWidth = 1100f;           // Yükseklik yazıya göre otomatik ayarlanır

    [Header("Yazma Hızı")]
    public float charactersPerSecond = 30f;
    public float commaPause = 0.12f;
    public float sentencePause = 0.35f;

    [Header("Ekranda Kalma")]
    public float holdTimeBase = 1.5f;
    public float holdTimePerChar = 0.04f;
    public float fadeDuration = 0.2f;

    [Header("Ses")]
    public SubtitleVoice defaultVoice;
    [Range(0f, 1f)] public float blipVolume = 0.5f;

    [Header("Atlama")]
    public bool allowSkip = true;
    public Key skipKey = Key.Enter;

    public bool IsPlaying => runner != null;

    // Kuyruktaki her şey bittiğinde tetiklenir (sonraki objektif ya da sahne için)
    public event System.Action OnQueueFinished;

    private readonly Queue<SubtitleLine> queue = new Queue<SubtitleLine>();
    private readonly Dictionary<BlipStyle, AudioClip> blipClips = new Dictionary<BlipStyle, AudioClip>();
    private Coroutine runner;
    private bool skipRequested;

    private CanvasGroup canvasGroup;
    private Text speakerText;
    private Text bodyText;
    private RectTransform boxRect;
    private AudioSource audioSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (font == null)
            font = Resources.Load<Font>("SubtitleFont");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        BuildUI();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (!allowSkip || !IsPlaying || skipKey == Key.None || Keyboard.current == null) return;
        if (Keyboard.current[skipKey].wasPressedThisFrame)
            skipRequested = true;
    }

    public void Enqueue(SubtitleLine line)
    {
        if (line == null || string.IsNullOrEmpty(line.text)) return;
        queue.Enqueue(line);
        if (runner == null)
            runner = StartCoroutine(PlayQueue());
    }

    public void Skip()
    {
        skipRequested = true;
    }

    public void Clear()
    {
        queue.Clear();
        if (runner != null)
        {
            StopCoroutine(runner);
            runner = null;
        }
        canvasGroup.alpha = 0f;
    }

    private IEnumerator PlayQueue()
    {
        yield return Fade(1f);

        while (queue.Count > 0)
            yield return PlayLine(queue.Dequeue());

        yield return Fade(0f);
        runner = null;
        OnQueueFinished?.Invoke();
    }

    private IEnumerator PlayLine(SubtitleLine line)
    {
        SubtitleVoice voice = line.voice != null ? line.voice : defaultVoice;
        string text = line.text;

        bool hasName = voice != null && !string.IsNullOrEmpty(voice.speakerName);
        speakerText.gameObject.SetActive(hasName);
        if (hasName)
        {
            speakerText.text = voice.speakerName;
            speakerText.color = voice.nameColor;
        }
        bodyText.color = voice != null ? voice.textColor : Color.white;
        RefreshLayout();

        float speed = charactersPerSecond * (voice != null ? voice.speedMultiplier : 1f);
        int blipEvery = voice != null ? voice.blipEvery : 2;
        int letterCount = 0;
        skipRequested = false;

        // Harf harf yaz
        for (int i = 0; i < text.Length && !skipRequested; i++)
        {
            SetVisibleCharacters(text, i + 1);

            char c = text[i];
            if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c))
            {
                if (letterCount % blipEvery == 0)
                    PlayBlip(voice);
                letterCount++;
            }

            float delay = 1f / speed;
            bool atWordEnd = i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]);
            if (atWordEnd && (c == '.' || c == '!' || c == '?'))
                delay += sentencePause;
            else if (atWordEnd && (c == ',' || c == ';' || c == ':'))
                delay += commaPause;

            yield return WaitOrSkip(delay);
        }

        SetVisibleCharacters(text, text.Length);
        skipRequested = false;

        float hold = line.holdTime > 0f ? line.holdTime : holdTimeBase + text.Length * holdTimePerChar;
        yield return WaitOrSkip(hold);
        skipRequested = false;
    }

    private IEnumerator WaitOrSkip(float duration)
    {
        float t = 0f;
        while (t < duration && !skipRequested)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void SetVisibleCharacters(string text, int count)
    {
        // Sadece görünen kısmı yazıyoruz, yazı ortalı olduğu için ortadan büyüyor
        bodyText.text = text.Substring(0, count);
        RefreshLayout();
    }

    // Kutu boyutu bir sonraki karede değil, anında yazının boyutuna göre hesaplansın.
    // Yoksa kutu ilk karede eski boyutta kalıp sonra küçülüyor.
    private void RefreshLayout()
    {
        if (boxRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(boxRect);
    }

    private void PlayBlip(SubtitleVoice voice)
    {
        AudioClip clip;
        float pitch = 1f, variation = 0.06f, volume = 1f;

        if (voice != null)
        {
            if (voice.blipSounds != null && voice.blipSounds.Length > 0)
                clip = voice.blipSounds[Random.Range(0, voice.blipSounds.Length)];
            else
                clip = GetBlip(voice.blipStyle);

            pitch = voice.pitch;
            variation = voice.pitchVariation;
            volume = voice.volume;
        }
        else
        {
            clip = GetBlip(BlipStyle.Bright);
        }

        // Öncekini kes, sesler üst üste binip çamurlaşmasın
        audioSource.Stop();
        audioSource.pitch = pitch + Random.Range(-variation, variation);
        audioSource.PlayOneShot(clip, blipVolume * volume);
    }

    private AudioClip GetBlip(BlipStyle style)
    {
        // Her stil bir kere üretilir, sonrası önbellekten gelir
        if (!blipClips.TryGetValue(style, out AudioClip clip))
        {
            clip = SubtitleBlipGenerator.Create(style);
            blipClips[style] = clip;
        }
        return clip;
    }

    private IEnumerator Fade(float target)
    {
        float start = canvasGroup.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    private void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Kutu: genişlik sabit, yükseklik içeriğe göre küçülür/büyür
        RectTransform box = CreateRect("Box", transform);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
        box.pivot = new Vector2(0.5f, 1f);
        box.anchoredPosition = new Vector2(0f, -topMargin);
        box.sizeDelta = new Vector2(boxWidth, 0f);
        box.gameObject.AddComponent<Image>().color = boxColor;
        boxRect = box;

        VerticalLayoutGroup layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 18, 18);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = box.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        speakerText = CreateText("Speaker", box, speakerFontSize, FontStyle.Bold);
        bodyText = CreateText("Text", box, fontSize, FontStyle.Normal);
        bodyText.lineSpacing = 1.1f;

        // İsim sadece konuşmacının adı varsa açılır
        speakerText.gameObject.SetActive(false);

        // Yazı boşken kutu çökmesin diye en az bir satır yüksekliği
        bodyText.gameObject.AddComponent<LayoutElement>().minHeight = fontSize * 1.3f;
    }

    private Text CreateText(string name, Transform parent, int size, FontStyle style)
    {
        Text text = CreateRect(name, parent).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.UpperCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.raycastTarget = false;

        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
