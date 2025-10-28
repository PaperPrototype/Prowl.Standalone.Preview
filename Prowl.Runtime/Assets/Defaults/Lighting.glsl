#ifndef LIGHTING_FUNCTIONS
#define LIGHTING_FUNCTIONS

#include "PBR"

// ------------------------------------------------------------------------------
// Shadow Sampling Functions
// ------------------------------------------------------------------------------
// Note: These can be used as helper functions if needed,
// but each light now handles its own shadow sampling in its shader

// Poisson disk sampling pattern for smooth PCF with fewer samples
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

// Vogel disk sampling for procedural smooth distribution
vec2 VogelDiskSample(int sampleIndex, int samplesCount, float phi) {
    float GoldenAngle = 2.4; // Golden angle in radians

    float r = sqrt(float(sampleIndex) + 0.5) / sqrt(float(samplesCount));
    float theta = float(sampleIndex) * GoldenAngle + phi;

    float sine = sin(theta);
    float cosine = cos(theta);

    return vec2(r * cosine, r * sine);
}

// Simple hash function for random rotation
float InterleavedGradientNoise(vec2 position) {
    vec3 magic = vec3(0.06711056, 0.00583715, 52.9829189);
    return fract(magic.z * fract(dot(position, magic.xy)));
}

float SampleDirectionalShadow(SunLightStruct sun, vec3 worldPos, vec3 worldNormal, sampler2D shadowAtlas, vec2 shadowAtlasSize)
{
    float BIAS_SCALE = 0.001;
    float NORMAL_BIAS_SCALE = 0.05;

    // Perform perspective divide to get NDC coordinates
    vec3 worldPosBiased = worldPos + (normalize(worldNormal) * sun.shadowNormalBias * NORMAL_BIAS_SCALE);
    vec4 lightSpacePos = sun.shadowMatrix * vec4(worldPosBiased, 1.0);
    vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;

    // Transform to [0,1] range
    projCoords = projCoords * 0.5 + 0.5;

    // Early exit if beyond shadow distance or outside shadow map
    if (projCoords.z > 1.0 ||
        projCoords.x < 0.0 || projCoords.x > 1.0 ||
        projCoords.y < 0.0 || projCoords.y > 1.0) {
        return 0.0;
    }

    // Get shadow atlas coordinates
    vec2 atlasCoords;
    atlasCoords.x = sun.atlasX + (projCoords.x * sun.atlasWidth);
    atlasCoords.y = sun.atlasY + (projCoords.y * sun.atlasWidth);

    float atlasSize = shadowAtlasSize.x;

    // Calculate shadow map boundaries in normalized atlas coordinates to prevent bleeding
    vec2 texelSize = vec2(1.0) / shadowAtlasSize;
    vec2 shadowMin = vec2(sun.atlasX, sun.atlasY) / atlasSize + texelSize * 0.5;
    vec2 shadowMax = vec2(sun.atlasX + sun.atlasWidth, sun.atlasY + sun.atlasWidth) / atlasSize - texelSize * 0.5;

    atlasCoords /= atlasSize;

    // Get current depth with bias
    float currentDepth = projCoords.z - (sun.shadowBias * BIAS_SCALE);

    float shadow = 0.0;

    // Check shadow quality: 0 = Hard, 1 = Soft
    if (sun.shadowQuality < 0.5) {
        // Hard shadows - single sample
        float closestDepth = texture(shadowAtlas, atlasCoords).r;
        shadow = currentDepth > closestDepth ? 1.0 : 0.0;
    } else {
        // Soft shadows - Poisson Disk PCF
        float filterRadius = 1.5; // Controls shadow softness

        // Random rotation to reduce banding artifacts
        float randomRotation = InterleavedGradientNoise(gl_FragCoord.xy) * 6.283185; // 2*PI
        float s = sin(randomRotation);
        float c = cos(randomRotation);
        mat2 rotationMatrix = mat2(c, -s, s, c);

        // Sample using Poisson disk pattern
        for(int i = 0; i < 16; i++) {
            vec2 offset = rotationMatrix * poissonDisk[i] * texelSize * filterRadius;
            // Clamp sample coordinates to shadow map boundaries to prevent bleeding
            vec2 sampleCoords = clamp(atlasCoords + offset, shadowMin, shadowMax);
            float pcfDepth = texture(shadowAtlas, sampleCoords).r;
            shadow += currentDepth > pcfDepth ? 1.0 : 0.0;
        }
        shadow /= 16.0;
    }

    // Apply shadow strength
    shadow *= sun.shadowStrength;

    return shadow;
}

// Note: Spot and Point light shadow sampling removed as they are no longer used.
// Each light type now implements its own shadow sampling in its dedicated shader.

#endif
