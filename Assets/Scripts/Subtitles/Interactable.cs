using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[Serializable]
public class InteractOutcome
{
    public string label = "Sonuç";
    [Min(0f)] public float weight = 1f;   // Yüksek olan daha sık çıkar
    public SubtitleLine[] lines;
    public UnityEvent onPicked = new UnityEvent();

    // Ağırlığa göre rastgele bir sonuç seçer
    public static InteractOutcome Pick(InteractOutcome[] list)
    {
        if (list == null || list.Length == 0)
            return null;

        float total = 0f;
        foreach (InteractOutcome o in list)
            total += o.weight;

        if (total <= 0f)
            return list[0];

        float roll = UnityEngine.Random.Range(0f, total);
        foreach (InteractOutcome o in list)
        {
            roll -= o.weight;
            if (roll <= 0f)
                return o;
        }
        return list[list.Length - 1];
    }
}

// Etkileşim objesi. Oyuncu objeye bakıp E'yi basılı tutunca (PlayerInteractor) bar dolar,
// dolunca sonuç seçilir ve altyazılar oynar. Collider gerekmez, hedefleme modelin boyutuna göre yapılır.
public class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    [Header("Etkileşim")]
    [FormerlySerializedAs("searchTime")]
    public float holdTime = 1.5f;          // E'nin basılı tutulma süresi
    public string prompt = "Search";       // Barın altında yazan kelime
    public bool oneTime = false;
    public SearchSequence group;           // Doluysa sonuç grup tarafından belirlenir

    [Header("Vurgu (Outline)")]
    public bool highlight = true;          // Aranabilirken sarı kenar çizgisi
    public Color highlightColor = new Color(1f, 0.8f, 0.1f);
    public float outlineThickness = 0.02f; // Metre

    [Header("Basılı Tutmaya Başlayınca (ilk sefer)")]
    public SubtitleLine[] searchLines;

    [Header("Sonuçlar (ağırlığa göre rastgele)")]
    public InteractOutcome[] outcomes;

    [Header("Olaylar")]
    public UnityEvent onStarted;
    public UnityEvent onFinished;

    private bool used;
    private bool searchLinesPlayed;
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

    public void BeginHold()
    {
        if (!searchLinesPlayed && searchLines != null && searchLines.Length > 0)
        {
            Subtitles.Say(searchLines);
            searchLinesPlayed = true;
        }
        onStarted.Invoke();
    }

    public void Complete()
    {
        InteractOutcome picked = group != null ? group.Register(this) : InteractOutcome.Pick(outcomes);
        if (picked != null)
        {
            if (picked.lines != null && picked.lines.Length > 0)
                Subtitles.Say(picked.lines);
            picked.onPicked?.Invoke();
        }

        used = true;
        onFinished.Invoke();
    }
}
