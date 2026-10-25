using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : MonoBehaviour
{
    // Kilitliyken (ör. E basılı tutulurken) hareket, bakış, koşu ve zıplama girişi yok sayılır
    public bool InputLocked { get; set; }
    // Doluysa karakter oyuncu yerine bu girişle yürür (cutscene'lerde otomatik yürüme)
    public Vector2? ScriptedMove { get; set; }
    public Vector2 MoveInput => ScriptedMove ?? (InputLocked ? Vector2.zero : rawMove);
    // Sadece fareyi kilitler, yürümek serbest kalır (ör. kamera bir şeye kilitliyken)
    public bool LookLocked { get; set; }
    public Vector2 LookInput => (InputLocked || LookLocked) ? Vector2.zero : rawLook;
    public bool IsGamepad { get; private set; }
    public bool IsCrouching { get; private set; }
    // Açıkken eğilme tuşu yok sayılır (ör. sadece yürüme ve koşmanın olduğu bölümler)
    public bool CrouchLocked { get; set; }
    // Doluysa karakter cutscene'de koşar
    public bool ScriptedSprint { get; set; }
    public bool IsSprinting => ScriptedSprint || (!InputLocked && rawSprint);
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction crouchAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private Vector2 rawMove;
    private Vector2 rawLook;
    private bool rawSprint;

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
        rawMove = moveAction.ReadValue<Vector2>();
        rawLook = lookAction.ReadValue<Vector2>();
        IsCrouching = !CrouchLocked && crouchAction.IsPressed();
        rawSprint = sprintAction.IsPressed();
        // FixedUpdate okuyana kadar basışı tut, yüksek FPS'te kaybolmasın
        if (jumpAction.WasPressedThisFrame() && !InputLocked)
            JumpPressed = true;
        JumpHeld = jumpAction.IsPressed();

        if (lookAction.activeControl != null)
        {
            IsGamepad = lookAction.activeControl.device is Gamepad;
        }
    }
}
