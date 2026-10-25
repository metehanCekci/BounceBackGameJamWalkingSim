using System.Linq;
using UnityEngine;

// ParkourMap'in yol rehberi: sahne verisine dokunmadan oyun başlarken kurulur (SirenCutscene.Awake çağırır),
// sahne editörde tekrar kaydedilse de kaybolmaz. Oyuncunun en arkasındaki siren kırmızı alarm kalır ve siren sesi
// ondan gelir; bunker'a en yakını ve yolun en önündeki sessiz, yeşil yol gösterici olur. Giriş cutscene'i önce kırmızıya,
// sonra yeşile baktırır; ekranda önce yeşil siren, bina çöküşünden uyanınca bunker işaretlenir (PathGuide).
// Sahnede oyuncu, bunker (SceneTransitionTrigger) ve en az 3 siren yoksa hiçbir şey yapmaz.
public static class ParkourGuide
{
    static readonly Color GuidePrimary = new Color(0.03f, 0.5f, 0.1f);
    static readonly Color GuideSecondary = new Color(0.1f, 0.7f, 0.2f);
    const float GuideIdleIntensity = 3f; // Kırmızıdan parlak: ışık arkası dönükken de uzaktan seçilsin

    public static void Apply(SirenCutscene intro)
    {
        PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>();
        SceneTransitionTrigger bunker = Object.FindAnyObjectByType<SceneTransitionTrigger>();
        AlarmSiren[] sirens = Object.FindObjectsByType<AlarmSiren>();
        if (player == null || bunker == null || sirens.Length < 3) return;

        Vector3 start = player.transform.position;
        Vector3 forward = player.transform.forward;
        Collider bunkerArea = bunker.GetComponent<Collider>();
        Vector3 bunkerCenter = bunkerArea != null ? bunkerArea.bounds.center : bunker.transform.position;
        float Ahead(AlarmSiren s) => Vector3.Dot(s.transform.position - start, forward);

        AlarmSiren alarm = sirens.OrderBy(Ahead).First();
        AlarmSiren bunkerSiren = sirens.Where(s => s != alarm)
            .OrderBy(s => (s.transform.position - bunkerCenter).sqrMagnitude).First();
        AlarmSiren roadSiren = sirens.Where(s => s != alarm && s != bunkerSiren)
            .OrderByDescending(Ahead).First();

        // Karakter sese dönüp kırmızıya bakıyor: alarm sesi oradan gelsin.
        // AlarmSiren.Awake bizden önce çalışmış olabilir, kaynağın kendisi de değiştirilir
        AudioClip clip = alarm.sirenSound != null
            ? alarm.sirenSound
            : sirens.Select(s => s.sirenSound).FirstOrDefault(c => c != null);
        if (clip != null)
        {
            alarm.sirenSound = clip;
            alarm.GetComponent<AudioSource>().clip = clip;
        }
        MakeGuide(roadSiren);
        MakeGuide(bunkerSiren);

        SubtitleVoice voice = intro.lines != null && intro.lines.Length > 0 ? intro.lines[0].voice : null;
        intro.focusTarget = alarm.transform;
        intro.guideTarget = roadSiren.transform;
        if (intro.guideLines == null || intro.guideLines.Length == 0)
            intro.guideLines = new[] { new SubtitleLine("Sığınağa koşmalıyım!", voice) };

        // Cutscene'in altına konur ki sahne yüklenirken doğru sahnede oluşsun
        var go = new GameObject("PathGuide");
        go.transform.SetParent(intro.transform, false);
        PathGuide guide = go.AddComponent<PathGuide>();
        guide.steps = new[]
        {
            new PathGuide.Step { target = roadSiren.transform, label = "Yeşil ışığa koş", offset = new Vector3(0f, 2f, 0f) },
            new PathGuide.Step { target = bunker.transform, label = "Sığınak", offset = bunkerCenter - bunker.transform.position },
        };
        intro.onFinished.AddListener(guide.Begin);

        EscapeCollapseCutscene escape = Object.FindAnyObjectByType<EscapeCollapseCutscene>();
        if (escape != null)
        {
            if (escape.lines == null || escape.lines.Length == 0)
                escape.lines = new[] { new SubtitleLine("Az kaldı... Sığınak yeşil ışığın orada!", voice) };
            escape.onFinished.AddListener(guide.Next);
        }
    }

    static void MakeGuide(AlarmSiren siren)
    {
        siren.primaryColor = GuidePrimary;
        siren.secondaryColor = GuideSecondary;
        siren.lampIdleIntensity = GuideIdleIntensity;
        siren.tintScreen = false; // Ekranı sadece kırmızı alarm boyasın: kırmızı tehlike, yeşil yol
        siren.sirenSound = null;
        siren.GetComponent<AudioSource>().clip = null;
    }
}
