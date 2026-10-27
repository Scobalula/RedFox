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

#include "ColorSpace.glsl"

void main()
{
    vec4 viewPosition = InverseProjection * vec4(ClipPosition, 1.0, 1.0);
    vec3 viewDirection = normalize(viewPosition.xyz / max(abs(viewPosition.w), 0.00001));
    vec3 worldDirection = normalize(mat3(InverseView) * viewDirection);

    // Explicit LOD avoids the derivative seam where the longitude wraps, and doubles as the background blur.
    vec2 uv = vec2(atan(worldDirection.z, worldDirection.x) / (2.0 * PI) + 0.5, acos(clamp(worldDirection.y, -1.0, 1.0)) / PI);
    vec3 color = textureLod(SkyboxTexture, uv, SkyboxLod).rgb * SkyboxTint.rgb * SkyboxIntensity;

    FragColor = vec4(EncodeDisplayColor(color), SkyboxTint.a);
}
