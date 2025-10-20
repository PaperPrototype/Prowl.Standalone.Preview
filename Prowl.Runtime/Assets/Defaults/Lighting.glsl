#ifndef LIGHTING_FUNCTIONS
#define LIGHTING_FUNCTIONS

#include "PBR"

// ------------------------------------------------------------------------------
// Light Structures and Unpacking Functions
// ------------------------------------------------------------------------------

struct SunLightStruct {
    vec3 direction;
    vec3 color;
    float intensity;
    mat4 shadowMatrix;
    float shadowBias;
    float shadowNormalBias;
    float shadowStrength;
    float shadowDistance;
    float shadowQuality;
    float atlasX;
    float atlasY;
    float atlasWidth;
};

struct SpotLightStruct {
    vec3 position;
    vec3 direction;
    vec3 color;
    float intensity;
    float range;
    float innerAngle;
    float outerAngle;
    mat4 shadowMatrix;
    float shadowBias;
    float shadowNormalBias;
    float shadowStrength;
    float shadowQuality;
    float atlasX;
    float atlasY;
    float atlasWidth;
};

struct PointLightStruct {
    vec3 position;
    vec3 color;
    float intensity;
    float range;
    float shadowBias;
    float shadowNormalBias;
    float shadowStrength;
    float shadowQuality;
    float atlasX;
    float atlasY;
    float atlasWidth;
};

// Helper function to get the directional light (sun) from global uniforms
SunLightStruct GetDirectionalLight() {
    SunLightStruct sun;
    sun.direction = prowl_SunDirection;
    sun.color = prowl_SunColor;
    sun.intensity = prowl_SunIntensity;
    sun.shadowMatrix = prowl_SunShadowMatrix;
    sun.shadowBias = prowl_SunShadowBias;
    sun.shadowNormalBias = prowl_SunShadowParams.x;
    sun.shadowStrength = prowl_SunShadowParams.y;
    sun.shadowDistance = prowl_SunShadowParams.z;
    sun.shadowQuality = prowl_SunShadowParams.w;
    sun.atlasX = prowl_SunAtlasParams.x;
    sun.atlasY = prowl_SunAtlasParams.y;
    sun.atlasWidth = prowl_SunAtlasParams.z;
    return sun;
}

// Helper function to unpack a point light from the packed arrays
PointLightStruct GetPointLight(int index) {
    PointLightStruct light;
    light.position = vec3(prowl_4PointLightPosX[index], prowl_4PointLightPosY[index], prowl_4PointLightPosZ[index]);
    light.color = vec3(prowl_4PointLightColorR[index], prowl_4PointLightColorG[index], prowl_4PointLightColorB[index]);
    light.intensity = prowl_4PointLightIntensity[index];
    light.range = prowl_4PointLightRange[index];
    light.shadowBias = prowl_4PointLightShadowBias[index];
    light.shadowNormalBias = prowl_4PointLightShadowNormalBias[index];
    light.shadowStrength = prowl_4PointLightShadowStrength[index];
    light.shadowQuality = prowl_4PointLightShadowQuality[index];
    light.atlasX = prowl_4PointLightAtlasX[index];
    light.atlasY = prowl_4PointLightAtlasY[index];
    light.atlasWidth = prowl_4PointLightAtlasWidth[index];
    return light;
}

// Helper function to unpack a spot light from the packed arrays
SpotLightStruct GetSpotLight(int index) {
    SpotLightStruct light;
    light.position = vec3(prowl_4SpotLightPosX[index], prowl_4SpotLightPosY[index], prowl_4SpotLightPosZ[index]);
    light.direction = vec3(prowl_4SpotLightDirX[index], prowl_4SpotLightDirY[index], prowl_4SpotLightDirZ[index]);
    light.color = vec3(prowl_4SpotLightColorR[index], prowl_4SpotLightColorG[index], prowl_4SpotLightColorB[index]);
    light.intensity = prowl_4SpotLightIntensity[index];
    light.range = prowl_4SpotLightRange[index];
    light.innerAngle = prowl_4SpotLightInnerAngle[index];
    light.outerAngle = prowl_4SpotLightOuterAngle[index];

    // Get the correct shadow matrix
    if (index == 0) light.shadowMatrix = prowl_SpotLightShadowMatrix0;
    else if (index == 1) light.shadowMatrix = prowl_SpotLightShadowMatrix1;
    else if (index == 2) light.shadowMatrix = prowl_SpotLightShadowMatrix2;
    else light.shadowMatrix = prowl_SpotLightShadowMatrix3;

    light.shadowBias = prowl_4SpotLightShadowBias[index];
    light.shadowNormalBias = prowl_4SpotLightShadowNormalBias[index];
    light.shadowStrength = prowl_4SpotLightShadowStrength[index];
    light.shadowQuality = prowl_4SpotLightShadowQuality[index];
    light.atlasX = prowl_4SpotLightAtlasX[index];
    light.atlasY = prowl_4SpotLightAtlasY[index];
    light.atlasWidth = prowl_4SpotLightAtlasWidth[index];
    return light;
}

// ------------------------------------------------------------------------------
// Shadow Sampling Functions
// ------------------------------------------------------------------------------

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

float SampleSpotLightShadow(SpotLightStruct light, vec3 worldPos, vec3 worldNormal, sampler2D shadowAtlas, vec2 shadowAtlasSize)
{
    float BIAS_SCALE = 0.001;
    float NORMAL_BIAS_SCALE = 0.05;

    // Check if shadows are enabled for this light
    if (light.atlasX < 0.0 || light.shadowStrength <= 0.0) {
        return 0.0;
    }

    // Perform perspective divide to get NDC coordinates
    vec3 worldPosBiased = worldPos + (normalize(worldNormal) * light.shadowNormalBias * NORMAL_BIAS_SCALE);
    vec4 lightSpacePos = light.shadowMatrix * vec4(worldPosBiased, 1.0);
    vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;

    // Transform to [0,1] range
    projCoords = projCoords * 0.5 + 0.5;

    // Early exit if outside shadow map
    if (projCoords.z > 1.0 ||
        projCoords.x < 0.0 || projCoords.x > 1.0 ||
        projCoords.y < 0.0 || projCoords.y > 1.0) {
        return 0.0;
    }

    // Get shadow atlas coordinates
    vec2 atlasCoords;
    atlasCoords.x = light.atlasX + (projCoords.x * light.atlasWidth);
    atlasCoords.y = light.atlasY + (projCoords.y * light.atlasWidth);

    float atlasSize = shadowAtlasSize.x;

    // Calculate shadow map boundaries in normalized atlas coordinates to prevent bleeding
    vec2 texelSize = vec2(1.0) / shadowAtlasSize;
    vec2 shadowMin = vec2(light.atlasX, light.atlasY) / atlasSize + texelSize * 0.5;
    vec2 shadowMax = vec2(light.atlasX + light.atlasWidth, light.atlasY + light.atlasWidth) / atlasSize - texelSize * 0.5;

    atlasCoords /= atlasSize;

    // Get current depth with bias
    float currentDepth = projCoords.z - (light.shadowBias * BIAS_SCALE);

    float shadow = 0.0;

    // Check shadow quality: 0 = Hard, 1 = Soft
    if (light.shadowQuality < 0.5) {
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
    shadow *= light.shadowStrength;

    return shadow;
}

float SamplePointLightShadow(PointLightStruct light, vec3 worldPos, sampler2D shadowAtlas, vec2 shadowAtlasSize)
{
    float BIAS_SCALE = 0.001;
    float NORMAL_BIAS_SCALE = 0.05;

    // Check if shadows are enabled for this light
    if (light.atlasX < 0.0 || light.shadowStrength <= 0.0) {
        return 0.0;
    }

    // Get direction from light to fragment
    vec3 lightToFrag = worldPos - light.position;
    vec3 absLightToFrag = abs(lightToFrag);

    // Determine which cubemap face to use based on the dominant axis
    int faceIndex;
    vec3 uvw = lightToFrag;
    float maxAxis = max(absLightToFrag.x, max(absLightToFrag.y, absLightToFrag.z));

    vec2 uv;
    float depth;

    // Select face and calculate UV coordinates
    if (absLightToFrag.x >= absLightToFrag.y && absLightToFrag.x >= absLightToFrag.z) {
        // X-axis dominant
        if (lightToFrag.x > 0.0) {
            // +X face (index 0, column 0, row 0)
            uv = vec2(lightToFrag.z, -lightToFrag.y) / absLightToFrag.x;
            faceIndex = 0;
        } else {
            // -X face (index 1, column 1, row 0)
            uv = vec2(-lightToFrag.z, -lightToFrag.y) / absLightToFrag.x;
            faceIndex = 1;
        }
    } else if (absLightToFrag.y >= absLightToFrag.x && absLightToFrag.y >= absLightToFrag.z) {
        // Y-axis dominant
        if (lightToFrag.y > 0.0) {
            // +Y face (index 2, column 0, row 1)
            uv = vec2(-lightToFrag.x, lightToFrag.z) / absLightToFrag.y;
            faceIndex = 2;
        } else {
            // -Y face (index 3, column 1, row 1)
            uv = vec2(-lightToFrag.x, -lightToFrag.z) / absLightToFrag.y;
            faceIndex = 3;
        }
    } else {
        // Z-axis dominant
        if (lightToFrag.z > 0.0) {
            // +Z face (index 4, column 0, row 2)
            uv = vec2(-lightToFrag.x, -lightToFrag.y) / absLightToFrag.z;
            faceIndex = 4;
        } else {
            // -Z face (index 5, column 1, row 2)
            uv = vec2(lightToFrag.x, -lightToFrag.y) / absLightToFrag.z;
            faceIndex = 5;
        }
    }

    // Convert UV from [-1, 1] to [0, 1]
    uv = uv * 0.5 + 0.5;

    // Calculate face offset in the 2x3 grid
    int col = faceIndex % 2;
    int row = faceIndex / 2;

    // Calculate atlas coordinates for this face
    vec2 atlasCoords;
    atlasCoords.x = light.atlasX + (col * light.atlasWidth) + (uv.x * light.atlasWidth);
    atlasCoords.y = light.atlasY + (row * light.atlasWidth) + (uv.y * light.atlasWidth);

    float atlasSize = shadowAtlasSize.x;

    // Calculate face boundaries in normalized atlas coordinates to prevent bleeding
    vec2 faceMin = vec2(light.atlasX + (col * light.atlasWidth),
                        light.atlasY + (row * light.atlasWidth)) / atlasSize;
    vec2 faceMax = vec2(light.atlasX + (col * light.atlasWidth) + light.atlasWidth,
                        light.atlasY + (row * light.atlasWidth) + light.atlasWidth) / atlasSize;

    // Add a small inset (0.5 texels) to prevent sampling exactly on boundaries
    vec2 texelSize = vec2(1.0) / shadowAtlasSize;
    faceMin += texelSize * 0.5;
    faceMax -= texelSize * 0.5;

    atlasCoords /= atlasSize;

    // Calculate current depth (distance from light normalized by range)
    float currentDistance = length(lightToFrag);
    float currentDepth = currentDistance / light.range;
    currentDepth -= (light.shadowBias * BIAS_SCALE);

    float shadow = 0.0;

    // Check shadow quality: 0 = Hard, 1 = Soft
    if (light.shadowQuality < 0.5) {
        // Hard shadows - single sample
        float closestDepth = texture(shadowAtlas, atlasCoords).r;
        shadow = currentDepth > closestDepth ? 1.0 : 0.0;
    } else {
        // Soft shadows - Poisson Disk PCF
        float filterRadius = 2.0; // Slightly larger for point lights due to cubemap

        // Random rotation to reduce banding artifacts
        float randomRotation = InterleavedGradientNoise(gl_FragCoord.xy) * 6.283185; // 2*PI
        float s = sin(randomRotation);
        float c = cos(randomRotation);
        mat2 rotationMatrix = mat2(c, -s, s, c);

        // Sample using Poisson disk pattern
        for(int i = 0; i < 16; i++) {
            vec2 offset = rotationMatrix * poissonDisk[i] * texelSize * filterRadius;
            // Clamp sample coordinates to face boundaries to prevent bleeding
            vec2 sampleCoords = clamp(atlasCoords + offset, faceMin, faceMax);
            float pcfDepth = texture(shadowAtlas, sampleCoords).r;
            shadow += currentDepth > pcfDepth ? 1.0 : 0.0;
        }
        shadow /= 16.0;
    }

    // Apply shadow strength
    shadow *= light.shadowStrength;

    return shadow;
}

// ------------------------------------------------------------------------------
// Light Calculation Functions
// ------------------------------------------------------------------------------

vec3 CalculateDirectionalLight(SunLightStruct sun, vec3 worldPos, vec3 worldNormal, vec3 cameraPos, vec3 albedo, float metallic, float roughness, float ao, sampler2D shadowAtlas, vec2 shadowAtlasSize)
{
    // Constants
    vec3 lightDir = normalize(sun.direction); // Direction from surface to light
    vec3 viewDir = normalize(-(worldPos - cameraPos));
    vec3 halfDir = normalize(lightDir + viewDir);

    // Calculate base reflectivity for metals vs non-metals
    vec3 F0 = vec3(0.04); // Default reflectivity for non-metals at normal incidence
    F0 = mix(F0, albedo, metallic); // For metals, base reflectivity is tinted by albedo

    // Calculate light radiance
    vec3 radiance = sun.color * sun.intensity;

    // Cook-Torrance BRDF
    float NDF = DistributionGGX(worldNormal, halfDir, roughness);
    float G = GeometrySmith(worldNormal, viewDir, lightDir, roughness);
    vec3 F = FresnelSchlick(max(dot(halfDir, viewDir), 0.0), F0);

    // Calculate specular and diffuse components
    vec3 kS = F; // Energy of light that gets reflected
    vec3 kD = vec3(1.0) - kS; // Energy of light that gets refracted
    kD *= 1.0 - metallic; // Metals don't have diffuse lighting

    // Put it all together
    float NdotL = max(dot(worldNormal, lightDir), 0.0);

    // Specular term
    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(worldNormal, viewDir), 0.0) * NdotL + 0.0001;
    vec3 specular = numerator / denominator;

    // Calculate shadow factor
    float shadow = SampleDirectionalShadow(sun, worldPos, worldNormal, shadowAtlas, shadowAtlasSize);
    float shadowFactor = 1.0 - shadow;

    // Final lighting contribution with shadow
    vec3 diffuse = kD * albedo / PI;
    return (diffuse + specular) * radiance * NdotL * shadowFactor * ao;
}

vec3 CalculateSpotLight(SpotLightStruct light, vec3 worldPos, vec3 worldNormal, vec3 cameraPos, vec3 albedo, float metallic, float roughness, float ao, sampler2D shadowAtlas, vec2 shadowAtlasSize)
{
    // Calculate direction from surface to light
    vec3 lightDir = normalize(light.position - worldPos);
    vec3 viewDir = normalize(-(worldPos - cameraPos));
    vec3 halfDir = normalize(lightDir + viewDir);

    // Calculate distance attenuation
    float distance = length(light.position - worldPos);
    if (distance > light.range) {
        return vec3(0.0);
    }

    // Physical distance attenuation (inverse square law with smoothing)
    float attenuation = clamp(1.0 - pow(distance / light.range, 4.0), 0.0, 1.0);
    attenuation = (attenuation * attenuation) / (distance * distance + 1.0);

    // Calculate spot light cone attenuation
    float theta = dot(lightDir, normalize(-light.direction));
    float epsilon = light.innerAngle - light.outerAngle;
    float spotAttenuation = clamp((theta - light.outerAngle) / epsilon, 0.0, 1.0);

    // Early exit if outside cone
    if (spotAttenuation <= 0.0) {
        return vec3(0.0);
    }

    // Calculate base reflectivity
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic);

    // Calculate light radiance with attenuation
    vec3 radiance = light.color * light.intensity * attenuation * spotAttenuation;

    // Cook-Torrance BRDF
    float NDF = DistributionGGX(worldNormal, halfDir, roughness);
    float G = GeometrySmith(worldNormal, viewDir, lightDir, roughness);
    vec3 F = FresnelSchlick(max(dot(halfDir, viewDir), 0.0), F0);

    // Calculate specular and diffuse components
    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - metallic;

    // Put it all together
    float NdotL = max(dot(worldNormal, lightDir), 0.0);

    // Specular term
    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(worldNormal, viewDir), 0.0) * NdotL + 0.0001;
    vec3 specular = numerator / denominator;

    // Calculate shadow factor
    float shadow = SampleSpotLightShadow(light, worldPos, worldNormal, shadowAtlas, shadowAtlasSize);
    float shadowFactor = 1.0 - shadow;

    // Final lighting contribution with shadow
    vec3 diffuse = kD * albedo / PI;
    return (diffuse + specular) * radiance * NdotL * shadowFactor * ao;
}

vec3 CalculatePointLight(PointLightStruct light, vec3 worldPos, vec3 worldNormal, vec3 cameraPos, vec3 albedo, float metallic, float roughness, float ao, sampler2D shadowAtlas, vec2 shadowAtlasSize)
{
    // Calculate direction from surface to light
    vec3 lightDir = normalize(light.position - worldPos);
    vec3 viewDir = normalize(-(worldPos - cameraPos));
    vec3 halfDir = normalize(lightDir + viewDir);

    // Calculate distance attenuation
    float distance = length(light.position - worldPos);
    if (distance > light.range) {
        return vec3(0.0);
    }

    // Physical distance attenuation (inverse square law with smoothing)
    float attenuation = clamp(1.0 - pow(distance / light.range, 4.0), 0.0, 1.0);
    attenuation = (attenuation * attenuation) / (distance * distance + 1.0);

    // Calculate base reflectivity
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic);

    // Calculate light radiance with attenuation
    vec3 radiance = light.color * light.intensity * attenuation;

    // Cook-Torrance BRDF
    float NDF = DistributionGGX(worldNormal, halfDir, roughness);
    float G = GeometrySmith(worldNormal, viewDir, lightDir, roughness);
    vec3 F = FresnelSchlick(max(dot(halfDir, viewDir), 0.0), F0);

    // Calculate specular and diffuse components
    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - metallic;

    // Put it all together
    float NdotL = max(dot(worldNormal, lightDir), 0.0);

    // Specular term
    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(worldNormal, viewDir), 0.0) * NdotL + 0.0001;
    vec3 specular = numerator / denominator;

    // Calculate shadow factor
    float shadow = SamplePointLightShadow(light, worldPos, shadowAtlas, shadowAtlasSize);
    float shadowFactor = 1.0 - shadow;

    // Final lighting contribution with shadow
    vec3 diffuse = kD * albedo / PI;
    return (diffuse + specular) * radiance * NdotL * shadowFactor * ao;
}

#endif
