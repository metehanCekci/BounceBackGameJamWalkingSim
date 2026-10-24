using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Bir grup etkileşim objesini (çuvallar) aratır. İlk birkaç arama kesinlikle boş çıkar,
// sonra eşya kalan çuvallardan birine rastgele yerleştirilir ve o çuval aranınca görev biter.
// Activate() çağrılana kadar hiçbir obje aranamaz (örneğin cutscene bitince çağır).
public class SearchSequence : MonoBehaviour
{
    [Header("Aranacak Objeler")]
    public Interactable[] sacks;

    [Tooltip("Bu kadar arama kesinlikle boş çıkar. Eşya bundan sonra kalanlardan birine konur.")]
    [Min(0)] public int guaranteedEmptyCount = 3;

    [Header("Diyaloglar (boş bırakılırsa varsayılan satırlar kullanılır)")]
    public SubtitleVoice emptyVoice;   // Varsayılan boş satırların sesi (ör. ChildSad)
    public SubtitleVoice foundVoice;   // Varsayılan bulma satırlarının sesi (ör. ChildNormal)
    public InteractOutcome[] emptyOutcomes;
    public InteractOutcome finalOutcome;

    [Header("Olaylar")]
    public UnityEvent onActivated;
    public UnityEvent onCompleted;

    public bool IsActive { get; private set; }
    public bool IsCompleted { get; private set; }

    private static readonly string[] DefaultEmptyLines =
    {
        "Nothing. Just old rags.",
        "Empty... someone got here before me.",
        "Only dust and broken glass.",
        "It smells awful. Nothing to eat.",
        "Just paper. I can't eat paper.",
    };

    private static readonly string[] DefaultFoundLines =
    {
        "Wait... an MRE bar! It's still sealed.",
        "Finally. Something to eat.",
    };

    private readonly HashSet<Interactable> searched = new HashSet<Interactable>();
    private Interactable itemSack;
    private InteractOutcome lastEmpty;

    void Awake()
    {
        // Çuvalları otomatik bu gruba bağla, her birinde ayrı ayrı ayar yapmak gerekmesin
        foreach (Interactable sack in sacks)
        {
            if (sack != null)
                sack.group = this;
        }
        ApplyDefaults();
    }

    private void ApplyDefaults()
    {
        if (emptyOutcomes == null || emptyOutcomes.Length == 0)
        {
            emptyOutcomes = new InteractOutcome[DefaultEmptyLines.Length];
            for (int i = 0; i < DefaultEmptyLines.Length; i++)
            {
                emptyOutcomes[i] = new InteractOutcome
                {
                    label = "Boş",
                    lines = new[] { new SubtitleLine(DefaultEmptyLines[i], emptyVoice) },
                };
            }
        }

        if (finalOutcome == null)
            finalOutcome = new InteractOutcome();

        if (finalOutcome.lines == null || finalOutcome.lines.Length == 0)
        {
            finalOutcome.lines = new SubtitleLine[DefaultFoundLines.Length];
            for (int i = 0; i < DefaultFoundLines.Length; i++)
                finalOutcome.lines[i] = new SubtitleLine(DefaultFoundLines[i], foundVoice);
        }
    }

    public void Activate()
    {
        if (IsActive || IsCompleted)
            return;

        IsActive = true;
        onActivated.Invoke();
    }

    public bool CanSearch(Interactable sack)
    {
        return IsActive && !IsCompleted && !searched.Contains(sack);
    }

    // Bir çuval aranınca çağrılır, gösterilecek sonucu döndürür
    public InteractOutcome Register(Interactable sack)
    {
        if (!CanSearch(sack))
            return null;
        searched.Add(sack);

        // İlk aramalar kesin boş (aranacak başka çuval kaldıysa)
        if (searched.Count <= guaranteedEmptyCount && HasUnsearched())
        {
            if (searched.Count == guaranteedEmptyCount)
                itemSack = PickRandomUnsearched();
            return PickEmpty();
        }

        if (itemSack != null && sack != itemSack)
            return PickEmpty();

        IsCompleted = true;
        IsActive = false;
        onCompleted.Invoke();
        return finalOutcome;
    }

    // Aynı boş satır arka arkaya gelmesin
    private InteractOutcome PickEmpty()
    {

        InteractOutcome picked = InteractOutcome.Pick(emptyOutcomes);
        for (int i = 0; i < 3 && emptyOutcomes.Length > 1 && picked == lastEmpty; i++)
            picked = InteractOutcome.Pick(emptyOutcomes);
        lastEmpty = picked;
        return picked;
    }

    private bool HasUnsearched()
    {
        foreach (Interactable s in sacks)
        {
            if (s != null && !searched.Contains(s))
                return true;
        }
        return false;
    }

    // Henüz aranmamış, kalan çuvallardan birini rastgele seçer
    private Interactable PickRandomUnsearched()
    {
        var remaining = new List<Interactable>();
        foreach (Interactable s in sacks)
        {
            if (s != null && !searched.Contains(s))
                remaining.Add(s);
        }

        if (remaining.Count == 0)
            return null;

        return remaining[Random.Range(0, remaining.Count)];
    }
}
