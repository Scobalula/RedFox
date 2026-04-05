in vec3 vWorldPos;
in vec3 vCameraRelativePos;
noperspective in vec3 vCameraRelativePosLinear;
in vec3 vViewPos;
in vec3 vNormal;
in vec3 vViewNormal;
in vec2 vTexCoord;
flat in int vHasNormals;
in float vClipW;

uniform vec3 uCameraPos;
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform vec3 uSkyColor;
uniform vec3 uGroundColor;
uniform float uAmbientStrength;
uniform vec4 uDiffuseColor;
uniform bool uHasDiffuseTexture;
uniform sampler2D uDiffuseTexture;
uniform float uEnvironmentMapExposure;
uniform float uEnvironmentMapIntensity;

// IBL uniforms
uniform samplerCube uIrradianceMap;
uniform samplerCube uPrefilterMap;
uniform float uPrefilterMaxMipLevel;
uniform sampler2D uBrdfLut;

// Skybox cubemap (used for non-IBL reflection fallback)
uniform bool uHasSkyMap;
uniform samplerCube uSkyMap;
uniform float uSkyMaxMipLevel;

// Material factors / textures
uniform float uMetallicFactor;
uniform float uRoughnessFactor;
uniform bool uHasMetallicRoughnessTexture;
uniform sampler2D uMetallicRoughnessTexture;
uniform bool uHasRoughnessTexture;
uniform sampler2D uRoughnessTexture;
uniform bool uHasGlossTexture;
uniform sampler2D uGlossTexture;
uniform bool uHasSpecularTexture;
uniform sampler2D uSpecularTexture;
uniform bool uUseLegacySpecular;
uniform vec3 uSpecularColor;
uniform float uSpecularStrength;
uniform bool uHasAoTexture;
uniform sampler2D uAoTexture;
uniform bool uDoubleSided;

uniform bool uUseIBL;
uniform int uShadingMode;

uniform float uFarPlane;

out vec4 FragColor;

const float PI = 3.14159265358979323846;
const int SHADING_MODE_PBR = 0;
const int SHADING_MODE_FULLBRIGHT = 1;

vec3 sRGBToLinear(vec3 color)
{
    return pow(color, vec3(2.2));
}

vec3 linearToSRGB(vec3 color)
{
    return pow(color, vec3(1.0 / 2.2));
}

vec3 tonemapReinhard(vec3 color)
{
    return color / (color + vec3(1.0));
}

// ------------------------------------------------------------------
// Cook-Torrance BRDF (learnopengl.com)
// ------------------------------------------------------------------

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a      = max(roughness * roughness, 1e-6);
    float a2     = a * a;
    float NdotH  = max(dot(N, H), 0.0);
    float denom  = (NdotH * a2 - NdotH) * NdotH + 1.0;
    return a2 / (PI * denom * denom);
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = max(roughness, 1e-3) + 1.0;
    float k = (r * r) / 8.0;
    return NdotV / (NdotV * (1.0 - k) + k);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(abs(dot(N, V)), 1e-4);
    float NdotL = max(dot(N, L), 0.0);
    return GeometrySchlickGGX(NdotL, roughness) * GeometrySchlickGGX(NdotV, roughness);
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness)
{
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// ------------------------------------------------------------------
// Main
// ------------------------------------------------------------------

void main()
{
    // ---- Albedo / normal ------------------------------------------------
    vec4 texColor = uHasDiffuseTexture ? texture(uDiffuseTexture, vTexCoord) : vec4(1.0);
    vec4 baseColor = texColor * uDiffuseColor;
    if (uShadingMode == SHADING_MODE_FULLBRIGHT)
    {
        FragColor = vec4(baseColor.rgb, baseColor.a);
        gl_FragDepth = log2(max(1e-6, vClipW + 1.0)) / log2(uFarPlane + 1.0);
        return;
    }

    vec3 V = normalize(-vCameraRelativePos);
    vec3 viewVector = normalize(-vViewPos);
    vec3 Ns = vHasNormals == 1
        ? normalize(vNormal)
        : vec3(0.0, 1.0, 0.0);
    vec3 viewNs = vHasNormals == 1
        ? normalize(vViewNormal)
        : vec3(0.0, 1.0, 0.0);
    vec3 geometricNormal = cross(dFdx(vCameraRelativePosLinear), dFdy(vCameraRelativePosLinear));
    vec3 viewGeometricNormal = cross(dFdx(vViewPos), dFdy(vViewPos));
    float geometricNormalLengthSquared = dot(geometricNormal, geometricNormal);
    float viewGeometricNormalLengthSquared = dot(viewGeometricNormal, viewGeometricNormal);
    vec3 Ng = geometricNormalLengthSquared > 1e-12
        ? geometricNormal * inversesqrt(geometricNormalLengthSquared)
        : Ns;
    vec3 viewNg = viewGeometricNormalLengthSquared > 1e-12
        ? viewGeometricNormal * inversesqrt(viewGeometricNormalLengthSquared)
        : viewNs;

    if (uDoubleSided)
    {
        if (!gl_FrontFacing)
        {
            Ng = -Ng;
            Ns = -Ns;
            viewNg = -viewNg;
            viewNs = -viewNs;
        }
    }
    else
    {
        Ng = faceforward(Ng, -V, Ng);
        Ns = faceforward(Ns, -V, Ng);
        viewNg = faceforward(viewNg, -viewVector, viewNg);
        viewNs = faceforward(viewNs, -viewVector, viewNg);
    }

    vec3 N = normalize(Ns);
    vec3 viewNormal = normalize(viewNs);
    float NdotV = max(abs(dot(N, V)), 1e-4);

    // ---- Material properties --------------------------------------------
    vec3  albedo      = sRGBToLinear(baseColor.rgb);

    float metallic = clamp(uMetallicFactor, 0.0, 1.0);
    float roughness = clamp(uRoughnessFactor, 0.0, 1.0);

    if (uHasMetallicRoughnessTexture)
    {
        // glTF combined metallic-roughness: G = roughness, B = metallic
        vec3 mr = texture(uMetallicRoughnessTexture, vTexCoord).rgb;
        roughness *= mr.g;
        metallic *= mr.b;
    }

    if (uHasRoughnessTexture)
        roughness *= texture(uRoughnessTexture, vTexCoord).r;
    else if (uHasGlossTexture)
        roughness *= (1.0 - texture(uGlossTexture, vTexCoord).r);

    float perceptualRoughness = roughness;
    float shadingRoughness = max(perceptualRoughness, 1e-3);

    float ao = uHasAoTexture ? texture(uAoTexture, vTexCoord).r : 1.0;

    vec3 legacySpecular = clamp(uSpecularColor * uSpecularStrength, vec3(0.0), vec3(1.0));
    if (uHasSpecularTexture)
        legacySpecular *= texture(uSpecularTexture, vTexCoord).rgb;

    vec3 dielectricF0 = vec3(0.04);
    if (uUseLegacySpecular)
        dielectricF0 = max(dielectricF0, legacySpecular);

    vec3 metallicF0 = albedo;
    if (uUseLegacySpecular && uSpecularStrength > 0.0)
        metallicF0 = mix(albedo, uSpecularColor, uSpecularStrength);

    vec3 F0 = mix(dielectricF0, metallicF0, metallic);

    // ---- Direct lighting (Cook-Torrance) --------------------------------
    vec3  Lo     = vec3(0.0);
    vec3  lightDir = normalize(-uLightDir);
    float NdotL    = max(dot(N, lightDir), 0.0);

    if (NdotL > 0.0)
    {
        vec3 H = normalize(V + lightDir);

        float NDF = DistributionGGX(N, H, shadingRoughness);
        float G   = GeometrySmith(N, V, lightDir, shadingRoughness);
        vec3  F   = fresnelSchlick(max(dot(H, V), 0.0), F0);

        vec3 numerator = NDF * G * F;
        float denom = 4.0 * NdotV * NdotL + 0.0001;
        vec3 specular = numerator / denom;

        vec3 kS = F;
        vec3 kD = (1.0 - kS) * (1.0 - metallic);

        vec3 radiance = uLightColor;
        vec3 diffuse = kD * albedo / PI;

        Lo += (diffuse + specular) * radiance * NdotL;
    }

    // ---- Ambient lighting ------------------------------------------------
    vec3 ambient = vec3(0.0);

    if (uUseIBL)
    {
        vec3 F_ambient = fresnelSchlickRoughness(NdotV, F0, perceptualRoughness);
        vec3 kD        = (1.0 - F_ambient) * (1.0 - metallic);

        vec3 irradiance = texture(uIrradianceMap, N).rgb * uEnvironmentMapExposure;
        vec3 diffuse    = irradiance * albedo;
        ambient        += kD * diffuse;

        vec3  R              = reflect(-V, N);
        vec3  filteredColor  = textureLod(uPrefilterMap, R, perceptualRoughness * uPrefilterMaxMipLevel).rgb * uEnvironmentMapExposure;
        vec3  sharpSample    = uHasSkyMap ? textureLod(uSkyMap, R, 0.0).rgb : textureLod(uPrefilterMap, R, 0.0).rgb;
        vec3  sharpColor     = sharpSample * uEnvironmentMapExposure;
        float filteredWeight = smoothstep(0.0, 0.12, perceptualRoughness);
        vec3  prefilteredColor = mix(sharpColor, filteredColor, filteredWeight);
        vec2  brdf           = texture(uBrdfLut, vec2(NdotV, perceptualRoughness)).rg;
        vec3  specularIBL    = prefilteredColor * (F_ambient * brdf.x + brdf.y);
        ambient             += specularIBL;

        ambient *= uEnvironmentMapIntensity;
        ambient *= ao;
    }
    else
    {
        // No-envmap fallback: hemisphere ambient
        if (!uHasSkyMap)
        {
            float hemiFactor = clamp(N.y * 0.5 + 0.5, 0.0, 1.0);
            vec3 hemiAmbient = mix(uGroundColor, uSkyColor, hemiFactor) * uAmbientStrength;
            vec3 kD_hemi = (1.0 - F0) * (1.0 - metallic);
            ambient = kD_hemi * albedo * hemiAmbient * ao;
        }
        else
        {
            // Envmap present but IBL disabled: cheap specular + diffuse approximation.
            vec3 F = fresnelSchlick(NdotV, F0);
            vec3 R = reflect(-V, N);
            vec3 envColor = textureLod(uSkyMap, R, perceptualRoughness * uSkyMaxMipLevel).rgb * uEnvironmentMapExposure;
            vec3 kD = (1.0 - F) * (1.0 - metallic);
            float hemiFactor = clamp(N.y * 0.5 + 0.5, 0.0, 1.0);
            vec3 hemiAmbient = mix(uGroundColor, uSkyColor, hemiFactor);
            ambient = (kD * albedo * hemiAmbient + envColor * F) * uEnvironmentMapIntensity * ao;
        }
    }

    // ---- Combine ---------------------------------------------------------
    vec3 finalColor = ambient + Lo;
    FragColor = vec4(linearToSRGB(tonemapReinhard(finalColor)), baseColor.a);
    gl_FragDepth = log2(max(1e-6, vClipW + 1.0)) / log2(uFarPlane + 1.0);
}
