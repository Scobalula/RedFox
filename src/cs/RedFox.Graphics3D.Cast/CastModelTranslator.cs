using Cast.NET;
using Cast.NET.Nodes;
using System.Numerics;

namespace RedFox.Graphics3D.Cast
{
    public static class CastModelTranslator
    {
        public static Model TranslateFrom(Graphics3DScene scene, ModelNode modelNode, string name)
        {
            var model = scene.AddObject<Model>(name);
            var materialLookup = new Dictionary<ulong, Material>();

            if (modelNode.Skeleton is SkeletonNode skeletonNode)
            {
                model.Skeleton = CastSkeletonTranslator.TranslateFrom(scene, skeletonNode, name + ".skeleton." + skeletonNode.GetHashCode());
            }

            foreach (var materialNode in modelNode.Materials)
            {
                var material = scene.AddObject<Material>(materialNode.Name);

                materialLookup.Add(materialNode.Hash, material);
                model.Materials.Add(material);
            }

            foreach (var meshNode in modelNode.Meshes)
            {
                model.Meshes.Add(CastMeshTranslator.TranslateFrom(scene, meshNode, materialLookup));
            }

            return model;
        }
    }
}
