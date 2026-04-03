/// @file brdf_lut.vert.glsl
/// @brief Vertex shader for BRDF LUT generation — fullscreen triangle.

out vec2 vTexCoord;

void main()
{
    vec2 positions[3] = vec2[](
        vec2(-1.0, -1.0),
        vec2( 3.0, -1.0),
        vec2(-1.0,  3.0)
    );

    vec2 pos = positions[gl_VertexID];
    gl_Position = vec4(pos, 0.0, 1.0);
    vTexCoord = pos * 0.5 + 0.5;
}
