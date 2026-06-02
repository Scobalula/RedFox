namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// Well-known chunk identifiers used by the ActorX PSK/PSKX (mesh) and PSA (animation) file formats.
/// Each identifier occupies a fixed 20-byte, null-padded field in an <see cref="ActorXChunkHeader"/>.
/// </summary>
public static class ActorXChunkId
{
    /// <summary>The leading header chunk of a PSK/PSKX mesh file.</summary>
    public const string MeshHeader = "ACTRHEAD";

    /// <summary>The leading header chunk of a PSA animation file.</summary>
    public const string AnimationHeader = "ANIMHEAD";

    /// <summary>Vertex positions (<see cref="System.Numerics.Vector3"/> per point).</summary>
    public const string Points = "PNTS0000";

    /// <summary>Wedges (texture vertices) with 16-bit point indices.</summary>
    public const string Wedges = "VTXW0000";

    /// <summary>Wedges (texture vertices) with 32-bit point indices, used by PSKX when point counts exceed 65535.</summary>
    public const string WedgesExtended = "VTXW3200";

    /// <summary>Triangles with 16-bit wedge indices.</summary>
    public const string Faces = "FACE0000";

    /// <summary>Triangles with 32-bit wedge indices, used by PSKX when wedge counts exceed 65535.</summary>
    public const string FacesExtended = "FACE3200";

    /// <summary>Materials.</summary>
    public const string Materials = "MATT0000";

    /// <summary>Reference skeleton bones (PSK).</summary>
    public const string Bones = "REFSKELT";

    /// <summary>Raw per-point bone influences (skin weights).</summary>
    public const string Weights = "RAWWEIGHTS";

    /// <summary>Bone names and reference pose for an animation (PSA), sharing the bone record layout.</summary>
    public const string BoneNames = "BONENAMES";

    /// <summary>Animation sequence descriptors (PSA).</summary>
    public const string AnimationInfo = "ANIMINFO";

    /// <summary>Raw animation keys (PSA).</summary>
    public const string AnimationKeys = "ANIMKEYS";
}
