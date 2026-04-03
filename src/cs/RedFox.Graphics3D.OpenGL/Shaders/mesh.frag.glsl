in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vTexCoord;
flat in int vHasNormals;

uniform vec3 uCameraPos;
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform vec3 uSkyColor;
uniform vec3 uGroundColor;
uniform vec3 uSpecularColor;
uniform float uAmbientStrength;
uniform float uSpecularStrength;
uniform float uShininess;
uniform vec4 uDiffuseColor;
uniform bool uHasDiffuseTexture;
uniform sampler2D uDiffuseTexture;
uniform bool uHasEnvironmentMap;
uniform sampler2D uEnvironmentMap;
uniform float uEnvironmentMapIntensity;

// IBL uniforms
uniform samplerCube uIrradianceMap;
uniform samplerCube uPrefilterMap;
uniform float uPrefilterMaxMipLevel;
uniform sampler2D uBrdfLut;

// Material IBL properties
uniform float uMetallic;
uniform float uRoughness;

uniform bool uUseIBL;

out vec4 FragColor;

const float PI = 3.14159265358979323846;

// ------------------------------------------------------------------
// Cook-Torrance BRDF (learnopengl.com)
// ------------------------------------------------------------------

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a      = roughness * roughness;
    float a2     = a * a;
    float NdotH  = max(dot(N, H), 0.0);
    float denom  = (NdotH * a2 - NdotH) * NdotH + 1.0;
    return a2 / (PI * denom * denom);
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    return NdotV / (NdotV * (1.0 - k) + k);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    return GeometrySchlickGGX(NdotL, roughness) * GeometrySchlickGGX(NdotV, roughness);
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// ------------------------------------------------------------------
// Main
// ------------------------------------------------------------------

void main()
{
    // ---- Albedo / normal ------------------------------------------------
    vec4 texColor = uHasDiffuseTexture ? texture(uDiffuseTexture, vTexCoord) : vec4(1.0);
    vec4 baseColor = texColor * uDiffuseColor;

    vec3 N = vHasNormals == 1
        ? normalize(vNormal)
        : normalize(cross(dFdx(vWorldPos), dFdy(vWorldPos)));

    vec3 V = normalize(uCameraPos - vWorldPos);

    // ---- Material properties --------------------------------------------
    float metallic    = uMetallic;
    float roughness   = uRoughness;
    vec3  albedo      = baseColor.rgb;
    vec3  F0          = mix(vec3(0.04), albedo, metallic);

    // ---- Direct lighting (unchanged Blinn-Phong) ------------------------
    vec3  Lo     = vec3(0.0);
    vec3  lightDir = normalize(-uLightDir);
    vec3  halfDir  = normalize(lightDir + V);
    float NdotL    = max(dot(N, lightDir), 0.0);

    if (NdotL > 0.0)
    {
        float NdotH = max(dot(N, halfDir), 0.0);
        vec3 radiance   = uLightColor;
        vec3 specular   = uSpecularColor * radiance * pow(NdotH, uShininess) * uSpecularStrength * NdotL;
        Lo += albedo * radiance * NdotL + specular;
    }

    // ---- Ambient lighting ------------------------------------------------
    vec3 ambient = vec3(0.0);

    if (uUseIBL)
    {
        // Diffuse IBL
        vec3 irradiance = texture(uIrradianceMap, N).rgb;
        vec3 F          = fresnelSchlick(max(dot(N, V), 0.0), F0);
        vec3 kD         = (1.0 - F) * (1.0 - metallic);
        vec3 diffuse    = irradiance * albedo;
        ambient        += kD * diffuse;

        // Specular IBL
        vec3  R              = reflect(-V, N);
        vec3  prefilteredColor = textureLod(uPrefilterMap, R, roughness * uPrefilterMaxMipLevel).rgb;
        vec2  brdf           = texture(uBrdfLut, vec2(max(dot(N, V), 0.0), roughness)).rg;
        vec3  specularIBL    = prefilteredColor * (F * brdf.x + brdf.y);
        ambient             += specularIBL;
    }
    else
    {
        // Fallback: hemisphere ambient
        float hemiFactor = clamp(N.y * 0.5 + 0.5, 0.0, 1.0);
        ambient = mix(uGroundColor, uSkyColor, hemiFactor) * uAmbientStrength;
    }

    // ---- Combine ---------------------------------------------------------
    vec3 finalColor = ambient + Lo;

    // Simple env reflection fallback when IBL disabled
    if (!uUseIBL && uHasEnvironmentMap)
    {
        float fresnel = pow(1.0 - max(dot(N, V), 0.0), 5.0);
        vec2 envUv = vec2(
            0.5 + atan(reflect(-V, N).z, reflect(-V, N).x) / 6.2831853071795864,
            acos(clamp(reflect(-V, N).y, -1.0, 1.0)) / 3.14159265358979323846
        );
        vec3 envColor = texture(uEnvironmentMap, envUv).rgb;
        finalColor = mix(finalColor, envColor, fresnel * uEnvironmentMapIntensity);
    }

    FragColor = vec4(finalColor, baseColor.a);
}
