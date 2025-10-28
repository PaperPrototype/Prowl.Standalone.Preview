Shader "Default/SSR"

Pass "SSR"
{
    Tags { "RenderOrder" = "Opaque" }

    // Rasterizer culling mode
    Blend Alpha
    ZTest Off
    ZWrite Off
    Cull Off

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
        layout(location = 0) out vec4 OutputColor;
        
        in vec2 TexCoords;

        uniform sampler2D _MainTex;
        uniform sampler2D _CameraDepthTexture;
        uniform sampler2D _GBufferB; // RGB = Normal (view space), A = ShadingMode
        uniform sampler2D _GBufferC; // R = Roughness, G = Metalness, B = Specular, A = Unused
        
        uniform int _RayStepCount;
        uniform float _ScreenEdgeFade;
        
        #include "Fragment"
        #include "Utilities"
		#include "PBR"
        
		vec3 calculateSSR(vec3 viewPos, vec3 screenPos, vec3 gBMVNorm, float dither) {
			vec3 viewDir = normalize(viewPos);
			vec3 reflectDir = reflect(viewDir, gBMVNorm);

			vec3 reflectedScreenPos = rayTrace(screenPos, viewPos, reflectDir, dither, _RayStepCount, 4, _CameraDepthTexture);
			if(reflectedScreenPos.z < 0.5) return vec3(0);

			// Get the world position of the hit point
			vec3 hitViewPos = getViewPos(reflectedScreenPos.xy, _CameraDepthTexture);

			// Calculate the expected point on the ray
			float rayLength = length(hitViewPos - viewPos);
			vec3 expectedViewPos = viewPos + reflectDir * rayLength;

			// Check if the hit point is close to the ray
			// If not, it's likely hitting geometry that shouldn't be reflected
			float threshold = 0.25;
			float dist = length(hitViewPos - expectedViewPos);

			if(dist > threshold) {
				return vec3(0);
			}

			return vec3(reflectedScreenPos.xy, 1.0);
		}

        void main()
        {
            // Start with the original color
			vec4 base = texture(_MainTex, TexCoords);
			vec3 color = base.rgb;
			OutputColor = base;

            // Get surface data from GBufferC
            vec4 gbufferC = texture(_GBufferC, TexCoords); // R: Roughness, G: Metalness, B: Specular
            float roughness = gbufferC.r;
            float metallic = gbufferC.g;
            float smoothness = 1.0 - roughness;

			smoothness = smoothness * smoothness;

			if(smoothness > 0.01)
			{
                // Get view-space normal from GBufferB and unpack from [0,1] to [-1,1]
                vec3 viewNormal = texture(_GBufferB, TexCoords).rgb * 2.0 - 1.0;
                vec3 normal = normalize(viewNormal);

				vec3 screenPos = getScreenPos(TexCoords, _CameraDepthTexture);
				vec3 viewPos = getViewFromScreenPos(screenPos);

				bool isMetal = metallic > 0.9;

				// Get fresnel
				vec3 F0 = vec3(0.04);
				F0 = mix(F0, color, metallic);
				vec3 fresnel = FresnelSchlick(max(dot(normal, normalize(-viewPos)), 0.0), F0);

				float dither = fract(sin(dot(TexCoords + vec2(_Time.y, _Time.y), vec2(12.9898,78.233))) * 43758.5453123);

				vec3 SSRCoord = calculateSSR(viewPos, screenPos, normalize(normal), dither);

				if(SSRCoord.z > 0.5)
				{
					vec3 reflectionColor = texture(_MainTex, SSRCoord.xy).xyz;
					OutputColor.rgb *= isMetal ? vec3(1.0 - smoothness) : 1.0 - fresnel;
					OutputColor.rgb += reflectionColor * fresnel;
				}
			}
        }
    }

    ENDGLSL
}
