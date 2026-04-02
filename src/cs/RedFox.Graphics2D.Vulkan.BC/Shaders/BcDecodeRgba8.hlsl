//--------------------------------------------------------------------------------------
// File: BcDecodeRgba8.hlsl
//
// Vulkan compute shader for decoding BC-compressed textures into tightly packed
// RGBA8 output buffers.
//--------------------------------------------------------------------------------------

[[vk::binding(2, 0)]] cbuffer cbDecode : register(b0)
{
    uint g_width;
    uint g_height;
};

[[vk::binding(0, 0)]] Texture2D<float4> g_Input : register(t0);
[[vk::binding(1, 0)]] RWStructuredBuffer<uint> g_Output : register(u0);

[numthreads(8, 8, 1)]
void DecodeMain(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= g_width || dispatchThreadId.y >= g_height)
    {
        return;
    }

    const float4 pixel = g_Input.Load(int3(dispatchThreadId.xy, 0));
    const uint4 packed = (uint4)round(saturate(pixel) * 255.0f);
    const uint index = dispatchThreadId.y * g_width + dispatchThreadId.x;

    g_Output[index] =
        packed.x |
        (packed.y << 8) |
        (packed.z << 16) |
        (packed.w << 24);
}
