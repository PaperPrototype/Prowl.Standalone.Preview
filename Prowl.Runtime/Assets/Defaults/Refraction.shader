Shader "Default/Refraction"

Properties
{
    _RefractionStrength ("Refraction Strength", Float) = 0.1
    _NoiseScale ("Noise Scale", Float) = 1.0
    _Tint ("Tint", Color) = (1.0, 1.0, 1.0, 1.0)
}

Pass "Refraction"
{
    GrabTexture "_GrabTexture"
    Tags { "RenderOrder" = "Transparent" }
    Blend Alpha
    ZWrite Off
    Cull Back

	GLSLPROGRAM
		Shared
		{
			// Simple 3D noise function
			float hash(vec3 p)
			{
				p = fract(p * 0.3183099 + 0.1);
				p *= 17.0;
				return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
			}

			float noise(vec3 x)
			{
				vec3 i = floor(x);
				vec3 f = fract(x);
				f = f * f * (3.0 - 2.0 * f);

				return mix(mix(mix(hash(i + vec3(0.0, 0.0, 0.0)),
								   hash(i + vec3(1.0, 0.0, 0.0)), f.x),
							   mix(hash(i + vec3(0.0, 1.0, 0.0)),
								   hash(i + vec3(1.0, 1.0, 0.0)), f.x), f.y),
						   mix(mix(hash(i + vec3(0.0, 0.0, 1.0)),
								   hash(i + vec3(1.0, 0.0, 1.0)), f.x),
							   mix(hash(i + vec3(0.0, 1.0, 1.0)),
								   hash(i + vec3(1.0, 1.0, 1.0)), f.x), f.y), f.z);
			}
		}

		Vertex
		{
            #include "Fragment"
            #include "VertexAttributes"

			out vec2 texCoord0;
			out vec3 worldPos;
			out vec4 screenPos;
			out vec3 vNormal;

			void main()
			{
				gl_Position = PROWL_MATRIX_MVP * vec4(vertexPosition, 1.0);
				texCoord0 = vertexTexCoord0;
				worldPos = (PROWL_MATRIX_M * vec4(vertexPosition, 1.0)).xyz;
				screenPos = gl_Position;
				vNormal = normalize(mat3(PROWL_MATRIX_M) * vertexNormal);
			}
		}

		Fragment
		{
            #include "Fragment"

			layout (location = 0) out vec4 fragColor;

			in vec2 texCoord0;
			in vec3 worldPos;
			in vec4 screenPos;
			in vec3 vNormal;

			uniform sampler2D _GrabTexture;
			uniform float _RefractionStrength;
			uniform float _NoiseScale;
			uniform vec4 _Tint;

			void main()
			{
				// Calculate screen space UV coordinates
				vec2 screenUV = (screenPos.xy / screenPos.w) * 0.5 + 0.5;

				// Sample noise texture
				vec3 noiseInput = worldPos * _NoiseScale + vec3(_Time * 0.1);
				float noiseValue = noise(noiseInput);

				// Create refraction offset using noise
				vec2 refractionOffset = vec2(
					noise(noiseInput + vec3(0.0, 0.0, 0.0)),
					noise(noiseInput + vec3(5.2, 1.3, 0.0))
				);
				refractionOffset = (refractionOffset * 2.0 - 1.0) * _RefractionStrength;

				// Apply refraction offset to screen UVs
				vec2 refractedUV = screenUV + refractionOffset;

				// Clamp UVs to avoid sampling outside the grabbed texture
				refractedUV = clamp(refractedUV, 0.0, 1.0);

				// Sample the grabbed texture with refracted UVs
				vec4 refractedColor = texture(_GrabTexture, refractedUV);

				// Debug: If grab texture is black/empty, show a debug color
				if (length(refractedColor.rgb) < 0.01) {
					fragColor = vec4(1.0, 0.0, 1.0, 0.5); // Magenta = grab failed
				} else {
					// Apply tint and alpha
					fragColor = vec4(refractedColor.rgb * _Tint.rgb, _Tint.a);
				}
			}
		}
	ENDGLSL
}
