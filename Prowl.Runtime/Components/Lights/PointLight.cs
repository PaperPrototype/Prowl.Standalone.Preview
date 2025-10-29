// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;

using Material = Prowl.Runtime.Resources.Material;
using Shader = Prowl.Runtime.Resources.Shader;

namespace Prowl.Runtime;

public class PointLight : Light
{
    public enum Resolution : int
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
    }

    public Resolution ShadowResolution = Resolution._256;
    public double Range = 10.0;

    private Material? _lightMaterial;

    // Shadow cubemap data - 6 faces stored in a 3x2 grid in the shadow atlas
    private Double4[] _shadowFaceParams = new Double4[6]; // xy = atlas pos, z = face size, w = far plane
    private Double4x4[] _shadowMatrices = new Double4x4[6]; // View-projection for each face
    private bool _shadowsValid = false;

    public override void Update()
    {
        GameObject.Scene.PushLight(this);
    }

    public override void DrawGizmos()
    {
        Debug.DrawWireSphere(Transform.Position, Range, Color.Yellow);
    }

    public override LightType GetLightType() => LightType.Point;

    public override void RenderShadows(RenderPipeline pipeline, Double3 cameraPosition, System.Collections.Generic.IReadOnlyList<IRenderable> renderables)
    {
        if (!DoCastShadows())
        {
            _shadowsValid = false;
            return;
        }

        int res = (int)ShadowResolution;
        Double3 lightPos = Transform.Position;

        // Reserve 3x2 grid in shadow atlas for 6 cubemap faces
        // Layout: [+X][-X][+Y]
        //         [-Y][+Z][-Z]
        int requestedWidth = res * 3;
        int requestedHeight = res * 2;
        Int2? slot = ShadowAtlas.ReserveTiles(requestedWidth, requestedHeight, GetLightID());

        if (slot == null)
        {
            _shadowsValid = false;
            return;
        }

        int atlasX = slot.Value.X;
        int atlasY = slot.Value.Y;

        // Define the 6 cube faces with their orientations
        // Each face needs: target direction and up vector
        (Double3 forward, Double3 up)[] faceOrientations = new[]
        {
            (Double3.UnitX,  -Double3.UnitY), // +X (right)
            (-Double3.UnitX, -Double3.UnitY), // -X (left)
            (Double3.UnitY,   Double3.UnitZ), // +Y (up)
            (-Double3.UnitY, -Double3.UnitZ), // -Y (down)
            (Double3.UnitZ,  -Double3.UnitY), // +Z (forward)
            (-Double3.UnitZ, -Double3.UnitY), // -Z (back)
        };

        // Create perspective projection for all faces (90 degree FOV for cubemap)
        Double4x4 projection = Double4x4.CreatePerspectiveFov(Maths.PI / 2.0, 1.0, 0.1, Range);

        // Render each face
        for (int faceIndex = 0; faceIndex < 6; faceIndex++)
        {
            // Calculate viewport position in 3x2 grid
            int gridX = faceIndex % 3;
            int gridY = faceIndex / 3;
            int viewportX = atlasX + (gridX * res);
            int viewportY = atlasY + (gridY * res);

            // Set viewport for this face
            Graphics.Device.Viewport(viewportX, viewportY, (uint)res, (uint)res);

            // Create view matrix for this face
            (Double3 forward, Double3 up) = faceOrientations[faceIndex];
            Double4x4 view = Double4x4.CreateLookTo(RenderPipeline.CAMERA_RELATIVE ? Double3.Zero : lightPos, forward, up);

            Frustum frustum = Frustum.FromMatrix(projection * view);

            // Calculate viewer data for this face
            Double3 right = Double3.Normalize(Double3.Cross(up, forward));
            ViewerData viewerData = new ViewerData(lightPos, forward, right, up);

            // Cull and render shadow casters for this face
            System.Collections.Generic.HashSet<int> culledRenderableIndices = pipeline.CullRenderables(renderables, frustum, LayerMask.Everything);
            pipeline.AssignCameraMatrices(view, projection);
            pipeline.DrawRenderables(renderables, "LightMode", "ShadowCaster", viewerData, culledRenderableIndices, false);

            // Store face data for shader
            _shadowMatrices[faceIndex] = projection * view;
            _shadowFaceParams[faceIndex] = new Double4(viewportX, viewportY, res, Range);
        }

        _shadowsValid = true;
    }

    private static Mesh? _mesh;
    public override void OnRenderLight(RenderTexture gBuffer, RenderTexture destination, RenderPipeline.CameraSnapshot css)
    {
        // Create sphere mesh if needed (shared by all point lights)
        if (_mesh == null || !_mesh.IsValid())
        {
            _mesh = Mesh.CreateSphere(1.0f, 8, 8); // Unit sphere, scaled by range
        }

        // Create material if needed
        _lightMaterial ??= new Material(Shader.LoadDefault(DefaultShader.PointLight));

        // Set GBuffer textures
        _lightMaterial.SetTexture("_GBufferA", gBuffer.InternalTextures[0]);
        _lightMaterial.SetTexture("_GBufferB", gBuffer.InternalTextures[1]);
        _lightMaterial.SetTexture("_GBufferC", gBuffer.InternalTextures[2]);
        _lightMaterial.SetTexture("_GBufferD", gBuffer.InternalTextures[3]);
        _lightMaterial.SetTexture("_CameraDepthTexture", gBuffer.InternalDepth);

        // Set point light properties
        _lightMaterial.SetVector("_LightPosition", Transform.Position);
        _lightMaterial.SetColor("_LightColor", Color);
        _lightMaterial.SetFloat("_LightIntensity", (float)Intensity);
        _lightMaterial.SetFloat("_LightRange", (float)Range);

        // Set shadow properties
        var shadowAtlas = ShadowAtlas.GetAtlas();
        _lightMaterial.SetTexture("_ShadowAtlas", shadowAtlas.InternalDepth);
        _lightMaterial.SetFloat("_ShadowsEnabled", _shadowsValid ? 1.0f : 0.0f);
        _lightMaterial.SetFloat("_ShadowBias", (float)ShadowBias);
        _lightMaterial.SetFloat("_ShadowNormalBias", (float)ShadowNormalBias);
        _lightMaterial.SetFloat("_ShadowStrength", (float)ShadowStrength);
        _lightMaterial.SetFloat("_ShadowQuality", (float)ShadowQuality);

        // Set shadow matrices and face parameters for all 6 faces
        for (int i = 0; i < 6; i++)
        {
            _lightMaterial.SetMatrix($"_ShadowMatrix{i}", _shadowMatrices[i]);
            _lightMaterial.SetVector($"_ShadowFaceParams{i}", _shadowFaceParams[i]);
        }

        // Create model matrix - scale sphere by range and position at light location
        Double4x4 model = this.Transform.LocalToWorldMatrix;
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

        // Draw sphere mesh
        Graphics.DrawMeshNow(_mesh, _lightMaterial, 0);
    }
}
