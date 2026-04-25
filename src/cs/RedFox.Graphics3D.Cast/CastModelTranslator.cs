using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Cast;

internal static class CastModelTranslator
{
    public static void Read(Scene scene, ModelNode modelNode, string name)
    {
        var model = scene.RootNode.AddNode<Model>(name);
        var materialLookup = new Dictionary<ulong, Material>();

        // Skeleton
        SkeletonBone[]? bones = null;
        if (modelNode.Skeleton is SkeletonNode skeletonNode)
        {
            bones = CastSkeletonTranslator.Read(model, skeletonNode, $"{name}_Skeleton");
        }

        // Materials
        foreach (var materialNode in modelNode.Materials)
        {
            var material = model.AddNode<Material>(materialNode.Name);

            if (materialNode.Diffuse is FileNode diffuseFile)
            {
                material.DiffuseMapName = material.AddNode(new Texture(diffuseFile.Path, "diffuse")).Name;
            }
            if (materialNode.Normal is FileNode normalFile)
            {
                material.NormalMapName = material.AddNode(new Texture(normalFile.Path, "normal")).Name;
            }
            if (materialNode.Specular is FileNode specularFile)
            {
                material.SpecularMapName = material.AddNode(new Texture(specularFile.Path, "specular")).Name;
            }

            materialLookup[materialNode.Hash] = material;
        }

        // Meshes
        foreach (var meshNode in modelNode.Meshes)
        {
            CastMeshTranslator.Read(model, meshNode, materialLookup, bones);
        }
    }

    public static void Write(CastNode root, Model model, SceneTranslationSelection selection)
    {
        var modelNode = root.AddNode<ModelNode>();

        // Skeleton — find bones from the scene
        var bones = selection.GetDescendants<SkeletonBone>();
        SceneNode[] exportedBoneNodes = Array.ConvertAll(bones, static bone => (SceneNode)bone);

        if (bones.Length > 0)
        {
            var skeletonNode = modelNode.AddNode<SkeletonNode>();
            var boneNodes = new BoneNode[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                var boneNode = skeletonNode.AddNode<BoneNode>();

                boneNode.AddString("n", bone.Name);

                if (bone.BindTransform.LocalPosition.HasValue)
                    boneNode.AddValue("lp", bone.BindTransform.LocalPosition.Value);
                if (bone.BindTransform.LocalRotation.HasValue)
                    boneNode.AddValue("lr", CastHelpers.CreateVector4FromQuaternion(bone.BindTransform.LocalRotation.Value));
                if (bone.BindTransform.WorldPosition.HasValue)
                    boneNode.AddValue("wp", bone.BindTransform.WorldPosition.Value);
                if (bone.BindTransform.WorldRotation.HasValue)
                    boneNode.AddValue("wr", CastHelpers.CreateVector4FromQuaternion(bone.BindTransform.WorldRotation.Value));

                boneNodes[i] = boneNode;
            }

            // Second pass — resolve parent indices
            for (int i = 0; i < bones.Length; i++)
            {
                int bestParentIndex = SceneNode.GetBestParentIndex(bones[i], exportedBoneNodes);
                uint parentIndex = bestParentIndex >= 0 ? (uint)bestParentIndex : uint.MaxValue;
                boneNodes[i].AddValue("p", parentIndex);
            }
        }

        // Meshes
        var boneTable = new Dictionary<SkeletonBone, int>(bones.Length);
        for (int i = 0; i < bones.Length; i++)
            boneTable[bones[i]] = i;

        foreach (Mesh mesh in model.GetDescendants<Mesh>(selection.Filter))
        {
            CastMeshTranslator.Write(modelNode, mesh, boneTable, selection);
        }

        // Materials
        foreach (Material material in model.GetDescendants<Material>(selection.Filter))
        {
            var materialNode = modelNode.AddNode(new MaterialNode(material.Name, "pbr"));

            foreach (var texture in material.EnumerateChildren<Texture>())
            {
                if (texture.Slot.Equals("diffuse", StringComparison.OrdinalIgnoreCase))
                {
                    var fileNode = materialNode.AddNode<FileNode>();
                    fileNode.Hash = CastHasher.Compute(texture.EffectiveFilePath);
                    fileNode.AddString("p", texture.EffectiveFilePath);
                    materialNode.AddValue("diffuse", fileNode.Hash);
                }
                else if (texture.Slot.Equals("normal", StringComparison.OrdinalIgnoreCase))
                {
                    var fileNode = materialNode.AddNode<FileNode>();
                    fileNode.Hash = CastHasher.Compute(texture.EffectiveFilePath);
                    fileNode.AddString("p", texture.EffectiveFilePath);
                    materialNode.AddValue("normal", fileNode.Hash);
                }
                else if (texture.Slot.Equals("specular", StringComparison.OrdinalIgnoreCase))
                {
                    var fileNode = materialNode.AddNode<FileNode>();
                    fileNode.Hash = CastHasher.Compute(texture.EffectiveFilePath);
                    fileNode.AddString("p", texture.EffectiveFilePath);
                    materialNode.AddValue("specular", fileNode.Hash);
                }
            }
        }
    }
}
