using UnityEngine;

public class RealisticPlayerCamera : MonoBehaviour
{
    [Header("Gerekli Referanslar")]
    public PlayerInputManager inputManager;
    public PlayerMovement playerMovement; // Hız senkronizasyonu için
    public Rigidbody playerRb; // Gerçek fiziksel hızı okumak için
    public Transform playerBody;
    public AudioSource footstepAudioSource;

    [Header("Adım Sesleri & Seviyeleri")]
    public AudioClip[] footstepSounds;          // Yürüme
    public AudioClip[] sprintFootstepSounds;    // Koşma (boşsa yürüme sesi kullanılır)
    public AudioClip[] crouchFootstepSounds;    // Eğilerek yürüme (boşsa yürüme sesi kullanılır)
    [Range(0f, 1f)] public float crouchVolume = 0.15f;
    [Range(0f, 1f)] public float walkVolume = 0.4f;
    [Range(0f, 1f)] public float sprintVolume = 0.7f;

    [Header("Zıplama & İniş Sesleri")]
    public AudioClip[] jumpSounds;    // Boşsa adım sesi kullanılır
    public AudioClip[] landSounds;    // Boşsa adım sesi kullanılır
    public AudioClip[] effortSounds;  // Sert inişte "hıh" nefes sesi (isteğe bağlı)
    [Range(0f, 1f)] public float jumpVolume = 0.5f;
    [Range(0f, 1f)] public float landVolume = 0.9f;

    [Header("Görüş Açısı (FOV)")]
    public float normalFOV = 60f;
    public float sprintFOV = 68f; // Koşunca hafifçe geri çekilen kamera efekti
    public float fovSmoothness = 4f;
    public float fallFOVBoost = 6f;       // Hızlı düşerken hafif genişleyen görüş
    public float fallFOVMaxSpeed = 10f;
    public float landFOVPunch = 3f;       // İniş anında görüş bir anlığına daralır
    public float nearClipPlane = 0.03f;   // Duvara dayanınca ekran köşeleri duvarın içine girip arkasını göstermesin

    [Header("Bakış Ayarları")]
    public float mouseSensitivity = 0.1f;
    public float gamepadSensitivity = 150f;
    public float maxPitch = 80f;
    public float lookSmoothness = 20f;

    [Header("Yürüme Sarsıntısı (Walk)")]
    public float walkBobSpeed = 12f;
    public float walkBobAmountY = 0.05f;
    public float walkBobAmountX = 0.03f;

    [Header("Eğilme Sarsıntısı (Crouch)")]
    public float crouchBobSpeed = 8f;
    public float crouchBobAmountY = 0.03f;
    public float crouchBobAmountX = 0.02f;

    [Header("Koşu Sarsıntısı (Sprint)")]
    public float sprintBobSpeed = 16f;
    public float sprintBobAmountY = 0.08f;
    public float sprintBobAmountX = 0.05f;

    [Header("Duruş & Yana Yatma")]
    public float standHeight = 0.8f;
    public float crouchHeight = 0.2f;
    public float heightSmoothness = 6f;
    public float tiltAngle = 3f;
    public float tiltSmooth = 5f;

    [Header("Zıplama Kamera Hareketi")]
    public float jumpDip = 0.05f;          // Kalkışta dizler bükülür gibi kısa çökme
    public float jumpPitchKick = -2.5f;    // Kalkışta kafa hafif yukarı kalkar
    public float airPitchPerSpeed = 0.35f; // Düşerken kafa hafif aşağı bakar
    public float airPitchMax = 4f;

    [Header("İniş Kamera Hareketi")]
    public float landDipMin = 0.06f;
    public float landDipMax = 0.28f;
    public float landPitchMin = 2f;
    public float landPitchMax = 9f;
    public float landRollMax = 2.5f;       // Sert inişte kafa hafif yana savrulur
    public float springStiffness = 120f;
    public float springDamping = 14f;

    [Header("Sarsıntı (Shake)")]
    public float shakeMaxAngle = 2.5f;
    public float shakeMaxOffset = 0.03f;
    public float shakeFrequency = 22f;
    public float shakeDecay = 2.2f;
    [Range(0f, 1f)] public float landShakeThreshold = 0.35f; // Bu sertliğin altında sarsıntı yok

    [Header("Engel Atlama (Vault) Kamera")]
    public PlayerVault playerVault;     // Boşsa oyuncudan otomatik bulunur
    public float vaultPitch = 8f;       // Engelin üstünden geçerken kafa öne eğilir
    public float climbPitch = 12f;      // Tırmanırken kafa engelin üstüne bakar
    public float vaultRoll = 4f;        // Eli engele koyunca gövde hafif yana yatar
    public AudioClip[] vaultSounds;     // Boşsa zıplama sesi kullanılır
    [Range(0f, 1f)] public float vaultVolume = 0.6f;

    [Header("Etkileşim Odağı")]
    public float focusSpeed = 6f;       // E basılı tutulurken kameranın objeye dönme hızı

    private float lookPitch;
    private float yaw;
    private float bobTimer;
    private float currentTilt;
    private float currentCameraY;
    private bool isStepSoundPlayed = false;
    private Vector2 currentLookDelta;
    private Camera cam;

    // Yay sistemleri: iniş/kalkış hareketleri sert kesilmeden, gerçek bir boyun gibi salınarak döner
    private float springPosY, springVelY;
    private float springPitch, springPitchVel;
    private float springRoll, springRollVel;
    private float airPitch;
    private float fovPunch;
    private float baseFOV;
    private float shakeTrauma;
    private float shakeSeed;
    private float shakeBoost = 1f;          // Dışarıdan gelen büyük sarsıntılarda (bina, patlama) şiddet çarpanı
    private float vaultPitchOffset;
    private float vaultRollOffset;
    private float vaultRollSign = 1f;
    private bool hasFocus;
    private Vector3 focusPoint;
    private float focusSpeedOverride = -1f;
    private float fovOverride = -1f;       // > 0 iken cutscene zoom'u
    private float fovOverrideSpeed = 3f;

    // Sırtüstü düşme pozu (0 = ayakta, 1 = yerde gökyüzüne bakıyor), PlayerKnockdown yönetir
    private float knockdownAmount;
    private const float KnockdownEyeHeight = -0.7f;  // Yerdeyken kameranın yerel yüksekliği (ayaklara yakın)
    private const float KnockdownPitch = -70f;       // Gökyüzüne bakış
    private const float KnockdownRoll = 8f;

    void Start()
    {
        cam = GetComponent<Camera>();
        currentCameraY = standHeight;
        shakeSeed = Random.Range(0f, 100f);
        baseFOV = cam != null ? cam.fieldOfView : normalFOV;
        if (cam != null)
            cam.nearClipPlane = nearClipPlane;
        // Ses seviyesini PlayOneShot ile veriyoruz, kaynağın kendi seviyesi tam kalsın
        if (footstepAudioSource != null)
            footstepAudioSource.volume = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerBody != null)
            yaw = playerBody.eulerAngles.y;
    }

    void OnEnable()
    {
        if (playerVault == null && playerMovement != null)
            playerVault = playerMovement.GetComponent<PlayerVault>();

        if (playerMovement != null)
        {
            playerMovement.OnJumped += HandleJumped;
            playerMovement.OnLanded += HandleLanded;
            playerMovement.OnTeleported += HandleTeleported;
        }
        if (playerVault != null)
        {
            playerVault.OnVaultStarted += HandleVaultStarted;
            playerVault.OnVaultEnded += HandleVaultEnded;
        }
    }

    void OnDisable()
    {
        if (playerMovement != null)
        {
            playerMovement.OnJumped -= HandleJumped;
            playerMovement.OnLanded -= HandleLanded;
            playerMovement.OnTeleported -= HandleTeleported;
        }
        if (playerVault != null)
        {
            playerVault.OnVaultStarted -= HandleVaultStarted;
            playerVault.OnVaultEnded -= HandleVaultEnded;
        }
    }

    void LateUpdate()
    {
        if (inputManager == null || playerBody == null || playerRb == null || playerMovement == null) return;

        HandleLook();
        UpdateSprings();
        HandleDynamicFOV();
        HandleHeadbobAndPosture();
    }

    private void HandleJumped()
    {
        springVelY -= jumpDip * springStiffness * 0.1f;
        springPitchVel += jumpPitchKick * springStiffness * 0.1f;

        PlayClip(jumpSounds.Length > 0 ? jumpSounds : footstepSounds, jumpVolume, 0.95f, 1.05f);
    }

    private void HandleLanded(float severity)
    {
        float dip = Mathf.Lerp(landDipMin, landDipMax, severity);
        float pitch = Mathf.Lerp(landPitchMin, landPitchMax, severity);

        // Anlık itme veriyoruz, yay geri kalanını doğal şekilde halleder
        springVelY -= dip * springStiffness * 0.1f;
        springPitchVel += pitch * springStiffness * 0.1f;
        springRollVel += Random.Range(-1f, 1f) * landRollMax * severity * springStiffness * 0.1f;
        fovPunch = -landFOVPunch * severity;

        if (severity > landShakeThreshold)
            shakeTrauma = Mathf.Clamp01(shakeTrauma + Mathf.InverseLerp(landShakeThreshold, 1f, severity));

        // Sert iniş daha tok (düşük pitch) ve yüksek sesli
        float volume = landVolume * Mathf.Lerp(0.5f, 1f, severity);
        float pitchBase = Mathf.Lerp(1.05f, 0.85f, severity);
        PlayClip(landSounds.Length > 0 ? landSounds : footstepSounds, volume, pitchBase - 0.05f, pitchBase + 0.05f);

        if (severity > 0.6f)
            PlayClip(effortSounds, volume * 0.8f, 0.95f, 1.05f);

        isStepSoundPlayed = true; // İniş sesinin hemen üstüne adım sesi binmesin
    }

    private void HandleVaultStarted(bool isClimb)
    {
        vaultRollSign = Random.value < 0.5f ? -1f : 1f;
        // Engele tutunup itilirken kısa bir çökme
        springVelY -= jumpDip * springStiffness * 0.1f;

        AudioClip[] clips = vaultSounds.Length > 0 ? vaultSounds : (jumpSounds.Length > 0 ? jumpSounds : footstepSounds);
        PlayClip(clips, vaultVolume, 0.95f, 1.05f);
    }

    private void HandleVaultEnded()
    {
        // Engelden inişi hafif bir iniş gibi hissettir
        springVelY -= landDipMin * springStiffness * 0.1f;
        springPitchVel += landPitchMin * springStiffness * 0.1f;

        PlayClip(landSounds.Length > 0 ? landSounds : footstepSounds, landVolume * 0.5f, 1f, 1.1f);
        isStepSoundPlayed = true;
    }

    private void HandleTeleported()
    {
        // Başa dönünce bakış yeni duruşa otursun, yarım kalan yay/sarsıntı hareketleri sıfırlansın
        if (playerBody != null)
            yaw = playerBody.eulerAngles.y;
        lookPitch = 0f;
        currentLookDelta = Vector2.zero;
        springPosY = springVelY = 0f;
        springPitch = springPitchVel = 0f;
        springRoll = springRollVel = 0f;
        airPitch = 0f;
        fovPunch = 0f;
        shakeTrauma = 0f;
        vaultPitchOffset = vaultRollOffset = 0f;
        hasFocus = false;
    }

    // Etkileşim sırasında kamera bu noktaya yumuşakça döner, fare girişi kilitli kalır
    // speed verilirse bu odak için focusSpeed yerine o kullanılır (düşük = daha yavaş dönüş)
    public void SetFocus(Vector3 worldPoint, float speed = -1f)
    {
        hasFocus = true;
        focusPoint = worldPoint;
        focusSpeedOverride = speed;
    }

    public void ClearFocus()
    {
        hasFocus = false;
    }

    // Işınlanma sonrası bakışı anında ayarla (yumuşak geçiş yok)
    public void SnapLook(float yawDegrees, float pitchDegrees)
    {
        yaw = yawDegrees;
        lookPitch = Mathf.Clamp(pitchDegrees, -maxPitch, maxPitch);
        currentLookDelta = Vector2.zero;
        if (playerBody != null)
            playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // Cutscene zoom'u: FOV verilen değere yumuşakça gider (küçük FOV = yakınlaşma)
    public void SetFovOverride(float fov, float speed = 3f)
    {
        fovOverride = fov;
        fovOverrideSpeed = speed;
    }

    public void ClearFovOverride()
    {
        fovOverride = -1f;
    }

    // Dışarıdan sarsıntı (ör. bina yıkılması, patlama). amount 0-1 arası, üst üste eklenir.
    // boost: normal sarsıntının kaç katı sert olsun (sarsıntı bitince 1'e döner)
    public void AddShake(float amount, float boost = 1f)
    {
        shakeTrauma = Mathf.Clamp01(shakeTrauma + amount);
        shakeBoost = Mathf.Max(shakeBoost, boost);
    }

    public void SetKnockdown(float amount)
    {
        knockdownAmount = Mathf.Clamp01(amount);
    }

    private void UpdateSprings()
    {
        float dt = Time.deltaTime;
        StepSpring(ref springPosY, ref springVelY, dt);
        StepSpring(ref springPitch, ref springPitchVel, dt);
        StepSpring(ref springRoll, ref springRollVel, dt);

        // Havadayken düşüş hızına göre kafa hafif aşağı bakar (yere hazırlanma refleksi)
        float targetAirPitch = 0f;
        if (!playerMovement.IsGrounded && playerRb.linearVelocity.y < 0f)
            targetAirPitch = Mathf.Min(-playerRb.linearVelocity.y * airPitchPerSpeed, airPitchMax);
        airPitch = Mathf.Lerp(airPitch, targetAirPitch, dt * 6f);

        // Atlama boyunca kafa önce öne eğilir/yana yatar, sona doğru toparlanır
        float targetVaultPitch = 0f;
        float targetVaultRoll = 0f;
        if (playerVault != null && playerVault.IsVaulting)
        {
            float arc = Mathf.Sin(playerVault.Progress * Mathf.PI);
            targetVaultPitch = arc * (playerVault.IsClimbing ? climbPitch : vaultPitch);
            targetVaultRoll = arc * vaultRoll * vaultRollSign;
        }
        vaultPitchOffset = Mathf.Lerp(vaultPitchOffset, targetVaultPitch, dt * 12f);
        vaultRollOffset = Mathf.Lerp(vaultRollOffset, targetVaultRoll, dt * 12f);

        fovPunch = Mathf.Lerp(fovPunch, 0f, dt * 8f);
        shakeTrauma = Mathf.Max(0f, shakeTrauma - dt * shakeDecay);
        if (shakeTrauma <= 0f)
            shakeBoost = 1f;
    }

    private void StepSpring(ref float value, ref float velocity, float dt)
    {
        // Sönümlü yay: büyük kare sürelerinde patlamasın diye adımı sınırla
        dt = Mathf.Min(dt, 0.033f);
        velocity += (-springStiffness * value - springDamping * velocity) * dt;
        value += velocity * dt;
    }

    private void HandleLook()
    {
        Vector2 rawLook = inputManager.LookInput;
        rawLook *= inputManager.IsGamepad ? (gamepadSensitivity * Time.deltaTime) : mouseSensitivity;
        currentLookDelta = Vector2.Lerp(currentLookDelta, rawLook, Time.deltaTime * lookSmoothness);

        yaw += currentLookDelta.x;
        lookPitch -= currentLookDelta.y;
        lookPitch = Mathf.Clamp(lookPitch, -maxPitch, maxPitch);

        if (hasFocus)
        {
            currentLookDelta = Vector2.zero;
            Vector3 dir = focusPoint - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float targetPitch = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                float t = Time.deltaTime * (focusSpeedOverride > 0f ? focusSpeedOverride : focusSpeed);
                yaw = Mathf.LerpAngle(yaw, targetYaw, t);
                lookPitch = Mathf.Lerp(lookPitch, Mathf.Clamp(targetPitch, -maxPitch, maxPitch), t);
            }
        }

        playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void HandleDynamicFOV()
    {
        if (cam == null) return;
        bool isSprinting = inputManager.IsSprinting && !playerMovement.IsCrouching && inputManager.MoveInput.y > 0;
        float targetFOV = isSprinting ? sprintFOV : normalFOV;

        if (!playerMovement.IsGrounded && playerRb.linearVelocity.y < 0f)
            targetFOV += fallFOVBoost * Mathf.Clamp01(-playerRb.linearVelocity.y / fallFOVMaxSpeed);

        float smoothness = fovSmoothness;
        if (fovOverride > 0f)
        {
            targetFOV = fovOverride;
            smoothness = fovOverrideSpeed;
        }

        baseFOV = Mathf.Lerp(baseFOV, targetFOV, Time.deltaTime * smoothness);
        cam.fieldOfView = baseFOV + fovPunch;
    }

    private void HandleHeadbobAndPosture()
    {
        // 1. Durum Tespiti
        bool isCrouch = playerMovement.IsCrouching;
        bool isSprint = inputManager.IsSprinting && !isCrouch && inputManager.MoveInput.y > 0;

        float targetBobSpeed = isCrouch ? crouchBobSpeed : (isSprint ? sprintBobSpeed : walkBobSpeed);
        float targetBobY = isCrouch ? crouchBobAmountY : (isSprint ? sprintBobAmountY : walkBobAmountY);
        float targetBobX = isCrouch ? crouchBobAmountX : (isSprint ? sprintBobAmountX : walkBobAmountX);

        // 2. Yükseklik Ayarı
        currentCameraY = Mathf.Lerp(currentCameraY, isCrouch ? crouchHeight : standHeight, Time.deltaTime * heightSmoothness);
        Vector3 newPos = transform.localPosition;
        newPos.y = currentCameraY;

        // 3. GERÇEK FİZİKSEL HIZ ORANTISI (Uçma sorununu çözen matematik)
        // Y eksenini(düşmeyi) saymadan sadece yatayda gerçekten ne kadar hızlı gittiğimize bakıyoruz
        float actualSpeed = new Vector3(playerRb.linearVelocity.x, 0, playerRb.linearVelocity.z).magnitude;

        // Olmamız gereken hıza ulaşıp ulaşmadığımızın oranı (Duvara takılırsan bu oran sıfıra yaklaşır)
        float speedRatio = Mathf.Clamp01(actualSpeed / playerMovement.CurrentExpectedSpeed);

        // Eğer karakter fiziken yerde hareket ediyorsa (havada adım atılmaz)
        if (playerMovement.IsGrounded && actualSpeed > 0.1f && inputManager.MoveInput.magnitude > 0.1f)
        {
            // Zamanlayıcıyı sadece gerçek hıza orantılı arttır!
            bobTimer += Time.deltaTime * (targetBobSpeed * speedRatio);

            float bobSin = Mathf.Sin(bobTimer);

            // Sarsıntı şiddetini de yavaşlamaya oranla yumuşatıyoruz
            newPos.y = currentCameraY + (bobSin * (targetBobY * speedRatio));
            newPos.x = Mathf.Cos(bobTimer / 2f) * (targetBobX * speedRatio);

            if (bobSin < -0.95f && !isStepSoundPlayed)
            {
                PlayDynamicFootstep(isCrouch, isSprint);
                isStepSoundPlayed = true;
            }
            else if (bobSin > 0f)
            {
                isStepSoundPlayed = false;
            }
        }
        else
        {
            bobTimer = 0;
            isStepSoundPlayed = false;
            newPos.y = Mathf.Lerp(newPos.y, currentCameraY, Time.deltaTime * 5f);
            newPos.x = Mathf.Lerp(newPos.x, 0f, Time.deltaTime * 5f);
        }

        // 4. Sarsıntı (trauma karesi ile: küçük değerlerde neredeyse hissedilmez, büyüklerde sert)
        float shake = shakeTrauma * shakeTrauma * shakeBoost;
        float t = Time.time * shakeFrequency;
        float shakePitch = (Mathf.PerlinNoise(shakeSeed, t) * 2f - 1f) * shakeMaxAngle * shake;
        float shakeYaw = (Mathf.PerlinNoise(shakeSeed + 1f, t) * 2f - 1f) * shakeMaxAngle * shake;
        float shakeRoll = (Mathf.PerlinNoise(shakeSeed + 2f, t) * 2f - 1f) * shakeMaxAngle * shake;
        newPos.x += (Mathf.PerlinNoise(shakeSeed + 3f, t) * 2f - 1f) * shakeMaxOffset * shake;

        newPos.y += springPosY;
        newPos.y = Mathf.Lerp(newPos.y, KnockdownEyeHeight, knockdownAmount);
        transform.localPosition = newPos;

        // 5. Yana Yatma (Tilt)
        float targetTilt = -inputManager.MoveInput.x * tiltAngle;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmooth);

        float finalPitch = Mathf.Clamp(lookPitch + springPitch + airPitch + shakePitch + vaultPitchOffset, -maxPitch - 10f, maxPitch + 10f);
        finalPitch = Mathf.Lerp(finalPitch, KnockdownPitch, knockdownAmount);
        float finalRoll = currentTilt + springRoll + shakeRoll + vaultRollOffset + KnockdownRoll * knockdownAmount;
        transform.localRotation = Quaternion.Euler(finalPitch, shakeYaw, finalRoll);
    }

    private void PlayDynamicFootstep(bool isCrouch, bool isSprint)
    {
        float targetVolume = isCrouch ? crouchVolume : (isSprint ? sprintVolume : walkVolume);
        AudioClip[] clips = footstepSounds;
        if (isCrouch && crouchFootstepSounds.Length > 0)
            clips = crouchFootstepSounds;
        else if (isSprint && sprintFootstepSounds.Length > 0)
            clips = sprintFootstepSounds;
        PlayClip(clips, targetVolume, 0.9f, 1.1f);
    }

    private void PlayClip(AudioClip[] clips, float volume, float minPitch, float maxPitch)
    {
        if (clips == null || clips.Length == 0 || footstepAudioSource == null) return;

        footstepAudioSource.pitch = Random.Range(minPitch, maxPitch);
        footstepAudioSource.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
    }
}
