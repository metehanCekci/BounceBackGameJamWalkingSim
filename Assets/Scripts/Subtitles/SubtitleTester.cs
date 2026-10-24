using UnityEngine;
using UnityEngine.InputSystem;

// Altyazı sistemini tek yerden test etmek için. İşin bitince bu scripti silebilirsin.
//   P      : tüm özellikleri sırayla test eder (stiller, isim, uzun metin, bekleme süresi, sessiz anlatıcı)
//   1 / 2  : Normal / Üzgün konuşmacı ile tek satır
//   3 / 4  : Düşman kızgın / Düşman üzgün konuşmacı ile tek satır
//   C      : altyazıyı hemen temizler
// Enter tuşu (SubtitleManager'daki Skip Key) satırı atlar.
// Konuşmacılar kodla oluşturulur, asset eklemene gerek yok.
public class SubtitleTester : MonoBehaviour
{
    private SubtitleVoice normalVoice;
    private SubtitleVoice sadVoice;
    private SubtitleVoice angryVoice;
    private SubtitleVoice gloomyVoice;

    void Start()
    {
        normalVoice = CreateVoice("Normal", new Color(1f, 0.85f, 0.3f), Color.white, BlipStyle.Bright, 1.1f);
        sadVoice = CreateVoice("Üzgün", new Color(0.6f, 0.8f, 1f), new Color(0.85f, 0.9f, 1f), BlipStyle.Sad, 1f);
        angryVoice = CreateVoice("Düşman", new Color(1f, 0.3f, 0.2f), new Color(1f, 0.9f, 0.9f), BlipStyle.Angry, 0.9f);
        gloomyVoice = CreateVoice("Düşman (üzgün)", new Color(0.7f, 0.5f, 0.9f), new Color(0.9f, 0.85f, 1f), BlipStyle.Gloomy, 0.9f);
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.pKey.wasPressedThisFrame)
            RunFullTest();
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            Subtitles.Say("Normal konuşmacı, parlak bip.", normalVoice);
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            Subtitles.Say("Üzgün konuşmacı, alçalan yumuşak bip.", sadVoice);
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            Subtitles.Say("Düşman kızgın konuşuyor! Silahı bırak!", angryVoice);
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            Subtitles.Say("Düşman üzgün de konuşabiliyor...", gloomyVoice);
        if (Keyboard.current.cKey.wasPressedThisFrame)
            Subtitles.Clear();
    }

    private void RunFullTest()
    {
        // Tüm satırlar sıraya girer. Test sırasında tekrar P'ye basarsan sona eklenir.
        Subtitles.Say(new[]
        {
            new SubtitleLine("Merhaba, ben normal konuşmacıyım. Parlak bip sesi çalmalı.", normalVoice),
            new SubtitleLine("Bu satır üzgün bir ses kullanıyor, perde aşağı iniyor...", sadVoice),
            new SubtitleLine("DUR! Silahı yere bırak!", angryVoice, 2f),
            new SubtitleLine("Neden böyle oluyor ki...", gloomyVoice),
            new SubtitleLine("Bu da isimsiz bir anlatıcı. Renk sadece yazıda, isim kutusu yok."),
            new SubtitleLine("Bu satır çok uzun olmalı ki kutunun satır satır büyüdüğünü görelim. Virgüller, noktalar ve soru işaretleri duraklamalı mı? Peki çalışıyor mu?", normalVoice),
            new SubtitleLine("Evet.", normalVoice, 0.8f),
            new SubtitleLine("Son satır, bu hızda bitmeli.", sadVoice),
        });
    }

    private static SubtitleVoice CreateVoice(string name, Color nameColor, Color textColor, BlipStyle style, float pitch)
    {
        SubtitleVoice voice = ScriptableObject.CreateInstance<SubtitleVoice>();
        voice.speakerName = name;
        voice.nameColor = nameColor;
        voice.textColor = textColor;
        voice.blipStyle = style;
        voice.pitch = pitch;
        return voice;
    }
}
