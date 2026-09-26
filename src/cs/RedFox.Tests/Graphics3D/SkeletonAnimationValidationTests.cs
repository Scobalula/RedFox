using System.Numerics;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Tests.Graphics3D;

public sealed class SkeletonAnimationValidationTests
{
    [Fact]
    public void Validate_TracksBoundToBonesWithOrderedKeys_ReturnsTrue()
    {
        SkeletonAnimation animation = new("Walk");
        SkeletonAnimationTrack track = new("Spine");
        track.AddRotationFrame(0f, Quaternion.Identity);
        track.AddRotationFrame(1f, Quaternion.Identity);
        animation.Tracks.Add(track);

        StringBuilder messages = new();

        Assert.True(animation.Validate(CreateSkeleton(), messages), messages.ToString());
    }

    [Fact]
    public void Validate_ReportsUnboundTracksAndUnorderedKeys()
    {
        SkeletonAnimation animation = new("Walk");
        SkeletonAnimationTrack missing = new("Tail");
        missing.AddTranslationFrame(0f, Vector3.Zero);
        SkeletonAnimationTrack unordered = new("Hips");
        unordered.AddTranslationFrame(1f, Vector3.Zero);
        unordered.AddTranslationFrame(0f, Vector3.One);
        animation.Tracks.Add(missing);
        animation.Tracks.Add(unordered);

        StringBuilder messages = new();

        Assert.False(animation.Validate(CreateSkeleton(), messages));
        Assert.Contains("track 'Tail' matches 0 bones", messages.ToString());
        Assert.Contains("track 'Hips' translation curve keys are not in ascending time order", messages.ToString());
    }

    private static Group CreateSkeleton()
    {
        Group root = new("Root");
        SkeletonBone hips = root.AddNode(new SkeletonBone("Hips"));
        hips.AddNode(new SkeletonBone("Spine"));
        return root;
    }
}
