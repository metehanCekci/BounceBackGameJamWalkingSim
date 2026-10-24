using UnityEngine;

public enum BlipStyle
{
    Bright, // Parlak, kısa kare dalga (normal)
    Sad,    // Alçalan perde, yumuşak üçgen dalga, uzun kuyruk (üzgün)
    Angry,  // Kalın, sert testere dalgası, hızlı sönen (düşman, celalli)
    Gloomy  // Kalın ve yavaş, sinüs dalgası, uzun kuyruk (düşman, üzgün)
}

// Ses dosyası olmadan konuşma bipleri üretir. Celeste / Undertale tarzı kısa sesler için.
public static class SubtitleBlipGenerator
{
    private const int SampleRate = 44100;

    public static AudioClip Create(BlipStyle style)
    {
        float duration, startFreq, endFreq, decayPower, gain;

        switch (style)
        {
            case BlipStyle.Sad:
                duration = 0.12f; startFreq = 320f; endFreq = 190f; decayPower = 1.5f; gain = 0.25f;
                break;
            case BlipStyle.Angry:
                duration = 0.09f; startFreq = 200f; endFreq = 120f; decayPower = 1.2f; gain = 0.3f;
                break;
            case BlipStyle.Gloomy:
                duration = 0.15f; startFreq = 210f; endFreq = 130f; decayPower = 1.6f; gain = 0.28f;
                break;
            default: // Bright
                duration = 0.045f; startFreq = 620f; endFreq = 520f; decayPower = 2f; gain = 0.25f;
                break;
        }

        int length = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[length];
        float phase = 0f;

        for (int i = 0; i < length; i++)
        {
            float progress = i / (float)length;

            // Perdeyi zamanla kaydır: çoğu stilde aşağı iner
            float freq = Mathf.Lerp(startFreq, endFreq, progress);
            phase += freq / SampleRate;

            float wave = Wave(style, phase);
            float envelope = Mathf.Pow(1f - progress, decayPower);
            data[i] = wave * gain * envelope;
        }

        AudioClip clip = AudioClip.Create("SubtitleBlip_" + style, length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Wave(BlipStyle style, float phase)
    {
        switch (style)
        {
            case BlipStyle.Sad:
            case BlipStyle.Gloomy:
                return style == BlipStyle.Sad ? Triangle(phase) : Mathf.Sin(phase * Mathf.PI * 2f);
            case BlipStyle.Angry:
                return Sawtooth(phase);
            default:
                return Square(phase);
        }
    }

    private static float Square(float phase)
    {
        return Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
    }

    private static float Triangle(float phase)
    {
        float p = Mathf.Repeat(phase, 1f);
        return 4f * Mathf.Abs(p - 0.5f) - 1f;
    }

    private static float Sawtooth(float phase)
    {
        return 2f * Mathf.Repeat(phase, 1f) - 1f;
    }
}
