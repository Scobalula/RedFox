using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace RedFox.Graphics3D.D3D11;

internal struct D3D11ConstantBufferSlot
{
    public ComPtr<ID3D11Buffer> Buffer;
    public byte[]? StagingData;
    public int SizeBytes;
}