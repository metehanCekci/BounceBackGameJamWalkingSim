using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

// ParkourMap'te oyuncuya yolu gösterir: başlangıcın arkasındaki siren kırmızı alarm olarak kalır (siren sesi ondan gelir),
// yolun sonundaki ve bunker'ın yanındaki sirenler sessiz, yeşil yol gösterici olur. Giriş cutscene'i önce kırmızıya,
// sonra yeşile baktırıp oraya koşturur; ekranda sırayla yeşil siren ve bunker işaretlenir (PathGuide).
// Sirenler konumdan bulunur: oyuncunun en arkasındaki kırmızı, bunker'a en yakını bunker sireni, en öndeki yol sireni.
// Tekrar çalıştırmak güvenli: aynı objeler güncellenir, olaylar ikinci kez eklenmez, elle yazılmış satırlar ezilmez.
public static class ParkourGuideSetup
{
    const string UndoName = "Parkur Yol Rehberi";
    const string GuideName = "PathGuide";
    const string BunkerGoalName = "BunkerGoal";

    static readonly Color GuidePrimary = new Color(0.03f, 0.5f, 0.1f);
    static readonly Color GuideSecondary = new Color(0.1f, 0.7f, 0.2f);
    const float GuideIdleIntensity = 3f; // Kırmızıdan parlak: ışık arkası dönükken de uzaktan seçilsin

    [MenuItem("Tools/Level/Parkur Yol Rehberini Kur (Açık Sahne)")]
    static void Setup()
    {
        PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>();
        SceneTransitionTrigger bunker = Object.FindAnyObjectByType<SceneTransitionTrigger>();
        AlarmSiren[] sirens = Object.FindObjectsByType<AlarmSiren>();
        if (player == null || bunker == null || sirens.Length < 3)
        {
            Debug.LogWarning("Parkur Yol Rehberi: sahnede oyuncu, bunker (SceneTransitionTrigger) ve en az 3 siren olmalı.");
            return;
        }

        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();

        Vector3 start = player.transform.position;
        Vector3 forward = player.transform.forward;
        Vector3 bunkerCenter = bunker.GetComponent<Collider>().bounds.center;
        float Ahead(AlarmSiren s) => Vector3.Dot(s.transform.position - start, forward);

        AlarmSiren alarm = sirens.OrderBy(Ahead).First();
        AlarmSiren bunkerSiren = sirens.Where(s => s != alarm)
            .OrderBy(s => (s.transform.position - bunkerCenter).sqrMagnitude).First();
        AlarmSiren roadSiren = sirens.Where(s => s != alarm && s != bunkerSiren)
            .OrderByDescending(Ahead).First();

        // Karakter sese dönüp kırmızıya bakıyor: alarm sesi oradan gelsin
        AudioClip clip = alarm.sirenSound != null
            ? alarm.sirenSound
            : sirens.Select(s => s.sirenSound).FirstOrDefault(c => c != null);
        if (clip != null)
            Edit(alarm, s => s.sirenSound = clip);
        else
            Debug.LogWarning("Parkur Yol Rehberi: hiçbir sirende ses yok, kırmızı sirene elle ata.");

        MakeGuide(roadSiren);
        MakeGuide(bunkerSiren);

        // Hedef işaretleri: önce yolun sonundaki yeşil siren, bina çöküşünden uyanınca bunker
        PathGuide guide = Object.FindAnyObjectByType<PathGuide>();
        if (guide == null)
        {
            var go = new GameObject(GuideName);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            guide = go.AddComponent<PathGuide>();
        }

        Transform goal = guide.transform.Find(BunkerGoalName);
        if (goal == null)
        {
            // Elle taşınabilsin diye sadece ilk kurulumda bunker'ın ortasına konur
            goal = new GameObject(BunkerGoalName).transform;
            Undo.RegisterCreatedObjectUndo(goal.gameObject, UndoName);
            goal.SetParent(guide.transform, false);
            goal.position = bunkerCenter;
        }

        Edit(guide, g => g.steps = new[]
        {
            new PathGuide.Step { target = roadSiren.transform, label = "Yeşil ışığa koş", offset = new Vector3(0f, 2f, 0f) },
            new PathGuide.Step { target = goal, label = "Sığınak" },
        });

        SirenCutscene intro = Object.FindAnyObjectByType<SirenCutscene>();
        SubtitleVoice voice = intro != null && intro.lines != null && intro.lines.Length > 0 ? intro.lines[0].voice : null;
        if (intro != null)
        {
            Edit(intro, c =>
            {
                c.focusTarget = alarm.transform;
                c.guideTarget = roadSiren.transform;
                if (c.guideLines == null || c.guideLines.Length == 0)
                    c.guideLines = new[] { new SubtitleLine("Sığınağa koşmalıyım!", voice) };
            });
            AddListenerOnce(intro, intro.onFinished, guide.Begin);
        }
        else
        {
            Debug.LogWarning("Parkur Yol Rehberi: SirenCutscene yok, PathGuide.Begin() elle bağlanmalı.");
        }

        EscapeCollapseCutscene escape = Object.FindAnyObjectByType<EscapeCollapseCutscene>();
        if (escape != null)
        {
            if (escape.lines == null || escape.lines.Length == 0)
                Edit(escape, e => e.lines = new[] { new SubtitleLine("Az kaldı... Sığınak yeşil ışığın orada!", voice) });
            AddListenerOnce(escape, escape.onFinished, guide.Next);
        }
        else
        {
            Debug.LogWarning("Parkur Yol Rehberi: EscapeCollapseCutscene yok, PathGuide.Next() elle bağlanmalı.");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(guide.gameObject.scene);
        Selection.activeObject = guide.gameObject;
        Debug.Log($"Parkur yol rehberi kuruldu. Kırmızı: {alarm.transform.root.name}, yeşil yol: {roadSiren.transform.root.name}, " +
                  $"yeşil bunker: {bunkerSiren.transform.root.name}. Sahneyi kaydetmeyi unutma.");
    }

    static void MakeGuide(AlarmSiren siren)
    {
        Edit(siren, s =>
        {
            s.primaryColor = GuidePrimary;
            s.secondaryColor = GuideSecondary;
            s.lampIdleIntensity = GuideIdleIntensity;
            s.tintScreen = false; // Ekranı sadece kırmızı alarm boyasın: kırmızı tehlike, yeşil yol
            s.sirenSound = null;
        });

        // Prefab'daki kaynakta kalan bir ses de çalmasın
        AudioSource source = siren.GetComponent<AudioSource>();
        if (source != null && source.clip != null)
            Edit(source, a => a.clip = null);
    }

    static void Edit<T>(T target, System.Action<T> change) where T : Object
    {
        Undo.RecordObject(target, UndoName);
        change(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    static void AddListenerOnce(Object owner, UnityEvent evt, UnityAction action)
    {
        for (int i = 0; i < evt.GetPersistentEventCount(); i++)
        {
            if (evt.GetPersistentTarget(i) == (Object)action.Target && evt.GetPersistentMethodName(i) == action.Method.Name)
                return;
        }
        Undo.RecordObject(owner, UndoName);
        UnityEventTools.AddPersistentListener(evt, action);
    }
}
