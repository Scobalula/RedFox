using Cast.NET;
using Cast.NET.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.Cast
{
    public static class CastMeshTranslator
    {
        public static Mesh TranslateFrom(Graphics3DScene scene, MeshNode meshNode, Dictionary<ulong, Material> materials)
        {
            var uvLayerCount = meshNode.UVLayerCount;
            var influences = meshNode.MaximumWeightInfluence;

            var mesh = new Mesh();

            var buffer = meshNode.VertexPositionBuffer;

            mesh.Positions = new(buffer.ValueCount);

            for (int i = 0; i < buffer.ValueCount; i++)
            {
                mesh.Positions.Add(buffer.Values[i]);
            }

            var normalsBuffer = meshNode.VertexNormalBuffer;

            if (normalsBuffer is not null)
            {
                if (normalsBuffer.ValueCount != buffer.ValueCount)
                    throw new DataMisalignedException("Vertex normal buffer does not contain same count as positions buffer");

                mesh.Normals = new(buffer.ValueCount);

                for (int i = 0; i < buffer.ValueCount; i++)
                {
                    mesh.Normals.Add(normalsBuffer.Values[i]);
                }
            }

            if (uvLayerCount > 0)
            {
                mesh.UVLayers = new(mesh.Positions.Count * uvLayerCount);

                for (int u = 0; u < uvLayerCount; u++)
                {
                    var uvLayer = meshNode.GetUVLayer(u) as CastArrayProperty<Vector2> ?? throw new NullReferenceException(nameof(meshNode.GetUVLayer));

                    if (uvLayer.ValueCount != buffer.ValueCount)
                        throw new DataMisalignedException($"UV Layer does not contain same count as positions buffer");

                    for (int i = 0; i < buffer.ValueCount; i++)
                    {
                        mesh.UVLayers.Add(uvLayer.Values[i], i, u);
                    }
                }
            }

            if (influences > 0)
            {
                mesh.Influences = new(buffer.ValueCount, influences);

                foreach (var (bone, weight) in meshNode.EnumerateBoneWeights())
                {
                    mesh.Influences.Add(new(bone, weight));
                }
            }

            foreach (var face in meshNode.EnumerateFaceIndices())
            {
                mesh.Faces.Add(face);
            }

            mesh.Materials.Add(materials[meshNode.MaterialHash]);

            return mesh;
        }
    }
}
