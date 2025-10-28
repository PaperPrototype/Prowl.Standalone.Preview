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
    #region Static Resources

    private static Mesh s_quadMesh;
    private static Mesh s_skyDome;
    private static Material s_defaultMaterial;
    private static Material s_skybox;
    private static Material s_gizmo;
    private static Material s_deferredCompose;

    private static RenderTexture? s_shadowMap;

    public static DefaultRenderPipeline Default { get; } = new();

    #endregion

    #region Resource Management

    private static void ValidateDefaults()
    {
        s_quadMesh ??= Mesh.GetFullscreenQuad();
        s_defaultMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Standard));
        s_skybox ??= new Material(Shader.LoadDefault(DefaultShader.ProceduralSkybox));
        s_gizmo ??= new Material(Shader.LoadDefault(DefaultShader.Gizmos));

        // Load deferred shaders
        s_deferredCompose ??= new Material(Shader.LoadDefault(DefaultShader.DeferredCompose));

        if (s_skyDome.IsNotValid())
        {
            Model skyDomeModel = Model.LoadDefault(DefaultModel.SkyDome) ?? throw new Exception("SkyDome model not found. Please ensure the model is included in the project.");
            s_skyDome = skyDomeModel.Meshes[0].Mesh;
        }
    }

    #endregion

    #region Main Rendering

    public override void Render(Camera camera, in RenderingData data)
    {
        ValidateDefaults();

        // Main rendering with correct order of operations
        Internal_Render(camera, data);

        PropertyState.ClearGlobals();

        base.Render(camera, in data);
    }

    private (List<ImageEffect> all, List<ImageEffect> opaque, List<ImageEffect> final) GatherImageEffects(Camera camera)
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

    #endregion

    #region Scene Rendering

    private void Internal_Render(Camera camera, in RenderingData data)
    {
        // =======================================================
        // 0. Setup variables, and prepare the camera
        bool isHDR = camera.HDR;
        (List<ImageEffect> all, List<ImageEffect> opaqueEffects, List<ImageEffect> finalEffects) = GatherImageEffects(camera);
        IReadOnlyList<IRenderableLight> lights = camera.GameObject.Scene.Lights;
        RenderTexture target = camera.UpdateRenderData();

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
        RenderShadowAtlas(css, lights, renderables);

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

            light.OnRenderLight(gBuffer, lightAccumulation, css);
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

    private void RenderShadowAtlas(CameraSnapshot css, IReadOnlyList<IRenderableLight> lights, IReadOnlyList<IRenderable> renderables)
    {
        Graphics.Device.BindFramebuffer(ShadowAtlas.GetAtlas().frameBuffer);
        Graphics.Device.Clear(0.0f, 0.0f, 0.0f, 1.0f, ClearFlags.Depth | ClearFlags.Stencil);

        // Process all lights - each light handles its own shadow rendering
        foreach (IRenderableLight light in lights)
        {
            if (css.CullingMask.HasLayer(light.GetLayer()) == false)
                continue;

            if (light is Light lightComponent)
            {
                lightComponent.RenderShadows(this, css.CameraPosition, renderables);
            }
        }
    }

    private void RenderSkybox(CameraSnapshot css)
    {
        // Set sun direction for skybox from scene's directional light
        var sun = css.Scene.Lights.FirstOrDefault(l => l is IRenderableLight rl && rl.GetLightType() == LightType.Directional);
        if (sun != null)
        {
            s_skybox.SetVector("_SunDir", sun.GetLightDirection());
        }

        Graphics.DrawMeshNow(s_skyDome, s_skybox);
    }

    private void RenderGizmos(CameraSnapshot css)
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

    private void DrawImageEffects(RenderTexture forwardBuffer, List<ImageEffect> effects, ref bool isHDR)
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
}
