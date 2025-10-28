// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Resources;
using Prowl.Vector;

using Material = Prowl.Runtime.Resources.Material;
using Shader = Prowl.Runtime.Resources.Shader;

namespace Prowl.Runtime.Rendering;

/// <summary>
/// Screen Space Reflections (SSR) effect that renders realistic reflections using the visible geometry on screen.
/// Works best on smooth, metallic surfaces and automatically fades on rough surfaces.
/// </summary>
public sealed class ScreenSpaceReflectionEffect : ImageEffect
{
    // Quality Settings

    /// <summary>Maximum number of steps to march along a ray. Higher = better quality but slower.</summary>
    public float MaxSteps = 64f;

    /// <summary>Number of binary search iterations to refine ray hit position. 0 = no refinement.</summary>
    public float BinarySearchIterations = 8f;

    // Appearance Settings

    /// <summary>Overall intensity of reflections. 0 = no reflections, 1 = full strength.</summary>
    public float Intensity = 1.0f;

    /// <summary>How much reflections fade near screen edges. Higher = sharper fade.</summary>
    public float ScreenEdgeFade = 4.0f;

    /// <summary>Mip bias when sampling reflection color. Higher = blurrier reflections.</summary>
    public float MipBias = 0.0f;

    /// <summary>Blur radius applied to reflections. Higher = softer reflections.</summary>
    public float BlurRadius = 1.0f;

    /// <summary>Resolution scale for ray marching. 0.5 = half resolution (better performance).</summary>
    public float ResolutionScale = 1.0f;

    // Private fields
    private Material _mat;
    private RenderTexture _reflectionDataRT;
    private RenderTexture _resolvedRT;
    private RenderTexture _blurTempRT;

    public override bool IsOpaqueEffect => true; // Run after deferred composition but before transparents

    public override void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // Lazy initialize material
        _mat ??= new Material(Shader.LoadDefault(DefaultShader.SSR));

        // Calculate scaled resolution
        int width = (int)(source.Width * ResolutionScale);
        int height = (int)(source.Height * ResolutionScale);

        // Ensure render textures are created and sized correctly
        EnsureRenderTexture(ref _reflectionDataRT, width, height, TextureImageFormat.Float4);
        EnsureRenderTexture(ref _resolvedRT, width, height, source.MainTexture.ImageFormat);
        EnsureRenderTexture(ref _blurTempRT, width, height, source.MainTexture.ImageFormat);

        // Pass 0: Ray March - Trace rays and output hit UVs + confidence
        _mat.SetFloat("_MaxSteps", MaxSteps);
        _mat.SetFloat("_BinarySearchIterations", BinarySearchIterations);
        _mat.SetFloat("_ScreenEdgeFade", ScreenEdgeFade);
        Graphics.Blit(source, _reflectionDataRT, _mat, 0);

        // Pass 1: Resolve - Sample scene color at hit points
        _mat.SetTexture("_ReflectionData", _reflectionDataRT.MainTexture);
        _mat.SetFloat("_MipBias", MipBias);
        Graphics.Blit(source, _resolvedRT, _mat, 1);

        // Pass 2: Blur Horizontal
        _mat.SetVector("_BlurDirection", new Double2(1.0, 0.0));
        _mat.SetFloat("_BlurRadius", BlurRadius);
        Graphics.Blit(_resolvedRT, _blurTempRT, _mat, 2);

        // Pass 3: Blur Vertical
        _mat.SetVector("_BlurDirection", new Double2(0.0, 1.0));
        _mat.SetFloat("_BlurRadius", BlurRadius);
        Graphics.Blit(_blurTempRT, _resolvedRT, _mat, 2);

        // Pass 4: Composite - Blend reflections with scene
        _mat.SetTexture("_ReflectionTex", _resolvedRT.MainTexture);
        _mat.SetFloat("_Intensity", Intensity);
        Graphics.Blit(source, destination, _mat, 3);
    }

    private void EnsureRenderTexture(ref RenderTexture rt, int width, int height, TextureImageFormat format)
    {
        if (rt.IsNotValid() || rt.Width != width || rt.Height != height || rt.MainTexture.ImageFormat != format)
        {
            rt?.Dispose();
            rt = new RenderTexture(width, height, false, [format]);
        }
    }

    public override void OnPostRender(Camera camera)
    {
        // Cleanup could be done here if needed
    }
}
