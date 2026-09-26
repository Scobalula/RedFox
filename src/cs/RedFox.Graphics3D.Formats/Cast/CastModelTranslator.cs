using CastNet.Nodes;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastModelTranslator
{
    public static void Read(Scene scene, ModelNode modelNode, string name, string? sourceDirectory)
    {
        var model = scene.RootNode.AddNode<MeshGroup>(name);
        var bones = modelNode.Skeleton is SkeletonNode skeletonNode ? CastSkeletonTranslator.Read(model, skeletonNode, $"{name}_Skeleton") : null;
        var materials = new Dictionary<MaterialNode, Material>();
        var meshes = new Dictionary<MeshNode, Mesh>();

        model.BindTransform.LocalPosition = modelNode.Position;
        model.BindTransform.LocalRotation = modelNode.Rotation;
        model.BindTransform.Scale = modelNode.Scale;

        if (bones is not null)
            CastConstraintTranslator.Read(model, modelNode.Skeleton!, bones);

        foreach (var materialNode in modelNode.EnumerateMaterials())
            materials[materialNode] = CastMaterialTranslator.Read(model, materialNode, sourceDirectory);

        foreach (var meshNode in modelNode.EnumerateMeshes())
            meshes[meshNode] = CastMeshTranslator.Read(model, meshNode, materials, bones);

        foreach (var hairNode in modelNode.EnumerateHairs())
            CastHairTranslator.Read(model, hairNode, materials);

        CastMorphTranslator.Read(modelNode, meshes);
    }

    public static void Write(RootNode root, SceneNode model, SceneTranslationSelection selection, string? targetDirectory)
    {
        var modelNode = root.AddNode(new ModelNode { Name = model.Name, Position = model.BindTransform.LocalPosition, Rotation = model.BindTransform.LocalRotation, Scale = model.BindTransform.Scale });
        var bones = selection.GetDescendants<SkeletonBone>();
        var boneTable = new Dictionary<SkeletonBone, int>(bones.Length);
        var materials = new Dictionary<Material, MaterialNode>();

        if (bones.Length > 0)
        {
            var skeletonNode = modelNode.AddNode<SkeletonNode>();
            var exportedBoneNodes = Array.ConvertAll(bones, static bone => (SceneNode)bone);
            var boneNodes = new Dictionary<SkeletonBone, BoneNode>(bones.Length);

            for (var i = 0; i < bones.Length; i++)
            {
                var transform = bones[i].BindTransform;

                boneNodes[bones[i]] = skeletonNode.AddNode(new BoneNode { Name = bones[i].Name, ParentIndex = SceneNode.GetBestParentIndex(bones[i], exportedBoneNodes), LocalPosition = transform.LocalPosition, LocalRotation = transform.LocalRotation, WorldPosition = transform.WorldPosition, WorldRotation = transform.WorldRotation, Scale = transform.Scale });
                boneTable[bones[i]] = i;
            }

            CastConstraintTranslator.Write(skeletonNode, model, boneNodes, selection);
        }

        foreach (var material in model.GetDescendants<Material>(selection.Filter))
            materials[material] = CastMaterialTranslator.Write(modelNode, material, targetDirectory);

        foreach (var mesh in model.GetDescendants<Mesh>(selection.Filter))
        {
            if (mesh.Materials is [Material material, ..])
                AddReferencedMaterial(modelNode, materials, material, mesh.Name, selection, targetDirectory);

            if (CastMeshTranslator.Write(modelNode, mesh, boneTable, materials) is MeshNode meshNode)
                CastMorphTranslator.Write(modelNode, meshNode, mesh);
        }

        foreach (var hair in model.GetDescendants<Hair>(selection.Filter))
        {
            if (hair.Material is Material material)
                AddReferencedMaterial(modelNode, materials, material, hair.Name, selection, targetDirectory);

            CastHairTranslator.Write(modelNode, hair, materials);
        }
    }

    private static void AddReferencedMaterial(ModelNode modelNode, Dictionary<Material, MaterialNode> materials, Material material, string ownerName, SceneTranslationSelection selection, string? targetDirectory)
    {
        if (materials.ContainsKey(material))
            return;
        if (!selection.Includes(material))
            throw new InvalidDataException($"Cannot write Cast: '{ownerName}' references material '{material.Name}' that is not included in the export selection.");

        materials[material] = CastMaterialTranslator.Write(modelNode, material, targetDirectory);
    }
}
