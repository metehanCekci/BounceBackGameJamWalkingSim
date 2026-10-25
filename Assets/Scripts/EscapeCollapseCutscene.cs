using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// İki (ya da daha fazla) bina aynı anda oyuncunun üstüne devrilir; kamera binalara bakar,
// karakter kaçış noktasına koşar ve son anda ağır çekimde atlayıp kendini atar, arkasında binalar yere çarpar.
// Bu bileşeni iki binanın ortasındaki trigger'a ekle, Buildings'e iki BuildingCollapse'ı sürükle.
[RequireComponent(typeof(Collider))]
public class EscapeCollapseCutscene : MonoBehaviour
{
    [Header("Binalar")]
    public BuildingCollapse[] buildings;   // Fall Towards boşsa bu trigger'a doğru devrilirler

    [Header("Kaçış")]
    public Transform escapePoint;          // Karakterin koşup atlayacağı yer (binaların dışında). Boşsa baktığı yön
    [Range(0.2f, 0.95f)] public float escapeAt = 0.7f; // Devrilmenin yüzde kaçında atlasın (geç = daha gerilimli)
    public float diveSpeed = 8f;           // Atlayışın ileri hızı
    public float diveUp = 4.5f;            // Atlayışın yukarı hızı

    [Header("Ağır Çekim")]
    public bool slowMotion = true;
    [Range(0.1f, 1f)] public float slowScale = 0.35f;
    public float slowDuration = 1.2f;      // Gerçek saniye

    [Header("Işınlanma (boşsa kurtulunca arkaya dönüp yıkıntıya bakar)")]
    public Transform wakeUpPoint;          // Atlayınca ekran kararır, oyuncu buraya ışınlanır, yerde gözlerini açar ve kalkar
    public float fadeOutTime = 0.8f;
    public float blackTime = 1.5f;         // Karanlıkta kalma
    public float lieTime = 2f;             // Gözlerini açtıktan sonra yerde yatma
    public float getUpTime = 1.2f;

    [Header("Kurtulduktan Sonra")]
    public float lookBackTime = 2.5f;      // Arkaya dönüp yıkıntıya bakma süresi
    public SubtitleLine[] lines;           // Her birinde ses ve metin
    public UnityEvent onFinished;

    private bool triggered;
    private float savedFixedDelta = -1f;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        // Binalar kendi trigger'larıyla ayrıca tetiklenmesin, zamanlamayı bu cutscene yönetir
        foreach (BuildingCollapse b in buildings)
        {
            if (b != null)
                b.triggerOnEnter = false;
        }
    }

    void OnDisable()
    {
        RestoreTime();
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null)
            StartCoroutine(Run(player));
    }

    private IEnumerator Run(PlayerMovement player)
    {
        triggered = true;

        PlayerInputManager input = player.inputManager;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        RealisticPlayerCamera cam = FindAnyObjectByType<RealisticPlayerCamera>();

        if (input != null) input.InputLocked = true;
        CinematicBars.Show();

        // Binaları başlat: kamerayı ve ezilmeyi bu cutscene yönetir
        float rumble = 0f, fall = 0f;
        foreach (BuildingCollapse b in buildings)
        {
            if (b == null) continue;
            b.lockCamera = false;
            b.cinematicBars = false;
            b.killPlayer = false;
            if (b.fallTowards == null) b.fallTowards = transform;
            b.Collapse();
            rumble = Mathf.Max(rumble, b.rumbleTime);
            fall = Mathf.Max(fall, b.fallTime);
        }

        // 1. Titrerken kamera sırayla binalara bakar
        int count = Mathf.Max(1, buildings.Length);
        foreach (BuildingCollapse b in buildings)
        {
            if (b == null || b.building == null) continue;
            Bounds bb = BuildingCollapse.GetBounds(b.building);
            Vector3 look = new Vector3(bb.center.x, bb.max.y * 0.8f + bb.min.y * 0.2f, bb.center.z);
            float t = 0f;
            while (t < rumble / count)
            {
                if (cam != null) cam.SetFocus(look);
                t += Time.deltaTime;
                yield return null;
            }
        }

        // 2. Devrilirken kaçış noktasına depar
        Vector3 escape = escapePoint != null
            ? escapePoint.position
            : player.transform.position + player.transform.forward * 10f;
        if (cam != null) cam.SetFocus(escape + Vector3.up);
        if (input != null)
        {
            input.ScriptedMove = new Vector2(0f, 1f);
            input.ScriptedSprint = true;
        }

        float runTime = fall * escapeAt;
        float elapsed = 0f;
        while (elapsed < runTime)
        {
            if (cam != null) cam.SetFocus(escape + Vector3.up);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. Son anda ağır çekimde atlayıp kendini at
        if (slowMotion)
        {
            savedFixedDelta = Time.fixedDeltaTime;
            Time.timeScale = slowScale;
            Time.fixedDeltaTime = savedFixedDelta * slowScale;
        }

        Vector3 dir = escape - player.transform.position;
        dir.y = 0f;
        dir.Normalize();
        rb.linearVelocity = dir * diveSpeed + Vector3.up * diveUp;
        if (cam != null) cam.AddShake(0.3f, 2f);

        // Işınlanma modu: havadayken ekran kararır, oyuncu başka yerde uyanır
        if (wakeUpPoint != null)
        {
            ScreenFader.Instance.FadeOut(fadeOutTime);
            float fadeT = 0f;
            while (fadeT < fadeOutTime)
            {
                if (cam != null) cam.SetFocus(escape);
                fadeT += Time.unscaledDeltaTime;
                yield return null;
            }
            RestoreTime();
            yield return WakeUp(player, input, cam);
            yield break;
        }

        float real = 0f;
        while (real < slowDuration)
        {
            if (cam != null) cam.SetFocus(escape);
            real += Time.unscaledDeltaTime;
            yield return null;
        }
        RestoreTime();

        // Yere inene kadar bekle (en fazla 2 sn)
        float wait = 0f;
        while (!player.IsGrounded && wait < 2f)
        {
            wait += Time.deltaTime;
            yield return null;
        }
        if (input != null)
        {
            input.ScriptedMove = null;
            input.ScriptedSprint = false;
        }

        // 4. Arkaya dönüp yıkıntıya bak
        Vector3 ruins = Vector3.zero;
        int n = 0;
        foreach (BuildingCollapse b in buildings)
        {
            if (b == null || b.building == null) continue;
            ruins += BuildingCollapse.GetBounds(b.building).center;
            n++;
        }
        ruins = n > 0 ? ruins / n : transform.position;

        if (lines != null && lines.Length > 0)
            Subtitles.Say(lines);

        float look2 = 0f;
        while (look2 < lookBackTime || Subtitles.IsPlaying)
        {
            if (cam != null) cam.SetFocus(ruins);
            look2 += Time.deltaTime;
            yield return null;
        }

        if (cam != null) cam.ClearFocus();
        if (input != null) input.InputLocked = false;
        CinematicBars.Hide();
        onFinished.Invoke();
    }

    private IEnumerator WakeUp(PlayerMovement player, PlayerInputManager input, RealisticPlayerCamera cam)
    {
        if (input != null)
        {
            input.ScriptedMove = null;
            input.ScriptedSprint = false;
        }

        // Karanlıkta: yeni yere ışınla, yerde sırtüstü yatar halde başla
        float wakeYaw = wakeUpPoint.eulerAngles.y;
        player.Teleport(wakeUpPoint.position, Quaternion.Euler(0f, wakeYaw, 0f));
        if (cam != null)
        {
            cam.ClearFocus();
            cam.SnapLook(wakeYaw, 0f);
            cam.SetKnockdown(1f);
        }

        yield return new WaitForSecondsRealtime(blackTime);

        // Gözlerini açar: yavaşça aralanır, bir kez kırpar, sonra tam açılır
        ScreenFader.Instance.FadeIn(1.5f);
        yield return new WaitForSecondsRealtime(1.1f);
        ScreenFader.Instance.FadeOut(0.12f);
        yield return new WaitForSecondsRealtime(0.18f);
        ScreenFader.Instance.FadeIn(0.8f);

        if (lines != null && lines.Length > 0)
            Subtitles.Say(lines);

        yield return new WaitForSeconds(lieTime);

        // Yavaşça kalk
        float t = 0f;
        while (t < getUpTime)
        {
            t += Time.deltaTime;
            if (cam != null) cam.SetKnockdown(1f - Mathf.SmoothStep(0f, 1f, t / getUpTime));
            yield return null;
        }
        if (cam != null) cam.SetKnockdown(0f);

        if (input != null) input.InputLocked = false;
        CinematicBars.Hide();
        onFinished.Invoke();
    }

    private void RestoreTime()
    {
        if (savedFixedDelta < 0f) return;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = savedFixedDelta;
        savedFixedDelta = -1f;
    }
}
