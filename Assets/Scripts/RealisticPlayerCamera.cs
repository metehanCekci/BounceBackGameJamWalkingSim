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
    public AudioClip[] footstepSounds; 
    [Range(0f, 1f)] public float crouchVolume = 0.15f;
    [Range(0f, 1f)] public float walkVolume = 0.4f;
    [Range(0f, 1f)] public float sprintVolume = 0.7f;

    [Header("Görüş Açısı (FOV)")]
    public float normalFOV = 60f;
    public float sprintFOV = 68f; // Koşunca hafifçe geri çekilen kamera efekti
    public float fovSmoothness = 4f;

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

    private float lookPitch;
    private float yaw;
    private float bobTimer;
    private float currentTilt;
    private float currentCameraY; 
    private bool isStepSoundPlayed = false;
    private Vector2 currentLookDelta;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        currentCameraY = standHeight;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (playerBody != null)
            yaw = playerBody.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (inputManager == null || playerBody == null || playerRb == null || playerMovement == null) return;

        HandleLook();
        HandleDynamicFOV();
        HandleHeadbobAndPosture();
    }

    private void HandleLook()
    {
        Vector2 rawLook = inputManager.LookInput;
        rawLook *= inputManager.IsGamepad ? (gamepadSensitivity * Time.deltaTime) : mouseSensitivity;
        currentLookDelta = Vector2.Lerp(currentLookDelta, rawLook, Time.deltaTime * lookSmoothness);

        yaw += currentLookDelta.x;
        lookPitch -= currentLookDelta.y;
        lookPitch = Mathf.Clamp(lookPitch, -maxPitch, maxPitch);

        playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void HandleDynamicFOV()
    {
        if (cam == null) return;
        bool isSprinting = inputManager.IsSprinting && !inputManager.IsCrouching && inputManager.MoveInput.y > 0;
        float targetFOV = isSprinting ? sprintFOV : normalFOV;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovSmoothness);
    }

    private void HandleHeadbobAndPosture()
    {
        // 1. Durum Tespiti
        bool isCrouch = inputManager.IsCrouching;
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

        // Eğer karakter fiziken hareket ediyorsa (sürtünme veya normal yürüyüş)
        if (actualSpeed > 0.1f && inputManager.MoveInput.magnitude > 0.1f)
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

        transform.localPosition = newPos;

        // 4. Yana Yatma (Tilt)
        float targetTilt = -inputManager.MoveInput.x * tiltAngle;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmooth);
        transform.localRotation = Quaternion.Euler(lookPitch, 0f, currentTilt);
    }

    private void PlayDynamicFootstep(bool isCrouch, bool isSprint)
    {
        if (footstepSounds.Length == 0 || footstepAudioSource == null) return;
        
        float targetVolume = isCrouch ? crouchVolume : (isSprint ? sprintVolume : walkVolume);
        footstepAudioSource.volume = targetVolume;
        footstepAudioSource.pitch = Random.Range(0.9f, 1.1f);
        footstepAudioSource.PlayOneShot(footstepSounds[Random.Range(0, footstepSounds.Length)]);
    }
}
