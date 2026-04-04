/// @file cubemap.vert.glsl
/// @brief Vertex shader for rendering to cubemap faces.
/// @details Renders a unit cube centered at origin. The position serves as
///          the sampling direction for the fragment shader.

layout (location = 0) in vec3 aPos;

uniform mat4 uProjection;
uniform mat4 uView;

out vec3 vWorldPos;

void main()
{
    vWorldPos = aPos;
    // Use only rotation from view matrix to sample the correct cubemap direction.
    // Ignore translation - we're rendering a unit cube around the camera.
    gl_Position = (uProjection * mat4(mat3(uView))) * vec4(aPos, 1.0);
}
