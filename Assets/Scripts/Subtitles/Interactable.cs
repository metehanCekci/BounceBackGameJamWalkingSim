using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

// Etkileşim objesi. Oyuncu objeye bakıp E'yi basılı tutunca (PlayerInteractor) bar dolar,
// dolunca bu objenin metni altyazı olarak oynar. Collider gerekmez, hedefleme modelin boyutuna göre yapılır.
public class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    [Header("Metin")]
    [TextArea(2, 4)] public string text;   // Aranınca oynayacak metin
    public SubtitleVoice voice;

    [Header("Etkileşim")]
    [FormerlySerializedAs("searchTime")]
    public float holdTime = 1.5f;          // E'nin basılı tutulma süresi
    public string prompt = "Search";       // Barın altında yazan kelime
    public bool oneTime = true;
    public SearchSequence group;           // Doluysa eşyanın çıktığı çuvalda grubun metni oynar

    [Header("Vurgu (Outline)")]
    public bool highlight = true;          // Aranabilirken sarı kenar çizgisi
    public Color highlightColor = new Color(1f, 0.8f, 0.1f);
    public float highlightBrightness = 2f; // Işıktan bağımsız; 1'in üstü bloom ile parlar
    public float outlineThickness = 0.02f; // Metre
    public bool marker = true;             // Yaklaşınca çuvalın üstünde çıkan ekran işareti
    public float markerDistance = 12f;     // Bu mesafeden yakında ve çuval görünürken gösterilir

    [Header("Olaylar")]
    public UnityEvent onFinished;

    private bool used;
    private Renderer[] renderers;
    private readonly List<Outline> outlines = new List<Outline>();
    private Material outlineMaterial;
    private WorldMarker worldMarker;

    private class Outline
    {
        public MeshRenderer renderer;
        public Transform source;   // Kenar çizgisinin kopyalandığı model parçası
        public Bounds meshBounds;
    }

    public bool CanInteract =>
        isActiveAndEnabled && !(oneTime && used) && (group == null || group.CanSearch(this));

    // Kameranın baktığı ve odaklandığı alan: görünen modelin sınırları
    public Bounds FocusBounds
    {
        get
        {
            Bounds b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (any) b.Encapsulate(r.bounds);
                else { b = r.bounds; any = true; }
            }
            if (!any)
                b.size = Vector3.one * 0.5f;
            return b;
        }
    }

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable()
    {
        All.Add(this);
        PlayerInteractor.EnsureExists();
    }

    void Start()
    {
        // Sahne yüklenirken kamera henüz hazır olmayabilir, burada tekrar dene
        PlayerInteractor.EnsureExists();

        if (highlight)
            CreateOutline();

        if (marker)
        {
            worldMarker = gameObject.AddComponent<WorldMarker>();
            worldMarker.color = highlightColor;
            worldMarker.size = 16f;
            worldMarker.hideWithin = 3f; // Yakında kenar çizgisi yeterli
            worldMarker.showWithin = markerDistance;
            worldMarker.requireLineOfSight = true; // Duvar arkasından görünmesin
            worldMarker.pointProvider = () =>
            {
                Bounds b = FocusBounds;
                return new Vector3(b.center.x, b.max.y + 0.35f, b.center.z);
            };
            worldMarker.enabled = false;
        }
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void OnDestroy()
    {
        if (outlineMaterial != null)
            Destroy(outlineMaterial);
    }

    void Update()
    {
        bool show = CanInteract;
        if (worldMarker != null && worldMarker.enabled != show)
            worldMarker.enabled = show;

        if (outlineMaterial == null) return;

        // Aranabilirken görünür; bakılan obje tam parlak, diğerleri yavaşça nabız gibi atar
        foreach (Outline o in outlines)
            o.renderer.enabled = show;
        if (!show) return;

        bool targeted = PlayerInteractor.Instance != null && PlayerInteractor.Instance.CurrentTarget == this;
        float pulse = targeted ? 1f : Mathf.Lerp(0.5f, 0.9f, (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f);
        outlineMaterial.SetColor("_EmissiveColor", highlightColor * (highlightBrightness * pulse));
    }

    public void Complete()
    {
        // Grup eşyayı bu çuvala koyduysa grubun metni, değilse bu çuvalın kendi metni oynar
        bool found = group != null && group.Register(this);
        if (found)
            group.SayFound();
        else if (!string.IsNullOrWhiteSpace(text))
            Subtitles.Say(text, voice);

        used = true;
        onFinished.Invoke();
    }

    // Objenin biraz büyütülmüş, ters yüzlü bir kopyası: arkadan taşan kısmı kenar çizgisi gibi görünür
    private void CreateOutline()
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("Outline: HDRP/Unlit shader bulunamadı, vurgu kapalı.", this);
            return;
        }

        // Unlit renk HDRP'de pozlamayla kararır (gündüz neredeyse siyah çıkıyordu);
        // pozlamadan bağımsız emission kullanıyoruz ki her ışıkta aynı parlaklıkta görünsün
        outlineMaterial = new Material(shader);
        outlineMaterial.SetColor("_UnlitColor", Color.black);
        outlineMaterial.SetColor("_EmissiveColor", highlightColor * highlightBrightness);
        outlineMaterial.SetFloat("_EmissiveExposureWeight", 0f);
        outlineMaterial.SetFloat("_CullMode", (float)CullMode.Front);
        outlineMaterial.SetFloat("_CullModeForward", (float)CullMode.Front);

        foreach (Renderer r in renderers)
        {
            if (!(r is MeshRenderer)) continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Mesh mesh = mf.sharedMesh;
            var go = new GameObject("Outline");
            go.transform.SetParent(r.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            var mats = new Material[mesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = outlineMaterial;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var outline = new Outline { renderer = mr, source = r.transform, meshBounds = mesh.bounds };
            FitOutline(outline, outlineThickness);
            outlines.Add(outline);
        }
    }

    // Kopyayı her yönde "thickness" metre taşacak kadar büyütür, merkezi modelinkiyle aynı kalır
    private static void FitOutline(Outline o, float thickness)
    {
        Bounds mb = o.meshBounds;
        Vector3 lossy = o.source.lossyScale;
        Vector3 scale = new Vector3(
            1f + 2f * thickness / Mathf.Max(0.05f, Mathf.Abs(mb.size.x * lossy.x)),
            1f + 2f * thickness / Mathf.Max(0.05f, Mathf.Abs(mb.size.y * lossy.y)),
            1f + 2f * thickness / Mathf.Max(0.05f, Mathf.Abs(mb.size.z * lossy.z)));

        Transform t = o.renderer.transform;
        t.localScale = scale;
        t.localPosition = mb.center - Vector3.Scale(mb.center, scale);
    }
}
