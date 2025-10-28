// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;

using Material = Prowl.Runtime.Resources.Material;
using Shader = Prowl.Runtime.Resources.Shader;

namespace Prowl.Runtime;

public class DirectionalLight : Light
{
    public enum Resolution : int
    {
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096,
    }

    public Resolution ShadowResolution = Resolution._1024;

    public double ShadowDistance = 50f;

    private Material? _lightMaterial;
    private Double4x4 _shadowMatrix;
    private Double4 _shadowAtlasParams;

    public override void Update()
    {
        GameObject.Scene.PushLight(this);
    }

    public override void DrawGizmos()
    {
        Debug.DrawArrow(Transform.Position, -Transform.Forward, Color.Yellow);
        Debug.DrawWireCircle(Transform.Position, Transform.Forward, 0.5f, Color.Yellow);
    }


    public override LightType GetLightType() => LightType.Directional;

    private void GetShadowMatrix(Double3 cameraPosition, int shadowResolution, out Double4x4 view, out Double4x4 projection)
    {
        Double3 forward = -Transform.Forward;
        projection = Double4x4.CreateOrtho(ShadowDistance, ShadowDistance, 0.1f, ShadowDistance);

        // Calculate texel size in world units
        double texelSize = (ShadowDistance * 2.0) / shadowResolution;

        // Build orthonormal basis for light space
        Double3 lightUp = Double3.Normalize(Transform.Up);
        Double3 lightRight = Double3.Normalize(Double3.Cross(lightUp, forward));
        lightUp = Double3.Normalize(Double3.Cross(forward, lightRight)); // Recompute to ensure orthogonality

        // Project camera position onto the light's perpendicular plane (X and Y in light space)
        double x = Double3.Dot(cameraPosition, lightRight);
        double y = Double3.Dot(cameraPosition, lightUp);

        // Snap to texel grid in light space
        x = Maths.Round(x / texelSize) * texelSize;
        y = Maths.Round(y / texelSize) * texelSize;

        // Reconstruct the snapped position (only X and Y are snapped, keep camera's position along light direction)
        Double3 snappedPosition = (lightRight * x) + (lightUp * y);

        // Position the shadow map at the snapped position, offset back by half the shadow distance
        view = Double4x4.CreateLookTo(snappedPosition - (forward * ShadowDistance * 0.5), forward, Transform.Up);
    }

    public override void RenderShadows(RenderPipeline pipeline, Double3 cameraPosition, System.Collections.Generic.IReadOnlyList<IRenderable> renderables)
    {
        int atlasX, atlasY, atlasWidth;

        if (!DoCastShadows())
        {
            // No shadows - set invalid atlas coordinates
            atlasX = -1;
            atlasY = -1;
            atlasWidth = 0;
        }
        else
        {
            // Get shadow resolution
            int res = (int)ShadowResolution;

            // Reserve space in shadow atlas
            Int2? slot = ShadowAtlas.ReserveTiles(res, res, GetLightID());

            if (slot != null)
            {
                atlasX = slot.Value.X;
                atlasY = slot.Value.Y;
                atlasWidth = res;

                // Draw the shadow map
                Double3 forward = -Transform.Forward; // directional light is inverted
                Double3 right = Transform.Right;
                Double3 up = Transform.Up;

                Graphics.Device.Viewport(slot.Value.X, slot.Value.Y, (uint)res, (uint)res);

                // Use camera-following shadow matrix for directional lights
                GetShadowMatrix(cameraPosition, res, out Double4x4 view, out Double4x4 proj);

                if (RenderPipeline.CAMERA_RELATIVE)
                    view.Translation *= new Double4(0, 0, 0, 1); // set all to 0 except W

                Frustum frustum = Frustum.FromMatrix(proj * view);

                System.Collections.Generic.HashSet<int> culledRenderableIndices = pipeline.CullRenderables(renderables, frustum, LayerMask.Everything);
                pipeline.AssignCameraMatrices(view, proj);
                pipeline.DrawRenderables(renderables, "LightMode", "ShadowCaster", new ViewerData(GetLightPosition(), forward, right, up), culledRenderableIndices, false);

                // Store shadow data for later use in OnRenderLight
                _shadowMatrix = proj * view;
                _shadowAtlasParams = new Double4(atlasX, atlasY, atlasWidth, 0);
            }
            else
            {
                // Failed to reserve atlas space
                atlasX = -1;
                atlasY = -1;
                atlasWidth = 0;
            }
        }
    }

    public override void OnRenderLight(RenderTexture gBuffer, RenderTexture destination, RenderPipeline.CameraSnapshot css)
    {
        // Create material if needed
        _lightMaterial ??= new Material(Shader.LoadDefault(DefaultShader.DirectionalLight));

        // Set GBuffer textures
        _lightMaterial.SetTexture("_GBufferA", gBuffer.InternalTextures[0]);
        _lightMaterial.SetTexture("_GBufferB", gBuffer.InternalTextures[1]);
        _lightMaterial.SetTexture("_GBufferC", gBuffer.InternalTextures[2]);
        _lightMaterial.SetTexture("_GBufferD", gBuffer.InternalTextures[3]);
        _lightMaterial.SetTexture("_CameraDepthTexture", gBuffer.InternalDepth);

        // Set shadow atlas texture and size
        var shadowAtlas = ShadowAtlas.GetAtlas();
        _lightMaterial.SetTexture("_ShadowAtlas", shadowAtlas.InternalDepth);
        _lightMaterial.SetVector("_ShadowAtlasSize", new Double2(shadowAtlas.Width, shadowAtlas.Height));

        // Set directional light properties
        _lightMaterial.SetVector("_LightDirection", Transform.Forward);
        _lightMaterial.SetColor("_LightColor", Color);
        _lightMaterial.SetFloat("_LightIntensity", (float)Intensity);

        // Set shadow properties
        _lightMaterial.SetMatrix("_ShadowMatrix", _shadowMatrix);
        _lightMaterial.SetFloat("_ShadowBias", (float)ShadowBias);
        _lightMaterial.SetFloat("_ShadowNormalBias", (float)ShadowNormalBias);
        _lightMaterial.SetFloat("_ShadowStrength", (float)ShadowStrength);
        _lightMaterial.SetFloat("_ShadowQuality", (float)ShadowQuality);
        _lightMaterial.SetVector("_ShadowAtlasParams", _shadowAtlasParams);

        // Draw fullscreen quad with the directional light shader
        Graphics.Blit(gBuffer, destination, _lightMaterial, 0, false, false);
    }
}
