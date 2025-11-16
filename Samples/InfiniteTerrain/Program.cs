using Prowl;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Runtime.Terrain;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Vector;
using Prowl.Runtime.Rendering;

namespace InfiniteTerrain;

public static class Program
{
    public static void Main()
    {
        new InfiniteTerrainApp().Run("Infinite Terrain Sample", 1280, 720);
    }
}

public class InfiniteTerrainApp : Game
{
    private Material? standardMaterial;
    private GameObject? cameraGO;
    private Scene? scene;

    public override void Initialize()
    {
        //DrawGizmos = true;
        scene = new Scene();
        scene.Ambient = new Scene.AmbientLightParams
        {
            Color = new Color(0.5f, 0.5f, 0.5f, 1.0f),
            Strength = 1.0f
        };

        // Create directional light
        GameObject lightGO = new("Directional Light");
        DirectionalLight light = lightGO.AddComponent<DirectionalLight>();
        light.ShadowQuality = ShadowQuality.Soft;
        lightGO.Transform.LocalEulerAngles = new Double3(-45, 45, 0);
        scene.Add(lightGO);

        // Create camera
        cameraGO = new("Main Camera");
        cameraGO.Tag = "Main Camera";
        cameraGO.Transform.Position = new(0, 5, -15);
        cameraGO.Transform.LocalEulerAngles = new Double3(15, 0, 0);
        Camera camera = cameraGO.AddComponent<Camera>();
        camera.Depth = -1;
        camera.HDR = true;
        camera.Effects =
        [
            new FXAAEffect(),
            new BokehDepthOfFieldEffect(),
            new KawaseBloomEffect(),
            new TonemapperEffect(),
        ];

        cameraGO.AddComponent<LookInput>();
        cameraGO.AddComponent<LookCamera>();

        scene.Add(cameraGO);

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

        scene.Activate();

        // Create terrain demo at origin
        CreateTerrainDemo(scene, new Double3(0, 0, 0));
        CreateTerrainDemo(scene, new Double3(-100, 0, 0));
        CreateTerrainDemo(scene, new Double3(0,0, -100));
        CreateTerrainDemo(scene, new Double3(-100, 0, -100));
    }

    private void CreateTerrainDemo(Scene scene, Double3 position)
    {
        // Create terrain GameObject
        GameObject terrainGO = new GameObject("GPU-Instanced Terrain");
        terrainGO.Transform.Position = position;

        // Generate heightmap and splatmap procedurally
        Texture2D heightmap = GenerateHeightmap(128, 128, (Float2)position.XZ);
        Texture2D splatmap = GenerateSplatmap(128, 128, (Float2)position.XZ);

        // Create terrain component
        TerrainComponent terrain = terrainGO.AddComponent<TerrainComponent>();
        terrainGO.AddComponent<TerrainCollider>();

        // Create material with terrain shader
        Material terrainMaterial = new Material(Shader.LoadDefault(DefaultShader.Terrain));
        terrain.Material = terrainMaterial;

        // Assign textures
        terrain.Heightmap = heightmap;
        terrain.Splatmap = splatmap;

        // var AppDir = System.AppContext.BaseDirectory;

        // Use default textures for layers
        terrain.Layer0Albedo = Texture2D.LoadFromFile("dirt.png");   // Base layer - white
        terrain.Layer1Albedo = Texture2D.LoadFromFile("grass.jpg");    // Mid layer - gray
        terrain.Layer2Albedo = Texture2D.LoadFromFile("dirt.png");    // High layer - grid pattern
        terrain.Layer3Albedo = Texture2D.LoadFromFile("grass.jpg");   // Peak layer - noise

        // Configure terrain settings
        terrain.TerrainSize = 100.0;              // 100x100 world units
        terrain.TerrainHeight = 20.0f;            // Max height 20 units
        terrain.MaxLODLevel = 4;                  // 6 levels of LOD
        terrain.MeshResolution = 16;              // 32x32 base mesh
        terrain.TextureTiling = 20.0f;            // Tile textures 20 times

        scene.Add(terrainGO);

        Debug.Log("GPU-Instanced Terrain created! Heightmap sampled in vertex shader with automatic LOD.");
        Debug.Log($"Terrain positioned at {position}. Use WASD + Mouse to fly camera and view it!");
    }
    
    private Texture2D GenerateHeightmap(uint width, uint height, Float2 offset)
    {
        // Create texture
        Texture2D heightmap = new Texture2D(width, height, true, TextureImageFormat.Color4b);
        var texelSize = 128f / 100f; // assuming terrain size of 100 units

        // Generate heightmap data using simple noise
        byte[] pixels = new byte[width * height * 4]; // RGBA

        for (uint y = 0; y < height; y++)
        {
            for (uint x = 0; x < width; x++)
            {
                uint index = (y * width + x) * 4;

                // Simple multi-octave noise for terrain
                float nx = (float)((x * texelSize) + offset.X) / 100;
                float ny = (float)((y * texelSize) + offset.Y) / 100;

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

    private Texture2D GenerateSplatmap(uint width, uint height, Float2 offset)
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

                float nx = (float)(x + offset.X) / width;
                float ny = (float)(y + offset.Y) / height;

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

}