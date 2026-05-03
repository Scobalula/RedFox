#version 300 es
precision highp float;
precision highp int;
precision highp samplerCube;

in vec2 ClipPosition;

uniform mat4 InverseView;
uniform mat4 InverseProjection;
uniform samplerCube SkyboxTexture;
uniform vec4 SkyboxTint;
uniform float SkyboxIntensity;

out vec4 FragColor;

void main()
{
    vec4 viewPosition = InverseProjection * vec4(ClipPosition, 1.0, 1.0);
    vec3 viewDirection = normalize(viewPosition.xyz / max(abs(viewPosition.w), 0.00001));
    vec3 worldDirection = normalize(mat3(InverseView) * viewDirection);
    vec4 color = texture(SkyboxTexture, worldDirection);
    FragColor = vec4(color.rgb * SkyboxTint.rgb * SkyboxIntensity, color.a * SkyboxTint.a);
}