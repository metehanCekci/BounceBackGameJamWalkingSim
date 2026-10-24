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
    public float normalHeight = 1.5f; 
    public float crouchHeight = 0.8f; 

    private Rigidbody rb;
    private RaycastHit slopeHit;
    private bool isGrounded;

    // Kameranın okuyabilmesi için mevcut hedeflenen hızı dışa açıyoruz
    public float CurrentExpectedSpeed { get; private set; } 

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = false; 
        rb.interpolation = RigidbodyInterpolation.Interpolate; 
    }

    void FixedUpdate()
    {
        if (inputManager == null) return;
        CheckGround();
        MovePlayer();
    }

    private void CheckGround()
    {
        float currentHeight = inputManager.IsCrouching ? crouchHeight : normalHeight;
        Vector3 boxHalfExtents = new Vector3(0.3f, 0.05f, 0.3f);
        float castDistance = (currentHeight * 0.5f) - boxHalfExtents.y + 0.15f;

        if (Physics.BoxCast(transform.position, boxHalfExtents, Vector3.down, out slopeHit, transform.rotation, castDistance))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            isGrounded = angle < maxSlopeAngle;
        }
        else
        {
            isGrounded = false;
        }
    }

    private Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }

    private void MovePlayer()
    {
        Vector3 moveDirection = transform.forward * inputManager.MoveInput.y + transform.right * inputManager.MoveInput.x;
        
        // Hız Durumu Karar Ağacı
        if (inputManager.IsCrouching) 
            CurrentExpectedSpeed = crouchSpeed;
        else if (inputManager.IsSprinting && inputManager.MoveInput.y > 0) // Sadece ileri giderken koşulabilir
            CurrentExpectedSpeed = sprintSpeed;
        else 
            CurrentExpectedSpeed = walkSpeed;

        if (isGrounded)
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            if (angle > 0)
            {
                rb.linearVelocity = GetSlopeMoveDirection(moveDirection) * CurrentExpectedSpeed;
            }
            else
            {
                rb.linearVelocity = new Vector3(moveDirection.x * CurrentExpectedSpeed, 0f, moveDirection.z * CurrentExpectedSpeed);
            }

            if (inputManager.MoveInput.magnitude == 0) rb.linearVelocity = Vector3.zero;
        }
        else
        {
            rb.linearVelocity = new Vector3(moveDirection.x * CurrentExpectedSpeed, rb.linearVelocity.y - (9.81f * Time.fixedDeltaTime * 2.5f), moveDirection.z * CurrentExpectedSpeed);
        }
    }
}
