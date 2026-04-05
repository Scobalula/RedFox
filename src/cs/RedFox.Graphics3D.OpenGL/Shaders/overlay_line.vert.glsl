layout (location = 0) in vec4 aClipPosition;
layout (location = 1) in vec4 aColor;

out vec4 vColor;

void main()
{
    vColor = aColor;
    gl_Position = aClipPosition;
}
