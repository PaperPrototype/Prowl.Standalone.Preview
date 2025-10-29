// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.GraphicsBackend.Primitives;
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
    public float MaxBlurRadius = 4.0f;

    private Material _mat;
    private RenderTexture _horizontalMRT;
    private RenderTexture _verticalResult;

    public override void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        _mat ??= new Material(Shader.LoadDefault(DefaultShader.BokehDoF));

        int width = source.Width;
        int height = source.Height;

        // Create or update MRT render texture for horizontal pass (3 color attachments for R, G, B)
        if (_horizontalMRT.IsNotValid() || _horizontalMRT.Width != width || _horizontalMRT.Height != height)
        {
            if (_horizontalMRT.IsValid())
                _horizontalMRT.Dispose();

            // Use floating point format to store complex number values (can be negative)
            _horizontalMRT = new RenderTexture(width, height, false, [
                TextureImageFormat.Float4,
                TextureImageFormat.Float4,
                TextureImageFormat.Float4
            ]);
        }

        // Create or update vertical result texture
        if (_verticalResult.IsNotValid() || _verticalResult.Width != width || _verticalResult.Height != height)
        {
            if (_verticalResult.IsValid())
                _verticalResult.Dispose();

            _verticalResult = new RenderTexture(width, height, false, [source.MainTexture.ImageFormat]);
        }

        // Set common shader properties
        _mat.SetFloat("_FocusStrength", FocusStrength);
        _mat.SetFloat("_ManualFocusPoint", ManualFocusPoint);
        _mat.SetFloat("_MaxBlurRadius", MaxBlurRadius);
        _mat.SetKeyword("AUTOFOCUS", UseAutoFocus);
        _mat.SetVector("_Resolution", new Double2(width, height));

        // Pass 0: Horizontal MRT - outputs to 3 render targets (R, G, B channels)
        _mat.SetTexture("_MainTex", source.MainTexture);
        Graphics.Blit(_horizontalMRT, _mat, 0);

        // Pass 1: Vertical Composite - reads from 3 horizontal textures and combines
        _mat.SetTexture("_HorizR", _horizontalMRT.InternalTextures[0]);
        _mat.SetTexture("_HorizG", _horizontalMRT.InternalTextures[1]);
        _mat.SetTexture("_HorizB", _horizontalMRT.InternalTextures[2]);
        Graphics.Blit(_verticalResult, _mat, 1);

        // Pass 2: Final Combine - blend with original image based on CoC
        _mat.SetTexture("_MainTex", source.MainTexture);
        _mat.SetTexture("_BlurredTex", _verticalResult.MainTexture);
        Graphics.Blit(source, destination, _mat, 2);
    }
}
