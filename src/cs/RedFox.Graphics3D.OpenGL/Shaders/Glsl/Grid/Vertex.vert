#version 300 es
precision highp float;
precision highp int;

uniform mat4 View;
uniform mat4 Projection;
uniform mat4 InverseView;
uniform mat4 InverseProjection;

const vec2 GridPositions[4] = vec2[4](
    vec2(-1.0, -1.0),
    vec2(1.0, -1.0),
    vec2(1.0, 1.0),
    vec2(-1.0, 1.0)
);

const int GridIndices[6] = int[6](0, 1, 2, 2, 3, 0);

out vec3 NearPoint;
out vec3 FarPoint;

vec3 UnprojectPoint(vec2 position, float depth)
{
    vec4 unprojected = InverseView * InverseProjection * vec4(position, depth, 1.0);
    return unprojected.xyz / unprojected.w;
}

void main()
{
    int index = GridIndices[gl_VertexID];
    vec2 position = GridPositions[index];

    NearPoint = UnprojectPoint(position, -1.0);
    FarPoint = UnprojectPoint(position, 1.0);
    gl_Position = vec4(position, 0.0, 1.0);
}