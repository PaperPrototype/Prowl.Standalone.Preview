// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Runtime.ParticleSystem;
using Prowl.Runtime.ParticleSystem.Modules;
using Prowl.Runtime.Terrain;
using Prowl.Vector;

namespace PhysicsCubes;

internal class Program
{
    static void Main(string[] args)
    {
        new PhysicsDemo().Run("Physics Demo", 1280, 720);
    }
}

public sealed class PhysicsDemo : Game
{
    private GameObject? cameraGO;
    private Scene? scene;
    private double selectedCubeMass = 1.0;
    private Material? standardMaterial;
    private GameObject? particleSystemGO;

    public override void Initialize()
    {
        //DrawGizmos = true;
        scene = new Scene();

        // Create directional light
        GameObject lightGO = new("Directional Light");
        DirectionalLight light = lightGO.AddComponent<DirectionalLight>();
        light.ShadowQuality = ShadowQuality.Soft;
        lightGO.Transform.LocalEulerAngles = new Double3(-45, 45, 0);
        scene.Add(lightGO);

        // Create camera
        GameObject cam = new("Main Camera");
        cam.Tag = "Main Camera";
        cam.Transform.Position = new(0, 5, -15);
        cam.Transform.LocalEulerAngles = new Double3(15, 0, 0);
        Camera camera = cam.AddComponent<Camera>();
        camera.Depth = -1;
        camera.HDR = true;
        cameraGO = cam;

        camera.Effects =
        [
            new FXAAEffect(),
            new BokehDepthOfFieldEffect(),
            new KawaseBloomEffect(),
            new TonemapperEffect(),
        ];

        scene.Add(cam);

        // Create single shared material
        standardMaterial = new Material(Shader.LoadDefault(DefaultShader.Standard));

        // Create floor (static)
        GameObject floor = new("Floor");
        MeshRenderer floorRenderer = floor.AddComponent<MeshRenderer>();
        floorRenderer.Mesh = Mesh.CreateCube(new Double3(20, 1, 20));
        floorRenderer.Material = standardMaterial;
        floorRenderer.MainColor = new Color(0.8f, 0.8f, 0.8f, 1.0f);
        floor.Transform.Position = new Double3(0, -0.5f, 0);

        // Add static rigidbody for floor
        BoxCollider floorCollider = floor.AddComponent<BoxCollider>();
        floorCollider.Size = new Double3(20, 1, 20);

        scene.Add(floor);

        //GameObject templeGO = new("Sun Temple");
        //templeGO.Transform.Position = new Double3(0, 0, 100);
        //templeGO.Transform.LocalScale = new Double3(100f);
        //var m = Model.LoadFromFile("fbxSunTemple\\SunTemple.fbx");
        //templeGO.AddComponent<ModelRenderer>().Model = m;
        //templeGO.AddComponent<ModelCollider>().Model = m;
        //scene.Add(templeGO);

        // Demo 1: Chain of connected cubes (BallSocket + DistanceLimit)
        CreateChainDemo(scene, new Double3(-8, 10, 0), new Color(1.0f, 0.7f, 0.2f, 1.0f));

        // Demo 2: Hinged door (HingeJoint)
        CreateHingedDoorDemo(scene, new Double3(0, 2, 0), new Color(0.2f, 1.0f, 0.5f, 1.0f));

        // Demo 3: Prismatic slider (PrismaticJoint)
        CreateSliderDemo(scene, new Double3(8, 3, 0), new Color(1.0f, 0.3f, 0.7f, 1.0f));

        // Demo 4: Ragdoll-style cone limits
        CreateRagdollDemo(scene, new Double3(-4, 8, -5), new Color(0.2f, 0.5f, 1.0f, 1.0f));

        // Demo 5: Powered motor demo
        CreateMotorDemo(scene, new Double3(4, 3, -5), new Color(1.0f, 0.7f, 0.2f, 1.0f));

        // Demo 6: GPU-Instanced Particle System!
        CreateParticleSystemDemo(scene, new Double3(0, 3, 5));

        // Demo 7: GPU-Instanced Terrain with LOD!
        CreateTerrainDemo(scene, new Double3(-50, -15, -50));

        scene.Activate();

        // Print controls
        Debug.Log("=== Physics Demo Controls ===");
        Debug.Log("WASD + Q/E: Move camera");
        Debug.Log("Right Mouse: Rotate camera");
        Debug.Log("Left Mouse: Shoot cube");
        Debug.Log("1-4: Change cube mass");
        Debug.Log("P: Toggle particle system");
        Debug.Log("I/J/K/L/U/O: Move particle system");
        Debug.Log("R: Reset scene");
        Debug.Log("X: Delete last shot cube");
        Debug.Log("============================");
    }

    private void CreateParticleSystemDemo(Scene scene, Double3 position)
    {
        // Create particle system GameObject
        particleSystemGO = new GameObject("Particle System Demo");
        particleSystemGO.Transform.Position = position;

        // Add particle system component
        ParticleSystemComponent particleSystem = particleSystemGO.AddComponent<ParticleSystemComponent>();

        // Create material with particle shader
        Material particleMaterial = new Material(Shader.LoadDefault(DefaultShader.Particle));
        particleMaterial.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White));
        particleMaterial.SetColor("_MainColor", new Color(1.0f, 0.8f, 0.4f, 1.0f));
        particleSystem.Material = particleMaterial;

        // Configure particle system settings
        particleSystem.MaxParticles = 1000;
        particleSystem.Duration = 2.0f;
        particleSystem.Looping = true;
        particleSystem.PlayOnEnable = true;
        particleSystem.Prewarm = false;
        particleSystem.SimulationSpace = SimulationSpace.Local;

        // Configure Initial module (required)
        particleSystem.Initial.Enabled = true;
        particleSystem.Initial.StartLifetime = new MinMaxCurve { Mode = MinMaxCurveMode.Random, MinValue = 1.0f, MaxValue = 2.5f };
        particleSystem.Initial.StartSpeed = new MinMaxCurve { Mode = MinMaxCurveMode.Random, MinValue = 2.0f, MaxValue = 5.0f };
        particleSystem.Initial.StartSize = new MinMaxCurve { Mode = MinMaxCurveMode.Random, MinValue = 0.1f, MaxValue = 0.3f };
        particleSystem.Initial.StartRotation = new MinMaxCurve { Mode = MinMaxCurveMode.Random, MinValue = 0.0f, MaxValue = 360.0f };
        particleSystem.Initial.StartColor = new MinMaxGradient
        {
            Mode = MinMaxGradientMode.RandomBetweenTwoColors,
            MinColor = new Color(1.0f, 0.5f, 0.2f, 1.0f), // Orange
            MaxColor = new Color(1.0f, 1.0f, 0.3f, 1.0f)  // Yellow
        };

       // Configure Emission module
       particleSystem.Emission.Enabled = true;
       particleSystem.Emission.RateOverTime = new MinMaxCurve(50.0f); // 500 particles per second

       // Configure emission shape (try Sphere, Box, Cone, LineSegment, Circle)
       particleSystem.Emission.Shape = EmissionShape.Cone;
       particleSystem.Emission.Radius = 0.0f;
       particleSystem.Emission.EmitFromShell = true; // Emit from surface only
       
       // Configure Size over Lifetime
       // Size starts at 1.0, grows to 1.5 at middle, then shrinks to 0 at end
       particleSystem.SizeOverLifetime.Enabled = true;
       particleSystem.SizeOverLifetime.SizeCurve = new MinMaxCurve
       {
           Mode = MinMaxCurveMode.Curve,
           Curve = new AnimationCurve([new KeyFrame(0.0, 1.0), new KeyFrame(0.5, 1.5), new KeyFrame(1.0, 0.0)])
       };
       
       // Configure Color over Lifetime (fade out)
       // Fade from full alpha to transparent
       particleSystem.ColorOverLifetime.Enabled = true;
       particleSystem.ColorOverLifetime.ColorGradient = new MinMaxGradient
       {
           Mode = MinMaxGradientMode.Gradient,
           Gradient = new Gradient()
           {
               ColorKeys = [new (Color.White, 0.0f), new (new Color(1, 0.8f, 0.6f, 1), 0.5f), new (new Color(0.5f, 0.3f, 0.2f, 1), 1.0f)],
               AlphaKeys = [new (1.0f, 0.0f), new (0.8f, 0.5f), new (0.0f, 1.0f)]
           }
       };
       
       // Configure Rotation over Lifetime
       particleSystem.RotationOverLifetime.Enabled = true;
       particleSystem.RotationOverLifetime.AngularVelocity = new MinMaxCurve
       {
           Mode = MinMaxCurveMode.Random,
           MinValue = -180.0f,
           MaxValue = 180.0f
       };
       
       // Configure Velocity over Lifetime (simulate wind/drift)
       particleSystem.VelocityOverLifetime.Enabled = true;
       particleSystem.VelocityOverLifetime.VelocityX = new MinMaxCurve
       {
           Mode = MinMaxCurveMode.Curve,
           Curve = new AnimationCurve([new KeyFrame(0.0, 0.0), new KeyFrame(1.0, 20.0)])
       };

        particleSystem.Collision.Enabled = true;
        particleSystem.Collision.Quality = CollisionQuality.Medium;

        scene.Add(particleSystemGO);

        Debug.Log("GPU-Instanced Particle System created! 5000 max particles with full GPU instancing.");
    }

    private void CreateChainDemo(Scene scene, Double3 startPos, Color color)
    {
        GameObject anchor = new("Chain Anchor");
        anchor.Transform.Position = startPos;
        Rigidbody3D anchorRb = anchor.AddComponent<Rigidbody3D>();
        anchorRb.IsStatic = true;
        SphereCollider anchorCollider = anchor.AddComponent<SphereCollider>();
        anchorCollider.Radius = 0.2f;
        MeshRenderer anchorRenderer = anchor.AddComponent<MeshRenderer>();
        anchorRenderer.Mesh = Mesh.CreateSphere(0.2f, 8, 8);
        anchorRenderer.Material = standardMaterial;
        anchorRenderer.MainColor = color;
        scene.Add(anchor);

        GameObject previousLink = anchor;
        for (int i = 0; i < 5; i++)
        {
            GameObject link = new($"Chain Link {i}");
            link.Transform.Position = startPos + new Double3(0, -(i + 1) * 1.5, 0);
            MeshRenderer linkRenderer = link.AddComponent<MeshRenderer>();
            linkRenderer.Mesh = Mesh.CreateCube(new Double3(0.5, 1, 0.5));
            linkRenderer.Material = standardMaterial;
            linkRenderer.MainColor = color;

            Rigidbody3D linkRb = link.AddComponent<Rigidbody3D>();
            linkRb.Mass = 1.0;

            BoxCollider linkCollider = link.AddComponent<BoxCollider>();
            linkCollider.Size = new Double3(0.5, 1, 0.5);

            // Connect with BallSocket at top
            BallSocketConstraint ballSocket = link.AddComponent<BallSocketConstraint>();
            ballSocket.ConnectedBody = previousLink.GetComponent<Rigidbody3D>();
            ballSocket.Anchor = new Double3(0, 0.5, 0);

            scene.Add(link);
            previousLink = link;
        }
    }

    private void CreateHingedDoorDemo(Scene scene, Double3 position, Color color)
    {
        // Door frame (static)
        GameObject frame = new("Door Frame");
        frame.Transform.Position = position;
        Rigidbody3D frameRb = frame.AddComponent<Rigidbody3D>();
        frameRb.IsStatic = true;
        BoxCollider frameCollider = frame.AddComponent<BoxCollider>();
        frameCollider.Size = new Double3(0.2, 3, 0.2);
        MeshRenderer frameRenderer = frame.AddComponent<MeshRenderer>();
        frameRenderer.Mesh = Mesh.CreateCube(new Double3(0.2, 3, 0.2));
        frameRenderer.Material = standardMaterial;
        frameRenderer.MainColor = color;
        scene.Add(frame);

        // Door (dynamic)
        GameObject door = new("Door");
        door.Transform.Position = position + new Double3(1.5, 0, 0);
        MeshRenderer doorRenderer = door.AddComponent<MeshRenderer>();
        doorRenderer.Mesh = Mesh.CreateCube(new Double3(3, 2.8, 0.1));
        doorRenderer.Material = standardMaterial;
        doorRenderer.MainColor = color;

        Rigidbody3D doorRb = door.AddComponent<Rigidbody3D>();
        doorRb.Mass = 2.0;

        BoxCollider doorCollider = door.AddComponent<BoxCollider>();
        doorCollider.Size = new Double3(3, 2.8, 0.1);

        // Hinge joint
        HingeJoint hinge = door.AddComponent<HingeJoint>();
        hinge.ConnectedBody = frameRb;
        hinge.Anchor = new Double3(-1.5, 0, 0);
        hinge.Axis = new Double3(0, 1, 0);
        hinge.MinAngleDegrees = -90;
        hinge.MaxAngleDegrees = 90;

        scene.Add(door);
    }

    private void CreateSliderDemo(Scene scene, Double3 position, Color color)
    {
        // Rail (static)
        GameObject rail = new("Slider Rail");
        rail.Transform.Position = position;
        Rigidbody3D railRb = rail.AddComponent<Rigidbody3D>();
        railRb.IsStatic = true;
        BoxCollider railCollider = rail.AddComponent<BoxCollider>();
        railCollider.Size = new Double3(0.1, 4, 0.1);
        MeshRenderer railRenderer = rail.AddComponent<MeshRenderer>();
        railRenderer.Mesh = Mesh.CreateCube(new Double3(0.1, 4, 0.1));
        railRenderer.Material = standardMaterial;
        railRenderer.MainColor = color;
        scene.Add(rail);

        // Slider (dynamic)
        GameObject slider = new("Slider");
        slider.Transform.Position = position + new Double3(0, 1, 0);
        MeshRenderer sliderRenderer = slider.AddComponent<MeshRenderer>();
        sliderRenderer.Mesh = Mesh.CreateCube(new Double3(1, 0.5, 1));
        sliderRenderer.Material = standardMaterial;
        sliderRenderer.MainColor = color;

        Rigidbody3D sliderRb = slider.AddComponent<Rigidbody3D>();
        sliderRb.Mass = 1.5;

        BoxCollider sliderCollider = slider.AddComponent<BoxCollider>();
        sliderCollider.Size = new Double3(1, 0.5, 1);

        // Prismatic joint (slider)
        PrismaticJoint prismatic = slider.AddComponent<PrismaticJoint>();
        prismatic.ConnectedBody = railRb;
        prismatic.Anchor = Double3.Zero;
        prismatic.Axis = new Double3(0, 1, 0);
        prismatic.MinDistance = -1.5;
        prismatic.MaxDistance = 1.5;
        prismatic.Pinned = true;

        scene.Add(slider);
    }

    private void CreateRagdollDemo(Scene scene, Double3 position, Color color)
    {
        // Torso (parent body)
        GameObject torso = new("Torso");
        torso.Transform.Position = position;
        MeshRenderer torsoRenderer = torso.AddComponent<MeshRenderer>();
        torsoRenderer.Mesh = Mesh.CreateCube(new Double3(1, 1.5, 0.5));
        torsoRenderer.Material = standardMaterial;
        torsoRenderer.MainColor = color;

        Rigidbody3D torsoRb = torso.AddComponent<Rigidbody3D>();
        torsoRb.Mass = 2.0;

        BoxCollider torsoCollider = torso.AddComponent<BoxCollider>();
        torsoCollider.Size = new Double3(1, 1.5, 0.5);

        scene.Add(torso);

        // Left arm with cone limit
        GameObject leftArm = new("Left Arm");
        leftArm.Transform.Position = position + new Double3(-0.75, 0.5, 0);
        MeshRenderer armRenderer = leftArm.AddComponent<MeshRenderer>();
        armRenderer.Mesh = Mesh.CreateCube(new Double3(1, 0.3, 0.3));
        armRenderer.Material = standardMaterial;
        armRenderer.MainColor = color;

        Rigidbody3D armRb = leftArm.AddComponent<Rigidbody3D>();
        armRb.Mass = 0.5;

        BoxCollider armCollider = leftArm.AddComponent<BoxCollider>();
        armCollider.Size = new Double3(1, 0.3, 0.3);

        // Ball socket for shoulder
        BallSocketConstraint shoulderBall = leftArm.AddComponent<BallSocketConstraint>();
        shoulderBall.ConnectedBody = torsoRb;
        shoulderBall.Anchor = new Double3(0.5, 0, 0);

        // Cone limit to restrict arm movement
        ConeLimitConstraint shoulderCone = leftArm.AddComponent<ConeLimitConstraint>();
        shoulderCone.ConnectedBody = torsoRb;
        shoulderCone.Axis = new Double3(1, 0, 0);
        shoulderCone.MinAngle = 0;
        shoulderCone.MaxAngle = 45;

        scene.Add(leftArm);
    }

    private void CreateMotorDemo(Scene scene, Double3 position, Color color)
    {
        // Base (static)
        GameObject motorBase = new("Motor Base");
        motorBase.Transform.Position = position;
        Rigidbody3D baseRb = motorBase.AddComponent<Rigidbody3D>();
        baseRb.IsStatic = true;
        BoxCollider baseCollider = motorBase.AddComponent<BoxCollider>();
        baseCollider.Size = new Double3(0.5, 0.5, 0.5);
        MeshRenderer baseRenderer = motorBase.AddComponent<MeshRenderer>();
        baseRenderer.Mesh = Mesh.CreateCube(new Double3(0.5, 0.5, 0.5));
        baseRenderer.Material = standardMaterial;
        baseRenderer.MainColor = color;
        scene.Add(motorBase);

        // Spinning platform
        GameObject platform = new("Spinning Platform");
        platform.Transform.Position = position + new Double3(0, 0.5, 0);
        MeshRenderer platformRenderer = platform.AddComponent<MeshRenderer>();
        platformRenderer.Mesh = Mesh.CreateCube(new Double3(2, 0.2, 2));
        platformRenderer.Material = standardMaterial;
        platformRenderer.MainColor = color;

        Rigidbody3D platformRb = platform.AddComponent<Rigidbody3D>();
        platformRb.Mass = 1.0;

        BoxCollider platformCollider = platform.AddComponent<BoxCollider>();
        platformCollider.Size = new Double3(2, 0.2, 2);

        // Hinge joint with motor
        HingeJoint motorHinge = platform.AddComponent<HingeJoint>();
        motorHinge.ConnectedBody = baseRb;
        motorHinge.Anchor = new Double3(0, -0.3, 0);
        motorHinge.Axis = new Double3(0, 1, 0);
        motorHinge.HasMotor = true;
        motorHinge.MotorTargetVelocity = 2.0; // Radians per second
        motorHinge.MotorMaxForce = 10.0;

        scene.Add(platform);
    }

    private void CreateTerrainDemo(Scene scene, Double3 position)
    {
        // Create terrain GameObject
        GameObject terrainGO = new GameObject("GPU-Instanced Terrain");
        terrainGO.Transform.Position = position;

        // Generate heightmap and splatmap procedurally
        Texture2D heightmap = GenerateHeightmap(128, 128);
        Texture2D splatmap = GenerateSplatmap(128, 128);

        // Create terrain component
        TerrainComponent terrain = terrainGO.AddComponent<TerrainComponent>();
        terrainGO.AddComponent<TerrainCollider>();

        // Create material with terrain shader
        Material terrainMaterial = new Material(Shader.LoadDefault(DefaultShader.Terrain));
        terrain.Material = terrainMaterial;

        // Assign textures
        terrain.Heightmap = heightmap;
        terrain.Splatmap = splatmap;

        // Use default textures for layers
        terrain.Layer0Albedo = Texture2D.White;   // Base layer - white
        terrain.Layer1Albedo = Texture2D.Gray;    // Mid layer - gray
        terrain.Layer2Albedo = Texture2D.Grid;    // High layer - grid pattern
        terrain.Layer3Albedo = Texture2D.Noise;   // Peak layer - noise

        // Configure terrain settings
        terrain.TerrainSize = 100.0;              // 100x100 world units
        terrain.TerrainHeight = 20.0f;            // Max height 20 units
        terrain.MaxLODLevel = 6;                  // 6 levels of LOD
        terrain.MeshResolution = 32;              // 32x32 base mesh
        terrain.TextureTiling = 20.0f;            // Tile textures 20 times

        scene.Add(terrainGO);

        Debug.Log("GPU-Instanced Terrain created! Heightmap sampled in vertex shader with automatic LOD.");
        Debug.Log($"Terrain positioned at {position}. Use WASD + Mouse to fly camera and view it!");
    }

    private Texture2D GenerateHeightmap(uint width, uint height)
    {
        // Create texture
        Texture2D heightmap = new Texture2D(width, height, true, TextureImageFormat.Color4b);

        // Generate heightmap data using simple noise
        byte[] pixels = new byte[width * height * 4]; // RGBA

        for (uint y = 0; y < height; y++)
        {
            for (uint x = 0; x < width; x++)
            {
                uint index = (y * width + x) * 4;

                // Simple multi-octave noise for terrain
                float nx = (float)x / width;
                float ny = (float)y / height;

                // Multiple octaves of noise
                float heightValue = 0.0f;
                heightValue += PerlinNoise(nx * 4, ny * 4) * 0.5f;      // Large features
                heightValue += PerlinNoise(nx * 8, ny * 8) * 0.25f;     // Medium features
                heightValue += PerlinNoise(nx * 16, ny * 16) * 0.125f;  // Small details

                // Normalize to 0-1
                heightValue = (heightValue + 1.0f) * 0.5f;
                heightValue = Math.Clamp(heightValue, 0.0f, 1.0f);

                byte value = (byte)(heightValue * 255);

                // Store as grayscale (R channel used by shader)
                pixels[index + 0] = value; // R
                pixels[index + 1] = value; // G
                pixels[index + 2] = value; // B
                pixels[index + 3] = 255;   // A
            }
        }

        heightmap.SetData(new Memory<byte>(pixels));
        heightmap.SetWrapModes(TextureWrap.ClampToEdge, TextureWrap.ClampToEdge);
        heightmap.SetTextureFilters(TextureMin.Linear, TextureMag.Linear);

        return heightmap;
    }

    private Texture2D GenerateSplatmap(uint width, uint height)
    {
        // Create texture
        Texture2D splatmap = new Texture2D(width, height, true, TextureImageFormat.Color4b);

        // Generate splatmap data
        byte[] pixels = new byte[width * height * 4]; // RGBA = 4 layers

        for (uint y = 0; y < height; y++)
        {
            for (uint x = 0; x < width; x++)
            {
                uint index = (y * width + x) * 4;

                float nx = (float)x / width;
                float ny = (float)y / height;

                // Generate blend weights based on position and noise
                // Layer 0 (R): Base layer - everywhere but reduced at higher "elevations"
                float noise = PerlinNoise(nx * 8, ny * 8);
                float heightNorm = (noise + 1.0f) * 0.5f; // 0-1

                float layer0 = Math.Max(0, 1.0f - heightNorm * 1.5f);        // Low areas
                float layer1 = 1.0f - Math.Abs(heightNorm - 0.4f) * 2.0f;    // Mid areas
                float layer2 = 1.0f - Math.Abs(heightNorm - 0.7f) * 2.0f;    // High areas
                float layer3 = Math.Max(0, (heightNorm - 0.8f) * 5.0f);      // Peaks

                // Normalize weights
                float sum = layer0 + layer1 + layer2 + layer3;
                if (sum > 0)
                {
                    layer0 /= sum;
                    layer1 /= sum;
                    layer2 /= sum;
                    layer3 /= sum;
                }

                pixels[index + 0] = (byte)(layer0 * 255); // R = Layer 0
                pixels[index + 1] = (byte)(layer1 * 255); // G = Layer 1
                pixels[index + 2] = (byte)(layer2 * 255); // B = Layer 2
                pixels[index + 3] = (byte)(layer3 * 255); // A = Layer 3
            }
        }

        splatmap.SetData(new Memory<byte>(pixels));
        splatmap.SetWrapModes(TextureWrap.ClampToEdge, TextureWrap.ClampToEdge);
        splatmap.SetTextureFilters(TextureMin.Linear, TextureMag.Linear);

        return splatmap;
    }

    // Simple Perlin-like noise function
    private float PerlinNoise(float x, float y)
    {
        // Simple smooth noise using sine waves
        float n = (float)(Math.Sin(x * 12.9898 + y * 78.233) * 43758.5453);
        n = n - (float)Math.Floor(n);

        // Smooth interpolation
        float fx = x - (float)Math.Floor(x);
        float fy = y - (float)Math.Floor(y);

        float a = Noise2D((int)x, (int)y);
        float b = Noise2D((int)x + 1, (int)y);
        float c = Noise2D((int)x, (int)y + 1);
        float d = Noise2D((int)x + 1, (int)y + 1);

        // Smooth interpolation
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);

        float i1 = Lerp(a, b, fx);
        float i2 = Lerp(c, d, fx);
        return Lerp(i1, i2, fy);
    }

    private float Noise2D(int x, int y)
    {
        int n = x + y * 57;
        n = (n << 13) ^ n;
        return (1.0f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f);
    }

    private float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    Mesh cubeShootMesh = null;
    int shootCounter = 0;
    GameObject lastShot = null;
    private void ShootCube()
    {
        // Create a cube at camera position
        GameObject cube = new("Shot Cube");
        lastShot = cube;
        cube.Transform.Position = cameraGO.Transform.Position + cameraGO.Transform.Forward * 2.0;

        MeshRenderer cubeRenderer = cube.AddComponent<MeshRenderer>();
        cubeShootMesh = cubeShootMesh.IsNotValid() ? Mesh.CreateCube(new Double3(0.5, 0.5, 0.5)) : cubeShootMesh;
        cubeRenderer.Mesh = cubeShootMesh;
        cubeRenderer.Material = standardMaterial;
        cubeRenderer.MainColor = new Color(1.0f, 0.3f, 0.3f, 1.0f);

        Rigidbody3D cubeRb = cube.AddComponent<Rigidbody3D>();
        cubeRb.Mass = selectedCubeMass;
        cubeRb.EnableSpeculativeContacts = true;

        BoxCollider cubeCollider = cube.AddComponent<BoxCollider>();
        cubeCollider.Size = new Double3(0.5, 0.5, 0.5);

        //var light = cube.AddComponent<PointLight>();
        //light.ShadowQuality = ShadowQuality.Soft;
        //light.Intensity = 32;
        //light.Color = new Color(RNG.Shared.NextDouble(), RNG.Shared.NextDouble(), RNG.Shared.NextDouble(), 1f);
        //light.Transform.Rotation = cameraGO.Transform.Rotation;

        scene.Add(cube);

        // Add velocity in the direction the camera is facing
        cubeRb.LinearVelocity = cameraGO.Transform.Forward * 1.0;


        shootCounter++;
        Debug.Log($"Shot cube #{shootCounter} with mass {selectedCubeMass}");
    }

    public override void EndUpdate()
    {
        //scene.DrawGizmos();

        if (particleSystemGO != null)
            particleSystemGO.Transform.Rotate(Double3.UnitX, 20f * Time.DeltaTime);

        // Camera movement
        Double2 movement = Double2.Zero;
        if (Input.GetKey(KeyCode.W)) movement += Double2.UnitY;
        if (Input.GetKey(KeyCode.S)) movement -= Double2.UnitY;
        if (Input.GetKey(KeyCode.A)) movement -= Double2.UnitX;
        if (Input.GetKey(KeyCode.D)) movement += Double2.UnitX;

        // forward/back
        cameraGO.Transform.Position += cameraGO.Transform.Forward * movement.Y * 10f * Time.DeltaTime;
        // left/right
        cameraGO.Transform.Position += cameraGO.Transform.Right * movement.X * 10f * Time.DeltaTime;

        // up/down
        float upDown = 0;
        if (Input.GetKey(KeyCode.E)) upDown += 1;
        if (Input.GetKey(KeyCode.Q)) upDown -= 1;
        cameraGO.Transform.Position += Double3.UnitY * upDown * 10f * Time.DeltaTime;

        // rotate with mouse
        if (Input.GetMouseButton(1))
        {
            Double2 delta = Input.MouseDelta;
            cameraGO.Transform.LocalEulerAngles += new Double3(delta.Y, delta.X, 0) * 0.1f;
        }

        // Reset scene with R key
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Clear and reinitialize
            scene.Clear();
            Initialize();
        }

        if (Input.GetKeyDown(KeyCode.X) && lastShot.IsValid())
        {
            lastShot.Dispose();
        }

        // Weight selection with number keys
        if (Input.GetKeyDown(KeyCode.Number1))
        {
            selectedCubeMass = 0.5;
            Debug.Log($"Cube weight set to: {selectedCubeMass}");
        }
        else if (Input.GetKeyDown(KeyCode.Number2))
        {
            selectedCubeMass = 1.0;
            Debug.Log($"Cube weight set to: {selectedCubeMass}");
        }
        else if (Input.GetKeyDown(KeyCode.Number3))
        {
            selectedCubeMass = 2.0;
            Debug.Log($"Cube weight set to: {selectedCubeMass}");
        }
        else if (Input.GetKeyDown(KeyCode.Number4))
        {
            selectedCubeMass = 5.0;
            Debug.Log($"Cube weight set to: {selectedCubeMass}");
        }

        // Shoot cube with left mouse button
        if (Input.GetMouseButton(0))
        {
            ShootCube();
        }

        // Toggle particle system with P key
        if (Input.GetKeyDown(KeyCode.P) && particleSystemGO.IsValid())
        {
            var particleSystem = particleSystemGO.GetComponent<ParticleSystemComponent>();
            if (particleSystem != null)
            {
                if (particleSystem.IsPlaying)
                {
                    particleSystem.Stop();
                    Debug.Log("Particle system stopped");
                }
                else
                {
                    particleSystem.Play();
                    Debug.Log("Particle system playing");
                }
            }
        }

        // Move particle system with I/J/K/L keys
        if (particleSystemGO.IsValid())
        {
            Double3 particleMovement = Double3.Zero;
            if (Input.GetKey(KeyCode.I)) particleMovement += Double3.UnitZ;  // Forward
            if (Input.GetKey(KeyCode.K)) particleMovement -= Double3.UnitZ;  // Back
            if (Input.GetKey(KeyCode.J)) particleMovement -= Double3.UnitX;  // Left
            if (Input.GetKey(KeyCode.L)) particleMovement += Double3.UnitX;  // Right
            if (Input.GetKey(KeyCode.U)) particleMovement += Double3.UnitY;  // Up
            if (Input.GetKey(KeyCode.O)) particleMovement -= Double3.UnitY;  // Down

            if (particleMovement != Double3.Zero)
            {
                particleSystemGO.Transform.Position += particleMovement * 5f * Time.DeltaTime;
            }
        }
    }
}
