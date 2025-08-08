using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.SEModel
{
    /// <summary>
    /// A class to translate a Model to SEModel
    /// </summary>
    public partial class SEModelTranslator
    {
        /// <inheritdoc/>
        public override void Read(Stream stream, string filePath, Graphics3DScene scene)
        {
            // SEModels can only contain a single skeleton
            // and model.
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var scale = 1.0f;
            var skeleton = new Skeleton($"{fileName}_skeleton");
            var result = new Model(fileName, skeleton);

            using var reader = new BinaryReader(stream, Encoding.Default, true);

            if (!Magic.SequenceEqual(reader.ReadBytes(Magic.Length)))
                throw new InvalidDataException("Invalid SEModel magic number");
            if (reader.ReadUInt16() != 0x1)
                throw new InvalidDataException("Invalid SEModel version");
            if (reader.ReadUInt16() != 0x14)
                throw new InvalidDataException("Invalid SEModel header size");

            var dataPresence = reader.ReadByte();
            var boneDataPresence = reader.ReadByte();
            var meshDataPresence = reader.ReadByte();

            var boneCount = reader.ReadInt32();
            var meshCount = reader.ReadInt32();
            var matCount = reader.ReadInt32();

            var reserved0 = reader.ReadByte();
            var reserved1 = reader.ReadByte();
            var reserved2 = reader.ReadByte();

            var boneNames = new string[boneCount];
            var boneParents = new int[boneCount];

            for (int i = 0; i < boneNames.Length; i++)
            {
                boneNames[i] = ReadUTF8String(reader);
            }

            var hasWorldTransforms = (boneDataPresence & (1 << 0)) != 0;
            var hasLocalTransforms = (boneDataPresence & (1 << 1)) != 0;
            var hasScaleTransforms = (boneDataPresence & (1 << 2)) != 0;

            for (int i = 0; i < boneCount; i++)
            {
                // For now, this flag is unused, and so a non-zero indicates
                // something is wrong in our book
                if (reader.ReadByte() != 0)
                    throw new IOException("Invalid SEModel bone flag");

                boneParents[i] = reader.ReadInt32();

                var bone = new SkeletonBone(boneNames[i]);

                if (hasWorldTransforms)
                {
                    bone.BaseTransform.WorldPosition = new Vector3(
                        reader.ReadSingle() * scale,
                        reader.ReadSingle() * scale,
                        reader.ReadSingle() * scale);
                    bone.BaseTransform.WorldRotation = new Quaternion(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                }

                if (hasLocalTransforms)
                {
                    bone.BaseTransform.LocalPosition = new Vector3(
                        reader.ReadSingle() * scale,
                        reader.ReadSingle() * scale,
                        reader.ReadSingle()) * scale;
                    bone.BaseTransform.LocalRotation = new Quaternion(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                }

                if (hasScaleTransforms)
                {
                    bone.BaseTransform.Scale = new Vector3(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                }

                result.Skeleton!.AddBone(bone);
            }

            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                if (boneParents[i] != -1)
                {
                    skeleton.Bones[i].MoveTo(skeleton.Bones[boneParents[i]]);
                }
            }

            var hasUVs = (meshDataPresence & (1 << 0)) != 0;
            var hasNormals = (meshDataPresence & (1 << 1)) != 0;
            var hasColours = (meshDataPresence & (1 << 2)) != 0;
            var hasWeights = (meshDataPresence & (1 << 3)) != 0;

            var materialIndices = new List<int>[meshCount];

            result.Meshes = [];

            for (int i = 0; i < meshCount; i++)
            {
                // For now, this flag is unused, and so a non-zero indicates
                // something is wrong in our book
                if (reader.ReadByte() != 0)
                    throw new IOException("Invalid SEModel mesh flag");

                var layerCount  = reader.ReadByte();
                var influences  = reader.ReadByte();
                var vertexCount = reader.ReadInt32();
                var faceCount   = reader.ReadInt32();

                var mesh = new Mesh();

                // Not necessary but initializes the collection capacity 
                // so we're not reallocating
                mesh.Positions.SetCapacity(vertexCount);
                mesh.Faces.SetCapacity(faceCount);


                // Positions
                for (int v = 0; v < vertexCount; v++)
                {
                    mesh.Positions.Add(new(
                        reader.ReadSingle() * scale,
                        reader.ReadSingle() * scale,
                        reader.ReadSingle() * scale));
                }
                // UVs
                if (hasUVs)
                {
                    mesh.UVLayers.SetCapacity(vertexCount, layerCount);

                    for (int v = 0; v < vertexCount; v++)
                    {
                        for (int l = 0; l < layerCount; l++)
                        {
                            mesh.UVLayers.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()), v, l);
                        }
                    }
                }
                // Normals
                if (hasNormals)
                {
                    mesh.Normals.SetCapacity(vertexCount);

                    for (int v = 0; v < vertexCount; v++)
                    {
                        mesh.Normals.Add(new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                    }
                }
                // Colours
                if (hasColours)
                {
                    mesh.Colours.SetCapacity(vertexCount);

                    for (int v = 0; v < vertexCount; v++)
                    {
                        mesh.Colours.Add(new(
                            reader.ReadByte() / 255.0f,
                            reader.ReadByte() / 255.0f,
                            reader.ReadByte() / 255.0f,
                            reader.ReadByte() / 255.0f));
                    }
                }
                // Weights
                if (hasWeights)
                {
                    mesh.Influences.SetCapacity(vertexCount, influences);

                    for (int v = 0; v < vertexCount; v++)
                    {
                        for (int w = 0; w < influences; w++)
                        {
                            mesh.Influences.Add(new(
                                boneCount <= 0xFF ? reader.ReadByte() :
                                boneCount <= 0xFFFF ? reader.ReadUInt16() :
                                reader.ReadInt32(),
                                reader.ReadSingle()), v, w);
                        }
                    }
                }
                // Faces
                for (int f = 0; f < faceCount; f++)
                {
                    if (vertexCount <= 0xFF)
                    {
                        mesh.Faces.Add(new(
                            reader.ReadByte(),
                            reader.ReadByte(),
                            reader.ReadByte()));
                    }
                    else if (vertexCount <= 0xFFFF)
                    {
                        mesh.Faces.Add(new(
                            reader.ReadUInt16(),
                            reader.ReadUInt16(),
                            reader.ReadUInt16()));
                    }
                    else
                    {
                        mesh.Faces.Add(new(
                            reader.ReadInt32(),
                            reader.ReadInt32(),
                            reader.ReadInt32()));
                    }
                }

                materialIndices[i] = new List<int>(layerCount);

                for (int m = 0; m < layerCount; m++)
                {
                    materialIndices[i].Add(reader.ReadInt32());
                }

                result.Meshes.Add(mesh);
            }

            for (int i = 0; i < matCount; i++)
            {
                var mtl = new Material(ReadUTF8String(reader));

                if (reader.ReadBoolean())
                {
                    mtl.Textures["DiffuseMap"]  = new(ReadUTF8String(reader), "DiffuseMap");
                    mtl.Textures["NormalMap"]   = new(ReadUTF8String(reader), "DiffuseMap");
                    mtl.Textures["SpecularMap"] = new(ReadUTF8String(reader), "DiffuseMap");
                }

                result.Materials.Add(mtl);
            }

            // Last pass for materials
            for (int i = 0; i < result.Meshes.Count; i++)
            {
                foreach (var index in materialIndices[i])
                    result.Meshes[i].Materials.Add(result.Materials[index]);
            }

            scene.Objects.Add(result);
            scene.Objects.Add(skeleton);
        }
    }
}
