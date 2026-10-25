using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;

// Kulenin tepesindeki hava saldırısı sireni ve dönen ikaz ışıkları (ön yüzü kırmızı, arkası beyaz).
// Ses haritanın her yerinden duyulur ama yönü kuleden gelir. Işıklardan biri sana döndükçe lamba parlar,
// ses kabarır ve bütün ekran o ışığın rengine boyanır: siren baskısı haritanın her yerinde hissedilsin.
[RequireComponent(typeof(AudioSource))]
public class AlarmSiren : MonoBehaviour
{
    [Header("Başlangıç")]
    public bool startActive = true;          // Kapalıysa siren sessiz bekler, Activate() ile başlar (ör. cutscene)

    [Header("Dönen Işık")]
    public Transform rotor;                  // Spot ışıklarının ve hoparlörün bağlı olduğu dönen parça
    public float rotationSpeed = 120f;       // Derece/sn
    public Renderer lamp;                    // Parlayan ikaz lambası
    [FormerlySerializedAs("lampColor")]
    public Color primaryColor = new Color(1f, 0.04f, 0.02f);  // Öne bakan ışık (kırmızı)
    public Color secondaryColor = new Color(1f, 0.95f, 0.9f); // Arkaya bakan ışık (beyaz)
    public float lampIdleIntensity = 0.3f;   // Arkası dönükken hafif yanar
    public float lampFlashIntensity = 6f;    // Tam sana dönünce; gündüz de bloom ile parlasın diye yüksek
    [Range(1f, 64f)] public float flashSharpness = 12f; // Büyüdükçe parlama daha kısa ve keskin

    [Header("Ekran Rengi")]
    public bool tintScreen = true;
    [Range(0f, 1f)] public float screenTintStrength = 0.6f; // Işık sana dönünce ekran o renge ne kadar boyanır
    public float secondaryFlashExposure = 0.4f;             // Beyaz ışık dönünce ekran bu kadar EV aydınlanır
    [Range(1f, 16f)] public float tintSharpness = 3f;       // Lambadan geniş tutulur: ışık üstünden geçiyormuş gibi

    [Header("Siren Sesi")]
    public AudioClip sirenSound;
    [Range(0f, 1f)] public float volume = 1f;
    public float hearingDistance = 600f;     // Bu mesafeye kadar ses yavaşça azalır
    [Range(0f, 1f)] public float farVolume = 0.35f;   // Haritanın en uzak yerinde bile en az bu kadar duyulur
    [Range(0f, 1f)] public float facingBoost = 0.3f;  // Hoparlör sana dönünce ses ne kadar kabarır

    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public bool IsActive { get; private set; }

    private AudioSource source;
    private MaterialPropertyBlock block;
    private Transform listener;

    // Her ışık rengi için ayrı global Volume; ağırlığı ışığın bize ne kadar döndüğü
    private Volume primaryTint;
    private Volume secondaryTint;
    private ColorAdjustments primaryAdjustments;
    private ColorAdjustments secondaryAdjustments;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        if (sirenSound != null)
            source.clip = sirenSound;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f; // Koşarken siren perdesi kaymasın
        source.priority = 0;      // Çok ses çalarken bile kesilmesin

        // Yakında tam ses, uzaklaştıkça azalır ama farVolume'un altına hiç inmez
        source.rolloffMode = AudioRolloffMode.Custom;
        source.maxDistance = hearingDistance;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.2f, Mathf.Lerp(farVolume, 1f, 0.45f)),
            new Keyframe(1f, farVolume)));

        block = new MaterialPropertyBlock();
    }

    void Start()
    {
        AudioListener ear = FindAnyObjectByType<AudioListener>();
        listener = ear != null ? ear.transform : null;

        // Rotor'un önüne bakan ışıklar ana renk, arkasına bakanlar ikinci renk
        if (rotor != null)
        {
            foreach (Light spot in rotor.GetComponentsInChildren<Light>())
                spot.color = Vector3.Dot(spot.transform.forward, rotor.forward) >= 0f ? primaryColor : secondaryColor;
        }

        if (tintScreen)
        {
            primaryTint = CreateTintVolume("SirenTint_Primary", false, out primaryAdjustments);
            secondaryTint = CreateTintVolume("SirenTint_Secondary", true, out secondaryAdjustments);
        }

        // Siren başlamadan da lamba kendi renginde dursun: ortak malzemenin kırmızısı yeşil sirende görünmesin
        SetLamp(primaryColor * lampIdleIntensity);

        if (startActive)
            Activate();
    }

    // Sireni, dönen ışıkları ve ekran rengini başlatır
    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        if (source.clip != null)
            source.Play();
    }

    void OnDestroy()
    {
        // Çalışırken oluşturulan profiller sahneyle birlikte silinmez
        if (primaryTint != null)
            Destroy(primaryTint.sharedProfile);
        if (secondaryTint != null)
            Destroy(secondaryTint.sharedProfile);
    }

    void Update()
    {
        if (!IsActive) return;

        if (rotor != null)
            rotor.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

        // Ön ışık mı arka ışık mı bize dönük, ne kadar?
        float front = 0f;
        float back = 0f;
        if (rotor != null && listener != null)
        {
            Vector3 toListener = Vector3.ProjectOnPlane(listener.position - rotor.position, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(rotor.forward, Vector3.up).normalized;
            float alignment = Vector3.Dot(forward, toListener);
            front = Mathf.Max(alignment, 0f);
            back = Mathf.Max(-alignment, 0f);
        }

        source.volume = volume * Mathf.Lerp(1f - facingBoost, 1f, Mathf.Max(front, back));

        if (lamp != null)
        {
            float frontFlash = Mathf.Pow(front, flashSharpness);
            float backFlash = Mathf.Pow(back, flashSharpness);
            SetLamp(primaryColor * Mathf.Lerp(lampIdleIntensity, lampFlashIntensity, frontFlash)
                + secondaryColor * (lampFlashIntensity * backFlash));
        }

        if (primaryTint != null)
        {
            // Değerler her kare yazılır ki Play modunda Inspector'dan canlı ayarlanabilsin
            primaryAdjustments.colorFilter.value = Color.Lerp(Color.white, primaryColor, screenTintStrength);
            secondaryAdjustments.colorFilter.value = Color.Lerp(Color.white, secondaryColor, screenTintStrength);
            secondaryAdjustments.postExposure.value = secondaryFlashExposure;
            primaryTint.weight = Mathf.Pow(front, tintSharpness);
            secondaryTint.weight = Mathf.Pow(back, tintSharpness);
        }
    }

    private void SetLamp(Color emission)
    {
        if (lamp == null) return;
        lamp.GetPropertyBlock(block);
        block.SetColor(BaseColorId, primaryColor);
        block.SetColor(EmissiveColorId, emission);
        lamp.SetPropertyBlock(block);
    }

    private Volume CreateTintVolume(string name, bool brighten, out ColorAdjustments adjustments)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        Volume tint = go.AddComponent<Volume>();
        tint.isGlobal = true;
        tint.priority = 50f; // Sahnenin ışık Volume'unun üstünde uygulanır
        tint.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        adjustments = profile.Add<ColorAdjustments>();
        adjustments.colorFilter.overrideState = true;
        // Sadece beyaz ışık ekranı aydınlatır; kırmızı sadece boyar, sahnenin pozlamasına dokunmaz
        adjustments.postExposure.overrideState = brighten;
        tint.sharedProfile = profile;
        return tint;
    }
}
