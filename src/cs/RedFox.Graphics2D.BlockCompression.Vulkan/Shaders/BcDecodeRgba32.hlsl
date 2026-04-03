//--------------------------------------------------------------------------------------
// File: BcDecodeRgba32.hlsl
//
// Vulkan compute shader for decoding BC-compressed textures into tightly packed
// RGBA32F output buffers.
//--------------------------------------------------------------------------------------

[[vk::binding(2, 0)]] cbuffer cbDecode : register(b0)
{
    uint g_width;
    uint g_height;
};

[[vk::binding(0, 0)]] Texture2D<float4> g_Input : register(t0);
[[vk::binding(1, 0)]] RWStructuredBuffer<float4> g_Output : register(u0);

[numthreads(8, 8, 1)]
void DecodeMain(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= g_width || dispatchThreadId.y >= g_height)
    {
        return;
    }

    const uint index = dispatchThreadId.y * g_width + dispatchThreadId.x;
    g_Output[index] = g_Input.Load(int3(dispatchThreadId.xy, 0));
}
