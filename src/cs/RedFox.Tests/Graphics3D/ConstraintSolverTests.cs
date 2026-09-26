using System.Numerics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;
using RedFox.Graphics3D.Solvers;

namespace RedFox.Tests.Graphics3D;

public sealed class ConstraintSolverTests
{
    [Fact]
    public void PointConstraint_MovesConstrainedNodeToSourceWithOffsetAndSkips()
    {
        Scene scene = new("point");
        SkeletonBone parent = scene.RootNode.AddNode(new SkeletonBone("parent"));
        SkeletonBone constrained = parent.AddNode(new SkeletonBone("constrained"));
        SkeletonBone source = scene.RootNode.AddNode(new SkeletonBone("source"));

        parent.BindTransform.LocalPosition = new Vector3(1f, 0f, 0f);
        constrained.BindTransform.LocalPosition = new Vector3(0f, 0f, 7f);
        source.BindTransform.LocalPosition = new Vector3(5f, 5f, 5f);

        new PointConstraintNode("point", constrained, source) { TranslationOffset = new Vector3(0f, 1f, 0f), SkipZ = true }.CreateSolver().Solve(0f);

        Assert.Equal(new Vector3(5f, 6f, 7f), constrained.GetActiveWorldPosition());
    }

    [Fact]
    public void ScaleConstraint_MatchesSourceScaleWithOffsetAndWeight()
    {
        Scene scene = new("scale");
        SkeletonBone constrained = scene.RootNode.AddNode(new SkeletonBone("constrained"));
        SkeletonBone source = scene.RootNode.AddNode(new SkeletonBone("source"));

        source.BindTransform.Scale = new Vector3(3f);

        new ScaleConstraintNode("scale", constrained, source) { ScaleOffset = new Vector3(2f, 1f, 1f), SkipY = true, Weight = 0.5f }.CreateSolver().Solve(0f);

        Assert.Equal(new Vector3(3.5f, 1f, 2f), constrained.GetLiveLocalScale());
    }

    [Fact]
    public void IKHandleNode_CreatesTwoBoneSolverForThreeBoneChains()
    {
        SkeletonBone root = new("root");
        SkeletonBone mid = root.AddNode(new SkeletonBone("mid"));
        SkeletonBone tip = mid.AddNode(new SkeletonBone("tip"));
        SkeletonBone target = new("target");

        IKHandleNode handle = new("ik", root, tip) { TargetNode = target, TargetOffset = Vector3.UnitX };
        TwoBoneIKSolver solver = Assert.IsType<TwoBoneIKSolver>(handle.CreateSolver());

        Assert.Equal([root, mid, tip], handle.GetChain());
        Assert.Same(target, solver.TargetNode);
        Assert.Equal(Vector3.UnitX, solver.TargetOffset);
    }

    [Fact]
    public void IKHandleNode_CreatesCCDSolverForLongerChains()
    {
        SkeletonBone root = new("root");
        SkeletonBone tip = root.AddNode(new SkeletonBone("a")).AddNode(new SkeletonBone("b")).AddNode(new SkeletonBone("tip"));

        CCDIKSolver solver = Assert.IsType<CCDIKSolver>(new IKHandleNode("ik", root, tip).CreateSolver());

        Assert.Equal(4, solver.Chain.Count);
    }

    [Fact]
    public void IKHandleNode_ReturnsEmptyChainWhenEndIsNotBelowStart()
    {
        Assert.Empty(new IKHandleNode("ik", new SkeletonBone("a"), new SkeletonBone("b")).GetChain());
    }

    [Fact]
    public void SkeletonAnimationSampler_ScalesAdditiveCurvesByBlendWeight()
    {
        Scene scene = new("additive");
        SkeletonBone bone = scene.RootNode.AddNode(new SkeletonBone("bone"));
        SkeletonAnimation animation = new("walk");
        SkeletonAnimationTrack track = new("bone");

        bone.BindTransform.LocalPosition = Vector3.Zero;
        track.TranslationCurve = AnimationCurve.CreateVector3(TransformSpace.Local, TransformType.Additive);
        track.TranslationCurve.Add(0f, new Vector3(4f, 0f, 0f));
        track.TranslationCurve.BlendWeight = 0.25f;
        animation.Tracks.Add(track);

        new SkeletonAnimationSampler("sampler", animation, scene.RootNode).Update(0f);

        Assert.Equal(new Vector3(1f, 0f, 0f), bone.GetLiveLocalPosition());
    }

    [Fact]
    public void OrientConstraint_LeavesSkippedAxesUnchanged()
    {
        Scene scene = new("orient");
        SkeletonBone parent = scene.RootNode.AddNode(new SkeletonBone("parent"));
        SkeletonBone constrained = parent.AddNode(new SkeletonBone("constrained"));
        SkeletonBone source = scene.RootNode.AddNode(new SkeletonBone("source"));

        parent.BindTransform.LocalRotation = Quaternion.Identity;
        constrained.BindTransform.LocalRotation = Quaternion.Identity;
        source.BindTransform.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.5f);

        new OrientConstraintNode("orient", constrained, source) { SkipZ = true }.CreateSolver().Solve(0f);
        Assert.True(Quaternion.Dot(Quaternion.Identity, constrained.GetLiveLocalRotation()) > 0.9999f);

        new OrientConstraintNode("orient", constrained, source) { SkipX = true }.CreateSolver().Solve(0f);
        Assert.True(MathF.Abs(Quaternion.Dot(source.GetActiveWorldRotation(), constrained.GetActiveWorldRotation())) > 0.9999f);
    }

    [Fact]
    public void ScaleConstraint_UsesWorldScale()
    {
        Scene scene = new("scale");
        SkeletonBone parent = scene.RootNode.AddNode(new SkeletonBone("parent"));
        SkeletonBone constrained = parent.AddNode(new SkeletonBone("constrained"));
        SkeletonBone source = scene.RootNode.AddNode(new SkeletonBone("source"));

        parent.BindTransform.Scale = new Vector3(2f);
        source.BindTransform.Scale = new Vector3(4f);

        new ScaleConstraintNode("scale", constrained, source).CreateSolver().Solve(0f);

        Assert.Equal(new Vector3(2f), constrained.GetLiveLocalScale());
    }

    [Fact]
    public void TwoBoneIKSolver_UsesTargetRotationWhenRequested()
    {
        Scene scene = new("ik");
        SkeletonBone root = scene.RootNode.AddNode(new SkeletonBone("root"));
        SkeletonBone mid = root.AddNode(new SkeletonBone("mid"));
        SkeletonBone tip = mid.AddNode(new SkeletonBone("tip"));
        SkeletonBone target = scene.RootNode.AddNode(new SkeletonBone("target"));
        Quaternion targetRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1f);

        mid.BindTransform.LocalPosition = new Vector3(0f, 0f, 2f);
        tip.BindTransform.LocalPosition = new Vector3(0f, 0f, 2f);
        target.BindTransform.LocalPosition = new Vector3(1f, 0f, 2f);
        target.BindTransform.LocalRotation = targetRotation;

        new IKHandleNode("ik", root, tip) { TargetNode = target, UseTargetRotation = true }.CreateSolver().Solve(0f);

        Assert.True(MathF.Abs(Quaternion.Dot(targetRotation, tip.GetActiveWorldRotation())) > 0.9999f);
    }

    [Fact]
    public void CreateAnimationPlayers_AddsSolversForConstraintsAndHandlesOnAnimatedSkeleton()
    {
        Scene scene = new("rig");
        SkeletonBone root = scene.RootNode.AddNode(new SkeletonBone("root"));
        SkeletonBone mid = root.AddNode(new SkeletonBone("mid"));
        SkeletonBone tip = mid.AddNode(new SkeletonBone("tip"));
        SkeletonAnimation animation = scene.RootNode.AddNode(new SkeletonAnimation("walk"));
        SkeletonAnimationTrack track = new("root");

        track.AddTranslationFrame(0f, Vector3.Zero);
        animation.Tracks.Add(track);
        scene.RootNode.AddNode(new IKHandleNode("ik", root, tip));
        scene.RootNode.AddNode(new ParentConstraintNode("parent", tip, root));
        scene.RootNode.AddNode(new PointConstraintNode("unrelated", new SkeletonBone("loose"), root));

        AnimationPlayer player = Assert.Single(scene.CreateAnimationPlayers());

        Assert.Equal(2, player.Solvers.Count);
        Assert.IsType<TwoBoneIKSolver>(player.Solvers[0]);
        Assert.IsType<ParentConstraint>(player.Solvers[1]);
    }

    [Fact]
    public void Skin_ClonePreservesSkinningMode()
    {
        Skin skin = new([], new RedFox.Graphics3D.Buffers.DataBuffer<int>(0, 1, 1), new RedFox.Graphics3D.Buffers.DataBuffer<float>(0, 1, 1)) { SkinningMode = SkinningMode.DualQuaternion };

        Assert.Equal(SkinningMode.DualQuaternion, skin.Clone().SkinningMode);
    }
}
