using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Referanslar")]
    public PlayerInputManager inputManager;

    [Header("Hareket Hızları")]
    public float walkSpeed = 3.0f;
    public float crouchSpeed = 1.5f;
    public float sprintSpeed = 4.8f; // Çocuk olduğu için abartı bir koşu değil, hafif depar

    [Header("Zemin & Fizik")]
    public float maxSlopeAngle = 50f;

    [Header("Eğilme")]
    [Range(0.3f, 1f)] public float crouchHeightRatio = 0.65f; // Eğilince collider boyu, ayakta boyun bu oranı kadar
    public float crouchTransitionSpeed = 6f; // Collider boyunun değişme hızı (birim/sn)

    [Header("Zıplama")]
    public float jumpHeight = 1.4f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Havada Kontrol & İniş")]
    [Range(0f, 1f)] public float airControl = 0.06f; // Havada yön değiştirmek zor olsun, momentum korunur
    public float minLandingSpeed = 2f;   // Bunun altındaki düşüşler iniş efekti tetiklemez
    public float hardLandingSpeed = 7f;  // Bu hızda inişte efektler en sert halini alır
    public float landingSlowDuration = 0.35f;
    [Range(0f, 1f)] public float landingSlowAmount = 0.5f; // Sert inişte anlık hız kaybı

    private Rigidbody rb;
    private Collider col;
    private CapsuleCollider capsule; // Eğilince boyu değişecek collider
    private float standColliderHeight;
    private Vector3 standColliderCenter;
    private bool isCrouched;
    private PlayerVault vault; // İsteğe bağlı, yoksa zıplama normal çalışır
    private RaycastHit slopeHit;
    private bool isGrounded;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float jumpGroundIgnoreTimer; // Zıpladıktan hemen sonra zemin kontrolü bizi yere yapıştırmasın
    private float lowestAirVelocityY;
    private float landingSlowTimer;
    private float landingSlowStrength;

    // Kameranın okuyabilmesi için mevcut hedeflenen hızı dışa açıyoruz
    public float CurrentExpectedSpeed { get; private set; }
    public bool IsGrounded => isGrounded;
    // Tuş bırakılsa bile tavan alçaksa eğik kalırız, kamera tuşu değil bunu okur
    public bool IsCrouching => isCrouched;

    // Kamera ve ses efektleri bunlara abone olur
    public event Action OnJumped;
    public event Action<float> OnLanded; // 0-1 arası iniş sertliği
    public event Action OnTeleported;    // Kamera bakışını yeni yöne oturtur

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        vault = GetComponent<PlayerVault>();
        capsule = col as CapsuleCollider;
        if (capsule != null)
        {
            standColliderHeight = capsule.height;
            standColliderCenter = capsule.center;
        }

        // Duvara sürtünüp havada asılı kalmayalım; yerde durmayı zaten hareket kodu sağlıyor
        col.material = new PhysicsMaterial("PlayerNoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        if (inputManager == null)
            inputManager = GetComponent<PlayerInputManager>();
        if (inputManager == null)
            inputManager = FindAnyObjectByType<PlayerInputManager>();

        rb.freezeRotation = true;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void FixedUpdate()
    {
        if (inputManager == null) return;

        // Engel atlarken hareketi PlayerVault yönetir
        if (vault != null && vault.IsVaulting)
        {
            isGrounded = false; // Kamera adım sallantısı/sesi yapmasın
            lowestAirVelocityY = 0f; // Atlama bitince iniş efekti tetiklenmesin, onu vault kendisi yapar
            inputManager.ConsumeJump(); // Atlarken basılan tuş inince zıplatmasın
            return;
        }

        UpdateCrouch();
        CheckGround();
        UpdateJumpTimers();
        if (vault != null && vault.IsVaulting) return;
        MovePlayer();
    }

    // Ölüp başa dönerken: hız, zıplama/iniş durumu ve yarım kalan atlama sıfırlanır
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        if (vault != null)
            vault.Cancel();

        // Interpolation açık; ikisini birden vermezsek eski yerden kayarak gelir
        transform.SetPositionAndRotation(position, rotation);
        rb.position = position;
        rb.rotation = rotation;
        rb.linearVelocity = Vector3.zero;

        isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        jumpGroundIgnoreTimer = 0f;
        lowestAirVelocityY = 0f;
        landingSlowTimer = 0f;
        if (inputManager != null)
            inputManager.ConsumeJump();

        OnTeleported?.Invoke();
    }

    private void UpdateCrouch()
    {
        bool wantsCrouch = inputManager.IsCrouching;
        // Tuşu bıraktık ama başımızın üstünde yer yoksa eğik kalmaya devam
        if (!wantsCrouch && isCrouched && !CanStandUp())
            wantsCrouch = true;
        isCrouched = wantsCrouch;

        if (capsule == null) return;

        // Ayaklar yerinde kalsın diye boy kısalırken merkezi de aşağı kaydır
        float targetHeight = isCrouched ? standColliderHeight * crouchHeightRatio : standColliderHeight;
        capsule.height = Mathf.MoveTowards(capsule.height, targetHeight, crouchTransitionSpeed * Time.fixedDeltaTime);
        capsule.center = standColliderCenter + Vector3.down * ((standColliderHeight - capsule.height) * 0.5f);
    }

    private bool CanStandUp()
    {
        if (capsule == null) return true;

        Bounds bounds = col.bounds;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.95f;
        float standHeight = standColliderHeight * transform.lossyScale.y;
        Vector3 feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Vector3 bottom = feet + Vector3.up * (radius + 0.05f);
        Vector3 top = feet + Vector3.up * (standHeight - radius);

        foreach (Collider other in Physics.OverlapCapsule(bottom, top, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (other != col) return false;
        }
        return true;
    }

    private void CheckGround()
    {
        bool wasGrounded = isGrounded;

        // Collider'ın gerçek (scale dahil) boyutunu kullan, inspector değerine güvenme
        Bounds bounds = col.bounds;
        Vector3 boxHalfExtents = new Vector3(bounds.extents.x * 0.9f, 0.05f, bounds.extents.z * 0.9f);
        float castDistance = bounds.extents.y - boxHalfExtents.y + 0.15f;

        if (jumpGroundIgnoreTimer > 0f)
        {
            jumpGroundIgnoreTimer -= Time.fixedDeltaTime;
            isGrounded = false;
        }
        // Trigger'lar (görev alanları, tünel Volume'u) zemin sayılmasın, üstlerinde havada durmayalım
        else if (Physics.BoxCast(bounds.center, boxHalfExtents, Vector3.down, out slopeHit, transform.rotation, castDistance,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            isGrounded = angle < maxSlopeAngle;
        }
        else
        {
            isGrounded = false;
        }

        if (!isGrounded)
        {
            // Havadayken ulaşılan en yüksek düşüş hızını tut (temas anında Rigidbody hızı sıfırlanmış olabilir)
            lowestAirVelocityY = Mathf.Min(lowestAirVelocityY, rb.linearVelocity.y);
        }
        else if (!wasGrounded)
        {
            HandleLanding(-lowestAirVelocityY);
            lowestAirVelocityY = 0f;
        }
    }

    private void HandleLanding(float impactSpeed)
    {
        if (impactSpeed < minLandingSpeed) return;

        float severity = Mathf.InverseLerp(minLandingSpeed, hardLandingSpeed, impactSpeed);
        landingSlowStrength = landingSlowAmount * severity;
        landingSlowTimer = landingSlowDuration * Mathf.Lerp(0.4f, 1f, severity);
        OnLanded?.Invoke(severity);
    }

    private Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }

    private void UpdateJumpTimers()
    {
        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.fixedDeltaTime;
        }

        if (inputManager.JumpPressed)
        {
            jumpBufferTimer = jumpBufferTime;
            inputManager.ConsumeJump();
        }
        else
        {
            jumpBufferTimer -= Time.fixedDeltaTime;
        }

        // Önümüzde atlanabilir engel varsa zıplamak yerine üstünden geç
        if (jumpBufferTimer > 0f && vault != null && vault.TryStartVault())
        {
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            return;
        }

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
            // Eğilirken zıplama daha zayıf
            if (isCrouched)
                jumpVelocity *= 0.75f;

            Vector3 velocity = rb.linearVelocity;
            velocity.y = jumpVelocity;
            rb.linearVelocity = velocity;

            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            jumpGroundIgnoreTimer = 0.15f;
            isGrounded = false;
            lowestAirVelocityY = 0f;
            OnJumped?.Invoke();
        }
    }

    private void MovePlayer()
    {
        Vector3 moveDirection = transform.forward * inputManager.MoveInput.y + transform.right * inputManager.MoveInput.x;

        if (isCrouched)
            CurrentExpectedSpeed = crouchSpeed;
        else if (inputManager.IsSprinting && inputManager.MoveInput.y > 0)
            CurrentExpectedSpeed = sprintSpeed;
        else
            CurrentExpectedSpeed = walkSpeed;

        // Sert inişten sonra bacaklar toparlanana kadar yavaşla
        if (landingSlowTimer > 0f)
        {
            landingSlowTimer -= Time.fixedDeltaTime;
            float recovery = 1f - Mathf.Clamp01(landingSlowTimer / landingSlowDuration);
            CurrentExpectedSpeed *= Mathf.Lerp(1f - landingSlowStrength, 1f, recovery);
        }

        Vector3 targetVelocity;

        if (isGrounded)
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            Vector3 slopeMoveDirection = angle > 0 ? GetSlopeMoveDirection(moveDirection) : moveDirection;

            targetVelocity = new Vector3(
                slopeMoveDirection.x * CurrentExpectedSpeed,
                Mathf.Max(0f, rb.linearVelocity.y),
                slopeMoveDirection.z * CurrentExpectedSpeed);

            if (inputManager.MoveInput.magnitude == 0)
            {
                targetVelocity = new Vector3(0f, targetVelocity.y, 0f);
                // Sürtünmesiz olduğumuz için yokuşta dururken aşağı kaymayalım
                rb.AddForce(-Physics.gravity, ForceMode.Acceleration);
            }

            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, 0.18f);
        }
        else
        {
            // Havada momentum korunur, tuşsuzken yavaşlamaz; tuşla sadece hafifçe yön verilir
            Vector3 horizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                Vector3 targetHorizontal = moveDirection * CurrentExpectedSpeed;
                horizontal = Vector3.Lerp(horizontal, targetHorizontal, airControl);
            }
            rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
        }
    }
}
