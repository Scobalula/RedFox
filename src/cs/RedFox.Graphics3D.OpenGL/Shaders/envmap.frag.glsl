in vec3 vDirection;

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
    vec3 dir = normalize(vDirection);
    vec3 envColor = uBlurEnabled
        ? textureLod(uSkyCubemap, dir, uBlurMipLevel).rgb
        : textureLod(uSkyCubemap, dir, 0.0).rgb;
    vec3 mapped = tonemapReinhard(envColor * uExposure);
    FragColor = vec4(linearToSRGB(mapped), 1.0);
}
