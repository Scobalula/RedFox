out vec2 vTexCoord;

uniform vec2 uDirection;
uniform vec2 uResolution;
uniform float uBlurRadius;

void main()
{
    // Generate fullscreen quad
    float x = -1.0 + float((gl_VertexID & 1) << 2);
    float y = -1.0 + float((gl_VertexID & 2) << 1);
    
    gl_Position = vec4(x, y, 0.0, 1.0);
    
    // Calculate texture coordinate
    vTexCoord = vec2(x * 0.5 + 0.5, y * 0.5 + 0.5);
}
