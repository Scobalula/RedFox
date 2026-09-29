// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Tests.Graphics3D;

/// <summary>
/// Unit tests for <see cref="SceneNode.Clone"/>.
/// </summary>
public sealed class SceneNodeCloneTests
{
    [Fact]
    public void Clone_Material_RedirectsTextureConnectionsToClonedChildren()
    {
        Material material = new("mat");
        Texture texture = material.AddNode(new Texture("diffuse.tga"));
        material.DiffuseMapName = "diffuse";
        material.Connect("diffuse", texture);

        Material clone = (Material)material.Clone();
        Texture clonedTexture = Assert.IsType<Texture>(Assert.Single(clone.Children!));

        Assert.NotSame(texture, clonedTexture);
        Assert.Same(clonedTexture, clone.GetDiffuseMap());
        Assert.Same(texture, material.GetDiffuseMap());
        Assert.Null(clone.Parent);
        Assert.Null(clone.Scene);
    }

    [Fact]
    public void Clone_Skeleton_CopiesHierarchyAndTransforms()
    {
        Group skeleton = new("skeleton");
        SkeletonBone root = skeleton.AddNode(new SkeletonBone("root"));
        SkeletonBone child = root.AddNode(new SkeletonBone("child"));
        child.BindTransform.LocalPosition = new Vector3(1, 2, 3);

        SceneNode clone = skeleton.Clone();
        SkeletonBone[] clonedBones = [.. clone.EnumerateHierarchy<SkeletonBone>()];

        Assert.Equal(["root", "child"], clonedBones.Select(bone => bone.Name));
        Assert.Same(clone, clonedBones[0].Parent);
        Assert.Same(clonedBones[0], clonedBones[1].Parent);
        Assert.NotSame(child.BindTransform, clonedBones[1].BindTransform);
        Assert.Equal(new Vector3(1, 2, 3), clonedBones[1].BindTransform.LocalPosition);
    }

    [Fact]
    public void Clone_Mesh_SharesBuffersButNotReferenceLists()
    {
        Material material = new("mat");
        Mesh mesh = new()
        {
            Positions = new DataBuffer<float>(1, 1, 3),
            Materials = [material],
        };

        Mesh clone = (Mesh)mesh.Clone();
        clone.Materials![0] = new Material("other");

        Assert.Same(mesh.Positions, clone.Positions);
        Assert.Same(material, mesh.Materials[0]);
    }

    [Fact]
    public void Clone_Scene_Throws()
    {
        Scene scene = new("scene");

        Assert.Throws<NotSupportedException>(() => scene.Clone());
    }

    [Fact]
    public void Clone_WideHierarchy_DoesNotThrow()
    {
        Group root = new("root");

        for (int i = 0; i < 12; i++)
            root.AddNode(new Group($"child_{i}"));

        SceneNode clone = root.Clone();

        Assert.Equal(12, clone.Children!.Count);
        Assert.Null(clone.Parent);
    }

    [Fact]
    public void Clone_WideHierarchy_DoesNotShareChildNameState()
    {
        Group root = new("root");

        for (int i = 0; i < 12; i++)
            root.AddNode(new Group($"child_{i}"));

        SceneNode clone = root.Clone();
        clone.AddNode(new Group("child_extra"));

        Assert.False(root.TryFindChild("child_extra", out _));
    }
}
