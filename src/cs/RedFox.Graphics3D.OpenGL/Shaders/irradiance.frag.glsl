/// @file irradiance.frag.glsl
/// @brief Fragment shader for generating the irradiance cubemap from SH coefficients.
/// @details The diffuse irradiance is evaluated from 2nd-order real spherical harmonics
///          that have already been convolved with the Lambertian cosine kernel.

in vec3 vWorldPos;

uniform vec3 uShCoefficients[9];

out vec4 FragColor;

void main()
{
    vec3 normal = normalize(vWorldPos);
    float x = normal.x;
    float y = normal.y;
    float z = normal.z;

    vec3 irradiance =
        uShCoefficients[0] * 0.282095 +
        uShCoefficients[1] * (0.488603 * y) +
        uShCoefficients[2] * (0.488603 * z) +
        uShCoefficients[3] * (0.488603 * x) +
        uShCoefficients[4] * (1.092548 * x * y) +
        uShCoefficients[5] * (1.092548 * y * z) +
        uShCoefficients[6] * (0.315392 * ((3.0 * z * z) - 1.0)) +
        uShCoefficients[7] * (1.092548 * x * z) +
        uShCoefficients[8] * (0.546274 * ((x * x) - (y * y)));

    FragColor = vec4(max(irradiance, vec3(0.0)), 1.0);
}
