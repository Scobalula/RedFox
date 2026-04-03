/// @file blur.frag.glsl
/// @brief High-quality separable Gaussian blur fragment shader.
/// @details This shader is kept for reference; the environment map blur
/// is now implemented directly inside envmap.frag.glsl for simplicity.

in vec2 vTexCoord;

uniform sampler2D uTexture;
uniform vec2 uDirection;
uniform vec2 uResolution;
uniform float uBlurRadius;

out vec4 FragColor;

float gaussianWeight(float distance, float sigma)
{
    float sigma2 = sigma * sigma;
    return exp(-(distance * distance) / (2.0 * sigma2));
}

void main()
{
    vec2 texelSize = 1.0 / uResolution;
    vec2 direction = normalize(uDirection) * texelSize;

    float sigma = max(uBlurRadius / 2.5, 0.5);
    int radius = int(ceil(uBlurRadius));
    radius = clamp(radius, 1, 32);

    vec2 uv = vTexCoord;
    vec3 result = vec3(0.0);
    float totalWeight = 0.0;

    for (int i = -32; i <= 32; i++)
    {
        if (i < -radius || i > radius) continue;

        float distance = abs(float(i));
        float weight = gaussianWeight(distance, sigma);

        vec2 offset = direction * float(i);
        result += texture(uTexture, uv + offset).rgb * weight;
        totalWeight += weight;
    }

    result /= totalWeight;
    FragColor = vec4(result, 1.0);
}
