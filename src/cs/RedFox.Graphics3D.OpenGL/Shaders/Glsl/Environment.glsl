#define MAX_LIGHTS 4
#define PI 3.14159265

uniform int EnvironmentLightCount;
uniform vec4 EnvironmentLightDirections[MAX_LIGHTS];
uniform vec3 EnvironmentLightPowers[MAX_LIGHTS];
uniform int HasEnvironmentMap;
uniform sampler2D EnvironmentMap;
uniform float EnvironmentMipLevels;
uniform float EnvironmentIntensity;

const vec3 StudioKeyDirection = vec3(0.48, 0.78, 0.40);

vec3 StudioEnvironment(vec3 direction, float roughness)
{
    vec3 ground = vec3(0.035, 0.035, 0.038);
    vec3 horizon = vec3(0.22, 0.22, 0.23);
    vec3 sky = vec3(0.50, 0.52, 0.56);
    float up = direction.y;
    vec3 color = up >= 0.0 ? mix(horizon, sky, pow(up, 0.55)) : mix(horizon, ground, pow(-up, 0.35));
    float spread = mix(0.02, 0.9, roughness);
    float box = smoothstep(1.0 - spread, 1.0, dot(direction, normalize(StudioKeyDirection)));

    return color + vec3(1.6, 1.55, 1.45) * box * mix(1.0, 0.25, roughness);
}

vec3 SampleEnvironment(vec3 direction, float roughness)
{
    if (HasEnvironmentMap == 0)
    {
        return StudioEnvironment(direction, roughness);
    }

    vec2 uv = vec2(atan(direction.z, direction.x) / (2.0 * PI) + 0.5, acos(clamp(direction.y, -1.0, 1.0)) / PI);
    float lod = roughness * max(EnvironmentMipLevels - 1.0, 0.0);
    return textureLod(EnvironmentMap, uv, lod).rgb * EnvironmentIntensity;
}
