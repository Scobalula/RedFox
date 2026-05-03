#version 300 es
precision highp float;
precision highp int;

out vec2 ClipPosition;

void main()
{
    vec2 positions[3] = vec2[](
        vec2(-1.0, -1.0),
        vec2(-1.0, 3.0),
        vec2(3.0, -1.0));

    ClipPosition = positions[gl_VertexID];
    gl_Position = vec4(ClipPosition, 1.0, 1.0);
}