uniform int HasAmbientOcclusionMap;
uniform highp sampler2D AmbientOcclusionMap;
uniform highp sampler2D SceneDepthMap;

const float AmbientOcclusionDepthTolerance = 0.00001;

float ScreenSpaceOcclusion()
{
    if (HasAmbientOcclusionMap == 0)
    {
        return 1.0;
    }

    ivec2 pixel = ivec2(gl_FragCoord.xy);
    if (gl_FragCoord.z < texelFetch(SceneDepthMap, pixel, 0).r - AmbientOcclusionDepthTolerance)
    {
        return 1.0;
    }

    return texelFetch(AmbientOcclusionMap, pixel, 0).r;
}
