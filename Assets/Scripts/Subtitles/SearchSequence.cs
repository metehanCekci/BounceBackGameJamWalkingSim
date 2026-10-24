using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Bir grup çuvalı aratır. İlk birkaç arama kesinlikle boş çıkar (çuvalın kendi metni oynar),
// sonra eşya kalan çuvallardan birine rastgele konur; o çuval aranınca "bulundu" metni oynar ve görev biter.
// Activate() çağrılana kadar hiçbir çuval aranamaz (örneğin cutscene bitince çağır).
public class SearchSequence : MonoBehaviour
{
    [Header("Aranacak Çuvallar")]
    public Interactable[] sacks;

    [Tooltip("Bu kadar arama kesinlikle boş çıkar. Eşya bundan sonra kalanlardan birine konur.")]
    [Min(0)] public int guaranteedEmptyCount = 3;

    [Header("Eşya Bulununca")]
    [TextArea(2, 4)] public string foundText = "Wait... an MRE bar! It's still sealed.";
    public SubtitleVoice foundVoice;

    [Header("Olaylar")]
    public UnityEvent onActivated;
    public UnityEvent onCompleted;

    public bool IsActive { get; private set; }
    public bool IsCompleted { get; private set; }

    private readonly HashSet<Interactable> searched = new HashSet<Interactable>();
    private Interactable itemSack;

    void Awake()
    {
        // Çuvalları otomatik bu gruba bağla, her birinde ayrı ayrı ayar yapmak gerekmesin
        foreach (Interactable sack in sacks)
        {
            if (sack != null)
                sack.group = this;
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

    public void SayFound()
    {
        if (!string.IsNullOrWhiteSpace(foundText))
            Subtitles.Say(foundText, foundVoice);
    }

    // Bir çuval aranınca çağrılır; eşya bu çuvaldaysa true döner
    public bool Register(Interactable sack)
    {
        if (!CanSearch(sack))
            return false;
        searched.Add(sack);

        // İlk aramalar kesin boş (aranacak başka çuval kaldıysa)
        if (searched.Count <= guaranteedEmptyCount && HasUnsearched())
        {
            if (searched.Count == guaranteedEmptyCount)
                itemSack = PickRandomUnsearched();
            return false;
        }

        if (itemSack != null && sack != itemSack)
            return false;

        IsCompleted = true;
        IsActive = false;
        onCompleted.Invoke();
        return true;
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
