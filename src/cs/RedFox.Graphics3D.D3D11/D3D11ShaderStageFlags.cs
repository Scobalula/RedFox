using System;

namespace RedFox.Graphics3D.D3D11;

[Flags]
internal enum D3D11ShaderStageFlags
{
    None = 0,
    Vertex = 1,
    Fragment = 2,
    Compute = 4,
}