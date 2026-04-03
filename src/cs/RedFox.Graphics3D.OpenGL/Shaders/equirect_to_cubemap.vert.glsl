/// @file equirect_to_cubemap.vert.glsl
/// @brief Vertex shader for equirectangular → cubemap conversion.
/// @details Renders a unit cube; fragment shader samples the equirectangular map.
layout (location = 0) in vec3 aPos;

uniform mat4 uProjection;
uniform mat4 uView;

out vec3 vWorldPos;

void main()
{
    vWorldPos = aPos;
    gl_Position = (uProjection * mat4(mat3(uView))) * vec4(aPos, 1.0);
}
