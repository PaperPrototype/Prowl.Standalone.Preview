Shader "Default/SpotLight"

Properties
{
}

Pass "SpotLight"
{
    Tags { "RenderOrder" = "Opaque" }

    // Fullscreen pass settings
    Cull None
    ZTest Off
    ZWrite Off
    Blend Additive  // Additive blending for light accumulation

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
		#include "PBR"

		layout (location = 0) out vec4 finalColor;

		in vec2 TexCoords;

		// GBuffer textures
		uniform sampler2D _GBufferA; // RGB = Albedo, A = AO
		uniform sampler2D _GBufferB; // RGB = Normal (view space), A = ShadingMode
		uniform sampler2D _GBufferC; // R = Roughness, G = Metalness, B = Specular, A = Unused
		uniform sampler2D _GBufferD; // Custom Data per Shading Mode (e.g., Emissive for Lit mode)
		uniform sampler2D _CameraDepthTexture; // Depth texture
		uniform sampler2D _ShadowAtlas;

		// Spot light uniforms
		uniform vec3 _LightPosition;
		uniform vec3 _LightDirection;
		uniform vec4 _LightColor;
		uniform float _LightIntensity;
		uniform float _LightRange;
		uniform float _SpotAngle;
		uniform float _InnerSpotAngle;
		uniform float _ShadowBias;
		uniform float _ShadowNormalBias;
		uniform float _ShadowStrength;
		uniform float _ShadowQuality;

		// Shadow mapping uniforms
		uniform mat4 _ShadowMatrix;
		uniform vec4 _ShadowAtlasParams; // xy: atlasPos, z: atlasSize, w: unused

		// Poisson disk sampling pattern for smooth PCF
		const vec2 poissonDisk[16] = vec2[](
			vec2(-0.94201624, -0.39906216),
			vec2(0.94558609, -0.76890725),
			vec2(-0.094184101, -0.92938870),
			vec2(0.34495938, 0.29387760),
			vec2(-0.91588581, 0.45771432),
			vec2(-0.81544232, -0.87912464),
			vec2(-0.38277543, 0.27676845),
			vec2(0.97484398, 0.75648379),
			vec2(0.44323325, -0.97511554),
			vec2(0.53742981, -0.47373420),
			vec2(-0.26496911, -0.41893023),
			vec2(0.79197514, 0.19090188),
			vec2(-0.24188840, 0.99706507),
			vec2(-0.81409955, 0.91437590),
			vec2(0.19984126, 0.78641367),
			vec2(0.14383161, -0.14100790)
		);

		// Simple hash function for random rotation
		float InterleavedGradientNoise(vec2 position) {
			vec3 magic = vec3(0.06711056, 0.00583715, 52.9829189);
			return fract(magic.z * fract(dot(position, magic.xy)));
		}

		// Reconstruct world position from depth
		vec3 WorldPosFromDepth(float depth, vec2 texCoord) {
			float z = depth * 2.0 - 1.0;
			vec4 clipSpacePosition = vec4(texCoord * 2.0 - 1.0, z, 1.0);
			mat4 invVP = inverse(PROWL_MATRIX_VP);
			vec4 worldSpacePosition = invVP * clipSpacePosition;
			worldSpacePosition /= worldSpacePosition.w;
			return worldSpacePosition.xyz;
		}

		// Sample spot light shadow with perspective shadow mapping
		float SampleShadow(vec3 worldPos, vec3 worldNormal)
		{
			// Check if shadow map is valid
			if (_ShadowAtlasParams.z <= 0.0) {
				return 0.0; // No shadow map
			}

			// Apply normal bias
			vec3 worldPosBiased = worldPos + (normalize(worldNormal) * _ShadowNormalBias);
			vec4 lightSpacePos = _ShadowMatrix * vec4(worldPosBiased, 1.0);
			vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;
			projCoords = projCoords * 0.5 + 0.5;

			// Early exit if outside shadow map
			if (projCoords.z > 1.0 || projCoords.x < 0.0 || projCoords.x > 1.0 || projCoords.y < 0.0 || projCoords.y > 1.0) {
				return 0.0;
			}

			// Get shadow atlas coordinates
			vec2 shadowAtlasSize = vec2(textureSize(_ShadowAtlas, 0));
			vec2 atlasCoords;
			atlasCoords.x = _ShadowAtlasParams.x + (projCoords.x * _ShadowAtlasParams.z);
			atlasCoords.y = _ShadowAtlasParams.y + (projCoords.y * _ShadowAtlasParams.z);

			float atlasSize = shadowAtlasSize.x;

			// Calculate shadow map boundaries to prevent bleeding
			vec2 texelSize = vec2(1.0) / shadowAtlasSize;
			vec2 shadowMin = vec2(_ShadowAtlasParams.x, _ShadowAtlasParams.y) / atlasSize + texelSize * 0.5;
			vec2 shadowMax = vec2(_ShadowAtlasParams.x + _ShadowAtlasParams.z, _ShadowAtlasParams.y + _ShadowAtlasParams.z) / atlasSize - texelSize * 0.5;

			atlasCoords /= atlasSize;



            // Slope-scale bias: adjust based on surface angle relative to light
            float cosTheta = clamp(dot(normalize(worldNormal), normalize(_LightDirection)), 0.0, 1.0);
            float slopeScaleBias = _ShadowBias * tan(acos(cosTheta));
            slopeScaleBias = clamp(slopeScaleBias, 0.0, _ShadowBias * 2.0);
            
            // Combined bias: base + slope + texel offset
            float finalBias = _ShadowBias + slopeScaleBias;
            
            // Get current depth with improved bias
            float currentDepth = projCoords.z - finalBias;



			float shadow = 0.0;

			// Check shadow quality: 0 = Hard, 1 = Soft
			if (_ShadowQuality < 0.5) {
				// Hard shadows
				float closestDepth = texture(_ShadowAtlas, atlasCoords).r;
				shadow = currentDepth > closestDepth ? 1.0 : 0.0;
			} else {
				// Soft shadows - Poisson Disk PCF
				float filterRadius = 1.5;
				float randomRotation = InterleavedGradientNoise(gl_FragCoord.xy) * 6.283185;
				float s = sin(randomRotation);
				float c = cos(randomRotation);
				mat2 rotationMatrix = mat2(c, -s, s, c);

				for(int i = 0; i < 8; i++) {
					vec2 offset = rotationMatrix * poissonDisk[i] * texelSize * filterRadius;
					vec2 sampleCoords = clamp(atlasCoords + offset, shadowMin, shadowMax);
					float pcfDepth = texture(_ShadowAtlas, sampleCoords).r;
					shadow += currentDepth > pcfDepth ? 1.0 : 0.0;
				}
				shadow /= 8.0;
			}

			return shadow * _ShadowStrength;
		}

		// Calculate spot light contribution
		vec3 CalculateSpotLight(vec3 worldPos, vec3 worldNormal, vec3 cameraPos, vec3 albedo, float metallic, float roughness, float ao)
		{
			vec3 lightToPixel = worldPos - _LightPosition;
			float distance = length(lightToPixel);
			vec3 lightDir = normalize(-lightToPixel);
			vec3 viewDir = normalize(-(worldPos - cameraPos));
			vec3 halfDir = normalize(lightDir + viewDir);

			// Distance attenuation (inverse square law with smoothstep at range)
			float distanceAttenuation = 1.0 / (distance * distance + 1.0);
			float rangeAttenuation = 1.0 - smoothstep(_LightRange * 0.8, _LightRange, distance);
			float attenuation = distanceAttenuation * rangeAttenuation;

			// Spot light cone attenuation (smooth falloff from inner to outer angle)
			float spotAngleRad = radians(_SpotAngle);
			float innerSpotAngleRad = radians(_InnerSpotAngle);
			float lightAngleCos = dot(normalize(_LightDirection), normalize(lightToPixel));
			float outerCos = cos(spotAngleRad);
			float innerCos = cos(innerSpotAngleRad);
			float spotAttenuation = smoothstep(outerCos, innerCos, lightAngleCos);
			attenuation *= spotAttenuation;

			// Early exit if outside spot cone or range
			if (attenuation <= 0.0001) {
				return vec3(0.0);
			}

			// Calculate base reflectivity for PBR
			vec3 F0 = vec3(0.04);
			F0 = mix(F0, albedo, metallic);

			// Light radiance with attenuation
			vec3 radiance = _LightColor.rgb * _LightIntensity * attenuation;

			// Cook-Torrance BRDF
			float NDF = DistributionGGX(worldNormal, halfDir, roughness);
			float G = GeometrySmith(worldNormal, viewDir, lightDir, roughness);
			vec3 F = FresnelSchlick(max(dot(halfDir, viewDir), 0.0), F0);

			// Specular and diffuse
			vec3 kS = F;
			vec3 kD = vec3(1.0) - kS;
			kD *= 1.0 - metallic;

			float NdotL = max(dot(worldNormal, lightDir), 0.0);

			// Specular term
			vec3 numerator = NDF * G * F;
			float denominator = 4.0 * max(dot(worldNormal, viewDir), 0.0) * NdotL + 0.0001;
			vec3 specular = numerator / denominator;

			// Calculate shadow
			float shadow = SampleShadow(worldPos, worldNormal);
			float shadowFactor = 1.0 - shadow;

			// Final lighting
			vec3 diffuse = kD * albedo / PI;
			return (diffuse + specular) * radiance * NdotL * shadowFactor * ao;
		}

		void main()
		{
			// Sample GBuffer
			vec4 gbufferA = texture(_GBufferA, TexCoords);
			vec4 gbufferB = texture(_GBufferB, TexCoords);
			vec4 gbufferC = texture(_GBufferC, TexCoords);

			// Extract material properties
			vec3 albedo = gbufferA.rgb;
			float ao = gbufferA.a;

			// Decode normal from [0,1] to [-1,1] range
			vec3 viewNormal = gbufferB.rgb * 2.0 - 1.0;
			float shadingMode = gbufferB.a;

			float roughness = gbufferC.r;
			float metallic = gbufferC.g;

			// Sample depth
			float depth = texture(_CameraDepthTexture, TexCoords).r;

			// Reconstruct world position
			vec3 worldPos = WorldPosFromDepth(depth, TexCoords);

			// Transform normal from view space to world space
			vec3 worldNormal = normalize(mat3(PROWL_MATRIX_I_V) * viewNormal);

			// Check shading mode (0 = Unlit, 1 = Lit)
			if (shadingMode != 1.0) {
				finalColor = vec4(0.0, 0.0, 0.0, 0.0);
				return;
			}

			// Calculate spot light contribution
			vec3 lighting = CalculateSpotLight(worldPos, worldNormal, _WorldSpaceCameraPos.xyz, albedo, metallic, roughness, ao);

			finalColor = vec4(lighting, 1.0);
		}
	}

	ENDGLSL
}
