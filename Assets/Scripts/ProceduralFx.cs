using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Ses ya da efekt dosyası olmadan kodla üretilen yıkım efektleri: gürleme, çarpma sesi ve toz bulutu.
public static class ProceduralFx
{
    private const int SampleRate = 44100;

    private static Material dustMaterial;

    // Derinden gelen, dalgalanan yer sarsıntısı
    public static AudioClip Rumble(float seconds)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
        float[] data = new float[length];
        float brown = 0f, smooth = 0f, phase = 0f;
        var rng = new System.Random(1234);

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float white = (float)rng.NextDouble() * 2f - 1f;
            brown = (brown + 0.02f * white) / 1.02f;      // Kahverengi gürültü: derin ve boğuk
            smooth += (brown - smooth) * 0.05f;           // Tizleri biraz daha kes

            phase += (38f + Mathf.Sin(t * 1.7f) * 6f) / SampleRate;
            float sub = Mathf.Sin(phase * Mathf.PI * 2f) * 0.35f;

            float fadeIn = Mathf.Clamp01(t / 0.4f);
            float fadeOut = Mathf.Clamp01((seconds - t) / 0.6f);
            float build = Mathf.Lerp(0.5f, 1f, t / seconds);   // Gittikçe şiddetlenir
            float wobble = 0.7f + 0.3f * Mathf.Sin(t * Mathf.PI * 2f * 2.3f);

            data[i] = (smooth * 6f + sub) * fadeIn * fadeOut * build * wobble;
        }
        return CreateClip("Rumble", data);
    }

    // Ağır gümleme + kırılma çıtırtıları + uzun yankı kuyruğu
    public static AudioClip Impact()
    {
        const float seconds = 3.5f;
        int length = Mathf.RoundToInt(SampleRate * seconds);
        float[] data = new float[length];
        float brown = 0f, lowNoise = 0f, phase = 0f, crackle = 0f;
        var rng = new System.Random(4321);

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float white = (float)rng.NextDouble() * 2f - 1f;

            // Gümleme: perdesi düşen alçak sinüs
            phase += Mathf.Lerp(60f, 28f, Mathf.Clamp01(t / 0.6f)) / SampleRate;
            float thump = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 3.5f);

            // Çarpmanın gövdesi: boğuk gürültü patlaması
            lowNoise += (white - lowNoise) * 0.08f;
            float body = lowNoise * Mathf.Exp(-t * 2.5f) * 1.6f;

            // Kırılan beton/cam çıtırtıları, ilk 1.5 saniyede seyrekleşerek
            if (rng.NextDouble() < 0.004 * Mathf.Exp(-t * 2f))
                crackle = (float)(rng.NextDouble() * 0.8 + 0.2);
            crackle *= 0.995f;
            float crack = crackle * white * 0.5f;

            // Uzun yankı kuyruğu
            brown = (brown + 0.02f * white) / 1.02f;
            float tail = brown * 5f * Mathf.Exp(-t * 1.1f);

            data[i] = thump * 1.2f + body + crack + tail;
        }
        return CreateClip("Impact", data);
    }

    // Tek bir "güm-güm" kalp atışı (döngüde çalınır, pitch artınca hızlanır)
    public static AudioClip Heartbeat()
    {
        const float seconds = 0.9f;
        int length = Mathf.RoundToInt(SampleRate * seconds);
        float[] data = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            data[i] = Thump(t, 0f, 1f) + Thump(t, 0.26f, 0.65f);
        }
        return CreateClip("Heartbeat", data);
    }

    private static float Thump(float t, float start, float strength)
    {
        float local = t - start;
        if (local < 0f) return 0f;
        float freq = Mathf.Lerp(65f, 40f, Mathf.Clamp01(local / 0.12f));
        return Mathf.Sin(local * freq * Mathf.PI * 2f) * Mathf.Exp(-local * 22f) * strength;
    }

    // Kısmen 3B çalar: yönü duyulur ama uzaktan da güçlü gelir
    public static void PlayClip(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null) return;

        var go = new GameObject("FxSound_" + clip.name);
        go.transform.position = position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 0.6f;
        source.minDistance = 20f;
        source.maxDistance = 400f;
        source.dopplerLevel = 0f;
        source.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

    // Yayılıp yavaşça dağılan toz bulutu. scale: 1 = bina çarpması, küçük değerler hafif toz
    public static void SpawnDust(Vector3 center, Vector3 area, float scale = 1f)
    {
        Material mat = GetDustMaterial();
        if (mat == null) return;

        // Kapalı oluştur: varsayılan (mavi) parçacıklar ayarlar uygulanmadan bir kare bile görünmesin
        var go = new GameObject("CollapseDust");
        go.SetActive(false);
        go.transform.position = center;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f * scale, 9f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(4f * scale, 10f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.015f;            // Toz hafifçe yükselir
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)(140 * scale + 10)),
            new ParticleSystem.Burst(0.35f, (short)(60 * scale + 5)),
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(1f, area.x), 0.5f, Mathf.Max(1f, area.z));

        // Hızla yayılır, sonra havada asılı kalır
        ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0.4f;
        limit.dampen = 0.12f;

        // Büyüyerek açılır, ömrü bitince küçülerek kaybolur (shader saydamlığı değiştiremediği için)
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1f), new Keyframe(0.8f, 0.95f), new Keyframe(1f, 0f)));

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        go.SetActive(true);
        ps.Play();
        Object.Destroy(go, 10f);
    }

    private static Material GetDustMaterial()
    {
        if (dustMaterial != null) return dustMaterial;

        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("ProceduralFx: HDRP/Unlit shader bulunamadı, toz efekti kapalı.");
            return null;
        }

        dustMaterial = new Material(shader) { name = "ProceduralDust" };
        dustMaterial.SetFloat("_SurfaceType", 1f);  // Saydam
        dustMaterial.SetFloat("_BlendMode", 0f);    // Alpha
        dustMaterial.SetTexture("_UnlitColorMap", CreateSoftCircle(64));
        dustMaterial.SetColor("_UnlitColor", new Color(0.52f, 0.48f, 0.43f, 0.4f));
        HDMaterial.ValidateMaterial(dustMaterial);
        return dustMaterial;
    }

    // Kenarları yumuşak, ortası dolu daire: tek tek parçacıklar bulut gibi kaynaşsın
    private static Texture2D CreateSoftCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = size * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float a = Mathf.Clamp01(1f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static AudioClip CreateClip(string name, float[] data)
    {
        // Patlamasın diye en yüksek değeri 0.9'a sabitle
        float peak = 0.0001f;
        foreach (float s in data)
            peak = Mathf.Max(peak, Mathf.Abs(s));
        float gain = 0.9f / peak;
        for (int i = 0; i < data.Length; i++)
            data[i] *= gain;

        AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
