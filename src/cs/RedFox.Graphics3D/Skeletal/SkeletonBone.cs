using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace RedFox.Graphics3D.Skeletal
{
    public class SkeletonBone : Graphics3DObject
    {
        /// <summary>
        /// Gets or Sets if this bone can be animated.
        /// </summary>
        public bool CanAnimate { get; set; }

        public SkeletonBone()
        {
            
        }

        public SkeletonBone(string boneName) : base(boneName) { }
    }
}