/// @file irradiance.frag.glsl
/// @brief Fragment shader for generating the irradiance cubemap.
/// @details For each fragment direction (surface normal N), samples the
///          upper hemisphere with cosine weighting to compute irradiance.

in vec3 vWorldPos;

uniform samplerCube uEnvironmentMap;

out vec4 FragColor;

void main()
{
    vec3 normal = normalize(vWorldPos);

    vec3 irradiance = vec3(0.0);

    // Create tangent space basis
    vec3 up = vec3(0.0, 1.0, 0.0);
    vec3 tangent = normalize(cross(up, normal));
    vec3 bitangent = cross(normal, tangent);

    // Hemisphere sampling
    float sampleDelta = 0.025;
    float nrSamples = 0.0;

    for (float phi = 0.0; phi < 6.2831853071795864; phi += sampleDelta)
    {
        for (float theta = 0.0; theta < 1.5707963267948966; theta += sampleDelta)
        {
            // Spherical to tangent-space cartesian
            vec3 tangentSample = vec3(
                sin(theta) * cos(phi),
                sin(theta) * sin(phi),
                cos(theta)
            );

            // Tangent to world space
            vec3 worldSample = tangentSample.x * tangent +
                               tangentSample.y * bitangent +
                               tangentSample.z * normal;

            irradiance += texture(uEnvironmentMap, worldSample).rgb *
                          cos(theta) * sin(theta);
            nrSamples++;
        }
    }

    irradiance = 3.14159265358979323846 * irradiance * (1.0 / nrSamples);

    FragColor = vec4(irradiance, 1.0);
}
