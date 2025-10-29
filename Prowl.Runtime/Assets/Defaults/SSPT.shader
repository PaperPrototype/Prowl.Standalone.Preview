Shader "Default/SSPT"

Properties
{
}

Pass "ScreenSpacePathTrace"
{
    Tags { "RenderOrder" = "Opaque" }
    Blend Off
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
            gl_Position = vec4(vertexPosition, 1.0);
            TexCoords = vertexTexCoord;
        }
    }

    Fragment
    {
        #include "Fragment"

        uniform sampler2D _MainTex;
        uniform sampler2D _CameraDepthTexture;
        uniform sampler2D _GBufferB; // Normals
        uniform sampler2D _GBufferA; // Albedo for energy conservation

        uniform int _SamplesPerPixel;
        uniform int _RaySteps;
        uniform float _RayLength;
        uniform float _Intensity;
        uniform int _FrameIndex;

        in vec2 TexCoords;

        layout(location = 0) out vec4 giOutput;

        // Project view space position to screen space (UV + depth)
        vec3 projectToScreenSpace(vec3 viewPos) {
            vec4 clipPos = PROWL_MATRIX_P * vec4(viewPos, 1.0);
            clipPos.xyz /= clipPos.w;
            return vec3(clipPos.xy * 0.5 + 0.5, clipPos.z);
        }

        // Screen-space ray marching (SSR-style)
        vec3 TraceScreenSpaceRay(vec3 startScreen, vec3 worldDir, inout NoiseGenerator noiseGen) {
            // Convert start screen to view space
            vec3 startView = getViewPos(startScreen.xy, startScreen.z);

            // Convert world direction to view space
            vec3 viewDir = mat3(PROWL_MATRIX_V) * worldDir;

            // Calculate end point in view space and project to screen
            vec3 endView = startView + viewDir * 10000.0;
            vec3 endScreen = projectToScreenSpace(endView);

            // Calculate screen space ray delta
            vec3 rayDelta = endScreen - startScreen;

            // Avoid degenerate rays
            if (length(rayDelta.xy) < 0.0001)
                return vec3(-1.0);

            // Calculate step size to ensure we sample at least once per pixel
            vec2 screenSize = _ScreenParams.xy;
            float screenSteps = max(abs(rayDelta.x) * screenSize.x, abs(rayDelta.y) * screenSize.y);
            float stepCount = min(screenSteps, float(_RaySteps));
            vec3 rayStep = rayDelta / max(stepCount, 1.0);

            // Start with jitter
            vec3 currentScreen = startScreen + rayStep + 0.001 * randNextF(noiseGen);

            for (float i = 0.0; i < stepCount; i += 1.0) {
                // Check bounds
                if (currentScreen.x < 0.0 || currentScreen.x > 1.0 ||
                    currentScreen.y < 0.0 || currentScreen.y > 1.0)
                    return vec3(-1.0);

                // Sample scene depth
                float sceneDepth = texture(_CameraDepthTexture, currentScreen.xy).r;

                // Check intersection (ray depth is behind scene depth)
                if (currentScreen.z >= sceneDepth) {
                    return vec3(currentScreen.xy, sceneDepth);
                }

                currentScreen += rayStep;
            }

            // No hit
            return vec3(-1.0);
        }

        // Calculate screen-space path traced global illumination (single bounce)
        vec3 CalculateSSPT(vec3 screenPos, vec3 viewPos, vec3 worldNormal) {
            // Initialize noise generator for this pixel with temporal variation
            NoiseGenerator noiseGen = createNoiseGenerator(gl_FragCoord, uint(_FrameIndex));

            vec3 giSum = vec3(0.0);

            // Cast multiple rays per pixel
            for (int spp = 0; spp < _SamplesPerPixel; ++spp) {
                // Sample random direction in cosine-weighted hemisphere (world space)
                vec3 rayDir = SampleCosineHemisphere(worldNormal, randNext2F(noiseGen));

                // Trace ray (SSR-style: screen space start, world space direction)
                vec3 hitPos = TraceScreenSpaceRay(screenPos, rayDir, noiseGen);

                // If ray hits something, sample the accumulated lighting
                if (hitPos.x >= 0.0) {
                    vec4 hitNormalData = texture(_GBufferB, hitPos.xy);

                    // Skip unlit surfaces (skybox, emissives, etc)
                    if (hitNormalData.a > 0.0) {
                        // Sample accumulated lighting at hit point
                        vec3 hitLighting = texture(_MainTex, hitPos.xy).rgb;

                        // Modulate by hit surface albedo for energy conservation
                        vec3 hitAlbedo = texture(_GBufferA, hitPos.xy).rgb;

                        giSum += hitLighting * hitAlbedo;
                    }
                }
            }

            // Average over all samples
            return giSum / float(_SamplesPerPixel);
        }

        void main()
        {
            float depth = texture(_CameraDepthTexture, TexCoords).r;

            // Sky pixels don't receive GI
            if (depth >= 1.0) {
                giOutput = vec4(0.0, 0.0, 0.0, 0.0);
                return;
            }

            // Get view space position and normal
            vec3 viewPos = getViewPos(TexCoords, depth);
            vec4 normalData = texture(_GBufferB, TexCoords);

            // Decode normal from [0,1] to [-1,1] and transform from view space to world space
            vec3 viewNormal = normalData.xyz * 2.0 - 1.0;
            vec3 worldNormal = normalize(mat3(PROWL_MATRIX_I_V) * viewNormal);

            // Check if this is a valid surface (not unlit)
            if (normalData.a <= 0.0) {
                giOutput = vec4(0.0, 0.0, 0.0, 0.0);
                return;
            }

            // Calculate global illumination (world space)
            vec3 screenPos = vec3(TexCoords, depth);
            vec3 gi = CalculateSSPT(screenPos, viewPos, worldNormal);

            // Energy conservation: scale by albedo
            vec3 albedo = texture(_GBufferA, TexCoords).rgb;
            gi *= albedo;

            // Output GI contribution
            giOutput = vec4(gi * _Intensity, 1.0);
        }
    }
    ENDGLSL
}

Pass "TemporalAccumulation"
{
    Tags { "RenderOrder" = "Opaque" }
    Blend Off
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
            gl_Position = vec4(vertexPosition, 1.0);
            TexCoords = vertexTexCoord;
        }
    }

    Fragment
    {
        #include "Fragment"

        uniform sampler2D _CurrentGI;
        uniform sampler2D _HistoryGI;
        uniform sampler2D _CameraDepthTexture;
        uniform float _TemporalBlend;

        in vec2 TexCoords;

        layout(location = 0) out vec4 fragColor;

        void main()
        {
            vec3 currentGI = texture(_CurrentGI, TexCoords).rgb;
            vec3 historyGI = texture(_HistoryGI, TexCoords).rgb;

            float depth = texture(_CameraDepthTexture, TexCoords).r;

            // Sky pixels don't get accumulated
            if (depth >= 1.0) {
                fragColor = vec4(currentGI, 1.0);
                return;
            }

            // Temporal accumulation with exponential moving average
            vec3 accumulated = mix(currentGI, historyGI, _TemporalBlend);

            fragColor = vec4(accumulated, 1.0);
        }
    }
    ENDGLSL
}

Pass "Blur"
{
    Tags { "RenderOrder" = "Opaque" }
    Blend Off
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
            gl_Position = vec4(vertexPosition, 1.0);
            TexCoords = vertexTexCoord;
        }
    }

    Fragment
    {
        #include "Fragment"

        uniform sampler2D _MainTex;
        uniform sampler2D _CameraDepthTexture;
        uniform vec2 _BlurDirection;
        uniform float _BlurRadius;

        in vec2 TexCoords;

        layout(location = 0) out vec4 fragColor;

        void main()
        {
            vec2 texelSize = 1.0 / _ScreenParams.xy;
            float centerDepth = texture(_CameraDepthTexture, TexCoords).r;

            vec3 result = texture(_MainTex, TexCoords).rgb;
            float totalWeight = 1.0;

            // Depth-aware bilateral blur
            for (int i = -3; i <= 3; i++) {
                if (i == 0) continue;

                float offset = float(i) * _BlurRadius;
                vec2 sampleUV = TexCoords + _BlurDirection * texelSize * offset;

                float sampleDepth = texture(_CameraDepthTexture, sampleUV).r;
                float depthDiff = abs(centerDepth - sampleDepth);

                // Weight based on depth similarity
                float weight = exp(-depthDiff * 50.0) * exp(-0.5 * float(i * i) / 4.0);

                result += texture(_MainTex, sampleUV).rgb * weight;
                totalWeight += weight;
            }

            fragColor = vec4(result / totalWeight, 1.0);
        }
    }
    ENDGLSL
}

Pass "Composite"
{
    Tags { "RenderOrder" = "Opaque" }
    Blend Off
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
            gl_Position = vec4(vertexPosition, 1.0);
            TexCoords = vertexTexCoord;
        }
    }

    Fragment
    {
        #include "Fragment"

        uniform sampler2D _MainTex;
        uniform sampler2D _GITex;
        uniform float _Intensity;

        in vec2 TexCoords;

        layout(location = 0) out vec4 fragColor;

        void main()
        {
            vec4 sceneColor = texture(_MainTex, TexCoords);
            vec3 gi = texture(_GITex, TexCoords).rgb;

            // Add global illumination to scene
            vec3 finalColor = sceneColor.rgb + gi * _Intensity;

            fragColor = vec4(finalColor, sceneColor.a);
        }
    }
    ENDGLSL
}
