// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Resources;
using Prowl.Vector;

using Material = Prowl.Runtime.Resources.Material;
using Shader = Prowl.Runtime.Resources.Shader;

namespace Prowl.Runtime.Rendering;

public sealed class FXAAEffect : ImageEffect
{
    public float EdgeThresholdMax = 0.0625f;  // 0.063 - 0.333 (lower = more AA, slower)
    public float EdgeThresholdMin = 0.0312f;  // 0.0312 - 0.0833 (trims dark edges)
    public float SubpixelQuality = 0.75f;     // 0.0 - 1.0 (subpixel AA amount)

    private Material _mat;

    public override void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        _mat ??= new Material(Shader.LoadDefault(DefaultShader.FXAA));

        // Set shader parameters
        _mat.SetFloat("_EdgeThresholdMax", EdgeThresholdMax);
        _mat.SetFloat("_EdgeThresholdMin", EdgeThresholdMin);
        _mat.SetFloat("_SubpixelQuality", SubpixelQuality);
        _mat.SetVector("_Resolution", new Double2(source.Width, source.Height));

        // Apply FXAA
        Graphics.Blit(source, destination, _mat, 0);
    }
}
