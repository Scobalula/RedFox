/// @file equirect_to_cubemap.frag.glsl
/// @brief Fragment shader for converting equirectangular environment map to cubemap.
/// @details For each fragment position (sampling direction), converts to equirectangular
///          UV and samples the source 2D equirectangular texture.

in vec3 vWorldPos;

uniform sampler2D uEquirectangularMap;

out vec4 FragColor;

vec2 equirectDirectionToUV(vec3 dir)
{
    float u = atan(dir.z, dir.x);
    float v = atan(dir.y, length(vec2(dir.x, dir.z)));
    u = u / (2.0 * 3.14159265358979323846) + 0.5;
    v = v / 3.14159265358979323846 + 0.5;
    return vec2(u, v);
}

void main()
{
    vec2 uv = equirectDirectionToUV(normalize(vWorldPos));
    vec3 color = texture(uEquirectangularMap, uv).rgb;
    FragColor = vec4(color, 1.0);
}
