using System.Numerics;
using RedFox.Graphics3D;

namespace RedFox.Tests.Graphics3D;

public sealed class SceneGraphTests
{
    private const float Tolerance = 1e-4f;

    private static readonly Quaternion QuarterTurnY = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);

    [Fact]
    public void AddNode_Self_Throws()
    {
        Group node = new("Node");

        Assert.Throws<SceneNodeParentException>(() => node.AddNode(node));
    }

    [Fact]
    public void AddNode_Ancestor_ThrowsAndLeavesGraphUnchanged()
    {
        Group root = new("Root");
        Group child = root.AddNode(new Group("Child"));

        Assert.Throws<SceneNodeParentException>(() => child.AddNode(root));
        Assert.Null(root.Parent);
        Assert.Single(child.EnumerateAncestors());
    }

    [Fact]
    public void MoveTo_Descendant_Throws()
    {
        Group root = new("Root");
        Group child = root.AddNode(new Group("Child"));
        Group grandChild = child.AddNode(new Group("GrandChild"));

        Assert.Throws<SceneNodeParentException>(() => root.MoveTo(grandChild));
        Assert.Throws<SceneNodeParentException>(() => child.MoveTo(child));
        Assert.Same(root, child.Parent);
    }

    [Fact]
    public void MoveTo_RemovesNodeFromPreviousParent()
    {
        Group root = new("Root");
        Group first = root.AddNode(new Group("First"));
        Group second = root.AddNode(new Group("Second"));
        Group node = first.AddNode(new Group("Node"));

        node.MoveTo(second);

        Assert.Empty(first.EnumerateChildren());
        Assert.Same(node, Assert.Single(second.EnumerateChildren()));
        Assert.Same(second, node.Parent);
    }

    [Fact]
    public void Dispose_DetachesNodeFromParent()
    {
        Group root = new("Root");
        Group child = root.AddNode(new Group("Child"));
        child.AddNode(new Group("GrandChild"));

        child.Dispose();

        Assert.Empty(root.EnumerateChildren());
        Assert.Null(child.Parent);
    }

    [Fact]
    public void WorldPose_ComposesParentRotationThenTranslation()
    {
        (_, Group child) = CreateChain();

        AssertApproximately(new Vector3(1f, 2f, 2f), child.GetBindWorldPosition());
        AssertApproximately(QuarterTurnY, child.GetBindWorldRotation());
    }

    [Fact]
    public void WorldMatrix_IsScaleRotationTranslationTimesParentWorld()
    {
        (_, Group child) = CreateChain();
        child.BindTransform.Scale = new Vector3(2f);

        Matrix4x4 expectedLocal = Matrix4x4.CreateScale(2f) * Matrix4x4.CreateTranslation(1f, 0f, 0f);
        Matrix4x4 expectedWorld = expectedLocal * Matrix4x4.CreateFromQuaternion(QuarterTurnY) * Matrix4x4.CreateTranslation(1f, 2f, 3f);

        AssertApproximately(expectedWorld, child.GetBindWorldMatrix());
        AssertApproximately(child.GetBindWorldPosition(), child.GetBindWorldMatrix().Translation);
    }

    [Fact]
    public void WorldPosition_IsRigidAndIgnoresAncestorScale()
    {
        (Group parent, Group child) = CreateChain();
        parent.BindTransform.Scale = new Vector3(10f);

        AssertApproximately(new Vector3(1f, 2f, 2f), child.GetBindWorldPosition());
        AssertApproximately(new Vector3(1f, 2f, -7f), child.GetBindWorldMatrix().Translation);
    }

    [Fact]
    public void WorldPose_ReflectsParentChangesAfterBeingQueried()
    {
        (Group parent, Group child) = CreateChain();
        _ = child.GetBindWorldPosition();

        parent.BindTransform.LocalPosition = Vector3.Zero;
        parent.BindTransform.LocalRotation = Quaternion.Identity;

        AssertApproximately(new Vector3(1f, 0f, 0f), child.GetBindWorldPosition());
        Assert.Null(child.BindTransform.WorldPosition);
    }

    [Fact]
    public void LocalPose_IsDerivedFromExplicitWorldValues()
    {
        (_, Group child) = CreateChain();
        child.BindTransform.LocalPosition = null;
        child.BindTransform.WorldPosition = new Vector3(1f, 2f, 1f);
        child.BindTransform.WorldRotation = Quaternion.Identity;

        AssertApproximately(new Vector3(2f, 0f, 0f), child.GetBindLocalPosition());
        AssertApproximately(Quaternion.Conjugate(QuarterTurnY), child.GetBindLocalRotation());
    }

    [Fact]
    public void MoveTo_PreserveWorld_KeepsWorldPoseAndDescendantsFollow()
    {
        (_, Group child) = CreateChain();
        Group grandChild = child.AddNode(new Group("GrandChild"));
        grandChild.BindTransform.LocalPosition = new Vector3(0f, 1f, 0f);
        Group target = new("Target");
        target.BindTransform.LocalPosition = new Vector3(-5f, 0f, 0f);

        Vector3 childWorld = child.GetBindWorldPosition();
        Vector3 grandChildWorld = grandChild.GetBindWorldPosition();

        child.MoveTo(target);

        AssertApproximately(childWorld, child.GetBindWorldPosition());
        AssertApproximately(grandChildWorld, grandChild.GetBindWorldPosition());
        AssertApproximately(new Vector3(0f, 1f, 0f), grandChild.GetBindLocalPosition());
    }

    [Fact]
    public void MoveTo_PreserveLocal_KeepsLocalPose()
    {
        (_, Group child) = CreateChain();
        child.BindTransform.LocalPosition = null;
        child.BindTransform.WorldPosition = new Vector3(1f, 2f, 1f);
        Group target = new("Target");

        Vector3 local = child.GetBindLocalPosition();
        child.MoveTo(target, ReparentTransformMode.PreserveLocal);

        AssertApproximately(local, child.GetBindLocalPosition());
        AssertApproximately(local, child.GetBindWorldPosition());
    }

    [Fact]
    public void ActivePose_FollowsAnimatedParentWhenNodeHasNoLiveValues()
    {
        (Group parent, Group child) = CreateChain();
        parent.LiveTransform.LocalPosition = new Vector3(10f, 0f, 0f);

        AssertApproximately(new Vector3(10f, 0f, -1f), child.GetActiveWorldPosition());
        AssertApproximately(child.GetActiveWorldMatrix().Translation, child.GetActiveWorldPosition());
    }

    [Fact]
    public void ActivePose_LiveWorldValuesDriveTheWorldMatrix()
    {
        (_, Group child) = CreateChain();
        child.LiveTransform.WorldPosition = new Vector3(4f, 5f, 6f);
        child.LiveTransform.WorldRotation = Quaternion.Identity;

        AssertApproximately(new Vector3(4f, 5f, 6f), child.GetActiveWorldMatrix().Translation);
        AssertApproximately(Quaternion.Conjugate(QuarterTurnY), child.GetLiveLocalRotation());
    }

    private static void AssertApproximately(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < Tolerance, $"Expected {expected}, got {actual}");

    private static void AssertApproximately(Quaternion expected, Quaternion actual) => Assert.True(MathF.Abs(Quaternion.Dot(expected, actual)) > 1f - Tolerance, $"Expected {expected}, got {actual}");

    private static void AssertApproximately(Matrix4x4 expected, Matrix4x4 actual)
    {
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
                Assert.True(MathF.Abs(expected[row, column] - actual[row, column]) < Tolerance, $"Expected {expected}, got {actual}");
        }
    }

    private static (Group Parent, Group Child) CreateChain()
    {
        Group parent = new("Parent");
        parent.BindTransform.LocalPosition = new Vector3(1f, 2f, 3f);
        parent.BindTransform.LocalRotation = QuarterTurnY;

        Group child = parent.AddNode(new Group("Child"));
        child.BindTransform.LocalPosition = new Vector3(1f, 0f, 0f);

        return (parent, child);
    }
}
