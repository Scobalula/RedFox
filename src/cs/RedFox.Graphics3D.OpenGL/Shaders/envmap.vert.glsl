/// @file envmap.vert.glsl
/// @brief Vertex shader for skybox cubemap rendering.
/// @details Renders a unit cube around the camera using only the view rotation.

layout (location = 0) in vec3 aPosition;

/// @brief Output cubemap sample direction passed to fragment shader.
out vec3 vDirection;

uniform mat4 uProjection;
uniform mat4 uView;

void main()
{
    vDirection = aPosition;
    mat4 viewRotation = mat4(mat3(uView));
    gl_Position = uProjection * viewRotation * vec4(aPosition, 1.0);
}
