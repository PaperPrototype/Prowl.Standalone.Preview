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

        // Fog
        Scene.FogParams fog = css.Scene.Fog;
        Double4 fogParams;
        fogParams.X = fog.Density / Maths.Sqrt(0.693147181); // ln(2)
        fogParams.Y = fog.Density / 0.693147181; // ln(2)
        fogParams.Z = -1.0 / (fog.End - fog.Start);
        fogParams.W = fog.End / (fog.End - fog.Start);
        GlobalUniforms.SetFogColor(fog.Color);
        GlobalUniforms.SetFogParams(fogParams);
        GlobalUniforms.SetFogStates(new Float3(
            fog.Mode == Scene.FogParams.FogMode.Linear ? 1 : 0,
            fog.Mode == Scene.FogParams.FogMode.Exponential ? 1 : 0,
            fog.Mode == Scene.FogParams.FogMode.ExponentialSquared ? 1 : 0
            ));

        // Ambient Lighting
        Scene.AmbientLightParams ambient = css.Scene.Ambient;
        GlobalUniforms.SetAmbientMode(new Double2(
            ambient.Mode == Scene.AmbientLightParams.AmbientMode.Uniform ? 1 : 0,
            ambient.Mode == Scene.AmbientLightParams.AmbientMode.Hemisphere ? 1 : 0
        ));

        GlobalUniforms.SetAmbientColor(ambient.Color);
        GlobalUniforms.SetAmbientSkyColor(ambient.SkyColor);
        GlobalUniforms.SetAmbientGroundColor(ambient.GroundColor);

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
        public Frustrum WorldFrustum = Frustrum.FromMatrix(camera.ProjectionMatrix * camera.ViewMatrix);
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
        HashSet<int> culledRenderableIndices = [];// CullRenderables(renderables, css.worldFrustum);

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
        // 6. Pre-Depth Pass
        // We draw objects to get the DepthBuffer but we also draw it into a ColorBuffer so we upload it as a Sampleable Texture
        RenderTexture preDepth = RenderTexture.GetTemporaryRT((int)css.PixelWidth, (int)css.PixelHeight, true, []);

        // Bind depth texture as the target
        Graphics.Device.BindFramebuffer(preDepth.frameBuffer);
        Graphics.Device.Clear(0f, 0f, 0, 0f, ClearFlags.Depth | ClearFlags.Stencil);

        // Draw depth for all visible objects
        DrawRenderables(renderables, "RenderOrder", "DepthOnly", new ViewerData(css), culledRenderableIndices, false);

        // =======================================================
        // 6.1. Set the depth texture for use in post-processing
        PropertyState.SetGlobalTexture("_CameraDepthTexture", preDepth.InternalDepth);

        // =======================================================
        // 7. Opaque geometry
        RenderTexture forwardBuffer = RenderTexture.GetTemporaryRT((int)camera.PixelWidth, (int)camera.PixelHeight, true, [
            isHDR ? TextureImageFormat.Float4 : TextureImageFormat.Color4b, // Albedo
            TextureImageFormat.Float2, // Motion Vectors
            TextureImageFormat.Float3, // Normals
            TextureImageFormat.Float2, // Surface
            ]);

        // Copy the depth buffer to the forward buffer
        // This is technically not needed, however, a big reason people do a Pre-Depth pass outside post processing like SSAO
        // Is so the GPU can early cull lighting calculations in forward rendering
        // This turns Forward rendering into essentially deferred in the eyes of lighting, as it now only calculates lighting for pixels that are actually visible
        Graphics.Device.BindFramebuffer(preDepth.frameBuffer, FBOTarget.Read);
        Graphics.Device.BindFramebuffer(forwardBuffer.frameBuffer, FBOTarget.Draw);
        Graphics.Device.BlitFramebuffer(0, 0, preDepth.Width, preDepth.Height, 0, 0, forwardBuffer.Width, forwardBuffer.Height, ClearFlags.Depth, BlitFilter.Nearest);

        // 7.1 Bind the forward buffer fully, The bit only binds it for Drawing into, We need to bind it for reading too
        Graphics.Device.BindFramebuffer(forwardBuffer.frameBuffer);
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

        DrawRenderables(renderables, "RenderOrder", "Opaque", new ViewerData(css), culledRenderableIndices, true);

        // 8.1 Set the Motion Vectors Texture for use in post-processing
        PropertyState.SetGlobalTexture("_CameraMotionVectorsTexture", forwardBuffer.InternalTextures[1]);
        // 8.2 Set the Normals Texture for use in post-processing
        PropertyState.SetGlobalTexture("_CameraNormalsTexture", forwardBuffer.InternalTextures[2]);
        // 8.3 Set the Surface Texture for use in post-processing
        PropertyState.SetGlobalTexture("_CameraSurfaceTexture", forwardBuffer.InternalTextures[3]);

        // 9. Apply opaque post-processing effects
        if (opaqueEffects.Count > 0)
            DrawImageEffects(forwardBuffer, opaqueEffects, ref isHDR);

        // 10. Transparent geometry
        DrawRenderables(renderables, "RenderOrder", "Transparent", new ViewerData(css), culledRenderableIndices, false);

        // 11. Apply final post-processing effects
        if (finalEffects.Count > 0)
            DrawImageEffects(forwardBuffer, finalEffects, ref isHDR);


        //if (data.DisplayGizmo)
        RenderGizmos(css);

        // 12. Blit the Result to the camera's Target whether thats the Screen or a RenderTexture

        // 13. Blit Result to target, If target is null Blit will go to the Screen/Window
        Graphics.Blit(forwardBuffer, target, null, 0, false, false);

        // 14. Post Render
        foreach (ImageEffect effect in all)
            effect.OnPostRender(camera);


        RenderTexture.ReleaseTemporaryRT(preDepth);
        RenderTexture.ReleaseTemporaryRT(forwardBuffer);

        // Reset bound framebuffer if any is bound
        Graphics.Device.UnbindFramebuffer();
        Graphics.Device.Viewport(0, 0, (uint)Window.InternalWindow.FramebufferSize.X, (uint)Window.InternalWindow.FramebufferSize.Y);
    }

    private static HashSet<int> CullRenderables(IReadOnlyList<IRenderable> renderables, Frustrum? worldFrustum, LayerMask cullingMask)
    {
        HashSet<int> culledRenderableIndices = [];
        for (int renderIndex = 0; renderIndex < renderables.Count; renderIndex++)
        {
            IRenderable renderable = renderables[renderIndex];

            //if (worldFrustum != null && CullRenderable(renderable, worldFrustum))
            //{
            //    culledRenderableIndices.Add(renderIndex);
            //    continue;
            //}

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
        ShadowAtlas.TryInitialize();
        ShadowAtlas.Clear();

        CreateLightBuffer(css.CameraPosition, css.CullingMask, lights, renderables);

        if (s_shadowMap.IsValid())
            PropertyState.SetGlobalTexture("_ShadowAtlas", s_shadowMap.InternalDepth);
        //PropertyState.SetGlobalBuffer("_Lights", LightBuffer, 0);
        //PropertyState.SetGlobalInt("_LightCount", LightCount);
        GlobalUniforms.SetShadowAtlasSize(new Double2(ShadowAtlas.GetSize(), ShadowAtlas.GetSize()));
    }

    // Reusable arrays to avoid allocations per frame
    private static (IRenderableLight light, double distanceSq)[] s_tempSpotLights = new (IRenderableLight, double)[32];
    private static (IRenderableLight light, double distanceSq)[] s_tempPointLights = new (IRenderableLight, double)[32];
    private static int s_tempSpotCount = 0;
    private static int s_tempPointCount = 0;

    private static void CreateLightBuffer(Double3 cameraPosition, LayerMask cullingMask, IReadOnlyList<IRenderableLight> lights, IReadOnlyList<IRenderable> renderables)
    {
        Graphics.Device.BindFramebuffer(ShadowAtlas.GetAtlas().frameBuffer);
        Graphics.Device.Clear(0.0f, 0.0f, 0.0f, 1.0f, ClearFlags.Depth | ClearFlags.Stencil);

        // We have AtlasWidth slots for shadow maps
        // a single shadow map can consume multiple slots if its larger then 128x128
        // We need to distribute these slots and resolutions out to lights
        // based on their distance from the camera
        int numDirLights = 0;
        int spotLightIndex = 0;
        int pointLightIndex = 0;
        const int MAX_SPOT_LIGHTS = 4;
        const int MAX_POINT_LIGHTS = 4;

        // Reset temp counts
        s_tempSpotCount = 0;
        s_tempPointCount = 0;
        DirectionalLight? closestDirectional = null;
        double closestDirDistSq = double.MaxValue;

        // Single pass: separate by type and calculate squared distances (faster than Distance)
        foreach (IRenderableLight light in lights)
        {
            if (cullingMask.HasLayer(light.GetLayer()) == false)
                continue;

            Double3 toLight = light.GetLightPosition() - cameraPosition;
            double distanceSq = toLight.X * toLight.X + toLight.Y * toLight.Y + toLight.Z * toLight.Z;

            if (light is DirectionalLight dir)
            {
                // Keep only the closest directional light
                if (distanceSq < closestDirDistSq)
                {
                    closestDirectional = dir;
                    closestDirDistSq = distanceSq;
                }
            }
            else if (light is SpotLight)
            {
                // Grow array if needed
                if (s_tempSpotCount >= s_tempSpotLights.Length)
                    Array.Resize(ref s_tempSpotLights, s_tempSpotLights.Length * 2);

                s_tempSpotLights[s_tempSpotCount++] = (light, distanceSq);
            }
            else if (light is PointLight)
            {
                // Grow array if needed
                if (s_tempPointCount >= s_tempPointLights.Length)
                    Array.Resize(ref s_tempPointLights, s_tempPointLights.Length * 2);

                s_tempPointLights[s_tempPointCount++] = (light, distanceSq);
            }
        }

        // Partial sort: only sort enough to get the N closest lights
        // This is O(n log k) instead of O(n log n) where k = MAX_LIGHTS
        PartialSort(s_tempSpotLights, s_tempSpotCount, MAX_SPOT_LIGHTS);
        PartialSort(s_tempPointLights, s_tempPointCount, MAX_POINT_LIGHTS);

        // Process directional light first
        if (closestDirectional.IsValid())
        {
            ProcessLight(closestDirectional, Math.Sqrt(closestDirDistSq), cameraPosition, renderables, ref numDirLights, ref spotLightIndex, ref pointLightIndex, MAX_SPOT_LIGHTS, MAX_POINT_LIGHTS);
        }

        // Process closest spot lights
        int spotLightsToProcess = Math.Min(s_tempSpotCount, MAX_SPOT_LIGHTS);
        for (int i = 0; i < spotLightsToProcess; i++)
        {
            (IRenderableLight light, double distanceSq) = s_tempSpotLights[i];
            ProcessLight(light, Math.Sqrt(distanceSq), cameraPosition, renderables, ref numDirLights, ref spotLightIndex, ref pointLightIndex, MAX_SPOT_LIGHTS, MAX_POINT_LIGHTS);
        }

        // Process closest point lights
        int pointLightsToProcess = Math.Min(s_tempPointCount, MAX_POINT_LIGHTS);
        for (int i = 0; i < pointLightsToProcess; i++)
        {
            (IRenderableLight light, double distanceSq) = s_tempPointLights[i];
            ProcessLight(light, Math.Sqrt(distanceSq), cameraPosition, renderables, ref numDirLights, ref spotLightIndex, ref pointLightIndex, MAX_SPOT_LIGHTS, MAX_POINT_LIGHTS);
        }

        // Set the light counts in global uniforms
        GlobalUniforms.SetSpotLightCount(spotLightIndex);
        GlobalUniforms.SetPointLightCount(pointLightIndex);
        GlobalUniforms.Upload();
    }

    // Partial sort: only sorts the first 'k' elements, much faster when k << n
    private static void PartialSort((IRenderableLight light, double distanceSq)[] array, int count, int k)
    {
        if (count <= 1 || k <= 0) return;

        k = Math.Min(k, count);

        // Use selection for small k, which is optimal for partial sorting
        for (int i = 0; i < k; i++)
        {
            int minIndex = i;
            double minDist = array[i].distanceSq;

            // Find minimum in remaining elements
            for (int j = i + 1; j < count; j++)
            {
                if (array[j].distanceSq < minDist)
                {
                    minDist = array[j].distanceSq;
                    minIndex = j;
                }
            }

            // Swap if needed
            if (minIndex != i)
            {
                (array[minIndex], array[i]) = (array[i], array[minIndex]);
            }
        }
    }

    private static void ProcessLight(IRenderableLight light, double distance, Double3 cameraPosition, IReadOnlyList<IRenderable> renderables,
        ref int numDirLights, ref int spotLightIndex, ref int pointLightIndex, int MAX_SPOT_LIGHTS, int MAX_POINT_LIGHTS)
    {
        // Calculate resolution based on distance (already calculated)
        int res = CalculateResolution(distance);
        if (light is DirectionalLight dir)
            res = (int)dir.ShadowResolution;

        if (light.DoCastShadows())
        {
            // Find a slot for the shadow map
            Int2? slot;
            bool isPointLight = light is PointLight;

            // Point lights need a 2x3 grid for cubemap faces
            if (isPointLight)
                slot = ShadowAtlas.ReserveCubemapTiles(res, light.GetLightID());
            else
                slot = ShadowAtlas.ReserveTiles(res, res, light.GetLightID());

            int AtlasX, AtlasY, AtlasWidth;

            if (slot != null)
            {
                AtlasX = slot.Value.X;
                AtlasY = slot.Value.Y;
                AtlasWidth = res;

                // Draw the shadow map
                s_shadowMap = ShadowAtlas.GetAtlas();

                // For point lights, render 6 faces
                if (isPointLight && light is PointLight pointLight)
                {
                    // Set point light uniforms for shadow rendering
                    PropertyState.SetGlobalVector("_PointLightPosition", pointLight.Transform.Position);
                    PropertyState.SetGlobalFloat("_PointLightRange", pointLight.Range);
                    PropertyState.SetGlobalFloat("_PointLightShadowBias", pointLight.ShadowBias);

                    for (int face = 0; face < 6; face++)
                    {
                        // Calculate viewport for this face in the 2x3 grid
                        int col = face % 2;
                        int row = face / 2;
                        int viewportX = AtlasX + (col * res);
                        int viewportY = AtlasY + (row * res);

                        Graphics.Device.Viewport(viewportX, viewportY, (uint)res, (uint)res);

                        pointLight.GetShadowMatrixForFace(face, out Double4x4 view, out Double4x4 proj, out Double3 forward, out Double3 up);
                        Double3 right = Double3.Cross(forward, up);
                        if (CAMERA_RELATIVE)
                            view.Translation *= new Double4(0, 0, 0, 1); // set all to 0 except W

                        Frustrum frustum = Frustrum.FromMatrix(proj * view);

                        HashSet<int> culledRenderableIndices = [];// CullRenderables(renderables, frustum);
                        AssignCameraMatrices(view, proj);
                        DrawRenderables(renderables, "LightMode", "ShadowCaster", new ViewerData(light.GetLightPosition(), forward, right, up), culledRenderableIndices, false);
                    }

                    // Reset uniforms for non-point lights
                    PropertyState.SetGlobalFloat("_PointLightRange", -1.0f);
                }
                else
                {
                    Double3 forward = ((MonoBehaviour)light).Transform.Forward;
                    if (light is DirectionalLight)
                        forward = -forward; // directional light is inverted atm
                    Double3 right = ((MonoBehaviour)light).Transform.Right;
                    Double3 up = ((MonoBehaviour)light).Transform.Up;

                    // Regular directional/spot light rendering
                    // Set range to -1 to indicate this is not a point light
                    PropertyState.SetGlobalFloat("_PointLightRange", -1.0f);

                    Graphics.Device.Viewport(slot.Value.X, slot.Value.Y, (uint)res, (uint)res);

                    // Use camera-following shadow matrix for directional lights
                    Double4x4 view, proj;
                    if (light is DirectionalLight dirLight)
                        dirLight.GetShadowMatrix(cameraPosition, res, out view, out proj);
                    else
                        light.GetShadowMatrix(out view, out proj);

                    if (CAMERA_RELATIVE)
                        view.Translation *= new Double4(0, 0, 0, 1); // set all to 0 except W

                    Frustrum frustum = Frustrum.FromMatrix(proj * view);

                    HashSet<int> culledRenderableIndices = [];// CullRenderables(renderables, frustum);
                    AssignCameraMatrices(view, proj);
                    DrawRenderables(renderables, "LightMode", "ShadowCaster", new ViewerData(light.GetLightPosition(), forward, right, up), culledRenderableIndices, false);
                }
            }
            else
            {
                AtlasX = -1;
                AtlasY = -1;
                AtlasWidth = 0;
            }


            if (light is DirectionalLight dirLight2)
            {
                dirLight2.UploadToGPU(CAMERA_RELATIVE, cameraPosition, AtlasX, AtlasY, AtlasWidth);
            }
            else if (light is SpotLight spotLight && spotLightIndex < MAX_SPOT_LIGHTS)
            {
                spotLight.UploadToGPU(CAMERA_RELATIVE, cameraPosition, AtlasX, AtlasY, AtlasWidth, spotLightIndex);
                spotLightIndex++;
            }
            else if (light is PointLight pointLight && pointLightIndex < MAX_POINT_LIGHTS)
            {
                pointLight.UploadToGPU(CAMERA_RELATIVE, cameraPosition, AtlasX, AtlasY, AtlasWidth, pointLightIndex);
                pointLightIndex++;
            }
        }
        else
        {
            if (light is DirectionalLight dirL)
            {
                dirL.UploadToGPU(CAMERA_RELATIVE, cameraPosition, -1, -1, 0);
            }
            else if (light is SpotLight spotLight && spotLightIndex < MAX_SPOT_LIGHTS)
            {
                spotLight.UploadToGPU(CAMERA_RELATIVE, cameraPosition, -1, -1, 0, spotLightIndex);
                spotLightIndex++;
            }
            else if (light is PointLight pointLight && pointLightIndex < MAX_POINT_LIGHTS)
            {
                pointLight.UploadToGPU(CAMERA_RELATIVE, cameraPosition, -1, -1, 0, pointLightIndex);
                pointLightIndex++;
            }
        }

        // Set the light counts in global uniforms
        GlobalUniforms.SetSpotLightCount(spotLightIndex);
        GlobalUniforms.SetPointLightCount(pointLightIndex);
        GlobalUniforms.Upload();
    }

    private static int CalculateResolution(double distance)
    {
        double t = Maths.Clamp(distance / 48f, 0, 1);
        int minSize = ShadowAtlas.GetMinShadowSize();
        int maxSize = ShadowAtlas.GetMaxShadowSize();
        int resolution = Maths.RoundToInt(Maths.Lerp(maxSize, minSize, t));

        // Clamp to valid range
        return Maths.Clamp(resolution, minSize, maxSize);
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

    private static bool CullRenderable(IRenderable renderable, Frustrum cameraFrustum)
    {
        renderable.GetCullingData(out bool isRenderable, out AABB bounds);

        return !isRenderable || !cameraFrustum.Intersects(bounds);
    }
}
