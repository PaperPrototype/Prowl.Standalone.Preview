Shader "Default/Particle"

Properties
{
    _MainTex ("Particle Texture", Texture2D) = "white"
    _MainColor ("Tint", Color) = (1.0, 1.0, 1.0, 1.0)
    _SoftParticlesFactor ("Soft Particles Factor", Float) = 1.0
}

Pass "Particle"
{
    Tags { "RenderOrder" = "Transparent" }

    // Particle settings
    Cull Off
    ZWrite Off
    Blend Alpha

	GLSLPROGRAM

		Vertex
		{
            #include "Fragment"
            #include "VertexAttributes"

			out vec2 texCoord0;
			out vec4 vColor;
            out vec3 worldPos;
            out float vLifetime;

#ifdef GPU_INSTANCING
            // Instance attributes (semantic 8-13)
            layout(location = 8) in vec4 instanceModelRow0;
            layout(location = 9) in vec4 instanceModelRow1;
            layout(location = 10) in vec4 instanceModelRow2;
            layout(location = 11) in vec4 instanceModelRow3;
            layout(location = 12) in vec4 instanceColor;
            layout(location = 13) in vec4 instanceCustomData;
#endif

			void main()
			{
#ifdef GPU_INSTANCING
                // Extract position and scale from instance matrix
                vec3 particlePosition = instanceModelRow3.xyz;

                // Calculate scale from the instance matrix column lengths
                float scaleX = length(instanceModelRow0.xyz);
                float scaleY = length(instanceModelRow1.xyz);

                // Extract rotation from the matrix (Z-axis rotation for billboards)
                // Get normalized right vector from first column
                vec3 matrixRight = instanceModelRow0.xyz / scaleX;
                vec3 matrixUp = instanceModelRow1.xyz / scaleY;

                // Calculate rotation angle from the XY components
                // This extracts the Z-axis rotation that was applied
                float rotationAngle = atan(matrixRight.y, matrixRight.x);

                // Create billboard matrix using camera's inverse view matrix
                // Get camera right and up vectors from view matrix (inverse)
                vec3 cameraRight = vec3(PROWL_MATRIX_V[0][0], PROWL_MATRIX_V[1][0], PROWL_MATRIX_V[2][0]);
                vec3 cameraUp = vec3(PROWL_MATRIX_V[0][1], PROWL_MATRIX_V[1][1], PROWL_MATRIX_V[2][1]);

                // Apply rotation to the quad vertices
                float cosRot = cos(rotationAngle);
                float sinRot = sin(rotationAngle);
                vec2 rotatedVertex = vec2(
                    vertexPosition.x * cosRot - vertexPosition.y * sinRot,
                    vertexPosition.x * sinRot + vertexPosition.y * cosRot
                );

                // Build billboard quad in world space with rotation applied
                vec3 worldPosition = particlePosition
                    + cameraRight * rotatedVertex.x * scaleX
                    + cameraUp * rotatedVertex.y * scaleY;

                gl_Position = PROWL_MATRIX_VP * vec4(worldPosition, 1.0);

                // Apply UV animation
                // instanceCustomData: x=lifetime, y=UV offsetX, z=UV offsetY, w=UV scale
                vec2 uvOffset = instanceCustomData.yz;
                float uvScale = instanceCustomData.w;
                texCoord0 = vertexTexCoord0 * uvScale + uvOffset;

                worldPos = worldPosition;

                // Multiply vertex color by instance color
                vColor = vertexColor * instanceColor;

                // Pass normalized lifetime from custom data
                vLifetime = instanceCustomData.x;
#else
                // Non-instanced fallback
                gl_Position = PROWL_MATRIX_MVP * vec4(vertexPosition, 1.0);
                texCoord0 = vertexTexCoord0;
                worldPos = (PROWL_MATRIX_M * vec4(vertexPosition, 1.0)).xyz;
                vColor = vertexColor;
                vLifetime = 0.0;
#endif
			}
		}

		Fragment
		{
            #include "Fragment"

			// GBuffer layout:
			// BufferA: RGB = Albedo, A = AO
			// BufferB: RGB = Normal (view space), A = ShadingMode
			// BufferC: R = Roughness, G = Metalness, B = Specular, A = Unused
			// BufferD: Custom Data per Shading Mode (e.g., shading mode 0 = Unlit with RGBA as Emissive)
			layout (location = 0) out vec4 gBufferA;
			layout (location = 1) out vec4 gBufferB;
			layout (location = 2) out vec4 gBufferC;
			layout (location = 3) out vec4 gBufferD;

			in vec2 texCoord0;
			in vec4 vColor;
            in vec3 worldPos;
            in float vLifetime;

			uniform sampler2D _MainTex;
			uniform vec4 _MainColor;
            uniform float _SoftParticlesFactor;

			void main()
			{
                // Sample particle texture
				vec4 albedo = texture(_MainTex, texCoord0) * vColor * _MainColor;

                // Discard fully transparent pixels
                if (albedo.a < 0.01)
                    discard;

                // Convert to linear space
                vec3 baseColor = gammaToLinearSpace(albedo.rgb);

                // Calculate view-space normal (face camera)
                vec3 viewNormal = vec3(0.0, 0.0, 1.0);

				// Output to GBuffer
				// BufferA: RGB = Albedo, A = Alpha (for blending)
				gBufferA = vec4(baseColor, albedo.a);

				// BufferB: RGB = Normal (view space), A = ShadingMode
				// ShadingMode: 0 = Unlit (particles are typically unlit)
				float shadingMode = 0.0; // Unlit
				gBufferB = vec4(viewNormal * 0.5 + 0.5, shadingMode);

				// BufferC: R = Roughness, G = Metalness, B = Specular, A = Unused
				gBufferC = vec4(1.0, 0.0, 0.0, 0.0); // Full roughness, no metal, no specular

				// BufferD: For Unlit mode (0), this stores emissive/glow data
				// We use the albedo as self-illumination for particles
				gBufferD = vec4(baseColor * albedo.a, 0.0);
			}
		}
	ENDGLSL
}
