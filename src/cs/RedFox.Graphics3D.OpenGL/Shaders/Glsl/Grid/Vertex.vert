#version 330 core

uniform mat4 View;
uniform mat4 Projection;
uniform vec3 CameraPosition;
uniform float GridSize;

const vec3 GridPositions[4] = vec3[4](
    vec3(-1.0, 0.0, -1.0),
    vec3(1.0, 0.0, -1.0),
    vec3(1.0, 0.0, 1.0),
    vec3(-1.0, 0.0, 1.0)
);

const int GridIndices[6] = int[6](0, 1, 2, 2, 3, 0);

out vec2 GridUv;
out vec2 CameraGridPosition;
out vec3 WorldPosition;

void main()
{
    int index = GridIndices[gl_VertexID];
    vec3 position = GridPositions[index] * GridSize;
    position.x += CameraPosition.x;
    position.z += CameraPosition.z;

    WorldPosition = position;
    GridUv = position.xz;
    CameraGridPosition = CameraPosition.xz;
    gl_Position = Projection * View * vec4(position, 1.0);
}