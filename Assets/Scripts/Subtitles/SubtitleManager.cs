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

    [Header("Sinematik Bantlar")]
    public float barBoxWidth = 1700f;        // Bantlar açıkken kutu genişler, yazı daha az satıra kayar
    public int minBarFontSize = 26;          // Yazı üst banta sığmazsa bu boyuta kadar küçülür

    [Header("Yazma Hızı")]
    public float charactersPerSecond = 30f;
    public float commaPause = 0.12f;
    public float sentencePause = 0.35f;
    [Range(0.1f, 1f)] public float dramaticSpeed = 0.6f;   // Başında ~ olan satırların hızı (Celeste gibi yavaş)
    [Range(0.5f, 1f)] public float dramaticPitch = 0.88f;  // Dramatik satırlarda bip sesi biraz pesleşir

    [Header("Ekranda Kalma")]
    public float holdTimeBase = 0.5f;      // Yazı bitince sonraki satıra geçmeden önce bekleme
    public float holdTimePerChar = 0f;     // Uzun satırlara eklenen ek bekleme (harf başına)
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
    private float blipPitchMultiplier = 1f;

    private CanvasGroup canvasGroup;
    private Text speakerText;
    private Text bodyText;
    private RectTransform boxRect;
    private RectTransform canvasRect;
    private VerticalLayoutGroup layout;
    private LayoutElement bodyMinHeight;
    private AudioSource audioSource;

    private string fullText = string.Empty;  // Yazılmakta olan satırın tamamı (kutu buna göre ölçülür)
    private float fittedBar = -1f;

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

    void LateUpdate()
    {
        // Bantlar açılıp kapanınca kutu yeni yerine geçsin
        if (!Mathf.Approximately(BarUnits(), fittedBar))
            FitToBars();
    }

    // Üst bandın varacağı yükseklik, canvas biriminde
    private float BarUnits()
    {
        float canvasHeight = canvasRect.rect.height > 0f ? canvasRect.rect.height : 1080f;
        return CinematicBars.TargetHeight * canvasHeight;
    }

    // Bantlar açıkken kutu üst bandın içinde ortalanır; satırın tamamı banta sığana kadar yazı küçülür.
    // Satırın tamamına göre ölçülür ki yazı ikinci satıra geçince kutu bandın altına taşmasın.
    private void FitToBars()
    {
        float bar = BarUnits();
        fittedBar = bar;
        bool inBars = bar > 1f;

        float canvasWidth = canvasRect.rect.width > 0f ? canvasRect.rect.width : 1920f;
        float width = inBars ? Mathf.Min(barBoxWidth, canvasWidth - 80f) : boxWidth;
        boxRect.sizeDelta = new Vector2(width, 0f);
        layout.padding = inBars ? new RectOffset(32, 32, 8, 8) : new RectOffset(32, 32, 18, 18);

        int size = fontSize;
        float height = MeasureBox(size, width);
        if (inBars)
        {
            while (height > bar && size > minBarFontSize)
            {
                size -= 2;
                height = MeasureBox(size, width);
            }
        }
        bodyText.fontSize = size;
        bodyMinHeight.minHeight = size * 1.3f;

        float y = inBars ? Mathf.Max(0f, (bar - height) * 0.5f) : topMargin;
        boxRect.anchoredPosition = new Vector2(0f, -y);
        RefreshLayout();
    }

    // Kutunun satırın tamamı yazıldığında alacağı yükseklik
    private float MeasureBox(int size, float width)
    {
        TextGenerationSettings settings = bodyText.GetGenerationSettings(new Vector2(width - layout.padding.horizontal, 0f));
        settings.fontSize = size;
        float textHeight = bodyText.cachedTextGeneratorForLayout.GetPreferredHeight(fullText, settings) / bodyText.pixelsPerUnit;

        float height = Mathf.Max(size * 1.3f, textHeight) + layout.padding.vertical;
        if (speakerText.gameObject.activeSelf)
            height += speakerText.preferredHeight + layout.spacing;
        return height;
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
        ClearText();
    }

    private IEnumerator PlayQueue()
    {
        // Kutu kapanırken yeni satır gelirse tekrar açılıp onu da oynat
        do
        {
            // Önceki konuşmanın yazısı kutu açılırken bir anlığına görünmesin
            ClearText();
            yield return Fade(1f);

            while (queue.Count > 0)
                yield return PlayLine(queue.Dequeue());

            yield return Fade(0f);
        }
        while (queue.Count > 0);

        ClearText();
        runner = null;
        OnQueueFinished?.Invoke();
    }

    private void ClearText()
    {
        bodyText.text = string.Empty;
        speakerText.gameObject.SetActive(false);
        RefreshLayout();
    }

    private IEnumerator PlayLine(SubtitleLine line)
    {
        SubtitleVoice voice = line.voice != null ? line.voice : defaultVoice;
        string text = line.text;

        // "~" ile başlayan satırlar dramatik: harfler ve sesler çok daha yavaş, biraz daha pes gelir
        float lineSpeed = line.speedMultiplier > 0f ? line.speedMultiplier : 1f;
        blipPitchMultiplier = 1f;
        if (text.StartsWith("~"))
        {
            text = text.Substring(1).TrimStart();
            lineSpeed *= dramaticSpeed;
            blipPitchMultiplier = dramaticPitch;
        }

        bool hasName = voice != null && !string.IsNullOrEmpty(voice.speakerName);
        speakerText.gameObject.SetActive(hasName);
        if (hasName)
        {
            speakerText.text = voice.speakerName;
            speakerText.color = voice.nameColor;
        }
        bodyText.color = voice != null ? voice.textColor : Color.white;
        fullText = text;
        FitToBars();

        float speed = charactersPerSecond * (voice != null ? voice.speedMultiplier : 1f) * lineSpeed;
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
        audioSource.pitch = (pitch + Random.Range(-variation, variation)) * blipPitchMultiplier;
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
        canvasRect = (RectTransform)transform;

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

        layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
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
        bodyMinHeight = bodyText.gameObject.AddComponent<LayoutElement>();
        bodyMinHeight.minHeight = fontSize * 1.3f;
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
