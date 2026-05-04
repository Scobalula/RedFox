cbuffer LightingConstants : register(b1)
{
    float3 AmbientColor;
    int LightCount;
    float4 LightDirectionsAndIntensity[4];
    float3 LightColors[4];
    float3 CameraPosition;
    int UseViewBasedLighting;
    int HasDiffuseMap;
    int UVLayerCount;
    float2 MaterialTexturePadding;
    float4 BaseColor;
    float MaterialSpecularStrength;
    float MaterialSpecularPower;
};

Texture2D DiffuseMap : register(t15);
SamplerState DiffuseMapSampler : register(s15);

struct PSInput
{
    float4 Position : SV_Position;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float2 TextureCoordinate : TEXCOORD2;
};

float4 Main(PSInput input) : SV_Target
{
    float3 normal = normalize(input.WorldNormal);
    float3 viewDirection = normalize(CameraPosition - input.WorldPosition);
    float3 ambient = AmbientColor;
    float4 surfaceColor = BaseColor;

    if (HasDiffuseMap != 0 && UVLayerCount > 0)
    {
        surfaceColor *= DiffuseMap.Sample(DiffuseMapSampler, input.TextureCoordinate);
    }

    if (UseViewBasedLighting != 0)
    {
        float facing = max(dot(normal, viewDirection), 0.0f);
        float3 lit = (ambient + float3(facing, facing, facing)) * surfaceColor.rgb;
        return float4(lit, surfaceColor.a);
    }

    float3 diffuse = float3(0.0f, 0.0f, 0.0f);
    float3 specular = float3(0.0f, 0.0f, 0.0f);
    int count = clamp(LightCount, 0, 4);
    for (int lightIndex = 0; lightIndex < count; lightIndex++)
    {
        float3 lightDirection = normalize(-LightDirectionsAndIntensity[lightIndex].xyz);
        float lightIntensity = LightDirectionsAndIntensity[lightIndex].w;
        float normalDotLight = max(dot(normal, lightDirection), 0.0f);
        diffuse += LightColors[lightIndex] * (lightIntensity * normalDotLight);

        if (normalDotLight > 0.0f)
        {
            float3 reflected = reflect(-lightDirection, normal);
            float spec = pow(max(dot(viewDirection, reflected), 0.0f), MaterialSpecularPower);
            specular += LightColors[lightIndex] * (lightIntensity * spec * MaterialSpecularStrength);
        }
    }

    float3 outputColor = ((ambient + diffuse) * surfaceColor.rgb) + specular;
    return float4(outputColor, surfaceColor.a);
}