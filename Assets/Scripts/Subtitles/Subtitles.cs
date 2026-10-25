using System;
using UnityEngine;

[Serializable]
public class SubtitleLine
{
    public SubtitleVoice voice;              // Boşsa yöneticideki varsayılan konuşmacı
    [TextArea(2, 4)] public string text;
    public float holdTime = 0f;              // Yazı bittikten sonra ekranda kalma süresi (0 = otomatik)
    [Tooltip("Yazılma hızı çarpanı. 1 = normal, 0.4 = dramatik yavaş. Metnin başına ~ koymak da yavaşlatır.")]
    public float speedMultiplier = 1f;

    public SubtitleLine() { }

    public SubtitleLine(string text, SubtitleVoice voice = null, float holdTime = 0f, float speedMultiplier = 1f)
    {
        this.text = text;
        this.voice = voice;
        this.holdTime = holdTime;
        this.speedMultiplier = speedMultiplier;
    }
}

// Kodun her yerinden kolay kullanım için kısayol:
//   Subtitles.Say("Merhaba!");
//   Subtitles.Say("Burası neresi?", annemVoice);
public static class Subtitles
{
    public static bool IsPlaying => SubtitleManager.Instance != null && SubtitleManager.Instance.IsPlaying;

    public static void Say(string text, SubtitleVoice voice = null, float holdTime = 0f)
    {
        Get().Enqueue(new SubtitleLine(text, voice, holdTime));
    }

    public static void Say(SubtitleLine[] lines)
    {
        SubtitleManager manager = Get();
        foreach (SubtitleLine line in lines)
            manager.Enqueue(line);
    }

    // Şu anki satırı anında tamamlar, tamamsa sonrakine geçer
    public static void Skip()
    {
        if (SubtitleManager.Instance != null)
            SubtitleManager.Instance.Skip();
    }

    // Sıradaki her şeyi siler ve altyazıyı kapatır
    public static void Clear()
    {
        if (SubtitleManager.Instance != null)
            SubtitleManager.Instance.Clear();
    }

    private static SubtitleManager Get()
    {
        // Sahnede yönetici yoksa otomatik oluştur, kurulum gerekmesin
        if (SubtitleManager.Instance == null)
            new GameObject("Subtitles").AddComponent<SubtitleManager>();
        return SubtitleManager.Instance;
    }
}
