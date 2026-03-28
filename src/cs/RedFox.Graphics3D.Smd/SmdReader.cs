using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Smd;

/// <summary>
/// Reads Valve SMD (.smd) text files and populates a <see cref="Scene"/> with parsed
/// skeleton, mesh, and animation data.
/// <para>
/// A <em>reference</em> SMD contains a <c>nodes</c>, a single-frame <c>skeleton</c>
/// (bind pose) and a <c>triangles</c> section.  The reader creates a <see cref="Skeleton"/>
/// hierarchy and a <see cref="Model"/> containing one <see cref="Mesh"/> per material group.
/// </para>
/// <para>
/// A <em>sequence</em> (animation) SMD contains the same <c>nodes</c> section but a
/// multi-frame <c>skeleton</c> section and no <c>triangles</c> section.  When two or more
/// time frames are present, the reader additionally creates a <see cref="SkeletonAnimation"/>
/// with one <see cref="SkeletonAnimationTrack"/> per bone.
/// </para>
/// </summary>
public sealed class SmdReader
{
    private readonly Stream                _stream;
    private readonly string                _name;
    private readonly SceneTranslatorOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="SmdReader"/>.
    /// </summary>
    /// <param name="stream">
    /// The stream containing SMD text data.
    /// </param>
    /// <param name="name">
    /// The scene or file name used when creating nodes.
    /// </param>
    /// <param name="options">
    /// Options that control translator behaviour.
    /// </param>
    public SmdReader(Stream stream, string name, SceneTranslatorOptions options)
    {
        _stream  = stream;
        _name    = name;
        _options = options;
    }

    /// <summary>
    /// Parses the SMD stream and populates <paramref name="scene"/> with the
    /// resulting skeleton, mesh, and/or animation data.
    /// </summary>
    /// <param name="scene">
    /// The scene to populate.
    /// </param>
    public void Read(Scene scene)
    {
        var boneInfos     = new List<(int Index, string Name, int ParentIndex)>();
        var frameData     = new Dictionary<int, List<(int BoneIdx, Vector3 LocalPos, Quaternion LocalRot)>>();
        var triGroups     = new Dictionary<string, List<SmdVertex>>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(_stream, leaveOpen: true);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var span = line.AsSpan().Trim();
            if (span.IsEmpty || span.StartsWith("//") || span.StartsWith(";")) continue;
            if (span.StartsWith("version")) continue;

            if (span.Equals("nodes", StringComparison.Ordinal))
                ParseNodes(reader, boneInfos);
            else if (span.Equals("skeleton", StringComparison.Ordinal))
                ParseSkeleton(reader, frameData);
            else if (span.Equals("triangles", StringComparison.Ordinal))
                ParseTriangles(reader, triGroups);
        }

        if (boneInfos.Count == 0)
            return;

        // ---- Build bone objects ----
        var bones = new SkeletonBone[boneInfos.Count];
        foreach (var (idx, boneName, _) in boneInfos)
            bones[idx] = new SkeletonBone(boneName);

        // Apply bind-pose local transforms from frame 0
        if (frameData.TryGetValue(0, out var frame0))
        {
            foreach (var (boneIdx, localPos, localRot) in frame0)
            {
                if ((uint)boneIdx >= (uint)bones.Length) continue;
                bones[boneIdx].BindTransform.LocalPosition = localPos;
                bones[boneIdx].BindTransform.LocalRotation = localRot;
            }
        }

        // ---- Build skeleton hierarchy ----
        var skeleton = scene.RootNode.AddNode<Skeleton>($"{_name}_Skeleton");
        foreach (var (idx, _, parentIdx) in boneInfos)
        {
            if (parentIdx < 0)
                bones[idx].MoveTo(skeleton, ReparentTransformMode.PreserveExisting);
            else if ((uint)parentIdx < (uint)bones.Length)
                bones[idx].MoveTo(bones[parentIdx], ReparentTransformMode.PreserveExisting);
        }

        // ---- Build model + meshes from triangles ----
        if (triGroups.Count > 0)
        {
            var model         = scene.RootNode.AddNode<Model>(_name);
            int maxInfluences = ComputeMaxInfluences(triGroups);

            foreach (var (materialName, verts) in triGroups)
            {
                var mesh = model.AddNode<Mesh>(materialName);
                BuildMesh(mesh, verts, bones, maxInfluences);

                if (!string.IsNullOrWhiteSpace(materialName))
                {
                    var mat = model.AddNode<Material>(materialName);
                    mesh.Materials = [mat];
                }
            }
        }

        // ---- Build animation if multiple frames are present ----
        if (frameData.Count > 1)
        {
            var anim = BuildAnimation(frameData, bones);
            scene.RootNode.AddNode(anim);
        }
    }

    // ------------------------------------------------------------------
    // Section parsers
    // ------------------------------------------------------------------

    /// <summary>
    /// Parses the SMD nodes section into bone index, name, and parent tuples.
    /// </summary>
    /// <param name="reader">
    /// The text reader positioned after the nodes header.
    /// </param>
    /// <param name="boneInfos">
    /// The destination list of parsed node entries.
    /// </param>
    public static void ParseNodes(StreamReader reader, List<(int Index, string Name, int ParentIndex)> boneInfos)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var span = line.AsSpan().Trim();
            if (span.Equals("end", StringComparison.Ordinal)) break;
            if (span.IsEmpty || span.StartsWith("//")) continue;

            // Format:  index "name" parentIndex
            if (!TryParseInt(ref span, out int idx)) continue;
            SkipWhitespace(ref span);
            if (!TryParseQuotedString(span, out string name, out int charsConsumed)) continue;
            span = span[charsConsumed..].TrimStart();
            if (!TryParseInt(ref span, out int parentIdx)) continue;

            boneInfos.Add((idx, name, parentIdx));
        }
    }

    /// <summary>
    /// Parses the SMD skeleton section and records local transforms per frame.
    /// </summary>
    /// <param name="reader">
    /// The text reader positioned after the skeleton header.
    /// </param>
    /// <param name="frameData">
    /// The destination frame map.
    /// </param>
    public static void ParseSkeleton(StreamReader reader, Dictionary<int, List<(int, Vector3, Quaternion)>> frameData)
    {
        int currentFrame = -1;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var span = line.AsSpan().Trim();
            if (span.Equals("end", StringComparison.Ordinal)) break;
            if (span.IsEmpty || span.StartsWith("//")) continue;

            if (span.StartsWith("time"))
            {
                var rest = span[4..].TrimStart();
                if (TryParseInt(ref rest, out int frame))
                {
                    currentFrame = frame;
                    frameData.TryAdd(currentFrame, []);
                }
                continue;
            }

            if (currentFrame < 0) continue;

            // Format:  boneIndex  tx ty tz  rx ry rz
            if (!TryParseInt(ref span, out int boneIdx)) continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float tx)) continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float ty)) continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float tz)) continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float rx)) continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float ry)) continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float rz)) continue;

            var localPos = new Vector3(tx, ty, tz);
            var localRot = EulerXYZToQuaternion(rx, ry, rz);
            frameData[currentFrame].Add((boneIdx, localPos, localRot));
        }
    }

    /// <summary>
    /// Parses the SMD triangles section and groups vertices by material name.
    /// </summary>
    /// <param name="reader">
    /// The text reader positioned after the triangles header.
    /// </param>
    /// <param name="groups">
    /// The destination material to vertex list map.
    /// </param>
    public static void ParseTriangles(StreamReader reader, Dictionary<string, List<SmdVertex>> groups)
    {
        string currentMaterial = "default";

        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            var span = line.AsSpan().Trim();

            if (span.Equals("end", StringComparison.Ordinal))
                break;
            if (span.IsEmpty || span.StartsWith("//"))
                continue;

            if (!span.IsEmpty && !char.IsDigit(span[0]) && span[0] != '-')
            {
                currentMaterial = span.ToString();

                if (!groups.ContainsKey(currentMaterial))
                    groups.Add(currentMaterial, []);

                continue;
            }

            if (!TryParseInt(ref span, out int parentBone))
                continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float vx))
                continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float vy))
                continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float vz))
                continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float nx))
                continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float ny))
                continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float nz))
                continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float u))
                continue;
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float v))
                continue;
            SkipWhitespace(ref span);

            var links = Array.Empty<(int BoneIndex, float Weight)>();

            if (!span.IsEmpty && TryParseInt(ref span, out int numLinks) && numLinks > 0)
            {
                links = new (int BoneIndex, float Weight)[numLinks];
                for (int i = 0; i < numLinks; i++)
                {
                    SkipWhitespace(ref span);
                    if (!TryParseInt(ref span, out int bIdx)) break;
                    SkipWhitespace(ref span);
                    if (!TryParseFloat(ref span, out float weight)) break;
                    links[i] = (bIdx, weight);
                }
            }

            if (!groups.TryGetValue(currentMaterial, out var bucket))
            {
                bucket = [];
                groups.Add(currentMaterial, bucket);
            }

            bucket.Add(new SmdVertex(new Vector3(vx, vy, vz), new Vector3(nx, ny, nz), new Vector2(u, v), parentBone, links));
        }
    }

    /// <summary>
    /// Computes the maximum skin influence count used by any parsed vertex.
    /// </summary>
    /// <param name="groups">
    /// The parsed material groups.
    /// </param>
    /// <returns>
    /// The highest number of bone influences needed for mesh buffers.
    /// </returns>
    public static int ComputeMaxInfluences(Dictionary<string, List<SmdVertex>> groups)
    {
        int max = 0;
        foreach (var verts in groups.Values)
        {
            foreach (var vtx in verts)
            {
                int count = vtx.Links.Length > 0 ? vtx.Links.Length : 1;
                if (count > max) max = count;
            }
        }
        return max;
    }

    /// <summary>
    /// Builds mesh vertex, normal, UV, face, and optional skinning buffers from parsed SMD vertices.
    /// </summary>
    /// <param name="mesh">
    /// The destination mesh.
    /// </param>
    /// <param name="vertices">
    /// The source parsed SMD vertices.
    /// </param>
    /// <param name="allBones">
    /// The scene bone array.
    /// </param>
    /// <param name="maxInfluences">
    /// The maximum number of influences per vertex.
    /// </param>
    public static void BuildMesh(Mesh mesh, List<SmdVertex> vertices, SkeletonBone[] allBones, int maxInfluences)
    {
        int vertCount = vertices.Count;

        var positions = new DataBuffer<float>(vertCount, 1, 3);
        var normals   = new DataBuffer<float>(vertCount, 1, 3);
        var uvLayers  = new DataBuffer<float>(vertCount, 1, 2);

        bool hasSkinning = maxInfluences > 0;

        DataBuffer<int>?   boneIndices = hasSkinning ? new DataBuffer<int>(vertCount,   maxInfluences, 1) : null;
        DataBuffer<float>? boneWeights = hasSkinning ? new DataBuffer<float>(vertCount, maxInfluences, 1) : null;

        var span = CollectionsMarshal.AsSpan(vertices);
        for (int i = 0; i < span.Length; i++)
        {
            ref readonly var vtx = ref span[i];

            positions.Add(i, 0, 0, vtx.Position.X);
            positions.Add(i, 0, 1, vtx.Position.Y);
            positions.Add(i, 0, 2, vtx.Position.Z);

            normals.Add(i, 0, 0, vtx.Normal.X);
            normals.Add(i, 0, 1, vtx.Normal.Y);
            normals.Add(i, 0, 2, vtx.Normal.Z);

            uvLayers.Add(i, 0, 0, vtx.UV.X);
            uvLayers.Add(i, 0, 1, vtx.UV.Y);

            if (boneIndices is null || boneWeights is null) continue;

            if (vtx.Links.Length > 0)
            {
                for (int j = 0; j < maxInfluences; j++)
                {
                    int   boneIdx = j < vtx.Links.Length ? vtx.Links[j].BoneIndex : 0;
                    float weight  = j < vtx.Links.Length ? vtx.Links[j].Weight : 0f;
                    boneIndices.Add(i, j, 0, boneIdx);
                    boneWeights.Add(i, j, 0, weight);
                }
            }
            else
            {
                // Rigidly bound to parent bone (weight 1)
                int parentBoneIdx = Math.Clamp(vtx.ParentBone, 0, allBones.Length - 1);
                boneIndices.Add(i, 0, 0, parentBoneIdx);
                boneWeights.Add(i, 0, 0, 1f);
                for (int j = 1; j < maxInfluences; j++)
                {
                    boneIndices.Add(i, j, 0, 0);
                    boneWeights.Add(i, j, 0, 0f);
                }
            }
        }

        // Sequential face indices — one index per vertex position in order
        int faceIndexCount = (vertCount / 3) * 3;
        var faceIndices    = new DataBuffer<int>(faceIndexCount, 1, 1);
        for (int f = 0; f < faceIndexCount; f++)
            faceIndices.Add(f, 0, 0, f);

        mesh.Positions  = positions;
        mesh.Normals    = normals;
        mesh.UVLayers   = uvLayers;
        mesh.FaceIndices = faceIndices;

        if (boneIndices is not null && boneWeights is not null)
        {
            mesh.BoneIndices = boneIndices;
            mesh.BoneWeights = boneWeights;
            mesh.SkinnedBones = allBones;
        }
    }

    /// <summary>
    /// Builds a skeleton animation from parsed per-frame bone transforms.
    /// </summary>
    /// <param name="frameData">
    /// The parsed frame transform data.
    /// </param>
    /// <param name="bones">
    /// The skeleton bones.
    /// </param>
    /// <returns>
    /// A populated skeleton animation.
    /// </returns>
    public static SkeletonAnimation BuildAnimation(Dictionary<int, List<(int BoneIdx, Vector3 LocalPos, Quaternion LocalRot)>> frameData, SkeletonBone[] bones)
    {
        var anim = new SkeletonAnimation("Animation", null, bones.Length, TransformType.Absolute)
        {
            Framerate     = 30f,
            TransformType = TransformType.Absolute,
        };

        var tracks = new SkeletonAnimationTrack[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            tracks[i] = new SkeletonAnimationTrack(bones[i].Name)
            {
                TransformType  = TransformType.Absolute,
                TransformSpace = TransformSpace.Local,
            };
            anim.Tracks.Add(tracks[i]);
        }

        foreach (var (frame, boneTransforms) in frameData.OrderBy(kv => kv.Key))
        {
            foreach (var (boneIdx, localPos, localRot) in boneTransforms)
            {
                if ((uint)boneIdx >= (uint)tracks.Length) continue;
                tracks[boneIdx].AddTranslationFrame(frame, localPos);
                tracks[boneIdx].AddRotationFrame(frame, localRot);
            }
        }

        return anim;
    }

    /// <summary>
    /// Trims leading whitespace from a span.
    /// </summary>
    /// <param name="span">The span to trim.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SkipWhitespace(ref ReadOnlySpan<char> span)
    {
        span = span.TrimStart();
    }

    /// <summary>
    /// Tries to parse an integer token from the start of a span.
    /// </summary>
    /// <param name="span">The source span.</param>
    /// <param name="value">The parsed integer value when successful.</param>
    /// <returns><see langword="true"/> when an integer token is parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseInt(ref ReadOnlySpan<char> span, out int value)
    {
        int end = 0;
        if (end < span.Length && span[end] == '-') end++;
        while (end < span.Length && char.IsAsciiDigit(span[end])) end++;
        if (end == 0 || (end == 1 && span[0] == '-')) { value = 0; return false; }
        bool ok = int.TryParse(span[..end], out value);
        span = span[end..];
        return ok;
    }

    /// <summary>
    /// Tries to parse a floating point token from the start of a span.
    /// </summary>
    /// <param name="span">
    /// The source span.
    /// </param>
    /// <param name="value">
    /// The parsed float value when successful.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a float token is parsed; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryParseFloat(ref ReadOnlySpan<char> span, out float value)
    {
        int end = 0;
        if (end < span.Length && span[end] == '-') end++;
        while (end < span.Length && (char.IsAsciiDigit(span[end]) || span[end] == '.' || span[end] == 'e' || span[end] == 'E'))
        {
            if ((span[end] == 'e' || span[end] == 'E') && end + 1 < span.Length && (span[end + 1] == '+' || span[end + 1] == '-'))
                end++; // consume sign after exponent
            end++;
        }
        if (end == 0 || (end == 1 && span[0] == '-')) { value = 0; return false; }
        bool ok = float.TryParse(span[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        span = span[end..];
        return ok;
    }

    /// <summary>
    /// Tries to parse a quoted string token.
    /// </summary>
    /// <param name="span">
    /// The source span.
    /// </param>
    /// <param name="name">
    /// The parsed string value without quotes when successful.
    /// </param>
    /// <param name="charsConsumed">
    /// The number of consumed characters in the source span.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a quoted token is parsed; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryParseQuotedString(ReadOnlySpan<char> span, out string name, out int charsConsumed)
    {
        if (span.IsEmpty || span[0] != '"')
        {
            name = string.Empty;
            charsConsumed = 0;
            return false;
        }

        int closeIdx = span[1..].IndexOf('"');
        if (closeIdx < 0)
        {
            name = string.Empty;
            charsConsumed = 0;
            return false;
        }

        name = new string(span[1..(closeIdx + 1)]);
        charsConsumed = closeIdx + 2; // opening + closing quotes
        return true;
    }

    // ------------------------------------------------------------------
    // Rotation conversion
    // ------------------------------------------------------------------

    /// <summary>
    /// Converts SMD Euler angles (XYZ intrinsic order, radians) to a quaternion.
    /// Equivalent to: Q = Qz * Qy * Qx — applies X first, then Y, then Z.
    /// </summary>
    /// <summary>
    /// Converts SMD Euler angles in XYZ intrinsic order into a normalized quaternion.
    /// </summary>
    /// <param name="rx">
    /// Rotation around X in radians.
    /// </param>
    /// <param name="ry">
    /// Rotation around Y in radians.
    /// </param>
    /// <param name="rz">
    /// Rotation around Z in radians.
    /// </param>
    /// <returns>
    /// The normalized quaternion equivalent to the input Euler angles.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion EulerXYZToQuaternion(float rx, float ry, float rz)
    {
        var qx = Quaternion.CreateFromAxisAngle(Vector3.UnitX, rx);
        var qy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, ry);
        var qz = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rz);
        return Quaternion.Normalize(qz * qy * qx);
    }
}
