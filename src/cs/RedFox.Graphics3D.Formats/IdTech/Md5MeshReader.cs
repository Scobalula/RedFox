using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;

namespace RedFox.Graphics3D.Formats.IdTech;

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

        var tokenizer = new TextTokenReader(text.AsSpan());

        int jointCount = 0;
        var joints = Array.Empty<Md5Joint>();
        var meshData = new List<(string Shader, Md5Vertex[] Vertices, int[] Triangles, Md5Weight[] Weights)>();

        while (tokenizer.TryReadToken(out var token))
        {
            if (token.SequenceEqual("MD5Version"))
            {
                if (!tokenizer.TryReadInt(out int version) || version != Md5Format.Version)
                    throw new InvalidDataException("Unsupported MD5 version: expected 10.");
                continue;
            }

            if (token.SequenceEqual("commandline"))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }

            if (token.SequenceEqual("numJoints"))
            {
                if (tokenizer.TryReadInt(out int parsedJointCount))
                    jointCount = parsedJointCount;
                continue;
            }

            if (token.SequenceEqual("numMeshes"))
            {
                tokenizer.TryReadInt(out _);
                continue;
            }

            if (token.SequenceEqual("joints"))
            {
                if (!tokenizer.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after joints keyword.");
                joints = ParseJoints(ref tokenizer, jointCount);
                continue;
            }

            if (token.SequenceEqual("mesh"))
            {
                if (!tokenizer.TryExpect('{'))
                    throw new InvalidDataException("Expected '{' after mesh keyword.");
                meshData.Add(ParseMesh(ref tokenizer));
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
            int parentIndex = joint.ParentIndex;

            if (parentIndex >= 0 && (uint)parentIndex < (uint)joints.Length)
            {
                ref readonly var parentJoint = ref joints[parentIndex];
                var inverseParentRotation = Quaternion.Conjugate(parentJoint.Orientation);
                bones[i].BindTransform.LocalPosition = Vector3.Transform(joint.Position - parentJoint.Position, inverseParentRotation);
                bones[i].BindTransform.LocalRotation = Quaternion.Normalize(inverseParentRotation * joint.Orientation);
            }
            else
            {
                bones[i].BindTransform.LocalPosition = joint.Position;
                bones[i].BindTransform.LocalRotation = joint.Orientation;
            }
        }

            var skeleton = scene.RootNode.AddNode(new Skeleton($"{_name}_Skeleton"));
        for (int i = 0; i < joints.Length; i++)
        {
            int parentIndex = joints[i].ParentIndex;
            if (parentIndex < 0)
                bones[i].MoveTo(skeleton, ReparentTransformMode.PreserveExisting);
            else if ((uint)parentIndex < (uint)bones.Length)
                bones[i].MoveTo(bones[parentIndex], ReparentTransformMode.PreserveExisting);
        }

        if (meshData.Count > 0)
        {
            var model = scene.RootNode.AddNode<MeshGroup>(_name);

            foreach (var (shader, vertices, triangles, weights) in meshData)
            {
                string meshName = !string.IsNullOrWhiteSpace(shader) ? shader : "default";
                var mesh = model.AddNode<Mesh>(meshName);
                BuildMesh(mesh, vertices, triangles, weights, joints, bones);

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
    /// <param name="tokenizer">The tokenizer, advanced past the consumed content on return.</param>
    /// <param name="capacity">The expected number of joints.</param>
    /// <returns>An array of parsed <see cref="Md5Joint"/> entries.</returns>
    public static Md5Joint[] ParseJoints(ref TextTokenReader tokenizer, int capacity)
    {
        var joints = new Md5Joint[capacity];
        int count = 0;

        while (!tokenizer.IsEmpty)
        {
            if (tokenizer.TryExpect('}'))
                break;

            if (!tokenizer.TryReadQuotedString(out var nameSpan))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            string name = new(nameSpan);

            if (!tokenizer.TryReadInt(out int parentIndex))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryExpect('('))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float positionX))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float positionY))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float positionZ))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryExpect(')'))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryExpect('('))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float orientationX))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float orientationY))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryReadFloat(out float orientationZ))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }
            if (!tokenizer.TryExpect(')'))
            {
                tokenizer.SkipRestOfLine();
                continue;
            }

            tokenizer.SkipRestOfLine();

            if ((uint)count < (uint)joints.Length)
                joints[count] = new Md5Joint(name, parentIndex, new Vector3(positionX, positionY, positionZ), Md5Format.ComputeQuaternion(orientationX, orientationY, orientationZ));
            count++;
        }

        return joints;
    }

    /// <summary>
    /// Parses a single <c>mesh { }</c> block from the tokenizer.
    /// The opening brace must already have been consumed.
    /// </summary>
    /// <param name="tokenizer">The tokenizer, advanced past the consumed content on return.</param>
    /// <returns>A tuple containing the shader name, vertex array, triangle index array, and weight array.</returns>
    public static (string Shader, Md5Vertex[] Vertices, int[] Triangles, Md5Weight[] Weights) ParseMesh(ref TextTokenReader tokenizer)
    {
        string shader = "default";
        Md5Vertex[] vertices = [];
        int[] triangles = [];
        Md5Weight[] weights = [];

        while (!tokenizer.IsEmpty)
        {
            if (tokenizer.TryExpect('}'))
                break;

            if (!tokenizer.TryReadToken(out var keyword))
                break;

            if (keyword.SequenceEqual("shader"))
            {
                if (tokenizer.TryReadQuotedString(out var s))
                    shader = new string(s);
                continue;
            }

            if (keyword.SequenceEqual("numverts"))
            {
                if (tokenizer.TryReadInt(out int vertexCount))
                    vertices = new Md5Vertex[vertexCount];
                continue;
            }

            if (keyword.SequenceEqual("numtris"))
            {
                if (tokenizer.TryReadInt(out int triangleCount))
                    triangles = new int[checked(triangleCount * 3)];
                continue;
            }

            if (keyword.SequenceEqual("numweights"))
            {
                if (tokenizer.TryReadInt(out int weightCount))
                    weights = new Md5Weight[weightCount];
                continue;
            }

            if (keyword.SequenceEqual("vert"))
            {
                if (!tokenizer.TryReadInt(out int vertexIndex))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if ((uint)vertexIndex >= (uint)vertices.Length)
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryExpect('('))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadFloat(out float textureCoordinateU))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadFloat(out float textureCoordinateV))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryExpect(')'))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadInt(out int firstWeightIndex))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadInt(out int weightCount))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                vertices[vertexIndex] = new Md5Vertex(new Vector2(textureCoordinateU, textureCoordinateV), firstWeightIndex, weightCount);
                continue;
            }

            if (keyword.SequenceEqual("tri"))
            {
                if (!tokenizer.TryReadInt(out int triangleIndex))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                int triangleBaseIndex = checked(triangleIndex * 3);
                if ((uint)(triangleBaseIndex + 2) >= (uint)triangles.Length)
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadInt(out int firstVertexIndex))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadInt(out int secondVertexIndex))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadInt(out int thirdVertexIndex))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                triangles[triangleBaseIndex] = firstVertexIndex;
                triangles[triangleBaseIndex + 1] = secondVertexIndex;
                triangles[triangleBaseIndex + 2] = thirdVertexIndex;
                continue;
            }

            if (keyword.SequenceEqual("weight"))
            {
                if (!tokenizer.TryReadInt(out int weightIndex))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if ((uint)weightIndex >= (uint)weights.Length)
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadInt(out int jointIndex))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadFloat(out float bias))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryExpect('('))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadFloat(out float weightPositionX))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadFloat(out float weightPositionY))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryReadFloat(out float weightPositionZ))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                if (!tokenizer.TryExpect(')'))
                {
                    tokenizer.SkipRestOfLine();
                    continue;
                }
                weights[weightIndex] = new Md5Weight(jointIndex, bias, new Vector3(weightPositionX, weightPositionY, weightPositionZ));
            }
        }

        return (shader, vertices, triangles, weights);
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
    /// <param name="vertices">The parsed MD5 vertices.</param>
    /// <param name="triangles">The parsed triangle indices (3 ints per triangle).</param>
    /// <param name="weights">The parsed weight table.</param>
    /// <param name="joints">The parsed bind-pose joints.</param>
    /// <param name="bones">The skeleton bone array aligned with joints.</param>
    public static void BuildMesh(Mesh mesh, Md5Vertex[] vertices, int[] triangles, Md5Weight[] weights, Md5Joint[] joints, SkeletonBone[] bones)
    {
        int vertexCount = vertices.Length;
        int triangleCount = triangles.Length / 3;
        int indexCount = triangleCount * 3;

        int maxInfluences = 0;
        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            if (vertices[vertexIndex].WeightCount > maxInfluences)
                maxInfluences = vertices[vertexIndex].WeightCount;
        }

        var positions = new DataBuffer<float>(vertexCount, 1, 3);
        var uvLayers = new DataBuffer<float>(vertexCount, 1, 2);
        var boneIndices = maxInfluences > 0 ? new DataBuffer<int>(vertexCount, maxInfluences, 1) : null;
        var boneWeights = maxInfluences > 0 ? new DataBuffer<float>(vertexCount, maxInfluences, 1) : null;

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            ref readonly var vertex = ref vertices[vertexIndex];
            var finalPosition = Vector3.Zero;

            for (int weightOffset = 0; weightOffset < vertex.WeightCount; weightOffset++)
            {
                int weightIndex = vertex.WeightIndex + weightOffset;
                if ((uint)weightIndex >= (uint)weights.Length)
                    continue;

                ref readonly var weight = ref weights[weightIndex];
                if ((uint)weight.JointIndex >= (uint)joints.Length)
                    continue;

                ref readonly var joint = ref joints[weight.JointIndex];
                var rotatedPos = Vector3.Transform(weight.Position, joint.Orientation);
                finalPosition += weight.Bias * (joint.Position + rotatedPos);

                if (boneIndices is not null && boneWeights is not null && weightOffset < maxInfluences)
                {
                    boneIndices.Add(vertexIndex, weightOffset, 0, weight.JointIndex);
                    boneWeights.Add(vertexIndex, weightOffset, 0, weight.Bias);
                }
            }

            if (boneIndices is not null && boneWeights is not null)
            {
                for (int weightOffset = vertex.WeightCount; weightOffset < maxInfluences; weightOffset++)
                {
                    boneIndices.Add(vertexIndex, weightOffset, 0, 0);
                    boneWeights.Add(vertexIndex, weightOffset, 0, 0f);
                }
            }

            positions.Add(vertexIndex, 0, 0, finalPosition.X);
            positions.Add(vertexIndex, 0, 1, finalPosition.Y);
            positions.Add(vertexIndex, 0, 2, finalPosition.Z);

            uvLayers.Add(vertexIndex, 0, 0, vertex.UV.X);
            uvLayers.Add(vertexIndex, 0, 1, vertex.UV.Y);
        }

        var faceIndices = new DataBuffer<int>(indexCount, 1, 1);
        for (int i = 0; i < indexCount; i++)
            faceIndices.Add(i, 0, 0, triangles[i]);

        var normals = new DataBuffer<float>(vertexCount, 1, 3);
        ComputeNormals(positions, faceIndices, triangleCount, vertexCount, normals);

        mesh.Positions = positions;
        mesh.Normals = normals;
        mesh.UVLayers = uvLayers;
        mesh.FaceIndices = faceIndices;

        if (boneIndices is not null && boneWeights is not null)
            mesh.Skin = new Skin(bones, boneIndices, boneWeights);
    }

    /// <summary>
    /// Computes per-vertex normals by accumulating face normals and normalizing.
    /// </summary>
    /// <param name="positions">The vertex position buffer.</param>
    /// <param name="faceIndices">The face index buffer.</param>
    /// <param name="triangleCount">The number of triangles.</param>
    /// <param name="vertexCount">The number of vertices.</param>
    /// <param name="normals">The output normal buffer to populate.</param>
    public static void ComputeNormals(DataBuffer<float> positions, DataBuffer<int> faceIndices, int triangleCount, int vertexCount, DataBuffer<float> normals)
    {
        Span<Vector3> accumulatedNormals = vertexCount <= 4096 ? stackalloc Vector3[vertexCount] : new Vector3[vertexCount];
        accumulatedNormals.Clear();

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            int firstVertexIndex = faceIndices.Get<int>(triangleIndex * 3, 0, 0);
            int secondVertexIndex = faceIndices.Get<int>(triangleIndex * 3 + 1, 0, 0);
            int thirdVertexIndex = faceIndices.Get<int>(triangleIndex * 3 + 2, 0, 0);

            var firstPosition = new Vector3(positions.Get<float>(firstVertexIndex, 0, 0), positions.Get<float>(firstVertexIndex, 0, 1), positions.Get<float>(firstVertexIndex, 0, 2));
            var secondPosition = new Vector3(positions.Get<float>(secondVertexIndex, 0, 0), positions.Get<float>(secondVertexIndex, 0, 1), positions.Get<float>(secondVertexIndex, 0, 2));
            var thirdPosition = new Vector3(positions.Get<float>(thirdVertexIndex, 0, 0), positions.Get<float>(thirdVertexIndex, 0, 1), positions.Get<float>(thirdVertexIndex, 0, 2));

            var faceNormal = Vector3.Cross(secondPosition - firstPosition, thirdPosition - firstPosition);
            accumulatedNormals[firstVertexIndex] += faceNormal;
            accumulatedNormals[secondVertexIndex] += faceNormal;
            accumulatedNormals[thirdVertexIndex] += faceNormal;
        }

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            var normal = Vector3.Normalize(accumulatedNormals[vertexIndex]);
            if (float.IsNaN(normal.X))
                normal = Vector3.UnitY;
            normals.Add(vertexIndex, 0, 0, normal.X);
            normals.Add(vertexIndex, 0, 1, normal.Y);
            normals.Add(vertexIndex, 0, 2, normal.Z);
        }
    }
}
