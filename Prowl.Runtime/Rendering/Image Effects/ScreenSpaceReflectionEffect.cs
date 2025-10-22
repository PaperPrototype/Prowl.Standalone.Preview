// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Resources;

using Material = Prowl.Runtime.Resources.Material;
using Shader = Prowl.Runtime.Resources.Shader;

namespace Prowl.Runtime.Rendering;

public sealed class ScreenSpaceReflectionEffect : ImageEffect
{
    public int RayStepCount = 16;
    public float ScreenEdgeFade = 0.1f;

    Material _mat;

    public override void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        _mat ??= new Material(Shader.LoadDefault(DefaultShader.SSR));

        // Set uniforms
        _mat.SetInt("_RayStepCount", RayStepCount);
        _mat.SetFloat("_ScreenEdgeFade", ScreenEdgeFade);

        // Set textures
        _mat.SetTexture("_MainTex", source.MainTexture);

        // Apply effect
        Graphics.Blit(source, destination, _mat, 0);
    }
}
