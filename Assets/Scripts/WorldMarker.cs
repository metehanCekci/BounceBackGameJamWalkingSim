using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Dünyadaki bir noktayı ekranda gösteren işaret; duvar arkasından ve uzaktan da görünür.
// Çuvallar aranabilirken kendilerine ekler, görev bitince çıkış (BoundaryTrigger) da işaretlenir.
// pointWhenOffscreen açıksa hedef ekran dışındayken ekran kenarında ok olarak durur.
public class WorldMarker : MonoBehaviour
{
    public static readonly List<WorldMarker> All = new List<WorldMarker>();

    public string label;                              // Boşsa sadece işaret
    public Color color = new Color(1f, 0.8f, 0.1f);
    public float size = 16f;                          // Piksel (1080p ekrana göre)
    public bool showDistance;
    public bool pointWhenOffscreen;
    public float hideWithin = 3f;                     // Bu kadar yakınken gizlenir, yakında obje zaten görünüyor
    public Vector3 worldOffset;

    // Doluysa hedef nokta bundan alınır (ör. modelin tepesi), yoksa objenin kendisi
    public System.Func<Vector3> pointProvider;

    public Vector3 Point => (pointProvider != null ? pointProvider() : transform.position) + worldOffset;

    void OnEnable()
    {
        All.Add(this);
        WorldMarkerHud.EnsureExists();
    }

    void OnDisable()
    {
        All.Remove(this);
    }
}

// Bütün WorldMarker'ları çizen ekran katmanı. Sahneye koymak gerekmez, ilk işaret açılınca kendini kurar.
public class WorldMarkerHud : MonoBehaviour
{
    private const float EdgeMargin = 70f;   // Ekran kenarına yapışan okun kenardan uzaklığı
    private const float FadeDistance = 1.5f;

    private static WorldMarkerHud instance;

    private class View
    {
        public RectTransform root;
        public RectTransform diamond;
        public Image diamondFill;
        public Image diamondBack;
        public RectTransform arrow;
        public Image[] arrowBars;
        public Text text;
    }

    private readonly List<View> views = new List<View>();
    private RectTransform canvasRect;
    private Font font;

    public static void EnsureExists()
    {
        if (instance == null)
            new GameObject("WorldMarkerHud").AddComponent<WorldMarkerHud>();
    }

    void Awake()
    {
        instance = this;

        // Ekran kararma perdesinin (90) ve altyazının altında kalır
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = (RectTransform)transform;

        font = Resources.Load<Font>("SubtitleFont");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        int used = 0;

        if (cam != null)
        {
            foreach (WorldMarker marker in WorldMarker.All)
            {
                if (Draw(cam, marker, used))
                    used++;
            }
        }

        for (int i = used; i < views.Count; i++)
            views[i].root.gameObject.SetActive(false);
    }

    private bool Draw(Camera cam, WorldMarker marker, int index)
    {
        Vector3 point = marker.Point;
        float distance = Vector3.Distance(cam.transform.position, point);
        if (distance < marker.hideWithin)
            return false;

        Vector3 screen = cam.WorldToScreenPoint(point);
        bool behind = screen.z < 0f;
        bool onScreen = !behind && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
        if (!onScreen && !marker.pointWhenOffscreen)
            return false;

        Vector2 position = screen;
        float angle = 0f;
        if (!onScreen)
        {
            // Ekran kenarına yapıştır; hedef arkadaysa yön ters çıkar, çeviriyoruz
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 dir = (Vector2)screen - center;
            if (behind)
                dir = -dir;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector2.down;

            float margin = EdgeMargin * Screen.height / 1080f;
            float sx = (center.x - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.x));
            float sy = (center.y - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
            position = center + dir * Mathf.Min(sx, sy);
            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        View view = GetView(index);
        view.root.gameObject.SetActive(true);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, position, null, out Vector2 local);
        view.root.anchoredPosition = local;

        float alpha = Mathf.Clamp01((distance - marker.hideWithin) / FadeDistance);
        Color color = marker.color;
        color.a *= alpha;

        float s = marker.size;
        view.diamond.sizeDelta = new Vector2(s, s) * 0.72f;
        view.diamondBack.rectTransform.sizeDelta = new Vector2(s, s) * 0.72f + new Vector2(6f, 6f);
        view.diamondFill.color = color;
        view.diamondBack.color = new Color(0f, 0f, 0f, 0.55f * alpha);

        view.arrow.gameObject.SetActive(!onScreen);
        if (!onScreen)
        {
            view.arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            view.arrow.anchoredPosition = Vector2.zero;
            foreach (Image bar in view.arrowBars)
                bar.color = color;
        }

        string text = marker.label;
        if (marker.showDistance)
            text = string.IsNullOrEmpty(text) ? $"{distance:0} m" : $"{text}\n{distance:0} m";
        view.text.text = text;
        view.text.color = new Color(1f, 1f, 1f, alpha);
        view.text.rectTransform.anchoredPosition = new Vector2(0f, -(s * 0.5f + 22f));
        return true;
    }

    private View GetView(int index)
    {
        while (views.Count <= index)
            views.Add(CreateView());
        return views[index];
    }

    private View CreateView()
    {
        var view = new View();
        view.root = CreateRect("Marker", transform);

        // Okunurluk için koyu kenarlı elmas: açık gökyüzünde de karanlıkta da seçilsin
        view.diamondBack = CreateRect("Back", view.root).gameObject.AddComponent<Image>();
        view.diamondBack.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        view.diamond = CreateRect("Diamond", view.root);
        view.diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        view.diamondFill = view.diamond.gameObject.AddComponent<Image>();

        // Ekran dışı yönü gösteren ok: iki çubuktan ">" şekli, uç +X'e bakar
        view.arrow = CreateRect("Arrow", view.root);
        view.arrowBars = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            RectTransform bar = CreateRect("Bar", view.arrow);
            bar.sizeDelta = new Vector2(18f, 4f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.anchoredPosition = new Vector2(30f, 0f);
            bar.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 40f : -40f);
            view.arrowBars[i] = bar.gameObject.AddComponent<Image>();
        }

        RectTransform textRect = CreateRect("Label", view.root);
        textRect.sizeDelta = new Vector2(300f, 60f);
        textRect.pivot = new Vector2(0.5f, 1f);
        view.text = textRect.gameObject.AddComponent<Text>();
        view.text.font = font;
        view.text.fontSize = 24;
        view.text.alignment = TextAnchor.UpperCenter;
        view.text.horizontalOverflow = HorizontalWrapMode.Overflow;
        view.text.raycastTarget = false;
        view.text.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);

        foreach (Graphic g in view.root.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;
        return view;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        return rect;
    }
}
