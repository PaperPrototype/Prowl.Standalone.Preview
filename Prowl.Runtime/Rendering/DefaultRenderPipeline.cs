// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

using Prowl.Runtime.GraphicsBackend;
using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Rendering.Shaders;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;

using Material = Prowl.Runtime.Resources.Material;
using Mesh = Prowl.Runtime.Resources.Mesh;
using Shader = Prowl.Runtime.Resources.Shader;

// TODO:
// 1. Image Effects need a Dispose method to clean up their resources, Camera needs to call it too

namespace Prowl.Runtime.Rendering;

public struct ViewerData
{
    public Double3 Position;
    public Double3 Forward;
    public Double3 Up;
    public Double3 Right;

    public ViewerData(DefaultRenderPipeline.CameraSnapshot css)
    {
        Position = css.CameraPosition;
        Forward = css.CameraForward;
        Up = css.CameraUp;
        Right = css.CameraRight;
    }

    public ViewerData(Double3 position, Double3 forward, Double3 right, Double3 up) : this()
    {
        Position = position;
        Forward = forward;
        Right = right;
        Up = up;
    }
}

/// <summary>
/// Default rendering pipeline implementation that handles standard forward rendering,
/// post-processing effects, shadows, and debug visualization.
/// </summary>
public class DefaultRenderPipeline : RenderPipeline
{
    const bool CAMERA_RELATIVE = false;


    #region Static Resources

    private static Mesh s_quadMesh;
    private static Mesh s_skyDome;
    private static Material s_defaultMaterial;
    private static Material s_skybox;
    private static Material s_gizmo;
    private static Material s_deferredLighting;
    private static Material s_deferredCompose;

    private static RenderTexture? s_shadowMap;

    public static DefaultRenderPipeline Default { get; } = new();
    public static HashSet<int> ActiveObjectIds { get => s_activeObjectIds; set => s_activeObjectIds = value; }

    private static Dictionary<int, Double4x4> s_prevModelMatrices = [];
    private static HashSet<int> s_activeObjectIds = [];
    private const int CLEANUP_INTERVAL_FRAMES = 120; // Clean up every 120 frames
    private static int s_framesSinceLastCleanup = 0;

    #endregion

    #region Resource Management

    private static void ValidateDefaults()
    {
        s_quadMesh ??= Mesh.GetFullscreenQuad();
        s_defaultMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        s_skybox ??= new Material(Shader.LoadDefault(DefaultShader.ProceduralSkybox));
        s_gizmo ??= new Material(Shader.LoadDefault(DefaultShader.Gizmos));

        // Load deferred shaders
        s_deferredLighting ??= new Material(Shader.LoadDefault(DefaultShader.DeferredLighting));
        s_deferredCompose ??= new Material(Shader.LoadDefault(DefaultShader.DeferredCompose));

        if (s_skyDome.IsNotValid())
        {
            Model skyDomeModel = Model.LoadDefault(DefaultModel.SkyDome) ?? throw new Exception("SkyDome model not found. Please ensure the model is included in the project.");
            s_skyDome = skyDomeModel.Meshes[0].Mesh;
        }
    }

    private static void CleanupUnusedModelMatrices()
    {
        // Increment frame counter
        s_framesSinceLastCleanup++;

        // Only perform cleanup at specified interval
        if (s_framesSinceLastCleanup < CLEANUP_INTERVAL_FRAMES)
            return;

        s_framesSinceLastCleanup = 0;

        // Remove all matrices that weren't used in this frame
        var unusedKeys = s_prevModelMatrices.Keys
            .Where(key => !ActiveObjectIds.Contains(key))
            .ToList();

        foreach (int key in unusedKeys)
            s_prevModelMatrices.Remove(key);

        // Clear the active IDs set for next frame
        ActiveObjectIds.Clear();
    }

    private static void TrackModelMatrix(int objectId, Double4x4 currentModel)
    {
        // Mark this object ID as active this frame
        ActiveObjectIds.Add(objectId);

        // Store current model matrix for next frame
        if (s_prevModelMatrices.TryGetValue(objectId, out Double4x4 prevModel))
            PropertyState.SetGlobalMatrix("prowl_PrevObjectToWorld", prevModel);
        else
            PropertyState.SetGlobalMatrix("prowl_PrevObjectToWorld", currentModel); // First frame, use current matrix

        s_prevModelMatrices[objectId] = currentModel;
    }

    #endregion

    #region Main Rendering

    public override void Render(Camera camera, in RenderingData data)
    {
        ValidateDefaults();

        // Main rendering with correct order of operations
        Internal_Render(camera, data);

        PropertyState.ClearGlobals();

        // Clean up unused matrices after rendering
        CleanupUnusedModelMatrices();
    }

    private static (List<ImageEffect> all, List<ImageEffect> opaque, List<ImageEffect> final) GatherImageEffects(Camera camera)
    {
        var all = new List<ImageEffect>();
        var opaqueEffects = new List<ImageEffect>();
        var finalEffects = new List<ImageEffect>();

        foreach (ImageEffect effect in camera.Effects)
        {
            all.Add(effect);

            if (effect.IsOpaqueEffect)
                opaqueEffects.Add(effect);
            else
                finalEffects.Add(effect);
        }

        return (all, opaqueEffects, finalEffects);
    }

    private static void SetupGlobalUniforms(CameraSnapshot css)
    {
        // Set View Rect
        //buffer.SetViewports((int)(camera.Viewrect.x * target.Width), (int)(camera.Viewrect.y * target.Height), (int)(camera.Viewrect.width * target.Width), (int)(camera.Viewrect.height * target.Height), 0, 1000);

        GlobalUniforms.SetPrevViewProj(css.PreviousViewProj);

        // Setup Default Uniforms for this frame
        // Camera
        GlobalUniforms.SetWorldSpaceCameraPos(CAMERA_RELATIVE ? Double3.Zero : css.CameraPosition);
        GlobalUniforms.SetProjectionParams(new Double4(1.0f, css.NearClipPlane, css.FarClipPlane, 1.0f / css.FarClipPlane));
        GlobalUniforms.SetScreenParams(new Double4(css.PixelWidth, css.PixelHeight, 1.0f + 1.0f / css.PixelWidth, 1.0f + 1.0f / css.PixelHeight));

        // Time
        GlobalUniforms.SetTime(new Double4(Time.TimeSinceStartup / 20, Time.TimeSinceStartup, Time.TimeSinceStartup * 2, Time.FrameCount));
        GlobalUniforms.SetSinTime(new Double4(Math.Sin(Time.TimeSinceStartup / 8), Math.Sin(Time.TimeSinceStartup / 4), Math.Sin(Time.TimeSinceStartup / 2), Math.Sin(Time.TimeSinceStartup)));
        GlobalUniforms.SetCosTime(new Double4(Math.Cos(Time.TimeSinceStartup / 8), Math.Cos(Time.TimeSinceStartup / 4), Math.Cos(Time.TimeSinceStartup / 2), Math.Cos(Time.TimeSinceStartup)));
        GlobalUniforms.SetDeltaTime(new Double4(Time.DeltaTime, 1.0f / Time.DeltaTime, Time.SmoothDeltaTime, 1.0f / Time.SmoothDeltaTime));

        // Upload the global uniform buffer
        GlobalUniforms.Upload();
    }

    private static void AssignCameraMatrices(Double4x4 view, Double4x4 projection)
    {
        GlobalUniforms.SetMatrixV(view);
        GlobalUniforms.SetMatrixIV(view.Invert());
        GlobalUniforms.SetMatrixP(projection);
        GlobalUniforms.SetMatrixVP(projection * view);

        // Upload the global uniform buffer
        GlobalUniforms.Upload();
    }

    #endregion

    #region Scene Rendering

    public struct CameraSnapshot(Camera camera)
    {
        public Scene Scene = camera.Scene;

        public Double3 CameraPosition = camera.Transform.Position;
        public Double3 CameraRight = camera.Transform.Right;
        public Double3 CameraUp = camera.Transform.Up;
        public Double3 CameraForward = camera.Transform.Forward;
        public LayerMask CullingMask = camera.CullingMask;
        public CameraClearFlags ClearFlags = camera.ClearFlags;
        public double NearClipPlane = camera.NearClipPlane;
        public double FarClipPlane = camera.FarClipPlane;
        public uint PixelWidth = camera.PixelWidth;
        public uint PixelHeight = camera.PixelHeight;
        public double Aspect = camera.Aspect;
        public Double4x4 OriginView = camera.OriginViewMatrix;
        public Double4x4 View = CAMERA_RELATIVE ? camera.OriginViewMatrix : camera.ViewMatrix;
        public Double4x4 ViewInverse = (CAMERA_RELATIVE ? camera.OriginViewMatrix : camera.ViewMatrix).Invert();
        public Double4x4 Projection = camera.ProjectionMatrix;
        public Double4x4 PreviousViewProj = camera.PreviousViewProjectionMatrix;
        public Frustum WorldFrustum = Frustum.FromMatrix(camera.ProjectionMatrix * camera.ViewMatrix);
        public DepthTextureMode DepthTextureMode = camera.DepthTextureMode; // Flags, Can be None, Normals, MotionVectors
    }

    private static void Internal_Render(Camera camera, in RenderingData data)
    {
        // =======================================================
        // 0. Setup variables, and prepare the camera
        bool isHDR = camera.HDR;
        (List<ImageEffect> all, List<ImageEffect> opaqueEffects, List<ImageEffect> finalEffects) = GatherImageEffects(camera);
        IReadOnlyList<IRenderableLight> lights = camera.GameObject.Scene.Lights;
        Double3 sunDirection = GetSunDirection(lights);
        RenderTexture target = camera.UpdateRenderData();

        PropertyState.SetGlobalVector("_SunDir", sunDirection);

        // =======================================================
        // 1. Pre Cull
        foreach (ImageEffect effect in all)
            effect.OnPreCull(camera);

        // =======================================================
        // 2. Take a snapshot of all Camera data
        CameraSnapshot css = new(camera);
        SetupGlobalUniforms(css);

        // =======================================================
        // 3. Cull Renderables based on Snapshot data
        IReadOnlyList<IRenderable> renderables = camera.GameObject.Scene.Renderables;
        HashSet<int> culledRenderableIndices = CullRenderables(renderables, css.WorldFrustum, css.CullingMask);

        // =======================================================
        // 4. Pre Render
        foreach (ImageEffect effect in all)
            effect.OnPreRender(camera);

        // =======================================================
        // 5. Setup Lighting and Shadows
        SetupLightingAndShadows(css, lights, renderables);

        // 5.1 Re-Assign camera matrices (The Lighting can modify these)
        AssignCameraMatrices(css.View, css.Projection);

        // =======================================================
        // 6. Create GBuffer for Deferred Rendering
        // GBuffer layout:
        // BufferA: RGB = Albedo, A = Alpha
        // BufferB: RGB = Normal (view space), A = ShadingMode
        // BufferC: R = Roughness, G = Metalness, B = Specular, A = AO
        // BufferD: Custom Data per Shading Mode (e.g., Emissive for Lit mode)
        RenderTexture gBuffer = RenderTexture.GetTemporaryRT((int)css.PixelWidth, (int)css.PixelHeight, true, [
            TextureImageFormat.Float4, // BufferA - Albedo + Alpha
            TextureImageFormat.Float4, // BufferB - Normal + ShadingMode
            TextureImageFormat.Float4, // BufferC - Roughness, Metalness, Specular, AO
            TextureImageFormat.Float4, // BufferD - Custom Data (Emissive, etc.)
            ]);

        // Bind GBuffer as the target
        Graphics.Device.BindFramebuffer(gBuffer.frameBuffer);
        // 6.1 Clear GBuffer
        switch (camera.ClearFlags)
        {
            case CameraClearFlags.Skybox:
                Graphics.Device.Clear(
                    (float)camera.ClearColor.R,
                    (float)camera.ClearColor.G,
                    (float)camera.ClearColor.B,
                    (float)camera.ClearColor.A,
                    ClearFlags.Color | ClearFlags.Depth
                );

                RenderSkybox(css);
                break;

            case CameraClearFlags.SolidColor:
                Graphics.Device.Clear(
                    (float)camera.ClearColor.R,
                    (float)camera.ClearColor.G,
                    (float)camera.ClearColor.B,
                    (float)camera.ClearColor.A,
                    ClearFlags.Color | ClearFlags.Depth
                );
                break;

            case CameraClearFlags.Depth:
                Graphics.Device.Clear(0, 0, 0, 0, ClearFlags.Depth);
                break;

            case CameraClearFlags.Nothing:
                // Do not clear anything
                break;
        }

        // 6.2 Draw opaque geometry to GBuffer
        DrawRenderables(renderables, "RenderOrder", "Opaque", new ViewerData(css), culledRenderableIndices, false);

        // =======================================================
        // 7. Deferred Lighting Pass - Render each light's contribution
        // Create light accumulation buffer
        RenderTexture lightAccumulation = RenderTexture.GetTemporaryRT((int)camera.PixelWidth, (int)camera.PixelHeight, false, [
            isHDR ? TextureImageFormat.Float4 : TextureImageFormat.Color4b, // Accumulated lighting
            ]);

        // Set GBuffer textures as global textures for shaders
        PropertyState.SetGlobalTexture("_GBufferA", gBuffer.InternalTextures[0]);
        PropertyState.SetGlobalTexture("_GBufferB", gBuffer.InternalTextures[1]);
        PropertyState.SetGlobalTexture("_GBufferC", gBuffer.InternalTextures[2]);
        PropertyState.SetGlobalTexture("_GBufferD", gBuffer.InternalTextures[3]);
        PropertyState.SetGlobalTexture("_CameraDepthTexture", gBuffer.InternalDepth);

        // Clear light accumulation to black
        Graphics.Device.BindFramebuffer(lightAccumulation.frameBuffer);
        Graphics.Device.Clear(0, 0, 0, 0, ClearFlags.Color);

        // Render each light's contribution (additive blending)
        foreach (IRenderableLight light in lights)
        {
            if (css.CullingMask.HasLayer(light.GetLayer()) == false)
                continue;

            // Only render directional lights for now
            if (light is DirectionalLight dirLight)
            {
                dirLight.OnRenderLight(gBuffer, lightAccumulation, css);
            }
        }

        // =======================================================
        // 8. Deferred Composition Pass - Combine light accumulation with GBuffer
        // Create final composition output
        RenderTexture composedOutput = RenderTexture.GetTemporaryRT((int)camera.PixelWidth, (int)camera.PixelHeight, true, [
            isHDR ? TextureImageFormat.Float4 : TextureImageFormat.Color4b,
            ]);

        // Set GBuffer and light textures for compose shader
        s_deferredCompose.SetTexture("_LightAccumulation", lightAccumulation.InternalTextures[0]);
        s_deferredCompose.SetTexture("_GBufferA", gBuffer.InternalTextures[0]);
        s_deferredCompose.SetTexture("_GBufferB", gBuffer.InternalTextures[1]);
        s_deferredCompose.SetTexture("_GBufferD", gBuffer.InternalTextures[3]);
        s_deferredCompose.SetTexture("_CameraDepthTexture", gBuffer.InternalDepth);

        // Set fog parameters
        Scene.FogParams fog = css.Scene.Fog;
        Double4 fogParams = Double4.Zero;
        fogParams.X = fog.Density / 1.2011224; // density/sqrt(ln(2))
        fogParams.Y = fog.Density / 0.693147181; // ln(2)
        fogParams.Z = -1.0 / (fog.End - fog.Start);
        fogParams.W = fog.End / (fog.End - fog.Start);
        s_deferredCompose.SetColor("_FogColor", fog.Color);
        s_deferredCompose.SetVector("_FogParams", fogParams);
        s_deferredCompose.SetVector("_FogStates", new Double3(
            fog.Mode == Scene.FogParams.FogMode.Linear ? 1 : 0,
            fog.Mode == Scene.FogParams.FogMode.Exponential ? 1 : 0,
            fog.Mode == Scene.FogParams.FogMode.ExponentialSquared ? 1 : 0
        ));

        // Set ambient lighting parameters
        Scene.AmbientLightParams ambient = css.Scene.Ambient;
        s_deferredCompose.SetVector("_AmbientMode", new Double2(
            ambient.Mode == Scene.AmbientLightParams.AmbientMode.Uniform ? 1 : 0,
            ambient.Mode == Scene.AmbientLightParams.AmbientMode.Hemisphere ? 1 : 0
        ));
        s_deferredCompose.SetColor("_AmbientColor", ambient.Color);
        s_deferredCompose.SetColor("_AmbientSkyColor", ambient.SkyColor);
        s_deferredCompose.SetColor("_AmbientGroundColor", ambient.GroundColor);

        // Perform composition
        Graphics.Blit(lightAccumulation, composedOutput, s_deferredCompose, 0, false, false);

        // Copy depth from GBuffer to composed output for transparent rendering
        Graphics.Device.BindFramebuffer(gBuffer.frameBuffer, FBOTarget.Read);
        Graphics.Device.BindFramebuffer(composedOutput.frameBuffer, FBOTarget.Draw);
        Graphics.Device.BlitFramebuffer(0, 0, gBuffer.Width, gBuffer.Height, 0, 0, composedOutput.Width, composedOutput.Height, ClearFlags.Depth, BlitFilter.Nearest);

        // Bind composed output for transparent rendering
        Graphics.Device.BindFramebuffer(composedOutput.frameBuffer);

        // =======================================================
        // 9. Apply opaque post-processing effects
        if (opaqueEffects.Count > 0)
            DrawImageEffects(composedOutput, opaqueEffects, ref isHDR);

        // =======================================================
        // 10. Transparent geometry (Forward rendered on top of composed result)
        DrawRenderables(renderables, "RenderOrder", "Transparent", new ViewerData(css), culledRenderableIndices, false);

        // =======================================================
        // 11. Apply final post-processing effects
        if (finalEffects.Count > 0)
            DrawImageEffects(composedOutput, finalEffects, ref isHDR);

        // =======================================================
        // 12. Render Gizmos
        RenderGizmos(css);

        // =======================================================
        // 13. Blit Result to target, If target is null Blit will go to the Screen/Window
        Graphics.Blit(composedOutput, target, null, 0, false, false);

        // =======================================================
        // 14. Post Render
        foreach (ImageEffect effect in all)
            effect.OnPostRender(camera);

        // =======================================================
        // 15. Cleanup temporary render textures
        RenderTexture.ReleaseTemporaryRT(gBuffer);
        RenderTexture.ReleaseTemporaryRT(lightAccumulation);
        RenderTexture.ReleaseTemporaryRT(composedOutput);

        // Reset bound framebuffer if any is bound
        Graphics.Device.UnbindFramebuffer();
        Graphics.Device.Viewport(0, 0, (uint)Window.InternalWindow.FramebufferSize.X, (uint)Window.InternalWindow.FramebufferSize.Y);
    }

    private static HashSet<int> CullRenderables(IReadOnlyList<IRenderable> renderables, Frustum? worldFrustum, LayerMask cullingMask)
    {
        HashSet<int> culledRenderableIndices = [];
        for (int renderIndex = 0; renderIndex < renderables.Count; renderIndex++)
        {
            IRenderable renderable = renderables[renderIndex];

            if (worldFrustum != null && CullRenderable(renderable, worldFrustum.Value))
            {
                culledRenderableIndices.Add(renderIndex);
                continue;
            }

            if (cullingMask.HasLayer(renderable.GetLayer()) == false)
            {
                culledRenderableIndices.Add(renderIndex);
                continue;
            }
        }
        return culledRenderableIndices;
    }

    private static void SetupLightingAndShadows(CameraSnapshot css, IReadOnlyList<IRenderableLight> lights, IReadOnlyList<IRenderable> renderables)
    {
        CreateLightBuffer(css.CameraPosition, css.CullingMask, lights, renderables);

        if (s_shadowMap.IsValid())
            PropertyState.SetGlobalTexture("_ShadowAtlas", s_shadowMap.InternalDepth);
        //PropertyState.SetGlobalBuffer("_Lights", LightBuffer, 0);
        //PropertyState.SetGlobalInt("_LightCount", LightCount);
        GlobalUniforms.SetShadowAtlasSize(new Double2(ShadowAtlas.GetSize(), ShadowAtlas.GetSize()));
    }

    private static void CreateLightBuffer(Double3 cameraPosition, LayerMask cullingMask, IReadOnlyList<IRenderableLight> lights, IReadOnlyList<IRenderable> renderables)
    {
        Graphics.Device.BindFramebuffer(ShadowAtlas.GetAtlas().frameBuffer);
        Graphics.Device.Clear(0.0f, 0.0f, 0.0f, 1.0f, ClearFlags.Depth | ClearFlags.Stencil);

        // Find closest directional light
        DirectionalLight? closestDirectional = null;
        double closestDirDistSq = double.MaxValue;

        foreach (IRenderableLight light in lights)
        {
            if (cullingMask.HasLayer(light.GetLayer()) == false)
                continue;

            if (light is DirectionalLight dir)
            {
                Double3 toLight = light.GetLightPosition() - cameraPosition;
                double distanceSq = toLight.X * toLight.X + toLight.Y * toLight.Y + toLight.Z * toLight.Z;

                // Keep only the closest directional light
                if (distanceSq < closestDirDistSq)
                {
                    closestDirectional = dir;
                    closestDirDistSq = distanceSq;
                }
            }
        }

        // Process directional light shadows
        if (closestDirectional.IsValid())
        {
            ProcessLight(closestDirectional, Math.Sqrt(closestDirDistSq), cameraPosition, renderables);
        }
    }

    private static void ProcessLight(DirectionalLight light, double distance, Double3 cameraPosition, IReadOnlyList<IRenderable> renderables)
    {
        // Get shadow resolution
        int res = (int)light.ShadowResolution;

        if (light.DoCastShadows())
        {
            // Reserve space in shadow atlas
            Int2? slot = ShadowAtlas.ReserveTiles(res, res, light.GetLightID());

            int AtlasX, AtlasY, AtlasWidth;

            if (slot != null)
            {
                AtlasX = slot.Value.X;
                AtlasY = slot.Value.Y;
                AtlasWidth = res;

                // Draw the shadow map
                s_shadowMap = ShadowAtlas.GetAtlas();

                Double3 forward = -light.Transform.Forward; // directional light is inverted
                Double3 right = light.Transform.Right;
                Double3 up = light.Transform.Up;

                // Set range to -1 to indicate this is not a point light
                PropertyState.SetGlobalFloat("_PointLightRange", -1.0f);

                Graphics.Device.Viewport(slot.Value.X, slot.Value.Y, (uint)res, (uint)res);

                // Use camera-following shadow matrix for directional lights
                light.GetShadowMatrix(cameraPosition, res, out Double4x4 view, out Double4x4 proj);

                if (CAMERA_RELATIVE)
                    view.Translation *= new Double4(0, 0, 0, 1); // set all to 0 except W

                Frustum frustum = Frustum.FromMatrix(proj * view);

                HashSet<int> culledRenderableIndices = CullRenderables(renderables, frustum, LayerMask.Everything);
                AssignCameraMatrices(view, proj);
                DrawRenderables(renderables, "LightMode", "ShadowCaster", new ViewerData(light.GetLightPosition(), forward, right, up), culledRenderableIndices, false);
            }
            else
            {
                AtlasX = -1;
                AtlasY = -1;
                AtlasWidth = 0;
            }

            // Prepare shadow data for light to use during rendering
            light.PrepareShadowData(CAMERA_RELATIVE, cameraPosition, AtlasX, AtlasY, AtlasWidth);
        }
        else
        {
            // No shadows, still need to prepare light data
            light.PrepareShadowData(CAMERA_RELATIVE, cameraPosition, -1, -1, 0);
        }
    }

    private static Double3 GetSunDirection(IReadOnlyList<IRenderableLight> lights)
    {
        if (lights.Count > 0 && lights[0] is IRenderableLight light && light.GetLightType() == LightType.Directional)
            return light.GetLightDirection();
        return Double3.UnitY;
    }

    private static void RenderSkybox(CameraSnapshot css)
    {
        s_skybox.SetMatrix("prowl_MatVP", css.Projection * css.OriginView);

        // Set sun direction for skybox from scene's directional light
        Double3 sunDir = GetSunDirection(css.Scene.Lights);
        s_skybox.SetVector("_SunDir", sunDir);

        Graphics.DrawMeshNow(s_skyDome, s_skybox);
    }

    private static void RenderGizmos(CameraSnapshot css)
    {
        Double4x4 vp = css.Projection * css.View;
        (Mesh? wire, Mesh? solid) = Debug.GetGizmoDrawData(CAMERA_RELATIVE, css.CameraPosition);

        if (wire.IsValid() || solid.IsValid())
        {
            // The vertices have already been transformed by the gizmo system to be camera relative (if needed) so we just need to draw them
            s_gizmo.SetMatrix("prowl_MatVP", vp);
            if (wire.IsValid()) Graphics.DrawMeshNow(wire, s_gizmo);
            if (solid.IsValid()) Graphics.DrawMeshNow(solid, s_gizmo);
        }

        //List<GizmoBuilder.IconDrawCall> icons = Debug.GetGizmoIcons();
        //if (icons != null)
        //{
        //    buffer.SetMaterial(s_gizmo);
        //
        //    foreach (GizmoBuilder.IconDrawCall icon in icons)
        //    {
        //        Vector3 center = icon.center;
        //        if (CAMERA_RELATIVE)
        //            center -= css.cameraPosition;
        //        Matrix4x4 billboard = Matrix4x4.CreateBillboard(center, Vector3.zero, css.cameraUp, css.cameraForward);
        //
        //        buffer.SetMatrix("_Matrix_VP", (billboard * vp).ToFloat());
        //        buffer.SetTexture("_MainTex", icon.texture);
        //
        //        buffer.DrawSingle(s_quadMesh);
        //    }
        //}
    }

    #endregion

    private static void DrawImageEffects(RenderTexture forwardBuffer, List<ImageEffect> effects, ref bool isHDR)
    {
        return;
        // Early exit if no effects to process
        if (effects == null || effects.Count == 0)
            return;

        // Create two buffers for ping-pong rendering
        RenderTexture sourceBuffer = forwardBuffer;

        // Determine if we need to start in LDR mode
        bool firstEffectIsLDR = effects.Count > 0 && effects[0].TransformsToLDR;
        TextureImageFormat destFormat = isHDR && !firstEffectIsLDR ? TextureImageFormat.Float4 : TextureImageFormat.Color4b;

        // Create destination buffer
        RenderTexture destBuffer = RenderTexture.GetTemporaryRT(
            forwardBuffer.Width,
            forwardBuffer.Height,
            false,
            [destFormat]
        );

        // Update HDR flag if needed
        if (firstEffectIsLDR)
        {
            isHDR = false;
        }

        // Keep track of temporary render textures that need cleanup
        List<RenderTexture> tempTextures = [destBuffer];

        try
        {
            // Process each effect
            for (int i = 0; i < effects.Count; i++)
            {
                ImageEffect effect = effects[i];

                // Handle HDR to LDR transition
                if (isHDR && effect.TransformsToLDR)
                {
                    isHDR = false;

                    // If destination buffer is HDR, we need to replace it with LDR
                    if (destBuffer != forwardBuffer)
                    {
                        RenderTexture.ReleaseTemporaryRT(destBuffer);
                        tempTextures.Remove(destBuffer);
                    }

                    // Create new LDR destination buffer
                    destBuffer = RenderTexture.GetTemporaryRT(
                        forwardBuffer.Width,
                        forwardBuffer.Height,
                        false,
                        [TextureImageFormat.Color4b]
                    );

                    if (destBuffer != forwardBuffer)
                    {
                        tempTextures.Add(destBuffer);
                    }
                }

                // Apply the effect
                effect.OnRenderImage(sourceBuffer, destBuffer);

                // Swap buffers for next iteration
                (destBuffer, sourceBuffer) = (sourceBuffer, destBuffer);

                // Update temp texture tracking after swap
                // sourceBuffer now contains the result, destBuffer is the old source
                if (sourceBuffer != forwardBuffer && !tempTextures.Contains(sourceBuffer))
                {
                    tempTextures.Add(sourceBuffer);
                }
                if (destBuffer == forwardBuffer)
                {
                    tempTextures.Remove(destBuffer);
                }
            }

            // After all effects, copy result back to forwardBuffer if needed
            if (sourceBuffer != forwardBuffer)
            {
                Graphics.Device.BindFramebuffer(sourceBuffer.frameBuffer, FBOTarget.Read);
                Graphics.Device.BindFramebuffer(forwardBuffer.frameBuffer, FBOTarget.Draw);
                Graphics.Device.BlitFramebuffer(
                    0, 0, sourceBuffer.Width, sourceBuffer.Height,
                    0, 0, forwardBuffer.Width, forwardBuffer.Height,
                    ClearFlags.Color, BlitFilter.Nearest
                );
            }
        }
        catch (Exception ex)
        {
            // Re-throw the exception after cleanup
            throw new Exception($"Error in DrawImageEffects: {ex.Message}", ex);
        }
        finally
        {
            // Clean up all temporary render textures
            foreach (RenderTexture tempRT in tempTextures)
            {
                if (tempRT != forwardBuffer)
                {
                    RenderTexture.ReleaseTemporaryRT(tempRT);
                }
            }
        }
    }

    /// <summary>
    /// Represents a render batch: a group of objects sharing the same material, mesh, and shader pass.
    /// Batching reduces GPU state changes by binding material uniforms once for all objects in the batch.
    /// </summary>
    private struct RenderBatch
    {
        public Material Material;      // Shared material for all objects in this batch
        public Mesh Mesh;              // Shared mesh for all objects in this batch
        public int PassIndex;          // Shader pass index
        public ulong MaterialHash;     // Hash of material uniforms (for sorting/grouping)
        public List<int> RenderableIndices;  // Indices of objects in this batch
    }

    /// <summary>
    /// Renders all given objects with optimized batching. Objects are grouped by (material, mesh, pass)
    /// to minimize GPU state changes. This achieves:
    /// - Material uniforms bound once per batch (instead of per object)
    /// - Mesh data uploaded once per batch
    /// - Shader variant selected once per batch
    /// - Per-object uniforms still bound individually
    ///
    /// Performance: 100 objects with same material = 1 material bind (vs 100 without batching)
    /// </summary>
    private static void DrawRenderables(IReadOnlyList<IRenderable> renderables, string shaderTag, string tagValue, ViewerData viewer, HashSet<int> culledRenderableIndices, bool updatePreviousMatrices)
    {
        bool hasRenderOrder = !string.IsNullOrWhiteSpace(shaderTag);

        // ========== PHASE 1: Build Batches ==========
        // Group renderables by (material hash, shader pass, mesh) for efficient rendering
        List<RenderBatch> batches = new();
        Dictionary<(ulong, int, Mesh), int> batchLookup = new();

        for (int renderIndex = 0; renderIndex < renderables.Count; renderIndex++)
        {
            // Skip culled objects
            if (culledRenderableIndices?.Contains(renderIndex) ?? false)
                continue;

            IRenderable renderable = renderables[renderIndex];
            Material material = renderable.GetMaterial();
            if (material.Shader.IsNotValid()) continue;

            // Extract mesh for batching (we query rendering data once here for grouping)
            renderable.GetRenderingData(viewer, out PropertyState _, out Mesh mesh, out Double4x4 _);
            if (mesh == null || mesh.VertexCount <= 0) continue;

            // Get material hash for batching - materials with identical uniforms will batch together
            ulong materialHash = material.GetStateHash();

            // Find ALL shader passes matching the requested tag (e.g., "Opaque", "Transparent", "ShadowCaster")
            // Multi-pass rendering: materials can have multiple passes with the same tag (e.g., terrain with many texture layers)
            int passIndex = -1;
            foreach (ShaderPass pass in material.Shader.Passes)
            {
                passIndex++;

                if (hasRenderOrder && !pass.HasTag(shaderTag, tagValue))
                    continue;

                // Found matching pass - add to appropriate batch
                // Batch key: (material hash, pass index, mesh) ensures each pass gets its own batch
                var batchKey = (materialHash, passIndex, mesh);
                if (batchLookup.TryGetValue(batchKey, out int batchIndex))
                {
                    // Batch already exists - add this object to it
                    batches[batchIndex].RenderableIndices.Add(renderIndex);
                }
                else
                {
                    // Create new batch for this unique material+pass+mesh combination
                    RenderBatch newBatch = new()
                    {
                        Material = material,
                        Mesh = mesh,
                        PassIndex = passIndex,
                        MaterialHash = materialHash,
                        RenderableIndices = new() { renderIndex }
                    };
                    batchLookup[batchKey] = batches.Count;
                    batches.Add(newBatch);
                }

                // Continue to next pass - materials can have multiple passes with the same tag
                // They will execute in order they appear in the shader file (Pass 0 → Pass 1 → Pass 2, etc.)
            }
        }

        // ========== PHASE 2: Draw Batches ==========
        // For each batch, bind state once then draw all objects in that batch
        foreach (RenderBatch batch in batches)
        {
            Material material = batch.Material;
            Mesh mesh = batch.Mesh;
            int passIndex = batch.PassIndex;

            // Configure shader keywords based on mesh attributes (normals, UVs, skinning, etc.)
            // Since all objects in the batch share the same mesh, this is done once per batch
            material.SetKeyword("HAS_NORMALS", mesh.HasNormals);
            material.SetKeyword("HAS_TANGENTS", mesh.HasTangents);
            material.SetKeyword("HAS_UV", mesh.HasUV);
            material.SetKeyword("HAS_UV2", mesh.HasUV2);
            material.SetKeyword("HAS_COLORS", mesh.HasColors || mesh.HasColors32);
            material.SetKeyword("HAS_BONEINDICES", mesh.HasBoneIndices);
            material.SetKeyword("HAS_BONEWEIGHTS", mesh.HasBoneWeights);
            material.SetKeyword("SKINNED", mesh.HasBoneIndices && mesh.HasBoneWeights);

            // Get shader pass and compiled variant for current keyword state
            ShaderPass pass = material.Shader.GetPass(passIndex);
            if (!pass.TryGetVariantProgram(material._localKeywords, out GraphicsProgram? variantNullable) || variantNullable == null)
                continue;

            GraphicsProgram variant = variantNullable;

            // Bind GlobalUniforms buffer (contains camera matrices, time, lighting data, etc.)
            // This is done per-batch because each shader variant is a separate GPU program object,
            // and uniform buffer bindings are per-program in OpenGL.
            //
            // TODO: Could be optimized with glBindBufferBase() for global binding points (OpenGL >=4.2)
            // Researched: We're limited to OpenGL <=4.1 for macOS support, which doesn't support
            // persistent uniform buffer bindings across programs. Current approach is correct for <=4.1.
            GraphicsBuffer? globalBuffer = GlobalUniforms.GetBuffer();
            if (globalBuffer != null)
            {
                Graphics.Device.BindUniformBuffer(variant, "GlobalUniforms", globalBuffer, 0);
            }

            // Apply global properties (lighting, fog, shadow maps, etc.)
            // Must be done per-batch because different shader variants may need different globals
            GraphicsProgram.UniformCache cache = variant.uniformCache;
            int texSlot = 0;
            PropertyState.ApplyGlobals(variant, cache, ref texSlot);

            // *** BATCHING OPTIMIZATION: Bind material uniforms ONCE for entire batch ***
            // All objects in this batch share the same material state
            PropertyState.ApplyMaterialUniforms(material._properties, variant, ref texSlot);

            // Set render state (depth test, blend mode, cull mode, etc.) once per batch
            Graphics.Device.SetState(pass.State);

            // Upload mesh data to GPU once per batch (shared by all objects)
            mesh.Upload();

            // ========== PHASE 3: Draw Objects in Batch ==========
            // Material/mesh state is already bound - only per-object uniforms change
            foreach (int renderIndex in batch.RenderableIndices)
            {
                IRenderable renderable = renderables[renderIndex];

                // Get per-object data (transform, instance properties)
                // Note: mesh is discarded (we already have it from the batch)
                renderable.GetRenderingData(viewer, out PropertyState properties, out Mesh _, out Double4x4 model);

                // Track model matrix for motion vectors (used in temporal effects like TAA)
                int instanceId = properties.GetInt("_ObjectID");
                if (updatePreviousMatrices && instanceId != 0)
                    TrackModelMatrix(instanceId, model);

                // Camera-relative rendering: subtract camera position to improve depth precision
                if (CAMERA_RELATIVE)
                    model.Translation -= new Double4(viewer.Position, 0.0);

                // Apply instance-specific uniforms (tint colors, bone matrices, etc.)
                // Texture slot counter continues from where material textures left off
                int instanceTexSlot = texSlot;
                PropertyState.ApplyInstanceUniforms(properties, variant, ref instanceTexSlot);

                // Directly bind per-object transform uniforms after all other uniforms to gaurantee they are set correctly
                Graphics.Device.SetUniformMatrix(variant, "prowl_ObjectToWorld", false, (Float4x4)model);
                Graphics.Device.SetUniformMatrix(variant, "prowl_WorldToObject", false, (Float4x4)model.Invert());

                // Execute draw call (mesh VAO already uploaded, just bind and draw)
                unsafe
                {
                    Graphics.Device.BindVertexArray(mesh.VertexArrayObject);
                    Graphics.Device.DrawIndexed(mesh.MeshTopology, (uint)mesh.IndexCount, mesh.IndexFormat == IndexFormat.UInt32, null);
                    Graphics.Device.BindVertexArray(null);
                }
            }
        }
    }

    private static bool CullRenderable(IRenderable renderable, Frustum cameraFrustum)
    {
        renderable.GetCullingData(out bool isRenderable, out AABB bounds);

        return !isRenderable || !cameraFrustum.Intersects(bounds);
    }
}
