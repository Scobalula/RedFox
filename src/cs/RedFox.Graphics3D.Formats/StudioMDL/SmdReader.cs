using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.StudioMDL;

/// <summary>
/// Reads Valve SMD (.smd) text files and populates a <see cref="Scene"/> with parsed skeleton, mesh, and animation data.
/// </summary>
public sealed class SmdReader
{
    private readonly Stream	_stream;
    private readonly string	_name;

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
        _stream	= stream;
        _name	= name;
    }

    /// <summary>
    /// Parses the SMD stream and populates <paramref name="scene"/> with the resulting skeleton, mesh, and/or animation data.
    /// </summary>
    /// <param name="scene">
    /// The scene to populate.
    /// </param>
    public void Read(Scene scene) => Read(scene, CancellationToken.None);

    /// <summary>
    /// Reads data with cancellation support.
    /// </summary>
    /// <param name="scene">The scene to read.</param>
    /// <param name="token">The cancellation token.</param>
    public void Read(Scene scene, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var	boneInfos= new List<(int Index, string Name, int ParentIndex)>();
        var	frameData= new Dictionary<int, List<(int BoneIdx, Vector3 LocalPos, Quaternion LocalRot)>>();
        var	triGroups= new Dictionary<string, List<SmdVertex>>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(_stream, leaveOpen: true);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            token.ThrowIfCancellationRequested();
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
        {
            if (triGroups.Count > 0)
                throw new InvalidDataException("SMD triangles require a bone table.");
            return;
        }

        Dictionary<int, int> indices = [];
        for (int i = 0; i < boneInfos.Count; i++)
            if (boneInfos[i].Index < 0 || !indices.TryAdd(boneInfos[i].Index, i))
                throw new InvalidDataException("Invalid or duplicate SMD bone ID.");
        for (int i = 0; i < boneInfos.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var info	= boneInfos[i];
            int parent	= -1;
            if (info.ParentIndex != -1 && !indices.TryGetValue(info.ParentIndex, out parent))
                throw new InvalidDataException("Invalid SMD parent ID.");
            boneInfos[i] = (i, info.Name, parent);
        }
        foreach (var frame in frameData.Values)
        {
            token.ThrowIfCancellationRequested();
            HashSet<int> seen = [];
            for (int i = 0; i < frame.Count; i++)
            {
                var value = frame[i];
                if (!indices.TryGetValue(value.BoneIdx, out int index) || !seen.Add(index))
                    throw new InvalidDataException("Invalid or duplicate SMD pose bone ID.");
                frame[i] = (index, value.LocalPos, value.LocalRot);
            }
        }

        var bones = new SkeletonBone[boneInfos.Count];
        foreach (var (boneIndex, boneName, _) in boneInfos)
            bones[boneIndex] = new SkeletonBone(boneName);

        if (frameData.TryGetValue(0, out var frame0))
        {
            foreach (var (boneIdx, localPos, localRot) in frame0)
            {
                bones[boneIdx].BindTransform.LocalPosition = localPos;
                bones[boneIdx].BindTransform.LocalRotation = localRot;
            }
        }

        var skeleton = scene.RootNode.AddNode(new Skeleton($"{_name}_Skeleton"));
        foreach (var (boneIndex, _, parentBoneIndex) in boneInfos)
        {
            token.ThrowIfCancellationRequested();
            if (parentBoneIndex < 0)
                bones[boneIndex].MoveTo(skeleton, ReparentTransformMode.PreserveExisting);
            else
                bones[boneIndex].MoveTo(bones[parentBoneIndex], ReparentTransformMode.PreserveExisting);
        }

        if (triGroups.Count > 0)
        {
            var model			= scene.RootNode.AddNode<MeshGroup>(_name);

            foreach (var (materialName, verts) in triGroups)
            {
                var mesh			= model.AddNode<Mesh>(materialName);
                int maxInfluences	= verts.Max(static vertex => Math.Max(1, vertex.Links.Length));
                BuildMesh(mesh, verts, bones, maxInfluences, indices);

                if (!string.IsNullOrWhiteSpace(materialName))
                {
                    var mat = scene.RootNode.AddNode<Material>(materialName);
                    mesh.Materials = [mat];
                }
            }
        }

        if (frameData.Count > 1)
        {
            var anim = BuildAnimation(frameData, bones);
            scene.RootNode.AddNode(anim);
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
    internal static void ParseTriangles(StreamReader reader, Dictionary<string, List<SmdVertex>> groups)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            ReadOnlySpan<char> material = line.AsSpan().Trim();
            if (material.IsEmpty || material.StartsWith("//"))
                continue;
            if (material.SequenceEqual("end"))
                return;
            string name = material.ToString();
            if (!groups.TryGetValue(name, out List<SmdVertex>? vertices))
            {
                vertices = [];
                groups.Add(name, vertices);
            }

            for (int corner = 0; corner < 3; corner++)
            {
                line = reader.ReadLine() ?? throw new InvalidDataException("Incomplete SMD triangle.");
                ReadOnlySpan<char> span = line.AsSpan().Trim();
                while (span.IsEmpty || span.StartsWith("//"))
                {
                    line	= reader.ReadLine() ?? throw new InvalidDataException("Incomplete SMD triangle.");
                    span	= line.AsSpan().Trim();
                }
                int comment = span.IndexOf("//");
                if (comment >= 0)
                    span = span[..comment].TrimEnd();
                if (!TryParseInt(ref span, out int parent) || !TryParseFloat(ref span, out float x) || !TryParseFloat(ref span, out float y) || !TryParseFloat(ref span, out float z) || !TryParseFloat(ref span, out float nx) || !TryParseFloat(ref span, out float ny) || !TryParseFloat(ref span, out float nz) || !TryParseFloat(ref span, out float u) || !TryParseFloat(ref span, out float v))
                    throw new InvalidDataException("Malformed SMD triangle vertex.");

                (int BoneIndex, float Weight)[] links = [];
                span = span.Trim();
                if (!span.IsEmpty)
                {
                    if (!TryParseInt(ref span, out int count) || count < 0 || count > span.Length)
                        throw new InvalidDataException("Invalid SMD influence count.");
                    links = new (int, float)[count];
                    for (int i = 0; i < count; i++)
                    {
                        if (!TryParseInt(ref span, out int bone) || !TryParseFloat(ref span, out float weight) || !float.IsFinite(weight) || weight < 0)
                            throw new InvalidDataException("Incomplete SMD influence list.");
                        links[i] = (bone, weight);
                    }
                }
                if (!span.Trim().IsEmpty)
                    throw new InvalidDataException("Unexpected SMD vertex fields.");
                vertices.Add(new SmdVertex(new Vector3(x, y, z), new Vector3(nx, ny, nz), new Vector2(u, v), parent, links));
            }
        }
        throw new InvalidDataException("Unterminated SMD triangles section.");
    }

    private static void ParseNodes(StreamReader reader, List<(int Index, string Name, int ParentIndex)> boneInfos)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var span = line.AsSpan().Trim();
            if (span.Equals("end", StringComparison.Ordinal)) break;
            if (span.IsEmpty || span.StartsWith("//")) continue;

            if (!TryParseInt(ref span, out int boneIndex)) throw new InvalidDataException("Malformed SMD record.");
            SkipWhitespace(ref span);
            if (!TryParseQuotedString(span, out string name, out int charsConsumed)) throw new InvalidDataException("Malformed SMD record.");
            span = span[charsConsumed..].TrimStart();
            if (!TryParseInt(ref span, out int parentBoneIndex)) throw new InvalidDataException("Malformed SMD record.");

            boneInfos.Add((boneIndex, name, parentBoneIndex));
        }
    }

    private static void ParseSkeleton(StreamReader reader, Dictionary<int, List<(int, Vector3, Quaternion)>> frameData)
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

            if (!TryParseInt(ref span, out int boneIdx)) throw new InvalidDataException("Malformed SMD record.");
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float tx)) throw new InvalidDataException("Malformed SMD record.");
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float ty)) throw new InvalidDataException("Malformed SMD record.");
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float tz)) throw new InvalidDataException("Malformed SMD record.");
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float rx)) throw new InvalidDataException("Malformed SMD record.");
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float ry)) throw new InvalidDataException("Malformed SMD record.");
            SkipWhitespace(ref span);
            if (!TryParseFloat(ref span, out float rz)) throw new InvalidDataException("Malformed SMD record.");

            var localPos = new Vector3(tx, ty, tz);
            var localRot = EulerXYZToQuaternion(rx, ry, rz);
            frameData[currentFrame].Add((boneIdx, localPos, localRot));
        }
    }

    private static void BuildMesh(Mesh mesh, List<SmdVertex> vertices, SkeletonBone[] allBones, int maxInfluences, IReadOnlyDictionary<int, int> boneIds)
    {
        int vertCount = vertices.Count;

        var	positions	= new DataBuffer<float>(vertCount, 1, 3);
        var	normals		= new DataBuffer<float>(vertCount, 1, 3);
        var	uvLayers	= new DataBuffer<float>(vertCount, 1, 2);

        bool hasSkinning = maxInfluences > 0;

        DataBuffer<int>?	boneIndices= hasSkinning ? new DataBuffer<int>(vertCount,	maxInfluences, 1) : null;
        DataBuffer<float>?	boneWeights= hasSkinning ? new DataBuffer<float>(vertCount, maxInfluences, 1) : null;

        var span = CollectionsMarshal.AsSpan(vertices);
        for (int i = 0; i < span.Length; i++)
        {
            ref readonly var vtx = ref span[i];

            positions.Add(vtx.Position);
            normals.Add(vtx.Normal);
            uvLayers.Add(vtx.UV);

            if (boneIndices is null || boneWeights is null) continue;

            var boneIndexElement	= boneIndices.Add();
            var boneWeightElement	= boneWeights.Add();

            if (vtx.Links.Length > 0)
            {
                for (int j = 0; j < maxInfluences; j++)
                {
                    int		boneIdx	= j < vtx.Links.Length ? boneIds[vtx.Links[j].BoneIndex] : 0;
                    float	weight	= j < vtx.Links.Length ? vtx.Links[j].Weight : 0f;
                    boneIndexElement.Add(boneIdx);
                    boneWeightElement.Add(weight);
                }
            }
            else
            {
                int parentBoneIdx = boneIds[vtx.ParentBone];
                boneIndexElement.Add(parentBoneIdx);
                boneWeightElement.Add(1f);
                for (int j = 1; j < maxInfluences; j++)
                {
                    boneIndexElement.Add(0);
                    boneWeightElement.Add(0f);
                }
            }
        }

        int	faceIndexCount	= (vertCount / 3) * 3;
        var	faceIndices		= new DataBuffer<int>(faceIndexCount, 1, 1);
        for (int f = 0; f < faceIndexCount; f++)
            faceIndices.Add(f);

        mesh.Positions		= positions;
        mesh.Normals		= normals;
        mesh.UVLayers		= uvLayers;
        mesh.FaceIndices	= faceIndices;

        if (boneIndices is not null && boneWeights is not null)
            mesh.Skin = new Skin(allBones, boneIndices, boneWeights);

        MeshWelder.Weld(mesh, 0f);
    }

    private static SkeletonAnimation BuildAnimation(Dictionary<int, List<(int BoneIdx, Vector3 LocalPos, Quaternion LocalRot)>> frameData, SkeletonBone[] bones)
    {
        var anim = new SkeletonAnimation("Animation", bones.Length, TransformType.Absolute)
        {
            Framerate		= 30f,
            TransformType	= TransformType.Absolute,
        };

        var tracks = new SkeletonAnimationTrack[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            tracks[i] = new SkeletonAnimationTrack(bones[i].Name)
            {
                TransformType	= TransformType.Absolute,
                TransformSpace	= TransformSpace.Local,
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SkipWhitespace(ref ReadOnlySpan<char> span)
    {
        span = span.TrimStart();
    }

    private static bool TryParseInt(ref ReadOnlySpan<char> span, out int value)
    {
        span = span.TrimStart();
        int end = 0;
        while (end < span.Length && !char.IsWhiteSpace(span[end]))
            end++;
        bool parsed = int.TryParse(span[..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        span = span[end..];
        return parsed;
    }

    private static bool TryParseFloat(ref ReadOnlySpan<char> span, out float value)
    {
        span = span.TrimStart();
        int end = 0;
        while (end < span.Length && !char.IsWhiteSpace(span[end]))
            end++;
        bool parsed = float.TryParse(span[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        span = span[end..];
        return parsed && float.IsFinite(value);
    }

    private static bool TryParseQuotedString(ReadOnlySpan<char> span, out string name, out int charsConsumed)
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

        name			= new string(span[1..(closeIdx + 1)]);
        charsConsumed	= closeIdx + 2;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Quaternion EulerXYZToQuaternion(float rx, float ry, float rz)
    {
        var qx = Quaternion.CreateFromAxisAngle(Vector3.UnitX, rx);
        var qy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, ry);
        var qz = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rz);
        return Quaternion.Normalize(qz * qy * qx);
    }
}
