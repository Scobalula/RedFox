using CastNet.Nodes;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastModelTranslator
{
    public static void Read(Scene scene, ModelNode modelNode, string name, string? sourceDirectory)
    {
        var model				= scene.RootNode.AddNode<MeshGroup>(name);
        SkeletonNode? skeleton	= modelNode.Skeleton;
        SkeletonBone[]? bones	= null;
        if (skeleton is not null)
            bones = CastSkeletonTranslator.Read(model, skeleton, $"{name}_Skeleton");
        var materials	= new Dictionary<MaterialNode, Material>();
        var meshes		= new Dictionary<MeshNode, Mesh>();

        model.BindTransform.LocalPosition = modelNode.Position;
        model.BindTransform.LocalRotation = modelNode.Rotation;
        model.BindTransform.Scale = modelNode.Scale;

        if (skeleton is not null && bones is not null)
            CastConstraintTranslator.Read(model, skeleton, bones);

        foreach (var materialNode in modelNode.EnumerateMaterials())
            materials[materialNode] = CastMaterialTranslator.Read(model, materialNode, sourceDirectory);

        foreach (var meshNode in modelNode.EnumerateMeshes())
            meshes[meshNode] = CastMeshTranslator.Read(model, meshNode, materials, bones);

        foreach (var hairNode in modelNode.EnumerateHairs())
            CastHairTranslator.Read(model, hairNode, materials);

        CastMorphTranslator.Read(modelNode, meshes);
    }

    public static void Write(RootNode root, SceneNode model, IReadOnlyList<SceneNode> content, SceneTranslationSelection selection, string? targetDirectory)
    {
        Transform modelTransform	= SkeletonExportTransforms.DecomposeMatrix(model.GetBindWorldMatrix());
        var modelNode				= root.AddNode(new ModelNode
        {
            Name		= model.Name,
            Position	= modelTransform.LocalPosition,
            Rotation	= modelTransform.LocalRotation,
            Scale		= modelTransform.Scale,
        });
        Dictionary<SkeletonBone, int> boneTable = [];
        List<SkeletonBone> boneList	= [];
        List<Material> materialList	= [];
        List<Mesh> meshList			= [];
        List<Hair> hairList			= [];
        foreach (SceneNode node in content)
        {
            switch (node)
            {
                case SkeletonBone bone: boneTable.Add(bone, boneList.Count); boneList.Add(bone); break;
                case Material material: materialList.Add(material); break;
                case Mesh mesh: meshList.Add(mesh); break;
                case Hair hair: hairList.Add(hair); break;
            }
        }
        SkeletonBone[] bones	= [.. boneList];
        var materials			= new Dictionary<Material, MaterialNode>();

        if (bones.Length > 0)
        {
            var skeletonNode		= modelNode.AddNode<SkeletonNode>();
            var exportedBoneNodes	= Array.ConvertAll(bones, static bone => (SceneNode)bone);
            var boneNodes			= new Dictionary<SkeletonBone, BoneNode>(bones.Length);

            Dictionary<SkeletonBone, SkeletonBone?> parents = SkeletonExportTransforms.BuildExportedParentMap(bones, exportedBoneNodes);
            for (var i = 0; i < bones.Length; i++)
            {
                var transform = SkeletonExportTransforms.DecomposeMatrix(SkeletonExportTransforms.GetRelativeBindMatrix(bones[i], parents[bones[i]] ?? model));

                boneNodes[bones[i]] = skeletonNode.AddNode(new BoneNode
                {
                    Name			= bones[i].Name,
                    ParentIndex		= parents[bones[i]] is { } parent ? boneTable[parent] : -1,
                    LocalPosition	= transform.LocalPosition,
                    LocalRotation	= transform.LocalRotation,
                    WorldPosition	= transform.WorldPosition,
                    WorldRotation	= transform.WorldRotation,
                    Scale			= transform.Scale,
                });
            }

            CastConstraintTranslator.Write(skeletonNode, content, boneNodes);
        }

        foreach (var material in materialList)
            materials[material] = CastMaterialTranslator.Write(modelNode, material, targetDirectory);

        foreach (var mesh in meshList)
        {
            if (mesh.Materials is [Material material, ..])
                AddReferencedMaterial(modelNode, materials, material, mesh.Name, selection, targetDirectory);

            if (CastMeshTranslator.Write(modelNode, mesh, boneTable, materials) is MeshNode meshNode)
                CastMorphTranslator.Write(modelNode, meshNode, mesh);
        }

        foreach (var hair in hairList)
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
