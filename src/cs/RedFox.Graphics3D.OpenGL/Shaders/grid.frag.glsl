in vec4 vColor;
in float vClipW;

uniform float uFarPlane;

out vec4 FragColor;

void main()
{
    FragColor = vColor;
    gl_FragDepth = log2(max(1e-6, vClipW + 1.0)) / log2(uFarPlane + 1.0);
}
