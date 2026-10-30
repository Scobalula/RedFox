using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;

namespace RedFox.Graphics3D.Formats.IdTech;

/// <summary>
/// Reads id Tech 4 MD5 mesh (<c>.md5mesh</c>) text files and populates a <see cref="Scene"/> with the parsed skeleton and skinned mesh data.
/// </summary>
public sealed class Md5MeshReader
{
    private readonly Stream _stream;
    private readonly string _name;

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
    }

    /// <summary>
    /// Parses the MD5 mesh stream and populates <paramref name="scene"/> with the resulting skeleton and skinned mesh data.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the file header is invalid or the data is malformed.
    /// </exception>
    public void Read(Scene scene) => Read(scene, CancellationToken.None);

    /// <summary>
    /// Parses the <c>joints { }</c> block from the tokenizer.
    /// </summary>
    /// <param name="tokenizer">The tokenizer, advanced past the consumed content on return.</param>
    /// <param name="capacity">The expected number of joints.</param>
    /// <returns>An array of parsed <see cref="Md5Joint"/> entries.</returns>
    public static Md5Joint[] ParseJoints(ref TextTokenReader tokenizer, int capacity)
    {
        var joints	= new Md5Joint[capacity];
        int count	= 0;

        while (true)
        {
            if (tokenizer.TryExpect('}'))
                break;

            if (!tokenizer.TryReadQuotedString(out var nameSpan))
                throw new InvalidDataException("Malformed MD5 record.");
            string name = new(nameSpan);

            if (!tokenizer.TryReadInt(out int parentIndex))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryExpect('('))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float positionX))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float positionY))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float positionZ))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryExpect(')'))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryExpect('('))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float orientationX))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float orientationY))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryReadFloat(out float orientationZ))
                throw new InvalidDataException("Malformed MD5 record.");
            if (!tokenizer.TryExpect(')'))
                throw new InvalidDataException("Malformed MD5 record.");

            if ((uint)count >= (uint)joints.Length)
                throw new InvalidDataException("Too many MD5 records.");
            joints[count] = new Md5Joint(name, parentIndex, new Vector3(positionX, positionY, positionZ), Md5Format.ComputeQuaternion(orientationX, orientationY, orientationZ));
            count++;
        }

        if (count != capacity)
            throw new InvalidDataException("MD5 joint count does not match its declaration.");
        return joints;
    }

    /// <summary>
    /// Parses a single <c>mesh { }</c> block from the tokenizer.
    /// </summary>
    /// <param name="tokenizer">The tokenizer, advanced past the consumed content on return.</param>
    /// <returns>A tuple containing the shader name, vertex array, triangle index array, and weight array.</returns>
    public static (string Shader, Md5Vertex[] Vertices, int[] Triangles, Md5Weight[] Weights) ParseMesh(ref TextTokenReader tokenizer)
    {
        string shader			= "default";
        Md5Vertex[] vertices	= [];
        int[] triangles			= [];
        Md5Weight[] weights		= [];
        bool[] vertexRecords	= [];
        int vertexRecordCount	= 0;
        bool[] triangleRecords	= [];
        int triangleRecordCount	= 0;
        bool[] weightRecords	= [];
        int weightRecordCount	= 0;

        while (true)
        {
            if (tokenizer.TryExpect('}'))
                break;

            if (!tokenizer.TryReadToken(out var keyword))
                throw new InvalidDataException("Unterminated MD5 mesh block.");

            if (keyword.SequenceEqual("shader"))
            {
                if (tokenizer.TryReadQuotedString(out var s))
                    shader = new string(s);
                continue;
            }

            if (keyword.SequenceEqual("numverts"))
            {
                if (!tokenizer.TryReadInt(out int vertexCount) || vertexCount < 0 || vertexCount > tokenizer.Remaining.Length)
                    throw new InvalidDataException("Invalid MD5 record count.");
                vertices		= new Md5Vertex[vertexCount];
                vertexRecords	= new bool[vertexCount];
                continue;
            }

            if (keyword.SequenceEqual("numtris"))
            {
                if (!tokenizer.TryReadInt(out int triangleCount) || triangleCount < 0 || triangleCount > tokenizer.Remaining.Length)
                    throw new InvalidDataException("Invalid MD5 record count.");
                triangles		= new int[checked(triangleCount * 3)];
                triangleRecords	= new bool[triangleCount];
                continue;
            }

            if (keyword.SequenceEqual("numweights"))
            {
                if (!tokenizer.TryReadInt(out int weightCount) || weightCount < 0 || weightCount > tokenizer.Remaining.Length)
                    throw new InvalidDataException("Invalid MD5 record count.");
                weights			= new Md5Weight[weightCount];
                weightRecords	= new bool[weightCount];
                continue;
            }

            if (keyword.SequenceEqual("vert"))
            {
                if (!tokenizer.TryReadInt(out int vertexIndex) || vertexRecords[vertexIndex])
                    throw new InvalidDataException("Malformed MD5 record.");
                vertexRecords[vertexIndex] = true;
                vertexRecordCount++;
                if (!tokenizer.TryExpect('('))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryReadFloat(out float textureCoordinateU))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryReadFloat(out float textureCoordinateV))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryExpect(')'))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryReadInt(out int firstWeightIndex))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryReadInt(out int weightCount))
                    throw new InvalidDataException("Malformed MD5 record.");
                vertices[vertexIndex] = new Md5Vertex(new Vector2(textureCoordinateU, textureCoordinateV), firstWeightIndex, weightCount);
                continue;
            }

            if (keyword.SequenceEqual("tri"))
            {
                if (!tokenizer.TryReadInt(out int triangleIndex) || triangleRecords[triangleIndex])
                    throw new InvalidDataException("Malformed MD5 record.");
                triangleRecords[triangleIndex] = true;
                triangleRecordCount++;
                int triangleBaseIndex = checked(triangleIndex * 3);
                if (!tokenizer.TryReadInt(out int firstVertexIndex))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryReadInt(out int secondVertexIndex))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryReadInt(out int thirdVertexIndex))
                    throw new InvalidDataException("Malformed MD5 record.");
                triangles[triangleBaseIndex] = firstVertexIndex;
                triangles[triangleBaseIndex + 1] = secondVertexIndex;
                triangles[triangleBaseIndex + 2] = thirdVertexIndex;
                continue;
            }

            if (keyword.SequenceEqual("weight"))
            {
                if (!tokenizer.TryReadInt(out int weightIndex) || weightRecords[weightIndex])
                    throw new InvalidDataException("Malformed MD5 record.");
                weightRecords[weightIndex] = true;
                weightRecordCount++;
                if (!tokenizer.TryReadInt(out int jointIndex))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryReadFloat(out float bias))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryExpect('('))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryReadFloat(out float weightPositionX))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryReadFloat(out float weightPositionY))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryReadFloat(out float weightPositionZ))
                    throw new InvalidDataException("Malformed MD5 record.");
                if (!tokenizer.TryExpect(')'))
                    throw new InvalidDataException("Malformed MD5 record.");
                weights[weightIndex] = new Md5Weight(jointIndex, bias, new Vector3(weightPositionX, weightPositionY, weightPositionZ));
            }
        }

        if (vertexRecordCount != vertices.Length || triangleRecordCount != triangles.Length / 3 || weightRecordCount != weights.Length)
            throw new InvalidDataException("MD5 record counts do not match their declarations.");
        return (shader, vertices, triangles, weights);
    }

    /// <summary>
    /// Builds vertex position, UV, normal, face-index, and skinning buffers for a <see cref="Mesh"/> from parsed MD5 mesh data.
    /// </summary>
    /// <param name="mesh">The destination mesh node.</param>
    /// <param name="vertices">The parsed MD5 vertices.</param>
    /// <param name="triangles">The parsed triangle indices (3 ints per triangle).</param>
    /// <param name="weights">The parsed weight table.</param>
    /// <param name="joints">The parsed bind-pose joints.</param>
    /// <param name="bones">The skeleton bone array aligned with joints.</param>
    public static void BuildMesh(Mesh mesh, Md5Vertex[] vertices, int[] triangles, Md5Weight[] weights, Md5Joint[] joints, SkeletonBone[] bones)
    {
        int vertexCount		= vertices.Length;
        int triangleCount	= triangles.Length / 3;
        int indexCount		= triangleCount * 3;

        int maxInfluences = 0;
        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            Md5Vertex vertex = vertices[vertexIndex];
            if (vertex.WeightIndex < 0 || vertex.WeightCount < 0 || (long)vertex.WeightIndex + vertex.WeightCount > weights.Length)
                throw new InvalidDataException("Invalid MD5 vertex weight range.");
            if (vertices[vertexIndex].WeightCount > maxInfluences)
                maxInfluences = vertices[vertexIndex].WeightCount;
        }

        var positions	= new DataBuffer<float>(vertexCount, 1, 3);
        var uvLayers	= new DataBuffer<float>(vertexCount, 1, 2);
        var boneIndices	= maxInfluences > 0 ? new DataBuffer<int>(vertexCount, maxInfluences, 1) : null;
        var boneWeights	= maxInfluences > 0 ? new DataBuffer<float>(vertexCount, maxInfluences, 1) : null;

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            ref readonly var vertex = ref vertices[vertexIndex];
            var finalPosition = Vector3.Zero;

            for (int weightOffset = 0; weightOffset < vertex.WeightCount; weightOffset++)
            {
                int weightIndex = vertex.WeightIndex + weightOffset;

                ref readonly var weight = ref weights[weightIndex];

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
            int firstVertexIndex	= faceIndices.Get<int>(triangleIndex * 3, 0, 0);
            int secondVertexIndex	= faceIndices.Get<int>(triangleIndex * 3 + 1, 0, 0);
            int thirdVertexIndex	= faceIndices.Get<int>(triangleIndex * 3 + 2, 0, 0);

            var firstPosition	= new Vector3(positions.Get<float>(firstVertexIndex, 0, 0), positions.Get<float>(firstVertexIndex, 0, 1), positions.Get<float>(firstVertexIndex, 0, 2));
            var secondPosition	= new Vector3(positions.Get<float>(secondVertexIndex, 0, 0), positions.Get<float>(secondVertexIndex, 0, 1), positions.Get<float>(secondVertexIndex, 0, 2));
            var thirdPosition	= new Vector3(positions.Get<float>(thirdVertexIndex, 0, 0), positions.Get<float>(thirdVertexIndex, 0, 1), positions.Get<float>(thirdVertexIndex, 0, 2));

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

    /// <summary>
    /// Reads data with cancellation support.
    /// </summary>
    /// <param name="scene">The scene to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public void Read(Scene scene, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var sr = new StreamReader(_stream, leaveOpen: true);
        string text = sr.ReadToEnd();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var tokenizer = new TextTokenReader(text.AsSpan());

        int jointCount	= 0;
        int meshCount	= -1;
        var joints		= Array.Empty<Md5Joint>();
        var meshData	= new List<(string Shader, Md5Vertex[] Vertices, int[] Triangles, Md5Weight[] Weights)>();

        while (tokenizer.TryReadToken(out var token))
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                if (!tokenizer.TryReadInt(out jointCount) || jointCount < 0 || jointCount > text.Length)
                    throw new InvalidDataException("Invalid MD5 joint count.");
                continue;
            }

            if (token.SequenceEqual("numMeshes"))
            {
                if (!tokenizer.TryReadInt(out meshCount) || meshCount < 0)
                    throw new InvalidDataException("Invalid MD5 mesh count.");
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

        if (joints.Length != jointCount || meshCount != meshData.Count)
            throw new InvalidDataException("MD5 declarations do not match their records.");

        var bones = new SkeletonBone[joints.Length];
        for (int i = 0; i < joints.Length; i++)
            bones[i] = new SkeletonBone(joints[i].Name);

        for (int i = 0; i < joints.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ref readonly var joint = ref joints[i];
            int parentIndex = joint.ParentIndex;

            if (parentIndex != -1)
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
            cancellationToken.ThrowIfCancellationRequested();
            int parentIndex = joints[i].ParentIndex;
            if (parentIndex < 0)
                bones[i].MoveTo(skeleton, ReparentTransformMode.PreserveExisting);
            else if ((uint)parentIndex < (uint)bones.Length)
                bones[i].MoveTo(bones[parentIndex], ReparentTransformMode.PreserveExisting);
        }

        if (meshData.Count > 0)
        {
            Dictionary<string, Material> materials = new(StringComparer.Ordinal);
            var model = scene.RootNode.AddNode<MeshGroup>(_name);

            foreach (var (shader, vertices, triangles, weights) in meshData)
            {
                string meshName	= !string.IsNullOrWhiteSpace(shader) ? shader : "default";
                var mesh		= model.AddNode<Mesh>($"{meshName}_{model.Children?.Count ?? 0}");
                BuildMesh(mesh, vertices, triangles, weights, joints, bones);

                if (!string.IsNullOrWhiteSpace(shader))
                {
                    if (!materials.TryGetValue(shader, out Material? mat))
                    {
                        mat = scene.RootNode.AddNode(new Material(shader));
                        materials.Add(shader, mat);
                    }
                    mesh.Materials = [mat];
                }
            }
        }
    }
}
