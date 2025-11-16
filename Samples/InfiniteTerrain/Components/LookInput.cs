using Prowl.Runtime;
using Prowl.Vector;

namespace InfiniteTerrain;

public class LookInput : MonoBehaviour
{
    private bool _cursorVisible = true;
    public bool CursorVisible
    {
        get
        {
            return _cursorVisible;
        }
        set
        {
            _cursorVisible = value;
            Input.SetCursorVisible(value, 0);
        }
    }

    // Input Actions
    private InputActionMap inputMap = null!;
    private InputAction moveAction = null!;
    private InputAction lookAction = null!;
    private InputAction gamepadLookAction = null!;
    private InputAction lookEnableAction = null!;
    private InputAction flyUpAction = null!;
    private InputAction flyDownAction = null!;
    private InputAction acceptAction = null!;
    private InputAction destroyAction = null!;

    // public getters
    public InputAction Movement => moveAction;
    public InputAction Look => lookAction;
    public InputAction GamepadLook => gamepadLookAction;
    public InputAction FlyUp => flyUpAction;
    public InputAction FlyDown => flyDownAction;
    public InputAction Accept => acceptAction;
    public InputAction Destroy => destroyAction;

    public override void OnEnable()
    {
        inputMap = new InputActionMap("Playground Game");

        // Movement (WASD + Gamepad)
        moveAction = inputMap.AddAction("Move", InputActionType.Value);
        moveAction.ExpectedValueType = typeof(Double2);

        // WASD
        moveAction.AddBinding(new Vector2CompositeBinding(
            InputBinding.CreateKeyBinding(KeyCode.S),
            InputBinding.CreateKeyBinding(KeyCode.W),
            InputBinding.CreateKeyBinding(KeyCode.A),
            InputBinding.CreateKeyBinding(KeyCode.D),
            true
        ));

        // JOYSTICK
        var leftStick = InputBinding.CreateGamepadAxisBinding(0, deviceIndex: 0);
        leftStick.Processors.Add(new DeadzoneProcessor(0.15f));
        leftStick.Processors.Add(new NormalizeProcessor());
        moveAction.AddBinding(leftStick);

        // Look (Gamepad)
        gamepadLookAction = inputMap.AddAction("GamepadLook", InputActionType.Value);
        gamepadLookAction.ExpectedValueType = typeof(Double2);
        var rightStick = InputBinding.CreateGamepadAxisBinding(1, deviceIndex: 0);
        rightStick.Processors.Add(new DeadzoneProcessor(0.15f));
        rightStick.Processors.Add(new ScaleProcessor(0.1f));
        rightStick.Processors.Add(new NormalizeProcessor());
        gamepadLookAction.AddBinding(rightStick);

        // Look (Mouse + Gamepad)
        lookAction = inputMap.AddAction("Look", InputActionType.Value);
        lookAction.ExpectedValueType = typeof(Double2);
        var mouse = new DualAxisCompositeBinding(
            InputBinding.CreateMouseAxisBinding(0),
            InputBinding.CreateMouseAxisBinding(1));
        mouse.Processors.Add(new ScaleProcessor(0.1f));
        lookAction.AddBinding(mouse);
        lookAction.AddBinding(rightStick);

        // Fly Up/Down
        flyUpAction = inputMap.AddAction("FlyUp", InputActionType.Button);
        flyUpAction.AddBinding(KeyCode.E);
        flyUpAction.AddBinding(GamepadButton.RightBumper);
        flyDownAction = inputMap.AddAction("FlyDown", InputActionType.Button);
        flyDownAction.AddBinding(KeyCode.Q);
        flyDownAction.AddBinding(GamepadButton.LeftBumper);

        // Enable or disable the mouse
        lookEnableAction = inputMap.AddAction("LookEnable", InputActionType.Button);
        lookEnableAction.AddBinding(KeyCode.Escape);
        lookEnableAction.AddBinding(GamepadButton.Back);

        // Click with left mouse pointer or A on gamepad
        acceptAction = inputMap.AddAction("Accept", InputActionType.Button);
        acceptAction.AddBinding(MouseButton.Left);
        acceptAction.AddBinding(GamepadButton.A);

        // Click with right mouse pointer or B on gamepad
        destroyAction = inputMap.AddAction("Destroy", InputActionType.Button);
        destroyAction.AddBinding(MouseButton.Right);
        destroyAction.AddBinding(GamepadButton.B);

        // finish setup
        Input.RegisterActionMap(inputMap);
        inputMap.Enable();
    }

    public override void Update()
    {
        // Toggle the mouse
        if (lookEnableAction.WasPressedThisFrame())
        {
            CursorVisible = !CursorVisible;
        }
    }
}