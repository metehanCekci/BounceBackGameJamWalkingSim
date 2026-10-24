using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Oyuncunun baktığı etkileşim objesini bulur. E basılı tutulunca ekranın ortasında dairesel bar dolar,
// bu sırada hareket/bakış kilitlenir ve kamera objeye odaklanır. Bar dolunca etkileşim tamamlanır.
// Sahneye eklemek gerekmez: ilk Interactable açıldığında kameraya kendini ekler.
public class PlayerInteractor : MonoBehaviour
{
    public static PlayerInteractor Instance { get; private set; }

    [Header("Hedefleme")]
    public float maxDistance = 2.5f;
    public float maxAngle = 12f;           // Bakış merkezinden bu kadar derece sapma tolere edilir

    [Header("Giriş")]
    public Key interactKey = Key.E;

    [Header("Kilit & Odak")]
    public bool focusOnTarget = true;      // Basılı tutarken kamera objeye kilitlenir

    [Header("Arayüz")]
    public float ringSize = 90f;
    public Color ringColor = Color.white;

    private Camera cam;
    private PlayerInputManager input;
    private RealisticPlayerCamera camController;

    private Interactable target;
    private Interactable holding;
    private float progress;
    private bool waitForRelease;

    private GameObject uiRoot;
    private CanvasGroup uiGroup;
    private Image fillImage;
    private Text promptText;

    // Outline'ın parlaklığı için: şu an bakılan ya da aranan obje
    public Interactable CurrentTarget => holding != null ? holding : target;

    public static void EnsureExists()
    {
        if (Instance != null) return;

        Camera main = Camera.main;
        if (main == null) main = FindAnyObjectByType<Camera>();
        if (main == null) return;

        main.gameObject.AddComponent<PlayerInteractor>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        input = FindAnyObjectByType<PlayerInputManager>();
        camController = GetComponent<RealisticPlayerCamera>();
        if (camController == null) camController = FindAnyObjectByType<RealisticPlayerCamera>();

        BuildUI();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Release(false);
        if (uiRoot != null) Destroy(uiRoot);
        Instance = null;
    }

    void Update()
    {
        bool held = IsHeld();
        if (!held) waitForRelease = false;

        if (holding == null)
        {
            target = FindTarget();
        }

        // Tuş bırakıldıysa ya da obje artık aranamıyorsa bar sıfırlanır
        if (holding != null && (!held || !holding.CanInteract || TooFar(holding)))
            Release(true);

        if (holding == null && target != null && held && !waitForRelease)
            BeginHold(target);

        if (holding != null)
        {
            progress += Time.deltaTime / Mathf.Max(0.05f, holding.holdTime);
            if (focusOnTarget && camController != null)
                camController.SetFocus(holding.FocusBounds.center);

            if (progress >= 1f)
            {
                Interactable done = holding;
                Release(false);
                waitForRelease = true; // Basılı tutmaya devam edince bir sonrakine geçmesin
                done.Complete();
            }
        }

        UpdateUI();
    }

    private bool IsHeld()
    {
        bool key = Keyboard.current != null && Keyboard.current[interactKey].isPressed;
        bool pad = Gamepad.current != null && Gamepad.current.buttonWest.isPressed;
        return key || pad;
    }

    private void BeginHold(Interactable t)
    {
        holding = t;
        progress = 0f;
        if (input != null) input.InputLocked = true;
        AudioManager.StartSearchSound();
    }

    private void Release(bool cancelled)
    {
        if (holding == null) return;

        // Yarıda bırakılınca ses kesilir; bar dolunca sesin sonu çalmaya devam eder
        if (cancelled)
            AudioManager.StopSearchSound();

        holding = null;
        progress = 0f;
        if (input != null) input.InputLocked = false;
        if (camController != null) camController.ClearFocus();
    }

    private bool TooFar(Interactable it)
    {
        if (cam == null) return true;
        Bounds b = it.FocusBounds;
        return Vector3.Distance(cam.transform.position, b.ClosestPoint(cam.transform.position)) > maxDistance + 1f;
    }

    // Kameranın bakış yönüne en yakın, menzildeki ve önü kapalı olmayan objeyi seçer
    private Interactable FindTarget()
    {
        if (cam == null) return null;

        Vector3 origin = cam.transform.position;
        Vector3 forward = cam.transform.forward;
        Interactable best = null;
        float bestAngle = maxAngle;
        float bestDist = float.MaxValue;

        foreach (Interactable it in Interactable.All)
        {
            if (it == null || !it.CanInteract) continue;

            Bounds b = it.FocusBounds;
            float along = Mathf.Clamp(Vector3.Dot(b.center - origin, forward), 0f, maxDistance);
            Vector3 closest = b.ClosestPoint(origin + forward * along);
            Vector3 toClosest = closest - origin;
            float dist = toClosest.magnitude;
            if (dist > maxDistance) continue;

            float angle = dist < 0.001f ? 0f : Vector3.Angle(forward, toClosest);
            if (angle > bestAngle || (Mathf.Approximately(angle, bestAngle) && dist >= bestDist)) continue;
            if (!HasLineOfSight(origin, it, b, dist)) continue;

            best = it;
            bestAngle = angle;
            bestDist = dist;
        }
        return best;
    }

    private bool HasLineOfSight(Vector3 origin, Interactable it, Bounds b, float surfaceDist)
    {
        Vector3 dir = b.center - origin;
        float len = dir.magnitude;
        if (len < 0.001f) return true;

        foreach (RaycastHit hit in Physics.RaycastAll(origin, dir / len, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<Interactable>() == it) continue;
            if (hit.collider.GetComponentInParent<PlayerMovement>() != null) continue;
            if (hit.distance < surfaceDist - 0.05f) return false;
        }
        return true;
    }

    private void UpdateUI()
    {
        Interactable shown = holding != null ? holding : (waitForRelease ? null : target);

        uiGroup.alpha = Mathf.MoveTowards(uiGroup.alpha, shown != null ? 1f : 0f, Time.deltaTime * 8f);
        fillImage.fillAmount = holding != null ? progress : 0f;
        if (shown != null)
            promptText.text = shown.prompt;
    }

    private void BuildUI()
    {
        uiRoot = new GameObject("InteractionUI");

        Canvas canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        uiGroup = uiRoot.AddComponent<CanvasGroup>();
        uiGroup.alpha = 0f;
        uiGroup.interactable = false;
        uiGroup.blocksRaycasts = false;

        Sprite ring = CreateRingSprite(128, 0.8f);
        CreateImage("RingBackground", ring, new Color(ringColor.r, ringColor.g, ringColor.b, 0.25f));

        fillImage = CreateImage("RingFill", ring, ringColor);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
        fillImage.fillOrigin = (int)Image.Origin360.Top;
        fillImage.fillClockwise = true;
        fillImage.fillAmount = 0f;

        Font font = Resources.Load<Font>("SubtitleFont");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Text key = CreateText("Key", font, 30, FontStyle.Bold, Vector2.zero, new Vector2(ringSize, ringSize));
        key.text = "E";
        promptText = CreateText("Prompt", font, 26, FontStyle.Normal, new Vector2(0f, -(ringSize * 0.5f + 24f)), new Vector2(400f, 40f));
    }

    private Image CreateImage(string name, Sprite sprite, Color color)
    {
        RectTransform rect = CreateRect(name);
        rect.sizeDelta = new Vector2(ringSize, ringSize);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text CreateText(string name, Font font, int size, FontStyle style, Vector2 position, Vector2 boxSize)
    {
        RectTransform rect = CreateRect(name);
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize;

        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.color = ringColor;
        text.raycastTarget = false;

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private RectTransform CreateRect(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(uiRoot.transform, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    // Dolum için halka şeklinde sprite (Image'ın Filled modu sprite olmadan çalışmaz)
    private static Sprite CreateRingSprite(int size, float innerRatio)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        float half = size * 0.5f;
        float aa = 1.5f / half;
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float outer = Mathf.Clamp01((1f - d) / aa);
                float inner = Mathf.Clamp01((d - innerRatio) / aa);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Min(outer, inner) * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
