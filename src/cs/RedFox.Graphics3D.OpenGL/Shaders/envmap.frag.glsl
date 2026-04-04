in vec3 vRayDirection;

uniform samplerCube uSkyCubemap;
uniform float uExposure;
uniform bool uBlurEnabled;
uniform float uBlurMipLevel;

out vec4 FragColor;

vec3 tonemapReinhard(vec3 color)
{
    return color / (color + vec3(1.0));
}

void main()
{
    vec3 dir = normalize(vRayDirection);
    float lod = (uBlurEnabled ? uBlurMipLevel : 0.0);
    vec3 envColor = textureLod(uSkyCubemap, dir, lod).rgb;
    FragColor = vec4(tonemapReinhard(envColor * uExposure), 1.0);
}
