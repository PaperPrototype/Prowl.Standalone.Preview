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
        uniform float _Thickness;
        uniform float _Intensity;
        uniform int _FrameIndex;

        in vec2 TexCoords;

        layout(location = 0) out vec4 giOutput;

        // Project view space position to screen space
        vec3 ViewToScreenSpace(vec3 viewPos) {
            vec4 clipPos = PROWL_MATRIX_P * vec4(viewPos, 1.0);
            clipPos.xyz /= clipPos.w;
            return clipPos.xyz * 0.5 + 0.5;
        }

        // Screen-space ray marching for path tracing
        vec3 TraceScreenSpaceRay(vec3 viewPos, vec3 viewDir, float dither) {
            // Don't trace rays pointing away from camera or behind it
            if (viewDir.z > max0(-viewPos.z))
                return vec3(-1.0);

            // Calculate end position in screen space
            vec3 rayEnd = ViewToScreenSpace(viewPos + viewDir * _RayLength);
            vec3 rayStart = ViewToScreenSpace(viewPos);
            vec3 rayDir = normalize(rayEnd - rayStart);

            // Calculate step size to stay within screen bounds
            float stepLength = minOf((step(0.0, rayDir) - rayStart) / rayDir) * rcp(float(_RaySteps));
            vec3 rayStep = rayDir * stepLength;

            // Start position with dither
            vec3 rayPos = rayStart + rayStep * dither;

            // Convert to texel coordinates for faster iteration
            vec2 screenSize = _ScreenParams.xy;
            vec2 rayPosTexel = rayPos.xy * screenSize;
            vec2 rayStepTexel = rayStep.xy * screenSize;

            for (int i = 0; i < _RaySteps; ++i) {
                // Check if ray is within screen bounds
                if (rayPos.x < 0.0 || rayPos.x > 1.0 || rayPos.y < 0.0 || rayPos.y > 1.0)
                    break;

                // Sample depth at current position
                float sampleDepth = texture(_CameraDepthTexture, rayPos.xy).r;

                // Check for intersection
                if (sampleDepth < rayPos.z) {
                        return vec3(rayPos.xy, sampleDepth);
                    //// Convert depths to view space for thickness check
                    //float sampleDepthView = ScreenToViewDepth(sampleDepth);
                    //float traceDepthView = ScreenToViewDepth(rayPos.z);
                    //
                    //// Check if intersection is within thickness threshold
                    //float depthDiff = traceDepthView - sampleDepthView;
                    //if (depthDiff > _Thickness * abs(traceDepthView)) {
                    //    // Hit! Return the hit position
                    //    return vec3(rayPos.xy, sampleDepth);
                    //}
                }

                // Step along ray
                rayPos += rayStep;
            }

            // No hit
            return vec3(-1.0);
        }

        // Calculate screen-space path traced global illumination
        vec3 CalculateSSPT(vec3 screenPos, vec3 viewPos, vec3 viewNormal) {
            // Initialize noise generator for this pixel
            uvec2 pixelCoord = uvec2(TexCoords * _ScreenParams.xy);
            NoiseGenerator noiseGen = initNoiseGenerator(pixelCoord, uint(_FrameIndex));

            vec3 giSum = vec3(0.0);
            int validSamples = 0;

            // Cast multiple rays per pixel
            for (int spp = 0; spp < _SamplesPerPixel; ++spp) {
                // Sample random direction in cosine-weighted hemisphere
                vec3 rayDir = SampleCosineHemisphere(viewNormal, nextVec2(noiseGen));
                float dither = nextFloat(noiseGen);

                // Offset start position slightly along normal to avoid self-intersection
                vec3 rayStart = viewPos + viewNormal * 0.01;

                // Trace ray
                vec3 hitPos = TraceScreenSpaceRay(rayStart, rayDir, dither);

                // If we hit something, sample the radiance
                if (hitPos.x >= 0.0) {
                    vec3 hitRadiance = texture(_MainTex, hitPos.xy).rgb;

                    // Weight by cosine term (already handled by cosine hemisphere sampling)
                    float NdotL = max0(dot(viewNormal, rayDir));
                    giSum += hitRadiance * NdotL;
                    validSamples++;
                }
            }

            // Average the samples
            if (validSamples > 0) {
                return giSum / float(validSamples);
            }

            return vec3(0.0);
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
            vec3 viewNormal = normalize(normalData.xyz * 2.0 - 1.0);

            // Check if this is a valid surface (not unlit)
            if (normalData.a <= 0.0) {
                giOutput = vec4(0.0, 0.0, 0.0, 0.0);
                return;
            }

            // Calculate global illumination
            vec3 screenPos = vec3(TexCoords, depth);
            vec3 gi = CalculateSSPT(screenPos, viewPos, viewNormal);

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
