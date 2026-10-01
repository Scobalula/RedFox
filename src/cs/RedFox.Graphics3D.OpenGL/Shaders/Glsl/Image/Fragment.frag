#version 300 es
precision highp float;
precision highp int;

uniform sampler2D ImageTexture;
uniform vec2 ImageSize;
uniform vec2 ViewportSize;
uniform vec2 Offset;
uniform float Zoom;
uniform vec4 ChannelMask;
uniform float Exposure;
uniform int ReconstructNormal;
uniform int Tile;
uniform int FlipY;
uniform int IsSingleChannel;
uniform int IsSigned;
uniform int EncodeSrgb;

out vec4 FragColor;

vec3 LinearToSrgb(vec3 color)
{
    color = clamp(color, 0.0, 1.0);
    return mix(color * 12.92, 1.055 * pow(color, vec3(1.0 / 2.4)) - 0.055, step(vec3(0.0031308), color));
}

vec4 SampleImage(vec2 texel)
{
    if (Zoom >= 1.0)
    {
        ivec2 coordinate = ivec2(texel);
        coordinate.y = FlipY != 0 ? int(ImageSize.y) - 1 - coordinate.y : coordinate.y;
        return texelFetch(ImageTexture, coordinate, 0);
    }

    vec2 uv = texel / ImageSize;
    uv.y = FlipY != 0 ? 1.0 - uv.y : uv.y;
    return texture(ImageTexture, uv);
}

void main()
{
    vec2 screen = vec2(gl_FragCoord.x, ViewportSize.y - gl_FragCoord.y);
    vec2 extent = Tile != 0 ? ImageSize * 3.0 : ImageSize;
    vec2 position = (screen - (ViewportSize * 0.5) - Offset) / Zoom + (extent * 0.5);
    if (any(lessThan(position, vec2(0.0))) || any(greaterThanEqual(position, extent)))
    {
        discard;
    }

    vec4 color = SampleImage(mod(position, ImageSize));
    if (ReconstructNormal != 0)
    {
        vec2 normal = IsSigned != 0 ? color.rg : color.rg * 2.0 - 1.0;
        color = vec4(vec3(normal, sqrt(max(0.0, 1.0 - dot(normal, normal)))) * 0.5 + 0.5, 1.0);
    }
    else if (IsSigned != 0)
    {
        color.rgb = color.rgb * 0.5 + 0.5;
    }

    color.rgb *= exp2(Exposure);
    if (EncodeSrgb != 0)
    {
        color.rgb = LinearToSrgb(color.rgb);
    }

    if (IsSingleChannel != 0)
    {
        color = vec4(color.rrr, 1.0);
    }

    float colorChannelCount = ChannelMask.r + ChannelMask.g + ChannelMask.b;
    if (colorChannelCount == 0.0)
    {
        color = vec4(ChannelMask.a > 0.5 ? color.aaa : vec3(0.0), 1.0);
    }
    else if (colorChannelCount == 1.0 && ChannelMask.a < 0.5)
    {
        color = vec4(vec3(dot(color.rgb, ChannelMask.rgb)), 1.0);
    }
    else
    {
        color = vec4(color.rgb * ChannelMask.rgb, ChannelMask.a > 0.5 ? clamp(color.a, 0.0, 1.0) : 1.0);
    }

    vec2 cell = floor(screen / 8.0);
    vec3 checker = vec3(mod(cell.x + cell.y, 2.0) < 1.0 ? 0.22 : 0.16);
    FragColor = vec4(mix(checker, color.rgb, color.a), 1.0);
}
