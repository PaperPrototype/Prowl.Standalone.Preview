Shader "Default/DeferredCompose"

Properties
{
}

Pass "Compose"
{
    Tags { "RenderOrder" = "Opaque" }

    // Fullscreen pass settings
    Cull None
    ZTest Off
    ZWrite Off
    Blend Off

	GLSLPROGRAM

	Vertex
	{
		layout (location = 0) in vec3 vertexPosition;
		layout (location = 1) in vec2 vertexTexCoord;

		out vec2 TexCoords;

		void main()
		{
			TexCoords = vertexTexCoord;
		    gl_Position = vec4(vertexPosition, 1.0);
		}
	}

	Fragment
	{
		#include "Fragment"

		layout (location = 0) out vec4 finalColor;

		in vec2 TexCoords;

		// GBuffer textures
		uniform sampler2D _GBufferA; // RGB = Albedo, A = AO
		uniform sampler2D _GBufferB; // RGB = Normal (view space), A = ShadingMode
		uniform sampler2D _GBufferD; // Custom Data per Shading Mode (e.g., Emissive for Lit mode)

		// Light accumulation buffer
		uniform sampler2D _LightAccumulation;

		void main()
		{
			// Sample textures
			vec4 gbufferB = texture(_GBufferB, TexCoords);
			vec4 gbufferD = texture(_GBufferD, TexCoords);
			vec3 lightAccumulation = texture(_LightAccumulation, TexCoords).rgb;

			float shadingMode = gbufferB.a;
			vec3 emission = gbufferD.rgb;

			// Check shading mode
			// 0 = Unlit, 1 = Lit
			if (shadingMode != 1.0) {
				// Unlit mode - use albedo + emission from GBuffer
				vec4 gbufferA = texture(_GBufferA, TexCoords);
				vec3 albedo = gbufferA.rgb;
				finalColor = vec4(albedo + emission, 1.0);
			} else {
				// Lit mode - combine light accumulation with emissive
				finalColor = vec4(lightAccumulation + emission, 1.0);
			}
		}
	}

	ENDGLSL
}
