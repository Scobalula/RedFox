#version 300 es
precision highp float;
precision highp int;

in vec3 WorldPosition;
in vec4 ColorValue;

uniform vec3 CameraPosition;
uniform float FadeStartDistance;
uniform float FadeEndDistance;

out vec4 FragColor;

void main()
{
    vec4 color = ColorValue;

    if (FadeEndDistance > FadeStartDistance)
    {
        float dist = distance(CameraPosition, WorldPosition);
        float t = clamp((dist - FadeStartDistance) / (FadeEndDistance - FadeStartDistance), 0.0, 1.0);
        color.a *= 1.0 - t;
    }

    if (color.a <= 0.01)
    {
        discard;
    }

    FragColor = color;
}