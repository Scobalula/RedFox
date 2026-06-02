namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// Describes a single animation sequence stored in a PSA file's <c>ANIMINFO</c> chunk.
/// </summary>
/// <param name="Name">The sequence name.</param>
/// <param name="Group">The sequence group name.</param>
/// <param name="TotalBones">The number of bones each frame stores keys for.</param>
/// <param name="RootInclude">Whether the root bone is included; unused by most tools.</param>
/// <param name="KeyCompressionStyle">The key compression style; unused for raw keys.</param>
/// <param name="KeyQuotum">The key quotum; unused for raw keys.</param>
/// <param name="KeyReduction">The key reduction factor; unused for raw keys.</param>
/// <param name="TrackTime">The total track time, typically the frame count.</param>
/// <param name="AnimRate">The playback rate in frames per second.</param>
/// <param name="StartBone">The first bone index; usually zero.</param>
/// <param name="FirstRawFrame">The offset of this sequence's first frame within the shared key array.</param>
/// <param name="RawFrameCount">The number of frames in this sequence.</param>
public readonly record struct ActorXAnimInfo(
    string Name,
    string Group,
    int TotalBones,
    int RootInclude,
    int KeyCompressionStyle,
    int KeyQuotum,
    float KeyReduction,
    float TrackTime,
    float AnimRate,
    int StartBone,
    int FirstRawFrame,
    int RawFrameCount);
