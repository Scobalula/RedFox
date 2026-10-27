#version 300 es
precision highp float;

const vec4 WireframeColor = vec4(0.78, 0.84, 0.94, 1.0);

out vec4 FragColor;

void main()
{
    FragColor = WireframeColor;
}
