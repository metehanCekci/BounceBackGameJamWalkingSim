using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

// Asker sahnesi: trigger'a girince karakter birkaç adım atar, arkadan asker bağırır, karakter arkasını döner.
// Sonra minigame başlar: askere yürürken hızlı yaklaşırsan askerin gerginliği artar, dolunca asker bağırır
// ve çocuk korkup geri çekilir. Asker ve çocuk bu sırada konuşur: asker savaş hakkında söylenir, çocuk cevap verir,
// asker telsizden takviye çağırır (hiç gelmezler). Askere belli mesafelere (hikâye durakları) gelince
// hareket kilitlenir, bantlar kapanır ve asker ile çocuk kısaca konuşur. Sonunda çocuk askere sarılır.
[RequireComponent(typeof(Collider))]
public class SoldierStandoff : MonoBehaviour
{
    [Header("Asker")]
    public Transform soldier;
    public SubtitleVoice soldierVoice;     // Bağırma, uyarı, takviye satırlarının sesi (ör. Soldier)
    public SubtitleVoice soldierSadVoice;  // Söylenme ve sakinleşme satırlarının sesi (ör. SoldierSad). Boşsa normal ses
    public float focusHeight = 1.2f;       // Kameranın bakacağı yükseklik, askerin ayağından itibaren (düşük = daha aşağı bakar)
    public bool soldierFacesPlayer = true; // Asker hep çocuğa döner
    public bool spawnBehindPlayer = true;  // Asker başta gizli; bağırmadan hemen önce çocuğun arkasında belirir
    public float spawnDistance = 15f;      // Çocuğun kaç metre arkasında belirsin (minigame'in uzunluğu bu mesafe - Hug Distance)

    [Header("Çocuk")]
    public SubtitleVoice childVoice;       // ör. ChildNormal
    public SubtitleVoice childSadVoice;    // ör. ChildSad. Boşsa normal ses

    [Header("Müzik")]
    public AudioClip music;                // Asker bağırınca başlar (boşsa müzik değişmez)

    [Header("Giriş")]
    public float introWalkTime = 1.5f;     // Trigger'dan sonra birkaç adım
    [Range(0.1f, 1f)] public float walkSpeed = 0.4f;
    public SubtitleLine[] shoutLines;      // Arkadan bağıran asker
    public float turnDelay = 1.2f;         // Asker bağırdıktan sonra arkasını dönmeden önce donup kalma süresi
    public float turnSpeed = 1.8f;         // Arkasını dönme hızı (düşük = daha yavaş, ürkek)
    public SubtitleLine[] afterTurnLines;  // Arkasını döner dönmez asker (boşsa "Ben sana hareket etme demedim mi?!")
    [TextArea] public string afterTurnReply; // Çocuğun cevabı (boşsa varsayılan)

    [Header("Minigame Kamerası")]
    [Range(0f, 1f)] public float minigameBarLevel = 0.5f; // Minigame boyunca bantların yüksekliği (1 = tam sinematik)
    public float trackSpeed = 3f;          // Kameranın askeri takip etme hızı (fare kilitli)
    [Range(0f, 2f)] public float shoutShake = 1f; // Sinirli satırlardaki ani sarsıntının çarpanı (0 = kapalı)

    [Header("Gerginlik")]
    // Asker "yavaşça gel" der: minigame'de karakter zaten yavaş yürür, bu güvenli. Sadece hızlanmak (koşmak) gerer.
    public float minigameWalkSpeed = 1.3f; // Minigame boyunca yürüme hızı (m/s), bitince eski hızına döner
    public float stressMultiplier = 2f;    // Bütün gerginlik artışlarının çarpanı (2 = iki kat hızlı kızar)
    public float walkRate = 0.3f;          // Yavaş yürüyüşten hızlı ama koşmadan yaklaşırken saniyede artış
    public float sprintRate = 1f;          // Koşarken
    public float calmRate = 0.2f;          // Geri çekilirken saniyede azalış
    public float slowSpeed = 2.2f;         // Bu hızın altı "yavaş" sayılır, gerginlik artmaz (en az Minigame Walk Speed x1.3)
    public float sprintSpeed = 3.8f;       // Bu hızın üstü "koşma" sayılır

    [Header("Hikâye Durakları (asker söylenir, çocuk cevap verir; boşlar için varsayılanlar)")]
    public float[] storyDistances = { 10f, 5f }; // Askere bu mesafelere gelince durulur ve konuşulur (büyükten küçüğe)
    public int beatsPerStop = 2;           // Her durakta kaç karşılıklı konuşma
    public SubtitleLine[] mutterLines;     // Asker savaş hakkında söylenir (duraklarda sırayla)
    [TextArea] public string[] mutterReplies; // Aynı sıradaki söylenmeye çocuğun cevabı (boş = cevap yok)

    [Header("Yürürken Telsiz")]
    public float chatterMin = 7f;          // Takviye satırları arası en az bekleme
    public float chatterMax = 12f;

    [Header("Tepkiler (boşlar için varsayılanlar)")]
    public SubtitleLine[] backupLines;     // Telsizden takviye çağırır (hiç gelmezler)
    [TextArea] public string[] backupReplies;
    public SubtitleLine[] warningLines;    // Gerginlik %50'yi geçince
    public SubtitleLine[] dangerLines;     // Gerginlik %80'i geçince
    public SubtitleLine[] stopLines;       // Gerginlik dolunca (çocuk geri çekilir)
    [TextArea] public string[] stopReplies;
    public SubtitleLine[] calmLines;       // Geri çekilip asker sakinleşince
    [TextArea] public string[] calmReplies;
    public SubtitleLine[] idleLines;       // Oyuncu uzun süre kıpırdamazsa asker yavaşça gelmesini söyler
    public float idleNudgeTime = 6f;

    [Header("Dur / Gel Komutları (askerin kafası karışık)")]
    public SubtitleLine[] haltLines;       // Asker aniden durmasını söyler
    [TextArea] public string[] haltReplies;
    public SubtitleLine[] comeLines;       // Biraz sonra tekrar gelmesini söyler
    public float[] haltDistances = { 12f, 7.5f, 4f }; // Askere bu mesafelere gelince "dur" der (büyükten küçüğe)
    public float haltPauseAfterTalk = 0.5f; // "Dur" konuşması bittikten sonra "gel" demeden önce bekleme
    public float disobeyRate = 0.5f;       // "Dur" denince yürümeye devam ederse saniyede gerginlik artışı

    [Header("Yolda Sohbet (askeri dinleyip yavaş gelen oyuncunun ödülü)")]
    public SubtitleLine[] walkTalkLines;   // Asker sorar
    [TextArea] public string[] walkTalkReplies; // Çocuk cevap verir

    [Header("Geri Çekilme")]
    public float retreatTime = 1.5f;
    [Range(0.1f, 1f)] public float retreatSpeed = 0.6f;
    [Range(0f, 1f)] public float stressAfterRetreat = 0.5f;

    [Header("Sarılma")]
    public float hugDistance = 3f;         // Bu kadar yaklaşınca çocuk askere koşar
    public float hugStopDistance = 1f;
    public float fadeOutTime = 1.2f;
    public float blackTime = 1.5f;
    public float fadeInTime = 2f;
    [Tooltip("Build listesindeki sıra. -1: bu sahneden sonraki sahne.")]
    public int nextSceneIndex = ScreenFader.NextScene;

    [Header("Gösterge")]
    public string barLabel = "Askerin gerginliği";
    public bool heartbeat = true;
    public bool vignette = true;

    [Header("Bitince")]
    public UnityEvent onHug;

    public float Stress { get; private set; }

    // ---- Varsayılan satırlar ----

    private static readonly string[] DefaultAfterTurn =
    {
        "Ben sana hareket etme demedim mi?!",
        "Ellerini görebileyim. Şimdi bana doğru gel... Yavaşça. Çok yavaş.",
    };
    private const string DefaultAfterTurnReply = "Tamam... tamam, geliyorum. Lütfen ateş etme...";

    private static readonly string[] DefaultHalt =
    {
        "Dur! Dur orada!",
        "Bekle! Kıpırdama!",
        "Dur... bir saniye dur.",
    };

    private static readonly string[] DefaultHaltReplies =
    {
        "Durdum! Durdum...",
        "",
        "Tamam...",
    };

    private static readonly string[] DefaultCome =
    {
        "Tamam... gel. Yavaşça.",
        "Devam et. Ağır ol.",
        "Gel... ama yavaş.",
    };

    private static readonly string[] DefaultWalkTalk =
    {
        "Kaç yaşındasın sen?",
        "Nereye gidiyordun böyle?",
        "Bu kadar yolu tek başına mı geldin?",
        "Yemek bulabildin mi bari?",
    };

    private static readonly string[] DefaultWalkTalkReplies =
    {
        "Dokuz... sanırım. Bu sene doğum günümü kutlayamadık.",
        "Bilmiyorum... Sadece yürüyordum. Yürüyünce korku biraz azalıyor.",
        "Bir köpek vardı... Bir süre benimle yürüdü. Sonra o da gitti.",
        "Bir MRE buldum... Yarısını sakladım. Sonraya.",
    };

    private static readonly string[] DefaultIdle =
    {
        "Ne bekliyorsun? Gel buraya... yavaşça.",
        "Dikilme orada. Ağır ağır yürü.",
    };

    // Sırayla hikâye: 1. durakta savaşın aldıklarını paylaşırlar, 2. durakta çocuk zarar görmemek için yalvarır,
    // asker yumuşar. Fazla duraklar için devam satırları da var.
    private static readonly string[] DefaultMutter =
    {
        "Bu savaş kimin için... Kimse hatırlamıyor artık.",
        "Benim de senin yaşında bir kardeşim var. Şimdi nerede, bilmiyorum.",
        "~Sana zarar vermek istemiyorum ufaklık. Ama emirler... emirler başka.",
        "Üç gündür uyumadım. Gözlerimi kapatınca hep o sesler.",
        "Emir emirdir diyorlar. Peki ya vicdan?",
    };

    private static readonly string[] DefaultMutterReplies =
    {
        "Ben de bilmiyorum... Kimse bana sormadı.",
        "~Belki o da şu an birini arıyordur... benim gibi. Lütfen... bana zarar verme.",
        "~Ben sadece bir çocuğum. Bir dakikalığına... silahın yokmuş gibi davranabilir misin?",
        "Ben de duyuyorum... her gece. Kulaklarımı kapatıyorum ama yine de duyuyorum.",
        "Vicdan ne demek bilmiyorum... ama bence sen iyi birisin.",
    };

    private static readonly string[] DefaultBackup =
    {
        "Telsizden haber verdim. Birazdan burada olurlar.",
        "Takviye yolda. Kıpırdama, onlar ne yapacağını bilir.",
        "Merkez, burada bir çocuk var! Tekrar ediyorum, bir çocuk var!",
        "Neredeler... Çoktan gelmeleri gerekirdi.",
    };

    private static readonly string[] DefaultBackupReplies =
    {
        "Lütfen... kimseyi çağırma. Çok korkuyorum.",
        "Sana bir şey yapmayacağım, söz veriyorum...",
        "Ben sadece bir çocuğum... lütfen.",
        "",
    };

    private static readonly string[] DefaultWarning =
    {
        "Yavaş ol!",
        "Yavaş dedim! Ağır ağır gel!",
        "Daha yavaş!",
    };

    private static readonly string[] DefaultDanger =
    {
        "Bir adım daha hızlı atarsan... Son kez söylüyorum, YAVAŞ!",
        "Dur! Yavaş gel demedim mi?!",
    };

    private static readonly string[] DefaultStop =
    {
        "GERİ ÇEKİL! Yavaş gel dedim!",
        "Geri! Baştan... ve bu sefer yavaşça!",
    };

    private static readonly string[] DefaultStopReplies =
    {
        "Tamam! Tamam, özür dilerim!",
        "Lütfen bağırma... korkuyorum...",
    };

    private static readonly string[] DefaultCalm =
    {
        "Tamam... Şimdi tekrar. Yavaşça gel.",
        "İşte böyle. Sakin ol, ağır ol.",
    };

    private static readonly string[] DefaultCalmReplies =
    {
        "Sakinim... bak, sakinim.",
        "Teşekkür ederim...",
    };


    // ---- Durum ----

    private bool triggered;
    private PlayerMovement player;
    private PlayerInputManager input;
    private Rigidbody rb;
    private RealisticPlayerCamera cam;

    private GameObject uiRoot;
    private CanvasGroup uiGroup;
    private Image barFill;
    private AudioSource heartSource;
    private Volume vignetteVolume;
    private Vignette vignetteOverride;

    private int mutterIndex, backupIndex, warningIndex, dangerIndex, stopIndex, calmIndex, idleIndex;
    private int haltIndex, comeIndex, walkTalkIndex;
    private float idleTimer;
    private float lastApproach;
    private bool halting;
    private float haltUntil;
    private int nextHaltIndex;
    private int nextStop;
    private float nextChatter;
    private float savedWalkSpeed = -1f;
    private bool warned, dangered, wasTense;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        // Animasyonla uğraşmamak için asker yürüyerek gelmez: başta gizli, sonra arkada belirir
        if (spawnBehindPlayer && soldier != null)
            soldier.gameObject.SetActive(false);

        afterTurnLines = Fill(afterTurnLines, DefaultAfterTurn);
        if (string.IsNullOrWhiteSpace(afterTurnReply)) afterTurnReply = DefaultAfterTurnReply;

        bool defaultMutter = mutterLines == null || mutterLines.Length == 0;
        mutterLines = Fill(mutterLines, DefaultMutter);
        if (defaultMutter && (mutterReplies == null || mutterReplies.Length == 0))
            mutterReplies = DefaultMutterReplies;

        bool defaultBackup = backupLines == null || backupLines.Length == 0;
        backupLines = Fill(backupLines, DefaultBackup);
        if (defaultBackup && (backupReplies == null || backupReplies.Length == 0))
            backupReplies = DefaultBackupReplies;

        idleLines = Fill(idleLines, DefaultIdle);

        bool defaultHalt = haltLines == null || haltLines.Length == 0;
        haltLines = Fill(haltLines, DefaultHalt);
        if (defaultHalt && (haltReplies == null || haltReplies.Length == 0))
            haltReplies = DefaultHaltReplies;
        comeLines = Fill(comeLines, DefaultCome);

        bool defaultWalkTalk = walkTalkLines == null || walkTalkLines.Length == 0;
        walkTalkLines = Fill(walkTalkLines, DefaultWalkTalk);
        if (defaultWalkTalk && (walkTalkReplies == null || walkTalkReplies.Length == 0))
            walkTalkReplies = DefaultWalkTalkReplies;
        warningLines = Fill(warningLines, DefaultWarning);
        dangerLines = Fill(dangerLines, DefaultDanger);

        bool defaultStop = stopLines == null || stopLines.Length == 0;
        stopLines = Fill(stopLines, DefaultStop);
        if (defaultStop && (stopReplies == null || stopReplies.Length == 0))
            stopReplies = DefaultStopReplies;

        bool defaultCalm = calmLines == null || calmLines.Length == 0;
        calmLines = Fill(calmLines, DefaultCalm);
        if (defaultCalm && (calmReplies == null || calmReplies.Length == 0))
            calmReplies = DefaultCalmReplies;
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered || soldier == null) return;
        PlayerMovement p = other.GetComponentInParent<PlayerMovement>();
        if (p == null) return;

        triggered = true;
        player = p;
        input = p.inputManager;
        rb = p.GetComponent<Rigidbody>();
        cam = FindAnyObjectByType<RealisticPlayerCamera>();
        StartCoroutine(Run());
    }

    void OnDestroy()
    {
        SetSlowWalk(false);
        if (uiRoot != null) Destroy(uiRoot);
        if (vignetteVolume != null)
        {
            Destroy(vignetteVolume.sharedProfile);
            Destroy(vignetteVolume.gameObject);
        }
    }

    void Update()
    {
        if (soldierFacesPlayer && triggered && soldier != null && soldier.gameObject.activeInHierarchy && player != null)
        {
            Vector3 dir = player.transform.position - soldier.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                soldier.rotation = Quaternion.Slerp(soldier.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 3f);
        }
    }

    private IEnumerator Run()
    {
        // 1. Giriş: birkaç adım, sonra arkadan bağırış ve arkasını dönme
        Lock(true);
        CinematicBars.Show();
        input.ScriptedMove = new Vector2(0f, walkSpeed);
        yield return new WaitForSeconds(introWalkTime);
        input.ScriptedMove = null;

        // Kamera ileri bakarken asker arkada belirir
        if (spawnBehindPlayer)
            SpawnBehindPlayer();
        Vector3 face = soldier.position + Vector3.up * focusHeight;

        if (music != null)
        {
            if (AudioManager.Instance == null)
                new GameObject("AudioManager").AddComponent<AudioManager>();
            AudioManager.Instance.PlayMusic(music);
        }

        if (shoutLines != null && shoutLines.Length > 0)
            Subtitles.Say(WithVoice(shoutLines, soldierVoice));

        // Bağırışı duyup irkilir, donup kalır, sonra yavaşça arkasını döner
        Shake(0.4f, 2f);
        yield return new WaitForSeconds(turnDelay);

        float t = 0f;
        while (t < 2f || Subtitles.IsPlaying)
        {
            cam.SetFocus(face, turnSpeed);
            t += Time.deltaTime;
            yield return null;
        }

        // Döner dönmez asker çıkışır, çocuk korkuyla cevap verir
        Subtitles.Say(WithVoice(afterTurnLines, soldierVoice));
        ChildSay(afterTurnReply, true);
        Shake(0.5f, 2.5f);

        // 2. Minigame: bantlar yarıya iner, fare kilitli, kamera hep askerde; sadece yürümek serbest
        CinematicBars.SetBaseLevel(minigameBarLevel);
        CinematicBars.Hide();
        input.LookLocked = true;
        SetSlowWalk(true);
        Lock(false);
        BuildEffects();
        Stress = 0f;
        nextChatter = Time.time + chatterMin;

        while (DistanceToSoldier() > hugDistance)
        {
            cam.SetFocus(face, trackSpeed);

            // Hikâye durağına gelindi: durulur, bantlar kapanır, asker ve çocuk konuşur
            if (nextStop < storyDistances.Length && DistanceToSoldier() <= storyDistances[nextStop])
            {
                nextStop++;
                yield return StoryStop(face);
                continue;
            }

            UpdateHalt();
            UpdateStress();
            UpdateReactions();
            UpdateChatter();
            UpdateEffects();

            if (Stress >= 1f)
                yield return Retreat(face);

            yield return null;
        }

        // 3. Sarılma: askere koş, ekran kararır, sonraki sahne
        yield return Hug(face);
    }

    private void UpdateStress()
    {
        Vector3 toSoldier = soldier.position - player.transform.position;
        toSoldier.y = 0f;
        toSoldier.Normalize();
        Vector3 v = rb.linearVelocity;
        float approach = Vector3.Dot(new Vector3(v.x, 0f, v.z), toSoldier);

        lastApproach = approach;

        if (approach < -0.3f)
        {
            // Geri çekilmek askeri sakinleştirir
            Stress -= calmRate * Time.deltaTime;
        }
        else if (halting && approach > 0.3f)
        {
            // "Dur" dedi ama yürümeye devam ediyor: söz dinlememek gerer
            Stress += disobeyRate * stressMultiplier * Time.deltaTime;
        }
        else if (approach >= SafeSpeed)
        {
            // Asker yavaş gel dedi: hızlanınca, hele koşunca gerilir
            Stress += (approach < RunSpeed ? walkRate : sprintRate) * stressMultiplier * Time.deltaTime;
        }
        // Yavaşça yaklaşmak ya da durmak gerginliği artırmaz
        Stress = Mathf.Clamp01(Stress);

        // Uzun süre kıpırdamayan oyuncuya asker ne yapması gerektiğini söyler
        idleTimer = (!halting && Mathf.Abs(approach) <= 0.3f) ? idleTimer + Time.deltaTime : 0f;
        if (idleTimer >= idleNudgeTime && !Subtitles.IsPlaying)
        {
            idleTimer = 0f;
            SayNext(idleLines, ref idleIndex, false, soldierVoice);
        }
    }

    // Gerginlik eşiklerini geçince asker hemen tepki verir (konuşmayı keserek)
    private void UpdateReactions()
    {
        if (!warned && Stress >= 0.5f)
        {
            warned = true;
            SayNext(warningLines, ref warningIndex, true, soldierVoice);
            Shake(0.3f, 1.5f);
        }
        if (!dangered && Stress >= 0.8f)
        {
            dangered = true;
            SayNext(dangerLines, ref dangerIndex, true, soldierVoice);
            Shake(0.6f, 2.5f);
        }

        if (Stress >= 0.6f)
            wasTense = true;

        // Geri çekilip sakinleştirince ikisi de rahatlar
        if (wasTense && Stress < 0.3f)
        {
            wasTense = false;
            int i = calmIndex;
            SayNext(calmLines, ref calmIndex, true, SadVoice);
            ChildSay(Reply(calmReplies, i), true);
        }

        // Tekrar uyarabilmesi için gerginliğin belirgin düşmesi gerekir
        if (Stress < 0.35f) warned = false;
        if (Stress < 0.65f) dangered = false;
    }

    // Askerin kafası karışık: arada bir "dur" der, biraz sonra "gel" der
    private void UpdateHalt()
    {
        if (halting)
        {
            // Konuşma sürdükçe bekler, bitince kısa bir duraksamayla "gel" der
            if (Subtitles.IsPlaying)
                haltUntil = Time.time + haltPauseAfterTalk;
            if (Time.time < haltUntil) return;

            halting = false;
            SayNext(comeLines, ref comeIndex, true, SadVoice);
            nextChatter = Mathf.Max(nextChatter, Time.time + 3f);
            return;
        }

        if (nextHaltIndex >= haltDistances.Length || DistanceToSoldier() > haltDistances[nextHaltIndex]) return;
        nextHaltIndex++;
        if (DistanceToSoldier() <= hugDistance + 0.5f) return;

        halting = true;
        haltUntil = Time.time + haltPauseAfterTalk;
        int i = haltIndex;
        SayNext(haltLines, ref haltIndex, true, soldierVoice);
        ChildSay(Reply(haltReplies, i), true);
        Shake(0.3f, 1.5f);
    }

    // Yürürken arada sırada: askeri dinleyip yavaşça gelene sohbet (ödül), diğerlerine telsizden takviye çağrısı
    private void UpdateChatter()
    {
        if (halting || Time.time < nextChatter) return;
        if (Subtitles.IsPlaying)
        {
            nextChatter = Time.time + 1f;
            return;
        }

        bool obedient = lastApproach > 0.3f && lastApproach < SafeSpeed && Stress < 0.35f;
        if (obedient && walkTalkIndex < walkTalkLines.Length)
        {
            int w = walkTalkIndex;
            SayNext(walkTalkLines, ref walkTalkIndex, false, SadVoice);
            ChildSay(Reply(walkTalkReplies, w), true);
        }
        else
        {
            int i = backupIndex;
            SayNext(backupLines, ref backupIndex, false, soldierVoice);
            ChildSay(Reply(backupReplies, i), true);
        }
        nextChatter = Time.time + Random.Range(chatterMin, chatterMax);
    }

    // Hikâye durağı: hareket kilitli, bantlar tam, gerginlik durur; asker söylenir, çocuk cevap verir
    private IEnumerator StoryStop(Vector3 face)
    {
        Lock(true);
        CinematicBars.Show();
        Subtitles.Clear();
        warned = dangered = false;
        halting = false;

        for (int n = 0; n < beatsPerStop; n++)
        {
            int i = mutterIndex;
            SayNext(mutterLines, ref mutterIndex, false, SadVoice);
            ChildSay(Reply(mutterReplies, i), true);

            yield return null;
            while (Subtitles.IsPlaying)
            {
                cam.SetFocus(face, trackSpeed);
                Stress = Mathf.MoveTowards(Stress, 0f, Time.deltaTime * 0.1f); // Konuştukça biraz yumuşar
                UpdateEffects();
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
        }

        CinematicBars.Hide();
        nextChatter = Time.time + chatterMin;
        Lock(false);
    }

    private IEnumerator Retreat(Vector3 face)
    {
        Lock(true);
        CinematicBars.Show(); // Geri atılırken tam sinematik
        int i = stopIndex;
        SayNext(stopLines, ref stopIndex, true, soldierVoice);
        ChildSay(Reply(stopReplies, i), true);
        Shake(0.9f, 3.5f); // En sinirli an: en sert sarsıntı

        // Çocuk korkuyla askere bakarak geri geri çekilir
        input.ScriptedMove = new Vector2(0f, -retreatSpeed);
        float t = 0f;
        while (t < retreatTime)
        {
            cam.SetFocus(face);
            UpdateEffects();
            t += Time.deltaTime;
            yield return null;
        }
        input.ScriptedMove = null;
        CinematicBars.Hide();

        Stress = stressAfterRetreat;
        warned = true;
        wasTense = true;
        nextChatter = Time.time + chatterMin;
        Lock(false);
    }

    private IEnumerator Hug(Vector3 face)
    {
        Lock(true);
        CinematicBars.Show();
        input.ScriptedMove = new Vector2(0f, 1f);
        input.ScriptedSprint = true;

        // Sarılma anında konuşma yok. Ekran koşarken kararmaya başlar, varınca simsiyah olur;
        // askerin suratına bakıp beklemeyelim. Kapanış sonraki sahnede.
        Subtitles.Clear();
        if (uiGroup != null) uiGroup.alpha = 0f;
        if (heartSource != null) heartSource.Stop();
        ScreenFader.Instance.TransitionToScene(nextSceneIndex, fadeOutTime, fadeInTime, null, 1f, blackTime);

        // Yaklaştıkça bakış yüzden göğsüne iner (çocuk yüzünü askere gömer)
        Vector3 chest = face - Vector3.up * 0.5f;
        float startDistance = Mathf.Max(DistanceToSoldier(), hugStopDistance + 0.01f);
        bool hugged = false;

        float t = 0f;
        while (t < fadeOutTime + blackTime)
        {
            float d = DistanceToSoldier();
            float k = Mathf.InverseLerp(startDistance, hugStopDistance, d);
            cam.SetFocus(Vector3.Lerp(face, chest, k));
            Stress = Mathf.MoveTowards(Stress, 0f, Time.deltaTime);
            UpdateEffects();

            if (!hugged && (d <= hugStopDistance || t >= 3f))
            {
                hugged = true;
                input.ScriptedMove = Vector2.zero;
                input.ScriptedSprint = false;
                onHug.Invoke();
            }
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void SpawnBehindPlayer()
    {
        Vector3 back = -player.transform.forward;
        back.y = 0f;
        back.Normalize();
        Vector3 origin = player.transform.position + Vector3.up * 0.5f;

        // Arkada duvar varsa duvarın önünde belirsin
        float distance = spawnDistance;
        if (Physics.Raycast(origin, back, out RaycastHit wall, spawnDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(1.5f, wall.distance - 0.7f);

        Vector3 pos = player.transform.position + back * distance;

        // Zemine oturt
        if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit ground, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            pos.y = ground.point.y;

        soldier.SetPositionAndRotation(pos, Quaternion.LookRotation(-back));
        soldier.gameObject.SetActive(true);
    }

    private float DistanceToSoldier()
    {
        Vector3 d = soldier.position - player.transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    private void Lock(bool locked)
    {
        if (input != null) input.InputLocked = locked;
    }

    // Minigame boyunca karakter parmak uçlarında gibi yavaş yürür: askerin istediği tempo bu.
    // Eğilme kapalı: sadece yürüme (güvenli) ve koşma (gerer) var.
    private void SetSlowWalk(bool slow)
    {
        if (player == null) return;
        if (input != null) input.CrouchLocked = slow;
        if (slow && savedWalkSpeed < 0f)
        {
            savedWalkSpeed = player.walkSpeed;
            player.walkSpeed = minigameWalkSpeed;
        }
        else if (!slow && savedWalkSpeed >= 0f)
        {
            player.walkSpeed = savedWalkSpeed;
            savedWalkSpeed = -1f;
        }
    }

    // Minigame'deki normal yürüyüş her zaman güvenli sayılır (Inspector'daki Slow Speed ondan düşük olsa bile)
    private float SafeSpeed => Mathf.Max(slowSpeed, minigameWalkSpeed * 1.3f);
    private float RunSpeed => Mathf.Max(sprintSpeed, SafeSpeed + 0.5f);

    private SubtitleVoice SadVoice => soldierSadVoice != null ? soldierSadVoice : soldierVoice;
    private SubtitleVoice ChildSad => childSadVoice != null ? childSadVoice : childVoice;

    // Ani sarsıntı: amount ne kadar uzun, boost ne kadar sert (sinir arttıkça ikisi de büyür)
    private void Shake(float amount, float boost)
    {
        if (cam != null && shoutShake > 0f)
            cam.AddShake(amount * shoutShake, Mathf.Max(1f, boost * shoutShake));
    }

    // Çocuğun satırı askerinkinin arkasından kuyruğa girer
    private void ChildSay(string text, bool sad)
    {
        if (!string.IsNullOrWhiteSpace(text))
            Subtitles.Say(text, sad ? ChildSad : childVoice);
    }

    private static string Reply(string[] replies, int index)
    {
        if (replies == null || replies.Length == 0) return null;
        return replies[index % replies.Length];
    }

    // Sıradaki satırı söyler; interrupt açıksa o an söyleneni keser (tepkiler hemen duyulsun)
    private void SayNext(SubtitleLine[] lines, ref int index, bool interrupt, SubtitleVoice voice)
    {
        if (lines == null || lines.Length == 0) return;
        if (interrupt)
            Subtitles.Clear();
        Subtitles.Say(WithVoice(new[] { lines[index % lines.Length] }, voice));
        index++;
    }

    // Satırın kendi sesi seçilmemişse verilen ses kullanılır
    private static SubtitleLine[] WithVoice(SubtitleLine[] lines, SubtitleVoice voice)
    {
        var result = new SubtitleLine[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            SubtitleLine l = lines[i];
            result[i] = new SubtitleLine(l.text, l.voice != null ? l.voice : voice, l.holdTime, l.speedMultiplier);
        }
        return result;
    }

    private static SubtitleLine[] Fill(SubtitleLine[] lines, string[] defaults)
    {
        if (lines != null && lines.Length > 0) return lines;
        var result = new SubtitleLine[defaults.Length];
        for (int i = 0; i < defaults.Length; i++)
            result[i] = new SubtitleLine(defaults[i]);
        return result;
    }

    private void UpdateEffects()
    {
        if (barFill != null)
        {
            barFill.fillAmount = Stress;
            barFill.color = Color.Lerp(new Color(1f, 0.9f, 0.6f), new Color(0.9f, 0.1f, 0.05f), Stress);
        }
        if (heartSource != null)
        {
            heartSource.volume = Mathf.Lerp(0f, 0.9f, Mathf.InverseLerp(0.15f, 1f, Stress));
            heartSource.pitch = Mathf.Lerp(0.8f, 1.7f, Stress);
        }
        if (vignetteOverride != null)
            vignetteOverride.intensity.value = Mathf.Lerp(0f, 0.55f, Stress);
    }

    private void BuildEffects()
    {
        BuildBar();

        if (heartbeat)
        {
            heartSource = gameObject.AddComponent<AudioSource>();
            heartSource.clip = ProceduralFx.Heartbeat();
            heartSource.loop = true;
            heartSource.spatialBlend = 0f;
            heartSource.volume = 0f;
            heartSource.Play();
        }

        if (vignette)
        {
            var go = new GameObject("StressVignette");
            vignetteVolume = go.AddComponent<Volume>();
            vignetteVolume.isGlobal = true;
            vignetteVolume.priority = 60f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignetteOverride = profile.Add<Vignette>();
            vignetteOverride.intensity.overrideState = true;
            vignetteOverride.intensity.value = 0f;
            vignetteOverride.smoothness.overrideState = true;
            vignetteOverride.smoothness.value = 0.6f;
            vignetteVolume.sharedProfile = profile;
        }
    }

    // Ekranın altında ortada ince bir gerginlik barı
    private void BuildBar()
    {
        uiRoot = new GameObject("StressUI");
        Canvas canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 85;
        CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        uiGroup = uiRoot.AddComponent<CanvasGroup>();
        uiGroup.blocksRaycasts = false;

        RectTransform back = CreateRect("Back", uiRoot.transform, new Vector2(0f, 90f), new Vector2(420f, 14f));
        back.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

        RectTransform fill = CreateRect("Fill", back, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(-2f, -2f);
        barFill = fill.gameObject.AddComponent<Image>();
        barFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillAmount = 0f;

        RectTransform labelRect = CreateRect("Label", uiRoot.transform, new Vector2(0f, 116f), new Vector2(600f, 36f));
        Text label = labelRect.gameObject.AddComponent<Text>();
        Font font = Resources.Load<Font>("SubtitleFont");
        label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 1f, 1f, 0.85f);
        label.text = barLabel;
        label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
