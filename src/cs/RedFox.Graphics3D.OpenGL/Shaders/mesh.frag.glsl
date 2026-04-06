in vec3 vWorldPos;
in vec3 vCameraRelativePos;
noperspective in vec3 vCameraRelativePosLinear;
in vec3 vViewPos;
in vec3 vNormal;
in vec3 vViewNormal;
in vec2 vTexCoord;
flat in int vHasNormals;
in float vClipW;
in vec4 vLightSpacePos;

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

uniform bool uEnableShadows;
uniform sampler2D uShadowMap;
uniform int uShadowQuality;     // 0=Low 1=Medium 2=High 3=Ultra
uniform float uShadowSoftness;  // penumbra width (light size), range ~0..3
uniform float uShadowIntensity; // how dark shadows get, range 0..1

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
// Shadow Mapping — IQ-inspired smooth penumbra with multi-quality PCF
// Based on Inigo Quilez's soft shadow approximation:
//   smoothstep: 0.25*(1+s)*(1+s)*(2-s)
// ------------------------------------------------------------------

// IQ smooth shadow curve — maps [-1,1] to [0,1] with soft penumbra
float iqSmoothShadow(float s)
{
    s = clamp(s, -1.0, 1.0);
    return 0.25 * (1.0 + s) * (1.0 + s) * (2.0 - s);
}

float sampleShadowPCF(vec3 projCoords, float bias, vec2 texelSize, float softness)
{
    float currentDepth = projCoords.z;
    float kernelScale = max(softness, 0.5);

    // Low quality: 3x3 (9 samples)
    if (uShadowQuality == 0)
    {
        float shadow = 0.0;
        for (int x = -1; x <= 1; ++x)
        {
            for (int y = -1; y <= 1; ++y)
            {
                float pcfDepth = texture(uShadowMap, projCoords.xy + vec2(x, y) * texelSize * kernelScale).r;
                shadow += currentDepth - bias > pcfDepth ? 1.0 : 0.0;
            }
        }
        return shadow / 9.0;
    }

    // Medium quality: 5x5 (25 samples)
    if (uShadowQuality == 1)
    {
        float shadow = 0.0;
        for (int x = -2; x <= 2; ++x)
        {
            for (int y = -2; y <= 2; ++y)
            {
                float pcfDepth = texture(uShadowMap, projCoords.xy + vec2(x, y) * texelSize * kernelScale).r;
                shadow += currentDepth - bias > pcfDepth ? 1.0 : 0.0;
            }
        }
        return shadow / 25.0;
    }

    // High quality: 16-sample Poisson disk
    if (uShadowQuality == 2)
    {
        float shadow = 0.0;
        vec2 poissonDisk[16];
        poissonDisk[0]  = vec2(-0.94201624, -0.39906216);
        poissonDisk[1]  = vec2( 0.94558609, -0.76890725);
        poissonDisk[2]  = vec2(-0.09418410, -0.92938870);
        poissonDisk[3]  = vec2( 0.34495938,  0.29387760);
        poissonDisk[4]  = vec2(-0.91588581,  0.45771432);
        poissonDisk[5]  = vec2(-0.81544232, -0.87912464);
        poissonDisk[6]  = vec2(-0.38277543,  0.27676845);
        poissonDisk[7]  = vec2( 0.97484398,  0.75648379);
        poissonDisk[8]  = vec2( 0.44323325, -0.97511554);
        poissonDisk[9]  = vec2( 0.53742981, -0.47373420);
        poissonDisk[10] = vec2(-0.26496911, -0.41893023);
        poissonDisk[11] = vec2( 0.79197514,  0.19090188);
        poissonDisk[12] = vec2(-0.24188840,  0.99706507);
        poissonDisk[13] = vec2(-0.81409955,  0.91437590);
        poissonDisk[14] = vec2( 0.19984126,  0.78641367);
        poissonDisk[15] = vec2( 0.14383161, -0.14100790);

        float radius = kernelScale * 2.5;
        for (int i = 0; i < 16; i++)
        {
            float pcfDepth = texture(uShadowMap, projCoords.xy + poissonDisk[i] * texelSize * radius).r;
            shadow += currentDepth - bias > pcfDepth ? 1.0 : 0.0;
        }
        return shadow / 16.0;
    }

    // Ultra quality: 25-sample Poisson disk with wider spread
    {
        float shadow = 0.0;
        vec2 poissonDisk[25];
        poissonDisk[0]  = vec2(-0.94201624, -0.39906216);
        poissonDisk[1]  = vec2( 0.94558609, -0.76890725);
        poissonDisk[2]  = vec2(-0.09418410, -0.92938870);
        poissonDisk[3]  = vec2( 0.34495938,  0.29387760);
        poissonDisk[4]  = vec2(-0.91588581,  0.45771432);
        poissonDisk[5]  = vec2(-0.81544232, -0.87912464);
        poissonDisk[6]  = vec2(-0.38277543,  0.27676845);
        poissonDisk[7]  = vec2( 0.97484398,  0.75648379);
        poissonDisk[8]  = vec2( 0.44323325, -0.97511554);
        poissonDisk[9]  = vec2( 0.53742981, -0.47373420);
        poissonDisk[10] = vec2(-0.26496911, -0.41893023);
        poissonDisk[11] = vec2( 0.79197514,  0.19090188);
        poissonDisk[12] = vec2(-0.24188840,  0.99706507);
        poissonDisk[13] = vec2(-0.81409955,  0.91437590);
        poissonDisk[14] = vec2( 0.19984126,  0.78641367);
        poissonDisk[15] = vec2( 0.14383161, -0.14100790);
        poissonDisk[16] = vec2(-0.50000000,  0.62500000);
        poissonDisk[17] = vec2( 0.37500000, -0.12500000);
        poissonDisk[18] = vec2(-0.68750000, -0.18750000);
        poissonDisk[19] = vec2( 0.06250000,  0.43750000);
        poissonDisk[20] = vec2( 0.81250000, -0.31250000);
        poissonDisk[21] = vec2(-0.43750000,  0.81250000);
        poissonDisk[22] = vec2( 0.62500000,  0.50000000);
        poissonDisk[23] = vec2(-0.12500000, -0.62500000);
        poissonDisk[24] = vec2( 0.25000000,  0.93750000);

        float radius = kernelScale * 3.5;
        for (int i = 0; i < 25; i++)
        {
            float pcfDepth = texture(uShadowMap, projCoords.xy + poissonDisk[i] * texelSize * radius).r;
            shadow += currentDepth - bias > pcfDepth ? 1.0 : 0.0;
        }
        return shadow / 25.0;
    }
}

float calculateShadow(vec4 fragPosLightSpace, vec3 normal, vec3 lightDirection)
{
    vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
    projCoords = projCoords * 0.5 + 0.5;

    if (projCoords.z > 1.0 || projCoords.x < 0.0 || projCoords.x > 1.0
        || projCoords.y < 0.0 || projCoords.y > 1.0)
        return 0.0;

    float bias = max(0.005 * (1.0 - dot(normal, lightDirection)), 0.0005);
    vec2 texelSize = 1.0 / vec2(textureSize(uShadowMap, 0));

    float rawShadow = sampleShadowPCF(projCoords, bias, texelSize, uShadowSoftness);

    // Apply IQ's smooth penumbra curve: map [0,1] through smoothstep
    // Transform raw shadow (0=lit, 1=shadow) to IQ range [-1,1] then apply curve
    float s = 1.0 - 2.0 * rawShadow; // map 0->1 (lit), 1->-1 (shadow)
    float smoothVis = iqSmoothShadow(s);
    float shadow = 1.0 - smoothVis;

    return shadow * uShadowIntensity;
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

    float shadowFactor = 0.0;
    if (uEnableShadows)
        shadowFactor = calculateShadow(vLightSpacePos, N, lightDir);

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

        Lo += (diffuse + specular) * radiance * NdotL * (1.0 - shadowFactor);
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
    // Attenuate ambient in shadowed regions for depth contrast
    float ambientShadowFactor = uEnableShadows ? mix(1.0, 0.45, shadowFactor) : 1.0;
    vec3 finalColor = ambient * ambientShadowFactor + Lo;

    FragColor = vec4(linearToSRGB(tonemapReinhard(finalColor)), baseColor.a);
    gl_FragDepth = log2(max(1e-6, vClipW + 1.0)) / log2(uFarPlane + 1.0);
}
