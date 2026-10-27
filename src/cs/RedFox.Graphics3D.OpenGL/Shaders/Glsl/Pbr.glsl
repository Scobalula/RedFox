in vec3 WorldPosition;
in vec3 WorldNormal;
in vec4 WorldTangent;
in vec2 TextureCoordinate;

#define MAX_LIGHTS 4
#define PI 3.14159265

uniform int LightCount;
uniform vec4 LightDirectionsAndIntensity[MAX_LIGHTS];
uniform vec3 LightColors[MAX_LIGHTS];
uniform int EnvironmentLightCount;
uniform vec4 EnvironmentLightDirections[MAX_LIGHTS];
uniform vec3 EnvironmentLightPowers[MAX_LIGHTS];
uniform int HasEnvironmentMap;
uniform sampler2D EnvironmentMap;
uniform float EnvironmentMipLevels;
uniform float EnvironmentIntensity;
uniform vec3 CameraPosition;
uniform int UseViewBasedLighting;
uniform int UseFlatShading;
uniform int UVLayerCount;
uniform int DepthPrepass;
uniform int AlphaToCoverage;

uniform vec4 BaseColor;
uniform vec4 EmissiveColor;
uniform float MaterialMetallic;
uniform float MaterialRoughness;
uniform float MaterialAlphaCutoff;
uniform float Reflectance;
uniform float NormalStrength;
uniform int NormalEncoding;
uniform float DetailNormalStrength;
uniform vec2 DetailNormalTiling;
uniform vec2 TextureTiling;
uniform float SubsurfaceStrength;
uniform vec3 SubsurfaceColor;
uniform float TranslucencyStrength;
uniform float HairShift;
uniform float HairSpecularStrength;

uniform sampler2D DiffuseMap;
uniform int HasDiffuseMap;
uniform mat4 DiffuseMapTransform;
uniform vec4 DiffuseMapBias;
uniform int DiffuseMapDecodeSrgb;

uniform sampler2D OpacityMap;
uniform int HasOpacityMap;
uniform mat4 OpacityMapTransform;
uniform vec4 OpacityMapBias;

uniform sampler2D NormalMap;
uniform int HasNormalMap;
uniform mat4 NormalMapTransform;
uniform vec4 NormalMapBias;

uniform sampler2D DetailNormalMap;
uniform int HasDetailNormalMap;
uniform mat4 DetailNormalMapTransform;
uniform vec4 DetailNormalMapBias;

uniform sampler2D RoughnessMap;
uniform int HasRoughnessMap;
uniform mat4 RoughnessMapTransform;
uniform vec4 RoughnessMapBias;

uniform sampler2D MetallicMap;
uniform int HasMetallicMap;
uniform mat4 MetallicMapTransform;
uniform vec4 MetallicMapBias;

uniform sampler2D SpecularMap;
uniform int HasSpecularMap;
uniform mat4 SpecularMapTransform;
uniform vec4 SpecularMapBias;
uniform int SpecularMapDecodeSrgb;

uniform sampler2D OcclusionMap;
uniform int HasOcclusionMap;
uniform mat4 OcclusionMapTransform;
uniform vec4 OcclusionMapBias;

uniform sampler2D EmissiveMap;
uniform int HasEmissiveMap;
uniform mat4 EmissiveMapTransform;
uniform vec4 EmissiveMapBias;
uniform int EmissiveMapDecodeSrgb;

uniform sampler2D SubsurfaceMap;
uniform int HasSubsurfaceMap;
uniform mat4 SubsurfaceMapTransform;
uniform vec4 SubsurfaceMapBias;

uniform sampler2D TranslucencyMap;
uniform int HasTranslucencyMap;
uniform mat4 TranslucencyMapTransform;
uniform vec4 TranslucencyMapBias;

uniform sampler2D HairShiftMap;
uniform int HasHairShiftMap;
uniform mat4 HairShiftMapTransform;
uniform vec4 HairShiftMapBias;

uniform sampler2D HairFlowMap;
uniform int HasHairFlowMap;
uniform mat4 HairFlowMapTransform;
uniform vec4 HairFlowMapBias;

uniform sampler2D IrisNormalMap;
uniform int HasIrisNormalMap;
uniform mat4 IrisNormalMapTransform;
uniform vec4 IrisNormalMapBias;

out vec4 FragColor;

const vec3 StudioKeyDirection = vec3(0.48, 0.78, 0.40);
const float ShadowFloor = 0.15;
const float MinimumRoughness = 0.04;
const float MaximumSpecularAntiAliasing = 0.18;
const float HairOcclusion = 0.3;
const vec3 CorneaReflectance = vec3(0.025);

struct Surface
{
    vec3 albedo;
    vec3 diffuseColor;
    vec3 specularColor;
    float roughness;
    float alpha;
    float occlusion;
    float subsurface;
    vec3 transmittance;
    float shift;
    vec3 normal;
    vec3 diffuseNormal;
    vec3 geometryNormal;
    vec3 strand;
    vec3 emission;
};

struct Lighting
{
    vec3 diffuse;
    vec3 specular;
};

bool HasMap(int hasMap)
{
    return hasMap != 0 && UVLayerCount > 0;
}

vec4 SampleMap(sampler2D map, mat4 transform, vec4 bias, vec2 uv)
{
    return transform * texture(map, uv) + bias;
}

vec4 SampleColorMap(sampler2D map, mat4 transform, vec4 bias, int decodeSrgb, vec2 uv)
{
    vec4 texel = texture(map, uv);
    if (decodeSrgb != 0)
    {
        texel.rgb = SrgbToLinear(texel.rgb);
    }

    return transform * texel + bias;
}

float Pow5(float x)
{
    float x2 = x * x;
    return x2 * x2 * x;
}

vec2 DecodeNormal(vec4 encoded, float strength)
{
    vec2 normal = encoded.xy * 2.0 - 1.0;
    if (NormalEncoding == 1)
    {
        vec2 squared = normal * abs(normal);
        vec2 diamond = vec2(squared.x + squared.y, squared.x - squared.y) * 0.5;
        normal = normalize(vec3(diamond, 1.0 - abs(diamond.x) - abs(diamond.y))).xy;
    }

    return normal * strength;
}

void BuildTangentFrame(vec3 vertexNormal, vec2 uv, out vec3 tangent, out vec3 bitangent)
{
    vec3 dp1 = dFdx(WorldPosition);
    vec3 dp2 = dFdy(WorldPosition);
    vec2 duv1 = dFdx(uv);
    vec2 duv2 = dFdy(uv);

    if (WorldTangent.w != 0.0)
    {
        tangent = WorldTangent.xyz - vertexNormal * dot(vertexNormal, WorldTangent.xyz);
        tangent *= inversesqrt(max(dot(tangent, tangent), 1e-20));
        bitangent = cross(vertexNormal, tangent) * sign(WorldTangent.w);
        return;
    }

    vec3 dp2perp = cross(dp2, vertexNormal);
    vec3 dp1perp = cross(vertexNormal, dp1);
    tangent = dp2perp * duv1.x + dp1perp * duv2.x;
    bitangent = dp2perp * duv1.y + dp1perp * duv2.y;
    float scale = inversesqrt(max(max(dot(tangent, tangent), dot(bitangent, bitangent)), 1e-20));
    tangent *= scale;
    bitangent *= scale;
}

vec3 PerturbNormal(vec2 tangentNormal, vec3 tangent, vec3 bitangent, vec3 facingNormal)
{
    float z = sqrt(max(1.0 - dot(tangentNormal, tangentNormal), 0.0));
    return normalize(tangent * tangentNormal.x + bitangent * tangentNormal.y + facingNormal * z);
}

float DistributionGGX(float alpha, float nDotH)
{
    float alpha2 = alpha * alpha;
    float d = nDotH * nDotH * (alpha2 - 1.0) + 1.0;
    return alpha2 / (PI * d * d + 1e-9);
}

float VisibilitySmith(float alpha, float nDotL, float nDotV)
{
    float lambdaV = nDotL * (nDotV * (1.0 - alpha) + alpha);
    float lambdaL = nDotV * (nDotL * (1.0 - alpha) + alpha);
    return 0.5 / max(lambdaV + lambdaL, 1e-5);
}

vec3 FresnelSchlick(vec3 f0, float cosine)
{
    return f0 + (1.0 - f0) * Pow5(clamp(1.0 - cosine, 0.0, 1.0));
}

vec2 EnvironmentBRDF(float roughness, float cosine)
{
    const vec4 c0 = vec4(-1.0, -0.0275, -0.572, 0.022);
    const vec4 c1 = vec4(1.0, 0.0425, 1.04, -0.04);
    vec4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * cosine)) * r.x + r.y;
    return vec2(-1.04, 1.04) * a004 + r.zw;
}

vec3 SpecularEnergy(Surface surface, float cosine)
{
    vec2 brdf = EnvironmentBRDF(surface.roughness, clamp(cosine, 0.0, 1.0));
    return surface.specularColor * brdf.x + brdf.y;
}

vec3 MultiBounce(float visibility, vec3 albedo)
{
    vec3 a = 2.0404 * albedo - 0.3324;
    vec3 b = -4.7951 * albedo + 0.6417;
    vec3 c = 2.7552 * albedo + 0.6903;
    return max(vec3(visibility), ((visibility * a + b) * visibility + c) * visibility);
}

vec3 TransmittedLight(Surface surface, vec3 lightDirection)
{
    return surface.diffuseColor * surface.transmittance * clamp(-dot(surface.geometryNormal, lightDirection), 0.0, 1.0) / PI;
}

float WrappedCosine(Surface surface, vec3 lightDirection, float falloff)
{
    float wrap = surface.subsurface * falloff * 0.5;
    vec3 normal = normalize(mix(surface.diffuseNormal, surface.geometryNormal, wrap));
    return clamp((dot(normal, lightDirection) + wrap) / ((1.0 + wrap) * (1.0 + wrap)), 0.0, 1.0);
}

vec3 DiffuseCosine(Surface surface, vec3 lightDirection)
{
    if (surface.subsurface <= 0.0)
    {
        return vec3(clamp(dot(surface.diffuseNormal, lightDirection), 0.0, 1.0));
    }

    return vec3(WrappedCosine(surface, lightDirection, SubsurfaceColor.r), WrappedCosine(surface, lightDirection, SubsurfaceColor.g), WrappedCosine(surface, lightDirection, SubsurfaceColor.b));
}

float StrandSine(vec3 strand, vec3 halfVector)
{
    float along = dot(strand, halfVector);
    return sqrt(max(1.0 - along * along, 0.0));
}

vec3 HairHighlights(Surface surface, vec3 lightDirection, vec3 viewDirection)
{
    vec3 halfVector = normalize(viewDirection + lightDirection);
    vec3 primaryStrand = normalize(surface.strand + (surface.shift - 0.1) * surface.normal);
    vec3 secondaryStrand = normalize(surface.strand - (surface.shift + 0.1) * surface.normal);
    float primaryPower = clamp(2.0 / max(surface.alpha * surface.alpha, 1e-4) - 2.0, 4.0, 1024.0);
    float secondaryPower = primaryPower * 0.25;
    float primary = pow(StrandSine(primaryStrand, halfVector), primaryPower) * (primaryPower + 2.0) / (2.0 * PI);
    float secondary = pow(StrandSine(secondaryStrand, halfVector), secondaryPower) * (secondaryPower + 2.0) / (2.0 * PI);
    float terminator = smoothstep(-0.1, 0.25, dot(surface.normal, lightDirection));
    vec3 fresnel = FresnelSchlick(surface.specularColor, clamp(dot(viewDirection, halfVector), 0.0, 1.0));

    return (fresnel * primary + surface.albedo * secondary * 0.25) * terminator * HairSpecularStrength;
}

Lighting EvaluateLight(Surface surface, vec3 lightDirection, vec3 viewDirection)
{
    vec3 halfVector = normalize(viewDirection + lightDirection);
    float nDotL = clamp(dot(surface.normal, lightDirection), 0.0, 1.0);
    float nDotV = max(dot(surface.normal, viewDirection), 1e-4);
    float nDotH = clamp(dot(surface.normal, halfVector), 0.0, 1.0);
    vec3 fresnel = FresnelSchlick(surface.specularColor, clamp(dot(viewDirection, halfVector), 0.0, 1.0));

    Lighting lighting;
    lighting.diffuse = surface.diffuseColor / PI * DiffuseCosine(surface, lightDirection) * (1.0 - fresnel) + TransmittedLight(surface, lightDirection);
#if defined(PBR_HAIR)
    lighting.specular = HairHighlights(surface, lightDirection, viewDirection);
#else
    lighting.specular = DistributionGGX(surface.alpha, nDotH) * VisibilitySmith(surface.alpha, nDotL, nDotV) * fresnel * nDotL;
#endif
    return lighting;
}

vec3 StudioEnvironment(vec3 direction, float roughness)
{
    vec3 ground = vec3(0.035, 0.035, 0.038);
    vec3 horizon = vec3(0.22, 0.22, 0.23);
    vec3 sky = vec3(0.50, 0.52, 0.56);
    float up = direction.y;
    vec3 color = up >= 0.0 ? mix(horizon, sky, pow(up, 0.55)) : mix(horizon, ground, pow(-up, 0.35));
    float spread = mix(0.02, 0.9, roughness);
    float box = smoothstep(1.0 - spread, 1.0, dot(direction, normalize(StudioKeyDirection)));

    return color + vec3(1.6, 1.55, 1.45) * box * mix(1.0, 0.25, roughness);
}

vec3 SampleEnvironment(vec3 direction, float roughness)
{
    if (HasEnvironmentMap == 0)
    {
        return StudioEnvironment(direction, roughness);
    }

    vec2 uv = vec2(atan(direction.z, direction.x) / (2.0 * PI) + 0.5, acos(clamp(direction.y, -1.0, 1.0)) / PI);
    float lod = roughness * max(EnvironmentMipLevels - 1.0, 0.0);
    return textureLod(EnvironmentMap, uv, lod).rgb * EnvironmentIntensity;
}

float DiscLightCosine(float nDotL, float radius)
{
    float sinRadius = sin(radius);
    if (nDotL >= sinRadius)
    {
        return nDotL;
    }

    float wrapped = max(nDotL + sinRadius, 0.0);
    return wrapped * wrapped / (4.0 * sinRadius);
}

float EnvironmentLobeShare(vec3 reflection, vec3 lightDirection, float alpha, float radius)
{
    float nDotL = dot(reflection, lightDirection);
    if (nDotL <= 0.0)
    {
        return 0.0;
    }

    float widened = min(alpha + radius * 0.5, 1.0);
    float widened2 = widened * widened;
    float nDotH = dot(reflection, normalize(reflection + lightDirection));
    float distribution = nDotH * nDotH * (widened2 - 1.0) + 1.0;
    float lobe = widened2 / (PI * distribution * distribution);
    return lobe * 0.25 * nDotL * (1.0 + 3.0 * pow(widened, 1.7));
}

void ApplyAlphaTest(float opacity, vec2 uv, vec2 opacitySize)
{
    int sampleMask = -1;
    if (MaterialAlphaCutoff > 0.0)
    {
        vec2 texelsAcross = max(abs(dFdx(uv * opacitySize)), abs(dFdy(uv * opacitySize)));
        float minification = clamp(log2(max(max(texelsAcross.x, texelsAcross.y), 1.0)), 0.0, 1.0);
        float edge = clamp((opacity - MaterialAlphaCutoff) / max(fwidth(opacity), 0.0001) + 0.5, 0.0, 1.0);
        float average = clamp(opacity / MaterialAlphaCutoff, 0.0, 1.0);
        float coverage = mix(edge, average, minification);

#ifdef GL_OES_sample_variables
        int samples = AlphaToCoverage != 0 ? gl_NumSamples : 1;
#else
        int samples = 1;
#endif
        int covered = samples > 1 ? int(coverage * float(samples) + 0.5) : (opacity > MaterialAlphaCutoff ? 1 : 0);
        if (covered == 0)
        {
            discard;
        }

        if (samples > 1)
        {
            sampleMask = (1 << covered) - 1;
        }
    }

#ifdef GL_OES_sample_variables
    gl_SampleMask[0] = sampleMask;
#endif
}

Surface ReadSurface(vec2 uv, vec3 albedo, vec3 emission)
{
    Surface surface;
    vec3 vertexNormal = normalize(WorldNormal);
    surface.geometryNormal = gl_FrontFacing ? vertexNormal : -vertexNormal;
    surface.albedo = albedo;
    surface.diffuseColor = albedo;
    surface.specularColor = vec3(0.16 * Reflectance * Reflectance);
    surface.occlusion = HasMap(HasOcclusionMap) ? SampleMap(OcclusionMap, OcclusionMapTransform, OcclusionMapBias, uv).x : 1.0;
    surface.subsurface = 0.0;
    surface.transmittance = vec3(0.0);
    surface.shift = 0.0;
    surface.emission = emission;

    vec3 tangent;
    vec3 bitangent;
    BuildTangentFrame(vertexNormal, uv, tangent, bitangent);
    surface.strand = bitangent * inversesqrt(max(dot(bitangent, bitangent), 1e-20));

    vec2 tangentNormal = HasMap(HasNormalMap) ? DecodeNormal(SampleMap(NormalMap, NormalMapTransform, NormalMapBias, uv), NormalStrength) : vec2(0.0);
    float roughness = HasMap(HasRoughnessMap) ? SampleMap(RoughnessMap, RoughnessMapTransform, RoughnessMapBias, uv).x : MaterialRoughness;

#if defined(PBR_EYE)
    surface.specularColor = CorneaReflectance;
#else
    float metallic = 0.0;
    if (HasMap(HasSpecularMap))
    {
        surface.specularColor = SampleColorMap(SpecularMap, SpecularMapTransform, SpecularMapBias, SpecularMapDecodeSrgb, uv).rgb;
    }
#if !defined(PBR_HAIR)
    else
    {
        metallic = clamp(HasMap(HasMetallicMap) ? SampleMap(MetallicMap, MetallicMapTransform, MetallicMapBias, uv).x : MaterialMetallic, 0.0, 1.0);
    }

    if (HasMap(HasDetailNormalMap))
    {
        tangentNormal += DecodeNormal(SampleMap(DetailNormalMap, DetailNormalMapTransform, DetailNormalMapBias, TextureCoordinate * DetailNormalTiling), DetailNormalStrength);
    }

    surface.subsurface = clamp(SubsurfaceStrength * (HasMap(HasSubsurfaceMap) ? SampleMap(SubsurfaceMap, SubsurfaceMapTransform, SubsurfaceMapBias, uv).x : 1.0), 0.0, 1.0);
#else
    surface.shift = HairShift + (HasMap(HasHairShiftMap) ? SampleMap(HairShiftMap, HairShiftMapTransform, HairShiftMapBias, uv).x : 0.0);
    if (HasMap(HasHairFlowMap))
    {
        vec2 flow = SampleMap(HairFlowMap, HairFlowMapTransform, HairFlowMapBias, uv).xy * 2.0 - 1.0;
        if (dot(flow, flow) > 0.01)
        {
            surface.strand = normalize(tangent * flow.x + bitangent * flow.y);
        }
    }
#endif

    float thinness = HasMap(HasTranslucencyMap) ? SampleMap(TranslucencyMap, TranslucencyMapTransform, TranslucencyMapBias, uv).x : 1.0;
    surface.transmittance = TranslucencyStrength * exp(-(1.0 - clamp(thinness, 0.0, 1.0)) * 4.0 / max(SubsurfaceColor, vec3(0.01)));
    surface.specularColor = mix(surface.specularColor, albedo, metallic);
    surface.diffuseColor = albedo * (1.0 - metallic);
#endif

    surface.normal = PerturbNormal(tangentNormal, tangent, bitangent, surface.geometryNormal);
    surface.diffuseNormal = surface.normal;

#if defined(PBR_EYE)
    if (HasMap(HasIrisNormalMap))
    {
        surface.diffuseNormal = PerturbNormal(DecodeNormal(SampleMap(IrisNormalMap, IrisNormalMapTransform, IrisNormalMapBias, uv), NormalStrength), tangent, bitangent, surface.geometryNormal);
    }
#endif

    float variance = 0.0;
#if !defined(PBR_HAIR)
    vec3 normalX = dFdx(surface.geometryNormal);
    vec3 normalY = dFdy(surface.geometryNormal);
    variance = min((dot(normalX, normalX) + dot(normalY, normalY)) / PI, MaximumSpecularAntiAliasing);
#endif
    roughness = clamp(roughness, 0.0, 1.0);
    surface.roughness = clamp(sqrt(roughness * roughness + variance), MinimumRoughness, 1.0);
    surface.alpha = surface.roughness * surface.roughness;

    return surface;
}

Lighting ShadeSurface(Surface surface)
{
    vec3 viewDirection = normalize(CameraPosition - WorldPosition);
    float nDotV = clamp(dot(surface.normal, viewDirection), 0.0, 1.0);
    float occlusion = ScreenSpaceOcclusion();
#if defined(PBR_HAIR)
    occlusion = mix(1.0, occlusion, HairOcclusion);
#endif
    occlusion *= surface.occlusion;

    Lighting lighting = Lighting(vec3(0.0), vec3(0.0));
    int count = UseViewBasedLighting != 0 ? 1 : clamp(LightCount, 0, MAX_LIGHTS);
    for (int i = 0; i < count; i++)
    {
        vec3 lightDirection = UseViewBasedLighting != 0 ? viewDirection : normalize(-LightDirectionsAndIntensity[i].xyz);
        vec3 lightColor = UseViewBasedLighting != 0 ? vec3(0.9) : LightColors[i] * LightDirectionsAndIntensity[i].w;
        Lighting light = EvaluateLight(surface, lightDirection, viewDirection);
        lighting.diffuse += lightColor * PI * light.diffuse;
        lighting.specular += lightColor * PI * light.specular;
    }

    float reflectionBlend = (1.0 - surface.roughness) * (sqrt(1.0 - surface.roughness) + surface.roughness);
    vec3 reflection = normalize(mix(surface.normal, reflect(-viewDirection, surface.normal), reflectionBlend));
    vec3 specularEnergy = SpecularEnergy(surface, nDotV);
    float specularOcclusion = clamp(pow(nDotV + occlusion, exp2(-16.0 * surface.alpha - 1.0)) - 1.0 + occlusion, 0.0, 1.0);

    vec3 unshadowedDiffuse = SampleEnvironment(surface.diffuseNormal, 1.0);
    vec3 unshadowedSpecular = SampleEnvironment(reflection, surface.roughness);
    vec3 blockedDiffuse = vec3(0.0);
    vec3 blockedSpecular = vec3(0.0);
    int environmentLightCount = clamp(EnvironmentLightCount, 0, MAX_LIGHTS);
    int shadowCount = min(ShadowCount, environmentLightCount);
    for (int i = 0; i < environmentLightCount; i++)
    {
        vec3 lightDirection = EnvironmentLightDirections[i].xyz;
        float radius = EnvironmentLightDirections[i].w;
        float visibility = i < shadowCount ? ShadowVisibility(i, WorldPosition, surface.geometryNormal) : 1.0;
        vec3 blocked = EnvironmentLightPowers[i] * (1.0 - visibility);
        vec3 visiblePower = EnvironmentLightPowers[i] * visibility * occlusion;
        blockedDiffuse += blocked / PI * max(DiscLightCosine(dot(surface.diffuseNormal, lightDirection), radius), 0.0);
        blockedSpecular += blocked * EnvironmentLobeShare(reflection, lightDirection, surface.alpha, radius);
        lighting.diffuse += visiblePower * TransmittedLight(surface, lightDirection);
#if defined(PBR_HAIR)
        lighting.specular += visiblePower * HairHighlights(surface, lightDirection, viewDirection);
#endif
    }

    vec3 luminanceWeights = vec3(0.2126, 0.7152, 0.0722);
    vec3 shadowedDiffuse = max(unshadowedDiffuse - blockedDiffuse, unshadowedDiffuse * ShadowFloor);
    float diffuseVisibility = dot(shadowedDiffuse, luminanceWeights) / max(dot(unshadowedDiffuse, luminanceWeights), 0.0001);
    vec3 environmentSpecular = max(unshadowedSpecular - blockedSpecular, unshadowedSpecular * ShadowFloor);
    vec3 scatterBleed = clamp(surface.subsurface * SubsurfaceColor * 0.3, 0.0, 1.0);
    vec3 environmentDiffuse = mix(unshadowedDiffuse * MultiBounce(diffuseVisibility, surface.albedo), unshadowedDiffuse, scatterBleed);

    lighting.diffuse += environmentDiffuse * surface.diffuseColor * clamp(1.0 - specularEnergy, 0.0, 1.0) * MultiBounce(occlusion, surface.albedo);
#if defined(PBR_HAIR)
    lighting.specular += shadowedDiffuse * surface.specularColor * specularOcclusion * HairSpecularStrength;
#else
    lighting.specular += environmentSpecular * specularEnergy * specularOcclusion;
#endif
    return lighting;
}

void WriteColor(vec3 surfaceColor, vec3 litColor, float opacity)
{
#if defined(PBR_TRANSPARENT)
    FragColor = vec4(surfaceColor * opacity + litColor - surfaceColor, opacity);
#else
    FragColor = vec4(litColor, 1.0);
#endif
}

void main()
{
    vec2 uv = TextureCoordinate * TextureTiling;
    vec4 diffuse = HasMap(HasDiffuseMap) ? SampleColorMap(DiffuseMap, DiffuseMapTransform, DiffuseMapBias, DiffuseMapDecodeSrgb, uv) : vec4(1.0);
    float opacity = diffuse.a;
    vec2 opacitySize = vec2(1.0);

    if (HasMap(HasOpacityMap))
    {
        opacity = SampleMap(OpacityMap, OpacityMapTransform, OpacityMapBias, uv).x;
        opacitySize = vec2(textureSize(OpacityMap, 0));
    }
    else if (HasMap(HasDiffuseMap))
    {
        opacitySize = vec2(textureSize(DiffuseMap, 0));
    }

    opacity = clamp(opacity * BaseColor.a, 0.0, 1.0);
    ApplyAlphaTest(opacity, uv, opacitySize);

    if (ShadowPass != 0 || DepthPrepass != 0)
    {
        FragColor = vec4(0.0);
        return;
    }

    vec3 albedo = max(diffuse.rgb * BaseColor.rgb, 0.0);
    if (UseFlatShading != 0)
    {
        vec3 flatColor = LinearToSrgb(clamp(albedo, 0.0, 1.0));
        WriteColor(flatColor, flatColor, opacity);
        return;
    }

    vec3 emission = HasMap(HasEmissiveMap) ? max(SampleColorMap(EmissiveMap, EmissiveMapTransform, EmissiveMapBias, EmissiveMapDecodeSrgb, uv).rgb * EmissiveColor.rgb, 0.0) : vec3(0.0);

#if defined(PBR_UNLIT)
    vec3 unlitColor = EncodeDisplayColor(albedo + emission);
    WriteColor(unlitColor, unlitColor, opacity);
#else
    Surface surface = ReadSurface(uv, albedo, emission);
    Lighting lighting = ShadeSurface(surface);
    WriteColor(EncodeDisplayColor(lighting.diffuse + surface.emission), EncodeDisplayColor(lighting.diffuse + surface.emission + lighting.specular), opacity);
#endif
}
