Shader "Default/Unlit"

Properties
{
    _MainTex ("Texture", Texture2D) = "white"
    _MainColor ("Tint", Color) = (1.0, 1.0, 1.0, 1.0)
}

Pass "Unlit"
{
    Tags { "RenderOrder" = "Opaque" }
    Cull Back

	GLSLPROGRAM
		Vertex
		{
            #include "Fragment"
            #include "VertexAttributes"

			out vec2 texCoord0;
			out vec3 worldPos;
			out vec4 currentPos;
			out vec4 previousPos;
			out float fogCoord;
			out vec4 vColor;
			out vec3 vNormal;

			void main()
			{
#ifdef SKINNED
				vec4 skinnedPos = GetSkinnedPosition(vertexPosition);
				vec3 skinnedNormal = GetSkinnedNormal(vertexNormal);

				gl_Position = PROWL_MATRIX_MVP * skinnedPos;
				fogCoord = gl_Position.z;
				currentPos = gl_Position;
				texCoord0 = vertexTexCoord0;

				vec4 prevWorldPos = PROWL_MATRIX_M_PREVIOUS * skinnedPos;
				previousPos = PROWL_MATRIX_VP_PREVIOUS * prevWorldPos;

				worldPos = (PROWL_MATRIX_M * skinnedPos).xyz;
				vColor = vertexColor;
				vNormal = normalize(mat3(PROWL_MATRIX_M) * skinnedNormal);
#else
				gl_Position = PROWL_MATRIX_MVP * vec4(vertexPosition, 1.0);
				fogCoord = gl_Position.z;
				currentPos = gl_Position;
				texCoord0 = vertexTexCoord0;

				vec4 prevWorldPos = PROWL_MATRIX_M_PREVIOUS * vec4(vertexPosition, 1.0);
				previousPos = PROWL_MATRIX_VP_PREVIOUS * prevWorldPos;

				worldPos = (PROWL_MATRIX_M * vec4(vertexPosition, 1.0)).xyz;
				vColor = vertexColor;
				vNormal = normalize(mat3(PROWL_MATRIX_M) * vertexNormal);
#endif
			}
		}

		Fragment
		{
            #include "Fragment"

			layout (location = 0) out vec4 gAlbedo;
			layout (location = 1) out vec4 gMotionVector;
			layout (location = 2) out vec4 gNormal;
			layout (location = 3) out vec4 gSurface;

			in vec2 texCoord0;
			in vec3 worldPos;
			in vec4 currentPos;
			in vec4 previousPos;
			in float fogCoord;
			in vec4 vColor;
			in vec3 vNormal;

			uniform sampler2D _MainTex;

			void main()
			{
				vec2 curNDC = (currentPos.xy / currentPos.w) - _CameraJitter;
				vec2 prevNDC = (previousPos.xy / previousPos.w) - _CameraPreviousJitter;
			    gMotionVector = vec4((curNDC - prevNDC) * 0.5, 0.0, 1.0);

				vec4 albedo = texture(_MainTex, texCoord0) * vColor;

                vec3 viewNormal = normalize(mat3(PROWL_MATRIX_V) * vNormal);
                gNormal = vec4(viewNormal, 1.0);

				gSurface = vec4(1.0, 0.0, 0.0, 1.0);

				vec3 baseColor = albedo.rgb;
				baseColor.rgb = gammaToLinearSpace(baseColor.rgb);

				gAlbedo = vec4(baseColor, albedo.a);
				gAlbedo.rgb = ApplyFog(fogCoord, gAlbedo.rgb);
			}
		}
	ENDGLSL
}

Pass "UnlitMotionVector"
{
    Tags { "RenderOrder" = "DepthOnly" }
    Cull Back

	GLSLPROGRAM
		Vertex
		{
            #include "Fragment"
            #include "VertexAttributes"

			void main()
			{
#ifdef SKINNED
				vec4 skinnedPos = GetSkinnedPosition(vertexPosition);
				gl_Position = PROWL_MATRIX_MVP * skinnedPos;
#else
				gl_Position = PROWL_MATRIX_MVP * vec4(vertexPosition, 1.0);
#endif
			}
		}

		Fragment
		{
            #include "Fragment"
			void main() {}
		}
	ENDGLSL
}
