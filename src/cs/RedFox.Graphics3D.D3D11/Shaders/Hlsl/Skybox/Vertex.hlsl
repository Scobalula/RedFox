struct VSOutput
{
    float4 Position : SV_Position;
    float2 ClipPosition : TEXCOORD0;
};

VSOutput Main(uint vertexId : SV_VertexID)
{
    float2 positions[3] =
    {
        float2(-1.0f, -1.0f),
        float2(-1.0f, 3.0f),
        float2(3.0f, -1.0f),
    };

    VSOutput output;
    output.ClipPosition = positions[vertexId];
    output.Position = float4(output.ClipPosition, 1.0f, 1.0f);
    return output;
}