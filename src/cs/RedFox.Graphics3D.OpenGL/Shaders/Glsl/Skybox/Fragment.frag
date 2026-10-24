#version 300 es
precision highp float;
precision highp int;
precision highp sampler2D;

#define PI 3.14159265

in vec2 ClipPosition;

uniform mat4 InverseView;
uniform mat4 InverseProjection;
uniform sampler2D SkyboxTexture;
uniform vec4 SkyboxTint;
uniform float SkyboxIntensity;
uniform float SkyboxLod;

out vec4 FragColor;

// Gran Turismo filmic curve, so HDR panoramas resolve to the same range as lit materials.
vec3 FilmicTonemap(vec3 x)
{
    const float maxNit = 1.0;
    const float contrast = 1.0;
    const float linearBegin = 0.22;
    const float linearLength = 0.4;
    const float toe = 1.33;

    float l0 = ((maxNit - linearBegin) * linearLength) / contrast;
    float s0 = linearBegin + l0;
    float s1 = linearBegin + contrast * l0;
    float c2 = (contrast * maxNit) / (maxNit - s1);
    float cp = -c2 / maxNit;

    vec3 w0 = 1.0 - smoothstep(0.0, linearBegin, x);
    vec3 w2 = step(linearBegin + l0, x);
    vec3 w1 = 1.0 - w0 - w2;

    vec3 shoulder = maxNit - (maxNit - s1) * exp(cp * (x - s0));
    vec3 linearPart = linearBegin + contrast * (x - linearBegin);
    vec3 toePart = linearBegin * pow(x / linearBegin, vec3(toe));

    return toePart * w0 + linearPart * w1 + shoulder * w2;
}

vec3 LinearToSrgb(vec3 color)
{
    return mix(color * 12.92, 1.055 * pow(color, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, color));
}

void main()
{
    vec4 viewPosition = InverseProjection * vec4(ClipPosition, 1.0, 1.0);
    vec3 viewDirection = normalize(viewPosition.xyz / max(abs(viewPosition.w), 0.00001));
    vec3 worldDirection = normalize(mat3(InverseView) * viewDirection);

    // Explicit LOD avoids the derivative seam where the longitude wraps, and doubles as the background blur.
    vec2 uv = vec2(atan(worldDirection.z, worldDirection.x) / (2.0 * PI) + 0.5, acos(clamp(worldDirection.y, -1.0, 1.0)) / PI);
    vec3 color = textureLod(SkyboxTexture, uv, SkyboxLod).rgb * SkyboxTint.rgb * SkyboxIntensity;

    FragColor = vec4(LinearToSrgb(clamp(FilmicTonemap(max(color, 0.0)), 0.0, 1.0)), SkyboxTint.a);
}
