namespace RedFox.Graphics3D.Formats.Wilt;

internal enum WiltTextureUsage : byte
{
    None				= 0,
    Diffuse				= 1,
    Normal				= 2,
    Specular			= 3,
    Metallic			= 4,
    Gloss				= 5,
    Roughness			= 6,
    Emissive			= 7,
    EmissiveMask		= 8,
    AmbientOcclusion	= 9,
    Cavity				= 10,
    Anisotropy			= 11,
}
