out vec3 vRayDirection;

uniform mat4 uInverseViewProjection;
uniform vec3 uCameraPos;

void main()
{
    float x = -1.0 + float((gl_VertexID & 1) << 2);
    float y = -1.0 + float((gl_VertexID & 2) << 1);

    vec2 clipPos = vec2(x, y);
    gl_Position = vec4(clipPos, 1.0, 1.0);

    vec4 worldFar = uInverseViewProjection * vec4(clipPos, 1.0, 1.0);
    vRayDirection = (worldFar.xyz / worldFar.w) - uCameraPos;
}
