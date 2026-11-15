#version 300 es
precision highp float;

uniform vec4 WireframeColor;

out vec4 FragColor;

void main()
{
    FragColor = vec4(WireframeColor.rgb * WireframeColor.a, WireframeColor.a);
}
