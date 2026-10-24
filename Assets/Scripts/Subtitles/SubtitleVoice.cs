using UnityEngine;

// Bir konuşmacının adı, rengi ve "Undertale" tarzı konuşma sesi.
// Project penceresinde sağ tık > Create > Altyazı > Konuşmacı ile oluşturulur.
[CreateAssetMenu(fileName = "YeniKonusmaci", menuName = "Altyazı/Konuşmacı")]
public class SubtitleVoice : ScriptableObject
{
    [Header("Kimlik")]
    public string speakerName = "";          // Boş bırakılırsa isim gösterilmez
    public Color nameColor = new Color(1f, 0.85f, 0.3f);
    public Color textColor = Color.white;

    [Header("Konuşma Sesi")]
    public AudioClip[] blipSounds;           // Boşsa aşağıdaki stile göre üretilen bip kullanılır
    public BlipStyle blipStyle = BlipStyle.Bright;
    [Range(0.1f, 3f)] public float pitch = 1f;
    [Range(0f, 0.5f)] public float pitchVariation = 0.06f;
    [Range(0f, 1f)] public float volume = 1f;
    [Min(1)] public int blipEvery = 2;       // Kaç harfte bir ses çalsın

    [Header("Hız")]
    [Range(0.2f, 3f)] public float speedMultiplier = 1f;
}
