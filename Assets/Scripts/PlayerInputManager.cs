using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool IsGamepad { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction crouchAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    void Awake()
    {
        moveAction = new InputAction("Move");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d")
            .With("Up", "<Gamepad>/leftStick/up")
            .With("Down", "<Gamepad>/leftStick/down")
            .With("Left", "<Gamepad>/leftStick/left")
            .With("Right", "<Gamepad>/leftStick/right");

        lookAction = new InputAction("Look");
        lookAction.AddBinding("<Pointer>/delta");
        lookAction.AddBinding("<Gamepad>/rightStick");

        crouchAction = new InputAction("Crouch", binding: "<Keyboard>/c");
        crouchAction.AddBinding("<Gamepad>/buttonEast"); 

        sprintAction = new InputAction("Sprint", binding: "<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/leftStickPress");

        jumpAction = new InputAction("Jump", binding: "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
    }

    void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        crouchAction.Enable();
        sprintAction.Enable();
        jumpAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        crouchAction.Disable();
        sprintAction.Disable();
        jumpAction.Disable();
    }

    public void ConsumeJump()
    {
        JumpPressed = false;
    }

    void Update()
    {
        MoveInput = moveAction.ReadValue<Vector2>();
        LookInput = lookAction.ReadValue<Vector2>();
        IsCrouching = crouchAction.IsPressed();
        IsSprinting = sprintAction.IsPressed();
        // FixedUpdate okuyana kadar basışı tut, yüksek FPS'te kaybolmasın
        if (jumpAction.WasPressedThisFrame())
            JumpPressed = true;
        JumpHeld = jumpAction.IsPressed();

        if (lookAction.activeControl != null)
        {
            IsGamepad = lookAction.activeControl.device is Gamepad;
        }
    }
}
