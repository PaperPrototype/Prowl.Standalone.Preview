// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace CarPhysicsDemo;

internal class Program
{
    static void Main(string[] args)
    {
        new CarPhysicsGame().Run("Car Physics Demo - WheelCollider", 1280, 720);
    }
}

public sealed class CarPhysicsGame : Game
{
    private Scene? scene;
    private Material? standardMaterial;
    private GameObject? carGO;
    private GameObject? cameraGO;
    private ThirdPersonCamera? thirdPersonCamera;
    private CarController? carController;

    public override void Initialize()
    {
        DrawGizmos = true;
        scene = new Scene();

        // Create directional light
        GameObject lightGO = new("Directional Light");
        DirectionalLight light = lightGO.AddComponent<DirectionalLight>();
        light.ShadowQuality = ShadowQuality.Soft;
        light.ShadowBias = 0.5f;
        lightGO.Transform.LocalEulerAngles = new Double3(-45, 45, 0);
        scene.Add(lightGO);

        // Create camera
        GameObject cam = new("Main Camera");
        cam.Tag = "Main Camera";
        cam.Transform.Position = new(0, 5, -10);
        Camera camera = cam.AddComponent<Camera>();
        camera.Depth = -1;
        camera.HDR = true;
        cameraGO = cam;

        camera.Effects =
        [
            new FXAAEffect(),
            new KawaseBloomEffect(),
            new TonemapperEffect(),
        ];

        scene.Add(cam);

        // Create shared material
        standardMaterial = new Material(Shader.LoadDefault(DefaultShader.Standard));

        // Create large ground with ramps and bumps
        CreateGround();
        CreateRampsAndObstacles();

        // Create car
        CreateCar();

        // Setup third-person camera
        thirdPersonCamera = cam.AddComponent<ThirdPersonCamera>();
        thirdPersonCamera.Target = carGO;
        thirdPersonCamera.Distance = 10f;
        thirdPersonCamera.Height = 3f;
        thirdPersonCamera.Smoothness = 5f;

        scene.Activate();

        Debug.Log("Controls:");
        Debug.Log("  WASD - Drive car");
        Debug.Log("  Space - Brake");
        Debug.Log("  Right Mouse - Look around camera");
        Debug.Log("  R - Reset car position");
    }

    private void CreateGround()
    {
        GameObject ground = new("Ground");
        MeshRenderer groundRenderer = ground.AddComponent<MeshRenderer>();
        groundRenderer.Mesh = Mesh.CreateCube(new Double3(200, 1, 200));
        groundRenderer.Material = standardMaterial;
        ground.Transform.Position = new Double3(0, -0.5, 0);

        Rigidbody3D groundRigidbody = ground.AddComponent<Rigidbody3D>();
        groundRigidbody.IsStatic = true;
        BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
        groundCollider.Size = new Double3(200, 1, 200);

        scene.Add(ground);
    }

    private void CreateRampsAndObstacles()
    {
        // Create several ramps at different angles
        CreateRamp(new Double3(-20, 0, 30), new Double3(10, 0.5, 15), 15); // Gentle ramp
        CreateRamp(new Double3(20, 0, 30), new Double3(10, 0.5, 15), 30); // Steeper ramp
        CreateRamp(new Double3(0, 0, -40), new Double3(15, 0.5, 10), -20); // Ramp going down

        // Create bumps scattered around
        CreateBump(new Double3(-30, 0, 0), 2f, new Color(0.7f, 0.5f, 0.3f, 1.0f));
        CreateBump(new Double3(-25, 0, -10), 1.5f, new Color(0.6f, 0.6f, 0.4f, 1.0f));
        CreateBump(new Double3(30, 0, 5), 2.5f, new Color(0.8f, 0.4f, 0.3f, 1.0f));
        CreateBump(new Double3(35, 0, -5), 1.8f, new Color(0.5f, 0.5f, 0.5f, 1.0f));
        CreateBump(new Double3(0, 0, 20), 2.2f, new Color(0.7f, 0.6f, 0.4f, 1.0f));

        // Create some walls/obstacles
        CreateWall(new Double3(-50, 1, 0), new Double3(2, 3, 20), new Color(0.6f, 0.3f, 0.2f, 1.0f));
        CreateWall(new Double3(50, 1, 0), new Double3(2, 3, 20), new Color(0.6f, 0.3f, 0.2f, 1.0f));
        CreateWall(new Double3(0, 1, -60), new Double3(40, 3, 2), new Color(0.6f, 0.3f, 0.2f, 1.0f));
    }

    private void CreateRamp(Double3 position, Double3 size, double angleDegrees)
    {
        GameObject ramp = new("Ramp");
        MeshRenderer rampRenderer = ramp.AddComponent<MeshRenderer>();
        rampRenderer.Mesh = Mesh.CreateCube(size);
        rampRenderer.Material = standardMaterial;
        //rampRenderer.MainColor = new Color(0.5f, 0.5f, 0.6f, 1.0f);

        ramp.Transform.Position = position + new Double3(0, size.Y * Math.Sin(angleDegrees * Math.PI / 180.0) / 2, 0);
        ramp.Transform.LocalEulerAngles = new Double3(angleDegrees, 0, 0);

        Rigidbody3D rampRigidbody = ramp.AddComponent<Rigidbody3D>();
        rampRigidbody.IsStatic = true;
        BoxCollider rampCollider = ramp.AddComponent<BoxCollider>();
        rampCollider.Size = size;

        scene.Add(ramp);
    }

    private void CreateBump(Double3 position, double radius, Color color)
    {
        GameObject bump = new("Bump");
        MeshRenderer bumpRenderer = bump.AddComponent<MeshRenderer>();
        bumpRenderer.Mesh = Mesh.CreateSphere((float)radius, 16, 16);
        bumpRenderer.Material = standardMaterial;
        //bumpRenderer.MainColor = color;

        bump.Transform.Position = position + new Double3(0, radius / 2, 0);

        Rigidbody3D bumpRigidbody = bump.AddComponent<Rigidbody3D>();
        bumpRigidbody.IsStatic = true;
        SphereCollider bumpCollider = bump.AddComponent<SphereCollider>();
        bumpCollider.Radius = radius;

        scene.Add(bump);
    }

    private void CreateWall(Double3 position, Double3 size, Color color)
    {
        GameObject wall = new("Wall");
        MeshRenderer wallRenderer = wall.AddComponent<MeshRenderer>();
        wallRenderer.Mesh = Mesh.CreateCube(size);
        wallRenderer.Material = standardMaterial;
        //wallRenderer.MainColor = color;

        wall.Transform.Position = position;

        Rigidbody3D wallRigidbody = wall.AddComponent<Rigidbody3D>();
        wallRigidbody.IsStatic = true;
        BoxCollider wallCollider = wall.AddComponent<BoxCollider>();
        wallCollider.Size = size;

        scene.Add(wall);
    }

    private void CreateCar()
    {
        // Create car body
        carGO = new GameObject("Car");
        MeshRenderer carRenderer = carGO.AddComponent<MeshRenderer>();
        carRenderer.Mesh = Mesh.CreateCube(new Double3(2, 0.5, 4));
        carRenderer.Material = standardMaterial;
        //carRenderer.MainColor = new Color(0.8f, 0.2f, 0.2f, 1.0f); // Red car

        carGO.Transform.Position = new Double3(0, 2, 0);

        // Add rigidbody
        Rigidbody3D carRigidbody = carGO.AddComponent<Rigidbody3D>();
        carRigidbody.Mass = 100.0; // Heavy car
        carRigidbody.LinearDamping = 0.0001f;
        carRigidbody.AngularDamping = 0.0001f;

        // Add collider for car body
        BoxCollider carCollider = carGO.AddComponent<BoxCollider>();
        carCollider.Size = new Double3(2, 0.5, 4);

        // Add car controller
        carController = carGO.AddComponent<CarController>();

        scene.Add(carGO);

        // Create 4 wheels as children
        CreateWheel("FrontLeft", new Double3(-1.0, -0.5, 1.5), carGO, true);
        CreateWheel("FrontRight", new Double3(1.0, -0.5, 1.5), carGO, true);
        CreateWheel("RearLeft", new Double3(-1.0, -0.5, -1.5), carGO, false);
        CreateWheel("RearRight", new Double3(1.0, -0.5, -1.5), carGO, false);
    }

    private void CreateWheel(string name, Double3 localPosition, GameObject parent, bool isFront)
    {
        GameObject wheel = new(name);
        wheel.Transform.Parent = parent.Transform;
        wheel.Transform.LocalPosition = localPosition;

        // Visual representation of wheel
        //MeshRenderer wheelRenderer = wheel.AddComponent<MeshRenderer>();
        //wheelRenderer.Mesh = Mesh.CreateCylinder(0.4f, 0.3f, 16);
        //wheelRenderer.Material = standardMaterial;
        //wheelRenderer.MainColor = new Color(0.2f, 0.2f, 0.2f, 1.0f); // Dark wheels

        // Rotate cylinder to align with wheel direction
        //wheel.Transform.LocalEulerAngles = new Double3(0, 0, 90);

        // Add wheel collider
        WheelCollider wheelCollider = wheel.AddComponent<WheelCollider>();
        wheelCollider.Radius = 0.5f;
        wheelCollider.SuspensionTravel = 0.5f;
        wheelCollider.SideFriction = 2.0f;
        wheelCollider.ForwardFriction = 6.0f;

        wheelCollider.AdjustWheelValues();

        //const float dampingFrac = 0.8f;
        //const float springFrac = 0.45f;
        //const float carMass = 100f;
        //const float carMassSplitPerWheel = carMass / 4f;
        //const float wheelMass = 100f * 0.03f;
        //wheelCollider.WheelInertia = 0.5f * (0.5f * 0.5f) * wheelMass;
        //wheelCollider.SuspensionStiffness = carMassSplitPerWheel * (float)Double3.Length(wheel.Scene.Physics.Gravity) / (wheelCollider.SuspensionTravel * springFrac);
        //wheelCollider.SuspensionDamping = 2.0f * (float)Math.Sqrt(wheelCollider.SuspensionStiffness * carMass) * 0.25f * dampingFrac;

        scene.Add(wheel);

        // Register wheel with car controller
        if (carController != null)
        {
            if (isFront)
                carController.frontWheels.Add(wheelCollider);
            else
                carController.rearWheels.Add(wheelCollider);
        }
    }

    public override void EndUpdate()
    {
        //// Reset car if R is pressed
        //if (Input.GetKeyDown(KeyCode.R))
        //{
        //    carGO.Transform.Position = new Double3(0, 2, 0);
        //    carGO.Transform.Rotation = Quaternion.Identity;
        //
        //    var rb = carGO.GetComponent<Rigidbody3D>();
        //    if (rb != null)
        //    {
        //        rb.LinearVelocity = Double3.Zero;
        //        rb.AngularVelocity = Double3.Zero;
        //    }
        //
        //    Debug.Log("Car reset");
        //}
    }
}

// Third-person camera controller that follows the car
public class ThirdPersonCamera : MonoBehaviour
{
    public GameObject? Target;
    public double Distance = 10.0;
    public double Height = 3.0;
    public double Smoothness = 5.0;
    public double MouseSensitivity = 0.1;

    private double currentYaw = 0;
    private double currentPitch = 20;

    public override void Update()
    {
        if (Target == null || !Target.IsValid()) return;

        // Mouse look
        if (Input.GetMouseButton(1))
        {
            Double2 mouseDelta = Input.MouseDelta;
            currentYaw += mouseDelta.X * MouseSensitivity;
            currentPitch -= mouseDelta.Y * MouseSensitivity;
            currentPitch = Math.Clamp(currentPitch, -30, 60); // Limit pitch
        }

        // Calculate desired camera position
        double yawRad = currentYaw * Math.PI / 180.0;
        double pitchRad = currentPitch * Math.PI / 180.0;

        double x = Math.Sin(yawRad) * Math.Cos(pitchRad);
        double y = Math.Sin(pitchRad);
        double z = Math.Cos(yawRad) * Math.Cos(pitchRad);

        Double3 direction = new Double3(x, y, z);
        Double3 offset = -direction * Distance + new Double3(0, Height, 0);
        Double3 desiredPosition = Target.Transform.Position + offset;

        // Smooth camera movement (manual lerp)
        double lerpFactor = Math.Clamp(Smoothness * Time.DeltaTime, 0, 1);
        Transform.Position = Transform.Position + (desiredPosition - Transform.Position) * lerpFactor;

        // Look at target
        Double3 lookDirection = Target.Transform.Position + new Double3(0, 1, 0) - Transform.Position;
        if (Double3.LengthSquared(lookDirection) > 0.001)
        {
            Transform.Rotation = Quaternion.LookRotation(lookDirection, Double3.UnitY);
        }
    }
}

// Car controller using WheelColliders
public class CarController : MonoBehaviour
{
    public List<WheelCollider> frontWheels = new();
    public List<WheelCollider> rearWheels = new();

    public double MaxSteerAngle = 30.0; // degrees
    public double MotorTorque = 3500.0;
    public double BrakeTorque = 100.0;

    private Rigidbody3D? rigidbody;
    private bool wheelsAdjusted = false;

    public override void OnEnable()
    {
        rigidbody = GetComponent<Rigidbody3D>();
    }

    //private void AdjustAllWheels()
    //{
    //    if (wheelsAdjusted) return;
    //    wheelsAdjusted = true;
    //
    //    foreach (var wheel in frontWheels)
    //    {
    //        if (wheel != null && wheel.IsValid())
    //            wheel.AdjustWheelValues();
    //    }
    //    foreach (var wheel in rearWheels)
    //    {
    //        if (wheel != null && wheel.IsValid())
    //            wheel.AdjustWheelValues();
    //    }
    //}

    public override void Update()
    {
        if (rigidbody == null) return;

        //// Adjust wheels on first update
        //AdjustAllWheels();

        // Get input
        double steering = 0;
        double throttle = 0;
        bool brake = false;

        if (Input.GetKey(KeyCode.A)) steering = -1;
        if (Input.GetKey(KeyCode.D)) steering = 1;
        if (Input.GetKey(KeyCode.W)) throttle = 1;
        if (Input.GetKey(KeyCode.S)) throttle = -1;
        if (Input.GetKey(KeyCode.Space)) brake = true;

        // Apply steering to front wheels
        float steerAngle = (float)(steering * MaxSteerAngle * Math.PI / 180.0);
        foreach (var wheel in frontWheels)
        {
            if (wheel != null && wheel.IsValid())
            {
                wheel.SteerAngle = steerAngle;
            }
        }

        // Apply motor torque to rear wheels
        float motorTorque = (float)(throttle * MotorTorque);
        foreach (var wheel in rearWheels)
        {
            if (wheel != null && wheel.IsValid())
            {
                wheel.AddTorque(motorTorque * (float)Time.DeltaTime);
            }
        }

        // Apply brake to all wheels
        if (brake)
        {
            foreach (var wheel in frontWheels)
            {
                if (wheel != null && wheel.IsValid())
                {
                    wheel.AddTorque(-wheel.AngularVelocity * (float)BrakeTorque * (float)Time.DeltaTime);
                }
            }
            foreach (var wheel in rearWheels)
            {
                if (wheel != null && wheel.IsValid())
                {
                    wheel.AddTorque(-wheel.AngularVelocity * (float)BrakeTorque * (float)Time.DeltaTime);
                }
            }
        }

        // Note: Wheel adjustment is called automatically by WheelCollider.AdjustWheelValues() on setup
    }
}
