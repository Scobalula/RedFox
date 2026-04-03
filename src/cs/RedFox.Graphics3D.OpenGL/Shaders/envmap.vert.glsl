/// @file envmap.vert.glsl
/// @brief Vertex shader for equirectangular environment map rendering.
/// @details Generates a fullscreen triangle and computes ray directions for each fragment.
/// The ray directions are calculated from the camera position through the far plane
/// of the view frustum, using the inverse view-projection matrix.

/// @brief Output ray direction passed to fragment shader.
out vec3 vRayDirection;

/// @brief Inverse view-projection matrix for converting clip space to world space.
uniform mat4 uInverseViewProjection;

/// @brief Camera position in world space.
uniform vec3 uCameraPos;

void main()
{
    // Generate fullscreen triangle vertices procedurally using gl_VertexID
    float x = -1.0 + float((gl_VertexID & 1) << 2);
    float y = -1.0 + float((gl_VertexID & 2) << 1);

    vec2 clipPos = vec2(x, y);
    gl_Position = vec4(clipPos, 1.0, 1.0);

    // Calculate ray direction from camera through far plane
    vec4 worldFar = uInverseViewProjection * vec4(clipPos, 1.0, 1.0);
    vRayDirection = (worldFar.xyz / worldFar.w) - uCameraPos;
}
