/// @file cubemap.frag.glsl
/// @brief Dummy fragment shader — cubemap.vert.glsl is always paired with
///        another fragment shader (equirect_to_cubemap, irradiance, or prefilter).
///        This file exists only to satisfy ShaderSource.LoadProgram.
out vec4 FragColor;
void main() { FragColor = vec4(1.0); }
