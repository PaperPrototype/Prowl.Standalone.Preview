using Prowl.Runtime;
using Prowl.Vector;

namespace InfiniteTerrain;

[RequireComponent(typeof(LookInput))]
public class LookCamera : MonoBehaviour
{
    private const double METERS_PER_SECOND = 10;
    private const double ANGLES_PER_SECOND = 100;
    private const double VIRTUAL_MOUSE_PIXELS_PER_SECOND = 1000;

    private LookInput? inputComponent = null;

    public override void OnEnable()
    {
        inputComponent = GetComponent<LookInput>();
    }

    public override void Update()
    {
        if (inputComponent is null) return;

        // Gamepad virtual mouse movement
        Double2 gamepadLook = inputComponent.GamepadLook.ReadValue<Double2>() * Time.DeltaTime * 1000f;
        if (inputComponent.CursorVisible)
        {
            if (Math.Abs(gamepadLook.X) > 0.01f || Math.Abs(gamepadLook.Y) > 0.01f)
            {
                Input.MousePosition += (Int2)Maths.Round(gamepadLook * Time.DeltaTime * VIRTUAL_MOUSE_PIXELS_PER_SECOND);
            }
            return;
        }

        // Camera movement
        Double3 movement = Double3.Zero;
        Double2 axis = inputComponent.Movement.ReadValue<Double2>();
        movement += Transform.Forward * -axis.Y * Time.DeltaTime * METERS_PER_SECOND;
        movement += Transform.Right * axis.X * Time.DeltaTime * METERS_PER_SECOND;
        if (inputComponent.FlyUp.IsPressed()) movement += Transform.Up * Time.DeltaTime * METERS_PER_SECOND;
        if (inputComponent.FlyDown.IsPressed()) movement -= Transform.Up * Time.DeltaTime * METERS_PER_SECOND;

        // Apply camera movement
        Transform.Position += movement;

        // Camera look
        Double2 lookInput = inputComponent.Look.ReadValue<Double2>() * Time.DeltaTime * 1000f;
        if (Math.Abs(lookInput.X) > 0.01f || Math.Abs(lookInput.Y) > 0.01f)
        {
            Transform.LocalEulerAngles += new Double3(lookInput.Y, lookInput.X, 0) * Time.DeltaTime * ANGLES_PER_SECOND;
        }
    }
}