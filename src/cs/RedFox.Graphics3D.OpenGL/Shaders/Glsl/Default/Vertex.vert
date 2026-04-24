#version 330 core

layout (location = 0) in vec3 Positions;
layout (location = 1) in vec3 Normals;

uniform mat4 Model;
uniform mat4 SceneAxis;
uniform mat4 View;
uniform mat4 Projection;

out vec3 WorldPosition;
out vec3 WorldNormal;

void main()
{
    mat4 worldMatrix = SceneAxis * Model;
    vec4 worldPosition = worldMatrix * vec4(Positions, 1.0);
    WorldPosition = worldPosition.xyz;
    mat3 normalMatrix = mat3(transpose(inverse(worldMatrix)));
    WorldNormal = normalize(normalMatrix * Normals);
    gl_Position = Projection * View * worldPosition;
}