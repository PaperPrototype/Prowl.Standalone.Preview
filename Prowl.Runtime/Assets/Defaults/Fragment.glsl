#ifndef SHADER_FRAGMENT
#define SHADER_FRAGMENT

#include "ShaderVariables"

#define PROWL_PI            3.14159265359
#define PROWL_TWO_PI        6.28318530718
#define PROWL_FOUR_PI       12.56637061436
#define PROWL_INV_PI        0.31830988618
#define PROWL_INV_TWO_PI    0.15915494309
#define PROWL_INV_FOUR_PI   0.07957747155
#define PROWL_HALF_PI       1.57079632679
#define PROWL_INV_HALF_PI   0.636619772367

// Colors ===========================================================
// http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
vec3 linearToGammaSpace(vec3 lin)
{
    return max(1.055 * pow(max(lin, vec3(0.0)), vec3(0.416666667)) - 0.055, 0.0);
}

// http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
vec3 gammaToLinearSpace(vec3 gamma)
{
    return gamma * (gamma * (gamma * 0.305306011 + 0.682171111) + 0.012522878);
}
// ============================================================================

float linearizeDepth(float depth, float near, float far) 
{
    float z = depth * 2.0 - 1.0; // Back to NDC [-1,1] range
    return (2.0 * near * far) / (far + near - z * (far - near));
}

float linearizeDepthFromProjection(float depth) {
    return linearizeDepth(depth, _ProjectionParams.y, _ProjectionParams.z);
}

float getFovFromProjectionMatrix(mat4 proj)
{
    // proj[1][1] is M11, the Y scale
    // FOV = 2 * atan(1/M11)
    return 2.0 * atan(1.0 / proj[1][1]);
}

// ----------------------------------------------------------------------------

vec3 projectAndDivide(mat4 matrix, vec3 pos) {
    vec4 p = matrix * vec4(pos, 1.0);
    return p.xyz / p.w;
}

vec3 getScreenPos(vec2 tc, sampler2D depthSampler) {
	return vec3(tc, texture(depthSampler, tc).x);
}
vec3 getScreenPos(vec2 tc, float depth) {
	return vec3(tc, depth);
}

vec3 getScreenFromViewPos(vec3 viewPos) {
	vec3 p = projectAndDivide(PROWL_MATRIX_P, viewPos);
	return p * 0.5 + 0.5;
}

vec3 getNDCFromScreenPos(vec3 screenPos) {
	return screenPos * 2.0 - 1.0;
}

vec3 getViewFromScreenPos(vec3 screenPos) {
	return projectAndDivide(inverse(PROWL_MATRIX_P), getNDCFromScreenPos(screenPos));
}

vec3 getViewPos(vec2 tc, sampler2D depthSampler) {
	return getViewFromScreenPos(getScreenPos(tc, depthSampler));
}
vec3 getViewPos(vec2 tc, float depth) {
	return getViewFromScreenPos(getScreenPos(tc, depth));
}

// ----------------------------------------------------------------------------


#endif
