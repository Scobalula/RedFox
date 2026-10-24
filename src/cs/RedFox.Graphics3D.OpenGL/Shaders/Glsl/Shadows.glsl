
#define MAX_SHADOWS 4

uniform int ShadowPass;
uniform int ShadowCount;
uniform highp sampler2DArrayShadow ShadowMap;
uniform float ShadowMapSize;
uniform mat4 ShadowMatrices[MAX_SHADOWS];
uniform vec2 ShadowFilterRadii[MAX_SHADOWS];
uniform int ShadowFilterTaps;
uniform float ShadowStrength;

const float ShadowMaximumReceiverSlope = 4.0;

float ShadowVisibility(int layer, vec3 worldPosition, vec3 geometryNormal)
{
    mat4 shadowMatrix = ShadowMatrices[layer];
    float texel = 1.0 / ShadowMapSize;

    vec3 rowU = vec3(shadowMatrix[0][0], shadowMatrix[1][0], shadowMatrix[2][0]);
    vec3 rowV = vec3(shadowMatrix[0][1], shadowMatrix[1][1], shadowMatrix[2][1]);
    vec3 rowDepth = vec3(shadowMatrix[0][2], shadowMatrix[1][2], shadowMatrix[2][2]);
    float nDotL = dot(geometryNormal, -normalize(rowDepth));
    float grazing = sqrt(clamp(1.0 - nDotL * nDotL, 0.0, 1.0));
    float texelWorldSize = texel / length(rowU);

    vec3 coord = (shadowMatrix * vec4(worldPosition + geometryNormal * texelWorldSize * (1.0 + 2.0 * grazing), 1.0)).xyz;

    if (coord.x <= 0.0 || coord.x >= 1.0 || coord.y <= 0.0 || coord.y >= 1.0)
    {
        return 1.0;
    }

    vec3 axisU = rowU / dot(rowU, rowU);
    vec3 axisV = rowV / dot(rowV, rowV);
    vec3 axisDepth = rowDepth / dot(rowDepth, rowDepth);
    float normalAlongDepth = dot(geometryNormal, axisDepth);
    vec2 normalAcross = vec2(dot(geometryNormal, axisU), dot(geometryNormal, axisV));
    float edgeOn = 0.125 * length(normalAcross);
    vec2 receiverSlope = clamp(-normalAcross * normalAlongDepth / (normalAlongDepth * normalAlongDepth + edgeOn * edgeOn + 1e-12), vec2(-ShadowMaximumReceiverSlope), vec2(ShadowMaximumReceiverSlope));
    float depthBias = texelWorldSize * 1.5 * length(rowDepth);

    vec2 radius = clamp(ShadowFilterRadii[layer], vec2(texel), vec2(texel * float(ShadowFilterTaps) * 8.0 / 3.0));
    float visible = 0.0;
    float total = 0.0;

    for (int y = 0; y < ShadowFilterTaps; y++)
    {
        for (int x = 0; x < ShadowFilterTaps; x++)
        {
            vec2 position = (vec2(float(x), float(y)) + 0.5) / float(ShadowFilterTaps) * 2.0 - 1.0;
            vec2 offset = position * radius;
            float weight = (1.5 - abs(position.x)) * (1.5 - abs(position.y));
            float depth = coord.z - depthBias + dot(receiverSlope, offset);
            visible += texture(ShadowMap, vec4(coord.xy + offset, float(layer), depth)) * weight;
            total += weight;
        }
    }

    return mix(1.0, visible / total, ShadowStrength);
}
