using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace RedFox.Graphics3D;


public class SkeletonBone : Skeleton
{
    public SkeletonBone(string name) : base(name) { }

    public SkeletonBone(string name, SceneNodeFlags flags) : base(name, flags) { }
}
