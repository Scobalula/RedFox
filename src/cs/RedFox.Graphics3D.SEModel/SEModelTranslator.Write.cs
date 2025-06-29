using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.SEModel
{
    /// <summary>
    /// A class to translate a Model to SEModel
    /// </summary>
    public partial class SEModelTranslator
    {
        /// <inheritdoc/>
        public override void Write(Stream stream, string filePath, Graphics3DScene scene)
        {
            var hasSkeleton = scene.TryGetFirstSkeleton(out var skeleton);


            var data = scene.GetFirstInstance<Model>();
            var boneCount = skeleton != null ? skeleton.Bones.Count : 0;
            var meshCount = data != null ? data.Meshes.Count : 0;
            var matCount = data != null ? data.Materials.Count : 0;
            var scale = 1.0f;

            using var writer = new BinaryWriter(stream, Encoding.Default, true);

            writer.Write(Magic);
            writer.Write((ushort)0x1);
            writer.Write((ushort)0x14);
            writer.Write((byte)0x7); // Data Presence
            writer.Write((byte)0x7); // Bone Data Presence
            writer.Write((byte)0xF); // Mesh Data Presence
            writer.Write(boneCount);
            writer.Write(meshCount);
            writer.Write(matCount);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);

            if (skeleton != null)
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    writer.Write(Encoding.ASCII.GetBytes(skeleton.Bones[i].Name));
                    writer.Write((byte)0);
                }

                foreach (var bone in skeleton.Bones)
                {
                    writer.Write((byte)0); // Unused flags

                    writer.Write(bone.Parent == null ? -1 : skeleton.Bones.IndexOf(bone.Parent));

                    var wt = bone.BaseWorldTranslation;
                    var wr = bone.BaseWorldRotation;
                    var lt = bone.BaseLocalTranslation;
                    var lr = bone.BaseLocalRotation;
                    var s = Vector3.One;

                    writer.Write(wt.X * scale);
                    writer.Write(wt.Y * scale);
                    writer.Write(wt.Z * scale);
                    writer.Write(wr.X);
                    writer.Write(wr.Y);
                    writer.Write(wr.Z);
                    writer.Write(wr.W);

                    writer.Write(lt.X * scale);
                    writer.Write(lt.Y * scale);
                    writer.Write(lt.Z * scale);
                    writer.Write(lr.X);
                    writer.Write(lr.Y);
                    writer.Write(lr.Z);
                    writer.Write(lr.W);

                    writer.Write(s.X);
                    writer.Write(s.Y);
                    writer.Write(s.Z);
                }
            }

            if (data != null)
            {
                foreach (var mesh in data.Meshes)
                {
                    var vertCount = mesh.Positions.Count;
                    var faceCount = mesh.Faces.Count;
                    var layerCount = mesh.UVLayers.Count <= 0 ? 1 : mesh.UVLayers.Dimension;
                    var influences = mesh.Influences.Count <= 0 || boneCount <= 0 ? 0 : mesh.Influences.Dimension;

                    writer.Write((byte)0); // Unused flags

                    writer.Write((byte)layerCount);
                    writer.Write((byte)influences);
                    writer.Write(vertCount);
                    writer.Write(faceCount);

                    // Positions
                    for (int i = 0; i < mesh.Positions.Count; i++)
                    {
                        writer.Write(mesh.Positions[i].X * scale);
                        writer.Write(mesh.Positions[i].Y * scale);
                        writer.Write(mesh.Positions[i].Z * scale);
                    }
                    // UVs
                    if (mesh.UVLayers.ElementCount == mesh.Positions.ElementCount && mesh.UVLayers.Dimension > 0)
                    {
                        for (int i = 0; i < mesh.Positions.Count; i++)
                        {
                            for (int l = 0; l < layerCount; l++)
                            {
                                writer.Write(mesh.UVLayers[i, l].X);
                                writer.Write(mesh.UVLayers[i, l].Y);
                            }
                        }
                    }
                    // Just write 0 values and let the user fix it up in other software.
                    else
                    {
                        writer.Write(new byte[8 * mesh.Positions.ElementCount * layerCount]);
                    }
                    // Normals
                    if (mesh.Normals.ElementCount == mesh.Positions.ElementCount && mesh.Normals.Dimension > 0)
                    {
                        for (int i = 0; i < mesh.Positions.Count; i++)
                        {
                            writer.Write(mesh.Normals[i].X);
                            writer.Write(mesh.Normals[i].Y);
                            writer.Write(mesh.Normals[i].Z);
                        }
                    }
                    // Just write 0 values and let the user fix it up in other software.
                    else
                    {
                        writer.Write(new byte[12 * mesh.Positions.ElementCount]);
                    }
                    // Colours
                    if (mesh.Colours.ElementCount == mesh.Positions.ElementCount && mesh.Colours.Dimension > 0)
                    {
                        for (int i = 0; i < mesh.Positions.Count; i++)
                        {
                            writer.Write((byte)(mesh.Colours[i].X * 255.0f));
                            writer.Write((byte)(mesh.Colours[i].Y * 255.0f));
                            writer.Write((byte)(mesh.Colours[i].Z * 255.0f));
                            writer.Write((byte)(mesh.Colours[i].W * 255.0f));
                        }
                    }
                    // Just write 0 values and let the user fix it up in other software.
                    else
                    {
                        writer.Write(new byte[4 * mesh.Positions.ElementCount]);
                    }
                    // Weights
                    if (influences != 0 && boneCount > 0)
                    {
                        for (int i = 0; i < mesh.Positions.Count; i++)
                        {
                            for (int w = 0; w < influences; w++)
                            {
                                var (index, value) = mesh.Influences[i, w];

                                if (boneCount <= 0xFF)
                                    writer.Write((byte)index);
                                else if (boneCount <= 0xFFFF)
                                    writer.Write((ushort)index);
                                else
                                    writer.Write(index);

                                writer.Write(value);
                            }
                        }
                    }

                    foreach (var (firstIndex, secondIndex, thirdIndex) in mesh.Faces)
                    {
                        if (vertCount <= 0xFF)
                        {
                            writer.Write((byte)firstIndex);
                            writer.Write((byte)secondIndex);
                            writer.Write((byte)thirdIndex);
                        }
                        else if (vertCount <= 0xFFFF)
                        {
                            writer.Write((ushort)firstIndex);
                            writer.Write((ushort)secondIndex);
                            writer.Write((ushort)thirdIndex);
                        }
                        else
                        {
                            writer.Write(firstIndex);
                            writer.Write(secondIndex);
                            writer.Write(thirdIndex);
                        }
                    }

                    foreach (var material in mesh.Materials)
                        writer.Write(data.Materials.IndexOf(material));
                }

                foreach (var material in data.Materials)
                {
                    writer.Write(Encoding.ASCII.GetBytes(material.Name));
                    writer.Write((byte)0);
                    writer.Write(true);
                    writer.Write(Encoding.ASCII.GetBytes(material.Textures.TryGetValue("DiffuseMap", out var img) ? img.FilePath : string.Empty));
                    writer.Write((byte)0);
                    writer.Write(Encoding.ASCII.GetBytes(material.Textures.TryGetValue("NormalMap", out img) ? img.FilePath : string.Empty));
                    writer.Write((byte)0);
                    writer.Write(Encoding.ASCII.GetBytes(material.Textures.TryGetValue("SpecularMap", out img) ? img.FilePath : string.Empty));
                    writer.Write((byte)0);
                }
            }
        }

    }
}
