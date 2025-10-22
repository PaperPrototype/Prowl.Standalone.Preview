// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Resources;
using Prowl.Vector;

using Material = Prowl.Runtime.Resources.Material;
using Shader = Prowl.Runtime.Resources.Shader;

namespace Prowl.Runtime.Rendering;

public sealed class BokehDepthOfFieldEffect : ImageEffect
{
    public bool UseAutoFocus = true;
    public float ManualFocusPoint = 0.5f;
    public float FocusStrength = 200.0f;

    //[Range(5.0f, 40.0f)]
    public float BlurRadius = 5.0f;

    //[Range(0.1f, 0.9f)]
    public float Quality = 0.9f;

    //[Range(0.25f, 1.0f)]
    public float DownsampleFactor = 0.5f;

    private Material _mat;
    private RenderTexture _downsampledRT;

    public override void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        _mat ??= new Material(Shader.LoadDefault(DefaultShader.BokehDoF));

        int width = (int)(source.Width * DownsampleFactor);
        int height = (int)(source.Height * DownsampleFactor);

        // Create or update downsampled render texture if needed
        if (_downsampledRT.IsNotValid() || _downsampledRT.Width != width || _downsampledRT.Height != height)
        {
            if (_downsampledRT.IsValid())
                _downsampledRT.Dispose();

            _downsampledRT = new RenderTexture(width, height, false, [source.MainTexture.ImageFormat]);
        }

        // Set shader properties
        _mat.SetFloat("_BlurRadius", BlurRadius);
        _mat.SetFloat("_FocusStrength", FocusStrength);
        _mat.SetFloat("_Quality", Quality);
        _mat.SetFloat("_ManualFocusPoint", ManualFocusPoint);
        _mat.SetKeyword("AUTOFOCUS", UseAutoFocus);
        _mat.SetVector("_Resolution", new Double2(source.Width, source.Height));

        // Two-pass approach:

        // Pass 1: Apply DoF at reduced resolution
        _mat.SetVector("_Resolution", new Double2(width, height));
        Graphics.Blit(source, _downsampledRT, _mat, 0); // DoFDownsample pass

        // Pass 2: Combine original image with blurred result
        _mat.SetTexture("_MainTex", source.MainTexture);
        _mat.SetTexture("_DownsampledDoF", _downsampledRT.MainTexture);
        _mat.SetVector("_Resolution", new Double2(source.Width, source.Height));
        Graphics.Blit(source, destination, _mat, 1); // DoFCombine pass
    }
}
