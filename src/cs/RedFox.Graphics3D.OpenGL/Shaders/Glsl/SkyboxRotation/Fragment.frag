#version 300 es
precision highp float;
precision highp int;
precision highp sampler2D;

uniform sampler2D SourceTexture;
uniform vec2 TargetSize;
uniform float SourceLod;
uniform float RotationOffset;

out vec4 FragColor;

void main()
{
    vec2 uv = gl_FragCoord.xy / TargetSize;
    FragColor = textureLod(SourceTexture, vec2(uv.x + RotationOffset, uv.y), SourceLod);
}
