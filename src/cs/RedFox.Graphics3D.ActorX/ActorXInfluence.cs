namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// An ActorX raw bone influence: the weight a single bone applies to a single mesh point.
/// </summary>
/// <param name="Weight">The influence weight.</param>
/// <param name="PointIndex">The index of the affected point within the points chunk.</param>
/// <param name="BoneIndex">The index of the influencing bone within the skeleton.</param>
public readonly record struct ActorXInfluence(float Weight, int PointIndex, int BoneIndex);
