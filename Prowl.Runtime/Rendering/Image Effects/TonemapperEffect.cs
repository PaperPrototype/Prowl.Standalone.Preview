// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Resources;

using Material = Prowl.Runtime.Resources.Material;
using Shader = Prowl.Runtime.Resources.Shader;

namespace Prowl.Runtime.Rendering;

public sealed class TonemapperEffect : ImageEffect
{
    public override bool TransformsToLDR => true;

    public float Contrast = 1.1f;
    public float Saturation = 1.1f;

    Material _mat;

    public override void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        _mat ??= new Material(Shader.LoadDefault(DefaultShader.Tonemapper));
        _mat.SetFloat("Contrast", Contrast);
        _mat.SetFloat("Saturation", Saturation);
        Graphics.Blit(source, destination, _mat, 0);
    }
}
