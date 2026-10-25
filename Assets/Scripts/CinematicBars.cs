using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Üstten ve alttan yavaşça kayan sinematik siyah bantlar. Sahneye eklemek gerekmez, kendini oluşturur.
// CinematicBars.Show() / CinematicBars.Hide(). Birden fazla yer aynı anda isteyebilir, hepsi bırakınca kapanır.
public class CinematicBars : MonoBehaviour
{
    public static float BarHeight = 0.12f;   // Ekran yüksekliğinin oranı (her bant)
    public static float SlideTime = 0.6f;

    private static CinematicBars instance;

    private int requests;
    private float current;
    private float baseLevel;   // İstek yokken bantların durduğu seviye (0 = kapalı, 1 = tam)
    private RectTransform top;
    private RectTransform bottom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("CinematicBars");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CinematicBars>();
        instance.Build();

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance == null) return;
        instance.requests = 0;
        instance.current = 0f;
        instance.baseLevel = 0f;
        instance.Apply();
    }

    // Bantların varacağı yükseklik (ekran oranı). Altyazı kutusu kayma bitmeden yerini buna göre alır.
    public static float TargetHeight
    {
        get
        {
            if (instance == null) return 0f;
            return BarHeight * (instance.requests > 0 ? 1f : instance.baseLevel);
        }
    }

    public static void Show()
    {
        if (instance != null)
            instance.requests++;
    }

    // Kaymadan direkt tam açık gelir (ör. sahne siyahtan açılırken bantlar zaten orada olsun)
    public static void ShowInstant()
    {
        if (instance == null) return;
        instance.requests++;
        instance.current = 1f;
        instance.Apply();
    }

    // Bantlar Show/Hide istekleri dışında bu seviyede kalır (ör. minigame boyunca yarım bant)
    public static void SetBaseLevel(float level)
    {
        if (instance != null)
            instance.baseLevel = Mathf.Clamp01(level);
    }

    public static void Hide()
    {
        if (instance != null)
            instance.requests = Mathf.Max(0, instance.requests - 1);
    }

    void Update()
    {
        float target = requests > 0 ? 1f : baseLevel;
        if (Mathf.Approximately(current, target)) return;

        current = Mathf.MoveTowards(current, target, Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) / SlideTime);
        Apply();
    }

    private void Apply()
    {
        float h = BarHeight * Mathf.SmoothStep(0f, 1f, current);
        top.anchorMin = new Vector2(0f, 1f - h);
        top.anchorMax = Vector2.one;
        bottom.anchorMin = Vector2.zero;
        bottom.anchorMax = new Vector2(1f, h);
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95; // Altyazının (100) altında: yazılar bantın üstünde okunur

        top = CreateBar("Top");
        bottom = CreateBar("Bottom");
        Apply();
    }

    private RectTransform CreateBar(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return rect;
    }
}
