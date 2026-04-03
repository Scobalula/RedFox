/// @file prefilter.frag.glsl
/// @brief Fragment shader for generating the prefiltered specular cubemap.
/// @details Uses GGX importance sampling for a given roughness level.
///          Each mip level is rendered with a different roughness.

in vec3 vWorldPos;

uniform samplerCube uEnvironmentMap;
uniform float uRoughness;
uniform float uEnvMapResolution;

out vec4 FragColor;

const uint SAMPLE_COUNT = 1024u;

/// Radical inverse for Hammersley sequence.
float RadicalInverseVdC(uint bits)
{
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
    bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
    bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
    return float(bits) * 2.3283064365386963e-10; // / 2^32
}

vec2 Hammersley(uint i, uint N)
{
    return vec2(float(i) / float(N), RadicalInverseVdC(i));
}

vec3 ImportanceSampleGGX(vec2 Xi, float roughness)
{
    float a = roughness * roughness;
    float phi = 2.0 * 3.14159265358979323846 * Xi.x;
    float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a * a - 1.0) * Xi.y));
    float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
    return vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
}

void main()
{
    vec3 N = normalize(vWorldPos);
    vec3 R = N;  // View direction = N for prefiltering (surface at normal incidence)
    vec3 V = R;

    vec3 tangent, bitangent;
    vec3 up = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    tangent = normalize(cross(up, N));
    bitangent = cross(N, tangent);

    mat3 TBN = mat3(tangent, bitangent, N);

    float totalWeight = 0.0;
    vec3 prefilteredColor = vec3(0.0);

    for (uint i = 0u; i < SAMPLE_COUNT; ++i)
    {
        vec2 Xi = Hammersley(i, SAMPLE_COUNT);
        vec3 H = ImportanceSampleGGX(Xi, uRoughness);
        vec3 L = normalize(2.0 * dot(V, H) * H - V);

        float NdotL = max(dot(N, L), 0.0);
        if (NdotL > 0.0)
        {
            prefilteredColor += texture(uEnvironmentMap, L).rgb * NdotL;
            totalWeight += NdotL;
        }
    }

    prefilteredColor = prefilteredColor / max(totalWeight, 0.0001);

    FragColor = vec4(prefilteredColor, 1.0);
}
