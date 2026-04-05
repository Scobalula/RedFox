layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec4 aColor;

uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uCameraPos;

out vec4 vColor;

void main()
{
    vColor = aColor;
    vec3 cameraRelativePos = aPosition - uCameraPos;
    mat4 viewRotation = mat4(mat3(uView));
    gl_Position = uProjection * viewRotation * vec4(cameraRelativePos, 1.0);
}
