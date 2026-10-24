using UnityEngine;

// Kulenin tepesindeki hava saldırısı sireni ve dönen kırmızı ikaz lambası.
// Ses haritanın her yerinden duyulur ama yönü kuleden gelir. Işıklar ve hoparlör birlikte döner:
// sana döndükleri an lamba parlar (gerçek ikaz lambalarındaki dönen yansıtıcı gibi) ve ses kabarır.
[RequireComponent(typeof(AudioSource))]
public class AlarmSiren : MonoBehaviour
{
    [Header("Dönen Işık")]
    public Transform rotor;                  // Spot ışıklarının ve hoparlörün bağlı olduğu dönen parça
    public float rotationSpeed = 120f;       // Derece/sn
    public Renderer lamp;                    // Parlayan kırmızı lamba
    public Color lampColor = new Color(1f, 0.04f, 0.02f);
    public float lampIdleIntensity = 0.3f;   // Arkası dönükken hafif yanar
    public float lampFlashIntensity = 6f;    // Tam sana dönünce; gündüz de bloom ile parlasın diye yüksek
    [Range(1f, 64f)] public float flashSharpness = 12f; // Büyüdükçe parlama daha kısa ve keskin

    [Header("Siren Sesi")]
    public AudioClip sirenSound;
    [Range(0f, 1f)] public float volume = 1f;
    public float hearingDistance = 600f;     // Bu mesafeye kadar ses yavaşça azalır
    [Range(0f, 1f)] public float farVolume = 0.35f;   // Haritanın en uzak yerinde bile en az bu kadar duyulur
    [Range(0f, 1f)] public float facingBoost = 0.3f;  // Hoparlör sana dönünce ses ne kadar kabarır

    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    private AudioSource source;
    private MaterialPropertyBlock block;
    private Transform listener;

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

        if (source.clip != null)
            source.Play();
    }

    void Update()
    {
        if (rotor != null)
            rotor.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

        // Arka arkaya iki ışık/hoparlör var, hangisi bize dönükse o sayılır
        float facing = 0f;
        if (rotor != null && listener != null)
        {
            Vector3 toListener = Vector3.ProjectOnPlane(listener.position - rotor.position, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(rotor.forward, Vector3.up).normalized;
            facing = Mathf.Abs(Vector3.Dot(forward, toListener));
        }

        source.volume = volume * Mathf.Lerp(1f - facingBoost, 1f, facing);

        if (lamp != null)
        {
            float flash = Mathf.Pow(facing, flashSharpness);
            lamp.GetPropertyBlock(block);
            block.SetColor(EmissiveColorId, lampColor * Mathf.Lerp(lampIdleIntensity, lampFlashIntensity, flash));
            lamp.SetPropertyBlock(block);
        }
    }
}
