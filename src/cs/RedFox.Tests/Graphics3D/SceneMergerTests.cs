using System.Numerics;
using RedFox.Graphics3D;

namespace RedFox.Tests.Graphics3D;

public sealed class SceneMergerTests
{
    [Fact]
    public void MergeNode_AllowsDuplicateNamesWhenDuplicateScopeIsNone()
    {
        Group targetParent = new("Target");
        Group existing = targetParent.AddNode(new Group("Node"));
        Group incoming = new("Node");
        SceneMergeOptions options = new()
        {
            DuplicateScope = SceneNodeMatchScope.None,
        };

        SceneNode result = SceneMerger.MergeNode(targetParent, incoming, options);

        Assert.Same(incoming, result);
        Assert.Equal([existing, incoming], targetParent.EnumerateChildren());
    }

    [Fact]
    public void MergeNode_PreserveWorldModeKeepsIncomingWorldPosition()
    {
        Group targetParent = new("Target");
        Group stagingParent = new("Staging");
        targetParent.BindTransform.LocalPosition = new Vector3(10f, 0f, 0f);
        stagingParent.BindTransform.LocalPosition = new Vector3(2f, 0f, 0f);
        Group incoming = stagingParent.AddNode(new Group("Incoming"));
        incoming.BindTransform.LocalPosition = new Vector3(3f, 0f, 0f);
        Vector3 worldPosition = incoming.GetBindWorldPosition();
        SceneMergeOptions options = new()
        {
            DuplicateScope = SceneNodeMatchScope.None,
            TransformMode = ReparentTransformMode.PreserveWorld,
        };

        SceneMerger.MergeNode(targetParent, incoming, options);

        Assert.Equal(worldPosition, incoming.GetBindWorldPosition());
    }
}
