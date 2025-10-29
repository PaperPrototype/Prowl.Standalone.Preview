// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Resources;

using Material = Prowl.Runtime.Resources.Material;
using Shader = Prowl.Runtime.Resources.Shader;

namespace Prowl.Runtime.Rendering;

public sealed class KawaseBloomEffect : ImageEffect
{
    public float Intensity = 1.5f;
    public float Threshold = 0.8f;
    public int Iterations = 6;
    public float Spread = 1f;

    private Material _bloomMaterial;

    public override void OnRenderEffect(RenderContext context)
    {
        // Create material if it doesn't exist
        _bloomMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Bloom));

        int width = context.Width / 4;
        int height = context.Height / 4;

        // Create ping-pong buffers
        RenderTexture pingPongBuffer0 = RenderTexture.GetTemporaryRT(width, height, false, [context.SceneColor.MainTexture.ImageFormat]);
        RenderTexture pingPongBuffer1 = RenderTexture.GetTemporaryRT(width, height, false, [context.SceneColor.MainTexture.ImageFormat]);

        // 1. Extract bright areas (threshold pass)
        _bloomMaterial.SetFloat("_Threshold", Threshold);
        Graphics.Blit(context.SceneColor, pingPongBuffer0, _bloomMaterial, 0);

        // 2. Apply Kawase blur ping-pong (multiple iterations with increasing radius)
        RenderTexture current = pingPongBuffer0;
        RenderTexture next = pingPongBuffer1;

        for (int i = 0; i < Iterations; i++)
        {
            float offset = (i * 0.5f + 0.5f) * Spread;
            _bloomMaterial.SetFloat("_Offset", offset);
            Graphics.Blit(current, next, _bloomMaterial, 1);

            // Swap buffers
            (next, current) = (current, next);
        }

        // 3. Composite the bloom with the original image (in-place)
        _bloomMaterial.SetTexture("_BloomTex", current.MainTexture);
        _bloomMaterial.SetFloat("_Intensity", Intensity);
        Graphics.Blit(context.SceneColor, context.SceneColor, _bloomMaterial, 2);

        // Release temporary render textures
        RenderTexture.ReleaseTemporaryRT(pingPongBuffer0);
        RenderTexture.ReleaseTemporaryRT(pingPongBuffer1);
    }

    public override void OnPostRender(Camera camera)
    {
        // Clean up resources if needed
    }
}
