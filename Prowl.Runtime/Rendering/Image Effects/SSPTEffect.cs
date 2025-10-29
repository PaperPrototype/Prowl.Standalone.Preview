// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.GraphicsBackend.Primitives;
using Prowl.Runtime.Resources;
using Prowl.Vector;

using Material = Prowl.Runtime.Resources.Material;
using Shader = Prowl.Runtime.Resources.Shader;

namespace Prowl.Runtime.Rendering;

/// <summary>
/// Screen-Space Path Tracing (SSPT) effect for real-time global illumination.
/// Traces rays in screen space to approximate indirect lighting bounces.
/// </summary>
public sealed class SSPTEffect : ImageEffect
{
    // Quality Settings

    /// <summary>Number of ray samples per pixel. Higher = better quality but much slower.</summary>
    public int SamplesPerPixel = 4; // [1, 2, 3, 4, 6, 8]

    /// <summary>Maximum number of steps per ray. Higher = better quality but slower.</summary>
    public int RaySteps = 32; // [8, 12, 16, 20, 24, 32]

    // Appearance Settings

    /// <summary>Maximum ray length in view space units. Larger = GI from more distant objects.</summary>
    public float RayLength = 10.0f;

    /// <summary>Surface thickness for ray intersection. Larger = more forgiving but less accurate.</summary>
    public float Thickness = 0.2f;

    /// <summary>Overall intensity of the global illumination effect.</summary>
    public float Intensity = 1.0f;

    /// <summary>Enable temporal accumulation to reduce noise (requires stable camera).</summary>
    public bool UseTemporalAccumulation = true;

    /// <summary>Temporal blend factor. Higher = more stable but more ghosting. Range: 0-1.</summary>
    public float TemporalBlend = 0.8f;

    /// <summary>Blur radius for denoising. Higher = smoother but less detailed.</summary>
    public float BlurRadius = 1.0f;

    /// <summary>Resolution scale for GI calculation. 0.5 = half resolution (better performance).</summary>
    public float ResolutionScale = 1.0f;

    // Private fields
    private Material _mat;
    private RenderTexture _giRT;
    private RenderTexture _historyRT;
    private RenderTexture _accumulatedRT;
    private RenderTexture _blurTempRT;
    private uint _frameIndex = 0;

    public override bool IsOpaqueEffect => true; // Run after deferred composition

    public override void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // Lazy initialize material
        _mat ??= new Material(Shader.LoadDefault(DefaultShader.SSPT));

        // Calculate scaled resolution
        int width = (int)(source.Width * ResolutionScale);
        int height = (int)(source.Height * ResolutionScale);

        // Ensure render textures are created and sized correctly
        EnsureRenderTexture(ref _giRT, width, height, TextureImageFormat.Float4);
        EnsureRenderTexture(ref _historyRT, width, height, TextureImageFormat.Float4);
        EnsureRenderTexture(ref _accumulatedRT, width, height, TextureImageFormat.Float4);
        EnsureRenderTexture(ref _blurTempRT, width, height, TextureImageFormat.Float4);

        // Pass 0: Screen-Space Path Tracing
        _mat.SetInt("_SamplesPerPixel", SamplesPerPixel);
        _mat.SetInt("_RaySteps", RaySteps);
        _mat.SetFloat("_RayLength", RayLength);
        _mat.SetFloat("_Thickness", Thickness);
        _mat.SetFloat("_Intensity", Intensity);
        _mat.SetInt("_FrameIndex", (int)_frameIndex);
        Graphics.Blit(source, _giRT, _mat, 0);

        // Pass 1: Temporal Accumulation (if enabled)
        RenderTexture giSource = _giRT;
        if (UseTemporalAccumulation && _frameIndex > 0)
        {
            _mat.SetTexture("_CurrentGI", _giRT.MainTexture);
            _mat.SetTexture("_HistoryGI", _historyRT.MainTexture);
            _mat.SetFloat("_TemporalBlend", TemporalBlend);
            Graphics.Blit(_giRT, _accumulatedRT, _mat, 1);
            giSource = _accumulatedRT;

            // Copy to history for next frame
            Graphics.Blit(_accumulatedRT, _historyRT);
        }
        else if (UseTemporalAccumulation && _frameIndex == 0)
        {
            // First frame - just copy to history
            Graphics.Blit(_giRT, _historyRT);
        }

        // Pass 2: Blur Horizontal (if blur is enabled)
        RenderTexture blurredGI = giSource;
        if (BlurRadius > 0.01f)
        {
            _mat.SetVector("_BlurDirection", new Double2(1.0, 0.0));
            _mat.SetFloat("_BlurRadius", BlurRadius);
            Graphics.Blit(giSource, _blurTempRT, _mat, 2);

            // Pass 2: Blur Vertical
            _mat.SetVector("_BlurDirection", new Double2(0.0, 1.0));
            _mat.SetFloat("_BlurRadius", BlurRadius);
            Graphics.Blit(_blurTempRT, _accumulatedRT, _mat, 2);
            blurredGI = _accumulatedRT;
        }

        // Pass 3: Composite - Add GI to scene
        _mat.SetTexture("_GITex", blurredGI.MainTexture);
        _mat.SetFloat("_Intensity", Intensity);
        Graphics.Blit(source, destination, _mat, 3);

        _frameIndex++;
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
        // Reset frame index on camera movement or settings change
        // This would ideally detect camera motion to reset temporal accumulation
    }
}
