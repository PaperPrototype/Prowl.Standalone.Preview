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
    private RenderTexture[] _pingPongBuffers = new RenderTexture[2];

    public override void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // Create material if it doesn't exist
        _bloomMaterial ??= new Material(Shader.LoadDefault(DefaultShader.Bloom));

        int width = source.Width / 4;
        int height = source.Height / 4;

        // Create ping-pong buffers if they don't exist
        for (int i = 0; i < 2; i++)
        {
            if (_pingPongBuffers[i].IsNotValid() || _pingPongBuffers[i].Width != width || _pingPongBuffers[i].Height != height)
            {
                _pingPongBuffers[i]?.Dispose();
                _pingPongBuffers[i] = new RenderTexture(width, height, false, [destination.MainTexture.ImageFormat]);
            }
        }

        // 1. Extract bright areas (threshold pass)
        _bloomMaterial.SetFloat("_Threshold", Threshold);
        Graphics.Blit(source, _pingPongBuffers[0], _bloomMaterial, 0);

        // 2. Apply Kawase blur ping-pong (multiple iterations with increasing radius)
        int current = 0;
        int next = 1;

        for (int i = 0; i < Iterations; i++)
        {
            float offset = (i * 0.5f + 0.5f) * Spread;
            _bloomMaterial.SetFloat("_Offset", offset);
            Graphics.Blit(_pingPongBuffers[current], _pingPongBuffers[next], _bloomMaterial, 1);

            // Swap buffers
            (next, current) = (current, next);
        }

        // 3. Composite the bloom with the original image
        _bloomMaterial.SetTexture("_BloomTex", _pingPongBuffers[current].MainTexture);
        _bloomMaterial.SetFloat("_Intensity", Intensity);
        Graphics.Blit(source, destination, _bloomMaterial, 2);
    }

    public override void OnPostRender(Camera camera)
    {
        // Clean up resources if needed
    }
}
