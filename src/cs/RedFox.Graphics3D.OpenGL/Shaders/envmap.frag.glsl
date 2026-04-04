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

vec3 linearToSRGB(vec3 color)
{
    return pow(color, vec3(1.0 / 2.2));
}

void main()
{
    vec3 dir = normalize(vRayDirection);
    float lod = uBlurEnabled ? uBlurMipLevel : 0.0;
    vec3 envColor = textureLod(uSkyCubemap, dir, lod).rgb;
    vec3 mapped = tonemapReinhard(envColor * uExposure);
    FragColor = vec4(linearToSRGB(mapped), 1.0);
}
