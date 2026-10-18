namespace RedFox.Graphics3D.Formats.Wilt;

// Values are the little-endian FourCC of each section name, e.g. "META".
internal enum WiltSection : uint
{
    Meta			= 0x4154454D,
    Bones			= 0x454E4F42,
    Materials		= 0x4C54414D,
    Meshes			= 0x4853454D,
    Hair			= 0x52494148,
    IKHandles		= 0x44484B49,
    Constraints		= 0x54534E43,
    Animation		= 0x4D494E41,
    SkeletalTracks	= 0x4C454B53,
    MorphTracks		= 0x4850524D,
    Actions			= 0x4E544341,
}
