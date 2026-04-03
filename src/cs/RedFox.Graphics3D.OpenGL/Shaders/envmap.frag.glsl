in vec3 vRayDirection;

uniform sampler2D uEnvironmentMap;
uniform float uExposure;

out vec4 FragColor;

vec2 equirectDirectionToUv(vec3 dir)
{
    float u = 0.5 + atan(dir.z, dir.x) / 6.28318530;
    float v = acos(clamp(dir.y, -1.0, 1.0)) / 3.14159265;
    return vec2(u, v);
}

vec3 tonemapReinhard(vec3 color)
{
    return color / (color + vec3(1.0));
}

void main()
{
    vec3 dir = normalize(vRayDirection);
    vec2 uv = equirectDirectionToUv(dir);
    vec3 envColor = texture(uEnvironmentMap, uv).rgb;
    FragColor = vec4(tonemapReinhard(envColor * uExposure), 1.0);
}
