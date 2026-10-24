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
    public float outlineThickness = 0.02f; // Metre

    [Header("Olaylar")]
    public UnityEvent onFinished;

    private bool used;
    private Renderer[] renderers;
    private readonly List<Renderer> outlineRenderers = new List<Renderer>();
    private Material outlineMaterial;

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
        if (outlineMaterial == null) return;

        // Aranabilirken görünür; bakılan obje tam parlak, diğerleri yavaşça nabız gibi atar
        bool show = CanInteract;
        foreach (Renderer r in outlineRenderers)
            r.enabled = show;

        if (show)
        {
            bool targeted = PlayerInteractor.Instance != null && PlayerInteractor.Instance.CurrentTarget == this;
            float pulse = targeted ? 1f : Mathf.Lerp(0.45f, 0.8f, (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f);
            outlineMaterial.SetColor("_UnlitColor", highlightColor * pulse);
        }
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

        outlineMaterial = new Material(shader);
        outlineMaterial.SetColor("_UnlitColor", highlightColor);
        outlineMaterial.SetFloat("_CullMode", (float)CullMode.Front);
        outlineMaterial.SetFloat("_CullModeForward", (float)CullMode.Front);

        foreach (Renderer r in renderers)
        {
            if (!(r is MeshRenderer)) continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Mesh mesh = mf.sharedMesh;
            Bounds mb = mesh.bounds;
            Vector3 lossy = r.transform.lossyScale;
            Vector3 scale = new Vector3(
                1f + 2f * outlineThickness / Mathf.Max(0.05f, Mathf.Abs(mb.size.x * lossy.x)),
                1f + 2f * outlineThickness / Mathf.Max(0.05f, Mathf.Abs(mb.size.y * lossy.y)),
                1f + 2f * outlineThickness / Mathf.Max(0.05f, Mathf.Abs(mb.size.z * lossy.z)));

            var go = new GameObject("Outline");
            go.transform.SetParent(r.transform, false);
            go.transform.localScale = scale;
            go.transform.localPosition = mb.center - Vector3.Scale(mb.center, scale);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            var mats = new Material[mesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = outlineMaterial;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            outlineRenderers.Add(mr);
        }
    }
}
