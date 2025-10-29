// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;

using Material = Prowl.Runtime.Resources.Material;
using Shader = Prowl.Runtime.Resources.Shader;

namespace Prowl.Runtime;

public class SpotLight : Light
{
    public enum Resolution : int
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
    }

    public Resolution ShadowResolution = Resolution._512;
    public double Range = 8.0;
    public double SpotAngle = 45.0; // Outer cone angle in degrees
    public double InnerSpotAngle = 30.0; // Inner cone angle in degrees for smooth falloff

    private Material? _lightMaterial;
    private Double4x4 _shadowMatrix;
    private Double4 _shadowAtlasParams; // xy = atlas pos, z = atlas size, w = 1.0

    public override void Update()
    {
        GameObject.Scene.PushLight(this);
    }

    public override void DrawGizmos()
    {
        Debug.DrawArrow(Transform.Position, Transform.Forward, Color.Yellow);

        // Draw cone to visualize spot light
        double outerAngleRad = SpotAngle * Maths.Deg2Rad;
        double radius = Maths.Tan(outerAngleRad) * Range;
        Double3 endPosition = Transform.Position + Transform.Forward * Range;

        // Draw cone outline
        int segments = 4;
        Double3 prevPoint = Double3.Zero;
        for (int i = 0; i <= segments; i++)
        {
            double angle = (i / (double)segments) * Maths.PI * 2.0;
            Double3 offset = (Transform.Right * Maths.Cos(angle) + Transform.Up * Maths.Sin(angle)) * radius;
            Double3 point = endPosition + offset;

            // Draw line from light position to cone edge
            Debug.DrawLine(Transform.Position, point, Color.Yellow);

            prevPoint = point;
        }

        // Draw circle at the end of the cone
        Debug.DrawWireCircle(endPosition, Transform.Forward, radius, Color.Yellow);
    }

    public override LightType GetLightType() => LightType.Spot;

    private void GetShadowMatrix(out Double4x4 view, out Double4x4 projection)
    {
        Double3 forward = Transform.Forward;
        Double3 position = Transform.Position;

        // Use perspective projection for spot light
        double fov = SpotAngle * 2.0; // Full cone angle
        projection = Double4x4.CreatePerspectiveFov(fov * Maths.Deg2Rad, 1.0, 0.1, Range);

        view = Double4x4.CreateLookTo(position, forward, Transform.Up);
    }

    public override void RenderShadows(RenderPipeline pipeline, Double3 cameraPosition, System.Collections.Generic.IReadOnlyList<IRenderable> renderables)
    {
        if (!DoCastShadows())
        {
            // No shadows
            _shadowAtlasParams = new Double4(-1, -1, 0, 0);
            return;
        }

        int res = (int)ShadowResolution;

        // Reserve space in shadow atlas
        Int2? slot = ShadowAtlas.ReserveTiles(res, res, GetLightID());

        if (slot != null)
        {
            int atlasX = slot.Value.X;
            int atlasY = slot.Value.Y;

            // Set viewport to atlas region
            Graphics.Device.Viewport(atlasX, atlasY, (uint)res, (uint)res);

            // Calculate shadow matrices
            GetShadowMatrix(out Double4x4 view, out Double4x4 proj);

            if (RenderPipeline.CAMERA_RELATIVE)
                view.Translation *= new Double4(0, 0, 0, 1); // set all to 0 except W

            Frustum frustum = Frustum.FromMatrix(proj * view);

            Double3 forward = Transform.Forward;
            Double3 right = Transform.Right;
            Double3 up = Transform.Up;

            // Cull and render shadow casters
            System.Collections.Generic.HashSet<int> culledRenderableIndices = pipeline.CullRenderables(renderables, frustum, LayerMask.Everything);
            pipeline.AssignCameraMatrices(view, proj);
            pipeline.DrawRenderables(renderables, "LightMode", "ShadowCaster", new ViewerData(GetLightPosition(), forward, right, up), culledRenderableIndices, false);

            // Store shadow data for shader
            _shadowMatrix = proj * view;
            _shadowAtlasParams = new Double4(atlasX, atlasY, res, 1.0);
        }
        else
        {
            // Failed to reserve atlas space
            _shadowAtlasParams = new Double4(-1, -1, 0, 0);
        }
    }

    private static Mesh? _mesh;
    public override void OnRenderLight(RenderTexture gBuffer, RenderTexture destination, RenderPipeline.CameraSnapshot css)
    {
        // Create cone mesh if needed (shared by all spot lights)
        if (_mesh == null || !_mesh.IsValid())
        {
            _mesh = Mesh.CreateSphere(1f, 6, 6);
        }

        // Create material if needed
        _lightMaterial ??= new Material(Shader.LoadDefault(DefaultShader.SpotLight));

        // Set GBuffer textures
        _lightMaterial.SetTexture("_GBufferA", gBuffer.InternalTextures[0]);
        _lightMaterial.SetTexture("_GBufferB", gBuffer.InternalTextures[1]);
        _lightMaterial.SetTexture("_GBufferC", gBuffer.InternalTextures[2]);
        _lightMaterial.SetTexture("_GBufferD", gBuffer.InternalTextures[3]);
        _lightMaterial.SetTexture("_CameraDepthTexture", gBuffer.InternalDepth);

        // Set shadow atlas texture
        var shadowAtlas = ShadowAtlas.GetAtlas();
        _lightMaterial.SetTexture("_ShadowAtlas", shadowAtlas.InternalDepth);

        // Set spot light properties
        _lightMaterial.SetVector("_LightPosition", Transform.Position);
        _lightMaterial.SetVector("_LightDirection", Transform.Forward);
        _lightMaterial.SetColor("_LightColor", Color);
        _lightMaterial.SetFloat("_LightIntensity", (float)Intensity);
        _lightMaterial.SetFloat("_LightRange", (float)Range);
        _lightMaterial.SetFloat("_SpotAngle", (float)SpotAngle);
        _lightMaterial.SetFloat("_InnerSpotAngle", (float)InnerSpotAngle);

        // Set shadow properties
        _lightMaterial.SetFloat("_ShadowBias", (float)ShadowBias);
        _lightMaterial.SetFloat("_ShadowNormalBias", (float)ShadowNormalBias);
        _lightMaterial.SetFloat("_ShadowStrength", (float)ShadowStrength);
        _lightMaterial.SetFloat("_ShadowQuality", (float)ShadowQuality);

        // Set shadow matrix and atlas parameters
        _lightMaterial.SetMatrix("_ShadowMatrix", _shadowMatrix);
        _lightMaterial.SetVector("_ShadowAtlasParams", _shadowAtlasParams);

#warning TODO: Use Cone and fit properly!
        // Combine transformations
        Double4x4 model = this.Transform.LocalToWorldMatrix;
        // scale by range
        Double4x4 scale = Double4x4.CreateScale(new Double3(Range, Range, Range));
        model = model * scale;

        // Handle camera-relative rendering
        if (RenderPipeline.CAMERA_RELATIVE)
            model.Translation -= new Double4(css.CameraPosition, 0.0);

        // Set transform matrices
        _lightMaterial.SetMatrix("prowl_ObjectToWorld", model);
        _lightMaterial.SetMatrix("prowl_WorldToObject", model.Invert());

        // Bind destination framebuffer
        Graphics.Device.BindFramebuffer(destination.frameBuffer);

        // Draw cone mesh instead of fullscreen quad
        Graphics.DrawMeshNow(_mesh, _lightMaterial, 0);
    }
}
