using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;

namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Reads id Tech 4 MD5 mesh (<c>.md5mesh</c>) text files and populates a <see cref="Scene"/>
/// with the parsed skeleton and skinned mesh data.
/// <para>
/// An MD5 mesh file contains a skeleton hierarchy expressed as joints with object-space
/// bind-pose transforms and one or more meshes.  Each mesh stores vertices, triangle
/// indices, and a weight table that binds vertices to joints.  Vertex positions are
/// computed by accumulating weighted joint-local offsets transformed into object space.
/// </para>
/// </summary>
public sealed class Md5MeshReader
{
    private readonly Stream _stream;
    private readonly string _name;
    private readonly SceneTranslatorOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="Md5MeshReader"/>.
    /// </summary>
    /// <param name="stream">The stream containing MD5 mesh text data.</param>
    /// <param name="name">The scene or file name used when creating nodes.</param>
    /// <param name="options">Options that control translator behaviour.</param>
    public Md5MeshReader(Stream stream, string name, SceneTranslatorOptions options)
    {
        _stream = stream;
        _name = name;
        _options = options;
    }

    /// <summary>
    /// Parses the MD5 mesh stream and populates <paramref name="scene"/> with the
    /// resulting skeleton and skinned mesh data.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the file header is invalid or the data is malformed.
    /// </exception>
    public void Read(Scene scene)
    {
        using var sr = new StreamReader(_stream, leaveOpen: true);
        string text = sr.ReadToEnd();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var tok = new TextTokenReader(text.AsSpan());

        int numJoints = 0;
        int numMeshes = 0;
        var joints = Array.Empty<Md5Joint>();
        var meshDatas = new List<(string Shader, Md5Vertex[] Verts, int[] Tris, Md5Weight[] Weights)>();

        while (tok.TryReadToken(out var token))
        {
            if (token.SequenceEqual("MD5Version"))
            {
                if (!tok.TryReadInt(out int version) || version != Md5Format.Version)
                    throw new InvalidDataException("Unsupported MD5 version: expected 10.");
                continue;
            }

            if (token.SequenceEqual("commandline"))
            {
                tok.SkipRestOfLine();
                continue;
            }

            if (token.SequenceEqual("numJoints"))
            {
                if (tok.TryReadInt(out int nj)) numJoints = nj;
                continue;
            }

            if (token.SequenceEqual("numMeshes"))
            {
                if (tok.TryReadInt(out int nm)) numMeshes = nm;
                continue;
            }

            if (token.SequenceEqual("joints"))
            {
                if (!tok.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after joints keyword.");
                joints = ParseJoints(ref tok, numJoints);
                continue;
            }

            if (token.SequenceEqual("mesh"))
            {
                if (!tok.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after mesh keyword.");
                meshDatas.Add(ParseMesh(ref tok));
            }
        }

        if (joints.Length == 0)
            return;

        // Build skeleton with local transforms from world-space joints
        var bones = new SkeletonBone[joints.Length];
        for (int i = 0; i < joints.Length; i++)
            bones[i] = new SkeletonBone(joints[i].Name);

        for (int i = 0; i < joints.Length; i++)
        {
            ref readonly var joint = ref joints[i];
            int parentIdx = joint.ParentIndex;

            if (parentIdx >= 0 && (uint)parentIdx < (uint)joints.Length)
            {
                ref readonly var parent = ref joints[parentIdx];
                var invParentRot = Quaternion.Conjugate(parent.Orientation);
                bones[i].BindTransform.LocalPosition = Vector3.Transform(joint.Position - parent.Position, invParentRot);
                bones[i].BindTransform.LocalRotation = Quaternion.Normalize(invParentRot * joint.Orientation);
            }
            else
            {
                bones[i].BindTransform.LocalPosition = joint.Position;
                bones[i].BindTransform.LocalRotation = joint.Orientation;
            }
        }

        var skeleton = scene.RootNode.AddNode<Skeleton>($"{_name}_Skeleton");
        for (int i = 0; i < joints.Length; i++)
        {
            int parentIdx = joints[i].ParentIndex;
            if (parentIdx < 0)
                bones[i].MoveTo(skeleton, ReparentTransformMode.PreserveExisting);
            else if ((uint)parentIdx < (uint)bones.Length)
                bones[i].MoveTo(bones[parentIdx], ReparentTransformMode.PreserveExisting);
        }

        if (meshDatas.Count > 0)
        {
            var model = scene.RootNode.AddNode<Model>(_name);

            foreach (var (shader, verts, tris, weights) in meshDatas)
            {
                string meshName = !string.IsNullOrWhiteSpace(shader) ? shader : "default";
                var mesh = model.AddNode<Mesh>(meshName);
                BuildMesh(mesh, verts, tris, weights, joints, bones);

                if (!string.IsNullOrWhiteSpace(shader))
                {
                    var mat = model.AddNode<Material>(shader);
                    mesh.Materials = [mat];
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Section parsers
    // ------------------------------------------------------------------

    /// <summary>
    /// Parses the <c>joints { }</c> block from the tokenizer.
    /// The opening brace must already have been consumed.
    /// </summary>
    /// <param name="tok">The tokenizer, advanced past the consumed content on return.</param>
    /// <param name="capacity">The expected number of joints.</param>
    /// <returns>An array of parsed <see cref="Md5Joint"/> entries.</returns>
    public static Md5Joint[] ParseJoints(ref TextTokenReader tok, int capacity)
    {
        var joints = new Md5Joint[capacity];
        int count = 0;

        while (!tok.IsEmpty)
        {
            if (tok.TryExpect('}'))
                break;

            if (!tok.TryReadQuotedString(out var nameSpan)) continue;
            string name = new(nameSpan);

            if (!tok.TryReadInt(out int parentIdx)) continue;
            if (!tok.TryExpect('(')) continue;
            if (!tok.TryReadFloat(out float px)) continue;
            if (!tok.TryReadFloat(out float py)) continue;
            if (!tok.TryReadFloat(out float pz)) continue;
            if (!tok.TryExpect(')')) continue;
            if (!tok.TryExpect('(')) continue;
            if (!tok.TryReadFloat(out float qx)) continue;
            if (!tok.TryReadFloat(out float qy)) continue;
            if (!tok.TryReadFloat(out float qz)) continue;
            if (!tok.TryExpect(')')) continue;

            tok.SkipRestOfLine();

            if ((uint)count < (uint)joints.Length)
                joints[count] = new Md5Joint(name, parentIdx, new Vector3(px, py, pz), Md5Format.ComputeQuaternion(qx, qy, qz));
            count++;
        }

        return joints;
    }

    /// <summary>
    /// Parses a single <c>mesh { }</c> block from the tokenizer.
    /// The opening brace must already have been consumed.
    /// </summary>
    /// <param name="tok">The tokenizer, advanced past the consumed content on return.</param>
    /// <returns>A tuple containing the shader name, vertex array, triangle index array, and weight array.</returns>
    public static (string Shader, Md5Vertex[] Verts, int[] Tris, Md5Weight[] Weights) ParseMesh(ref TextTokenReader tok)
    {
        string shader = "default";
        Md5Vertex[] verts = [];
        int[] tris = [];
        Md5Weight[] weights = [];

        while (!tok.IsEmpty)
        {
            if (tok.TryExpect('}'))
                break;

            if (!tok.TryReadToken(out var keyword))
                break;

            if (keyword.SequenceEqual("shader"))
            {
                if (tok.TryReadQuotedString(out var s))
                    shader = new string(s);
                continue;
            }

            if (keyword.SequenceEqual("numverts"))
            {
                if (tok.TryReadInt(out int nv)) verts = new Md5Vertex[nv];
                continue;
            }

            if (keyword.SequenceEqual("numtris"))
            {
                if (tok.TryReadInt(out int nt)) tris = new int[nt * 3];
                continue;
            }

            if (keyword.SequenceEqual("numweights"))
            {
                if (tok.TryReadInt(out int nw)) weights = new Md5Weight[nw];
                continue;
            }

            if (keyword.SequenceEqual("vert"))
            {
                if (!tok.TryReadInt(out int idx)) continue;
                if ((uint)idx >= (uint)verts.Length) { tok.SkipRestOfLine(); continue; }
                if (!tok.TryExpect('(')) continue;
                if (!tok.TryReadFloat(out float u)) continue;
                if (!tok.TryReadFloat(out float v)) continue;
                if (!tok.TryExpect(')')) continue;
                if (!tok.TryReadInt(out int wIdx)) continue;
                if (!tok.TryReadInt(out int wCount)) continue;
                verts[idx] = new Md5Vertex(new Vector2(u, v), wIdx, wCount);
                continue;
            }

            if (keyword.SequenceEqual("tri"))
            {
                if (!tok.TryReadInt(out int triIdx)) continue;
                int baseIdx = triIdx * 3;
                if ((uint)(baseIdx + 2) >= (uint)tris.Length) { tok.SkipRestOfLine(); continue; }
                if (!tok.TryReadInt(out int v0)) continue;
                if (!tok.TryReadInt(out int v1)) continue;
                if (!tok.TryReadInt(out int v2)) continue;
                tris[baseIdx] = v0;
                tris[baseIdx + 1] = v1;
                tris[baseIdx + 2] = v2;
                continue;
            }

            if (keyword.SequenceEqual("weight"))
            {
                if (!tok.TryReadInt(out int idx)) continue;
                if ((uint)idx >= (uint)weights.Length) { tok.SkipRestOfLine(); continue; }
                if (!tok.TryReadInt(out int jointIdx)) continue;
                if (!tok.TryReadFloat(out float bias)) continue;
                if (!tok.TryExpect('(')) continue;
                if (!tok.TryReadFloat(out float wx)) continue;
                if (!tok.TryReadFloat(out float wy)) continue;
                if (!tok.TryReadFloat(out float wz)) continue;
                if (!tok.TryExpect(')')) continue;
                weights[idx] = new Md5Weight(jointIdx, bias, new Vector3(wx, wy, wz));
            }
        }

        return (shader, verts, tris, weights);
    }

    // ------------------------------------------------------------------
    // Mesh construction
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds vertex position, UV, normal, face-index, and skinning buffers for a
    /// <see cref="Mesh"/> from parsed MD5 mesh data.
    /// <para>
    /// Vertex positions are computed by accumulating weighted joint-local offsets
    /// transformed into object space using the bind-pose joint orientations.
    /// Normals are computed from the resulting triangle geometry.
    /// </para>
    /// </summary>
    /// <param name="mesh">The destination mesh node.</param>
    /// <param name="verts">The parsed MD5 vertices.</param>
    /// <param name="tris">The parsed triangle indices (3 ints per triangle).</param>
    /// <param name="weights">The parsed weight table.</param>
    /// <param name="joints">The parsed bind-pose joints.</param>
    /// <param name="bones">The skeleton bone array aligned with joints.</param>
    public static void BuildMesh(Mesh mesh, Md5Vertex[] verts, int[] tris, Md5Weight[] weights, Md5Joint[] joints, SkeletonBone[] bones)
    {
        int vertCount = verts.Length;
        int triCount = tris.Length / 3;
        int indexCount = triCount * 3;

        int maxInfluences = 0;
        for (int i = 0; i < vertCount; i++)
        {
            if (verts[i].WeightCount > maxInfluences)
                maxInfluences = verts[i].WeightCount;
        }

        var positions = new DataBuffer<float>(vertCount, 1, 3);
        var uvLayers = new DataBuffer<float>(vertCount, 1, 2);
        var boneIndices = maxInfluences > 0 ? new DataBuffer<int>(vertCount, maxInfluences, 1) : null;
        var boneWeights = maxInfluences > 0 ? new DataBuffer<float>(vertCount, maxInfluences, 1) : null;

        for (int i = 0; i < vertCount; i++)
        {
            ref readonly var vert = ref verts[i];
            var finalPos = Vector3.Zero;

            for (int w = 0; w < vert.WeightCount; w++)
            {
                int wIdx = vert.WeightIndex + w;
                if ((uint)wIdx >= (uint)weights.Length) continue;

                ref readonly var weight = ref weights[wIdx];
                if ((uint)weight.JointIndex >= (uint)joints.Length) continue;

                ref readonly var joint = ref joints[weight.JointIndex];
                var rotatedPos = Vector3.Transform(weight.Position, joint.Orientation);
                finalPos += weight.Bias * (joint.Position + rotatedPos);

                if (boneIndices is not null && boneWeights is not null && w < maxInfluences)
                {
                    boneIndices.Add(i, w, 0, weight.JointIndex);
                    boneWeights.Add(i, w, 0, weight.Bias);
                }
            }

            if (boneIndices is not null && boneWeights is not null)
            {
                for (int w = vert.WeightCount; w < maxInfluences; w++)
                {
                    boneIndices.Add(i, w, 0, 0);
                    boneWeights.Add(i, w, 0, 0f);
                }
            }

            positions.Add(i, 0, 0, finalPos.X);
            positions.Add(i, 0, 1, finalPos.Y);
            positions.Add(i, 0, 2, finalPos.Z);

            uvLayers.Add(i, 0, 0, vert.UV.X);
            uvLayers.Add(i, 0, 1, vert.UV.Y);
        }

        var faceIndices = new DataBuffer<int>(indexCount, 1, 1);
        for (int i = 0; i < indexCount; i++)
            faceIndices.Add(i, 0, 0, tris[i]);

        var normals = new DataBuffer<float>(vertCount, 1, 3);
        ComputeNormals(positions, faceIndices, triCount, vertCount, normals);

        mesh.Positions = positions;
        mesh.Normals = normals;
        mesh.UVLayers = uvLayers;
        mesh.FaceIndices = faceIndices;

        if (boneIndices is not null && boneWeights is not null)
        {
            mesh.BoneIndices = boneIndices;
            mesh.BoneWeights = boneWeights;
            mesh.SkinnedBones = bones;
        }
    }

    /// <summary>
    /// Computes per-vertex normals by accumulating face normals and normalizing.
    /// </summary>
    /// <param name="positions">The vertex position buffer.</param>
    /// <param name="faceIndices">The face index buffer.</param>
    /// <param name="triCount">The number of triangles.</param>
    /// <param name="vertCount">The number of vertices.</param>
    /// <param name="normals">The output normal buffer to populate.</param>
    public static void ComputeNormals(DataBuffer<float> positions, DataBuffer<int> faceIndices, int triCount, int vertCount, DataBuffer<float> normals)
    {
        Span<Vector3> accumNormals = vertCount <= 4096 ? stackalloc Vector3[vertCount] : new Vector3[vertCount];
        accumNormals.Clear();

        for (int t = 0; t < triCount; t++)
        {
            int i0 = faceIndices.Get<int>(t * 3, 0, 0);
            int i1 = faceIndices.Get<int>(t * 3 + 1, 0, 0);
            int i2 = faceIndices.Get<int>(t * 3 + 2, 0, 0);

            var p0 = new Vector3(positions.Get<float>(i0, 0, 0), positions.Get<float>(i0, 0, 1), positions.Get<float>(i0, 0, 2));
            var p1 = new Vector3(positions.Get<float>(i1, 0, 0), positions.Get<float>(i1, 0, 1), positions.Get<float>(i1, 0, 2));
            var p2 = new Vector3(positions.Get<float>(i2, 0, 0), positions.Get<float>(i2, 0, 1), positions.Get<float>(i2, 0, 2));

            var faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
            accumNormals[i0] += faceNormal;
            accumNormals[i1] += faceNormal;
            accumNormals[i2] += faceNormal;
        }

        for (int i = 0; i < vertCount; i++)
        {
            var n = Vector3.Normalize(accumNormals[i]);
            if (float.IsNaN(n.X)) n = Vector3.UnitY;
            normals.Add(i, 0, 0, n.X);
            normals.Add(i, 0, 1, n.Y);
            normals.Add(i, 0, 2, n.Z);
        }
    }
}
