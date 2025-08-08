using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.Skeletal
{
    public class Skeleton : Graphics3DObject
    {
        /// <summary>
        /// Gets or Sets the bones stored within this skeleton.
        /// </summary>
        public List<SkeletonBone> Bones { get; set; } = [];

        public Skeleton() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Skeleton"/>.
        /// </summary>
        /// <param name="name">The name of the <see cref="Graphics3DObject"/> object.</param>
        public Skeleton(string name) : base(name)
        {
            Bones = [];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Skeleton"/>.
        /// </summary>
        /// <param name="name">The name of the <see cref="Graphics3DObject"/> object.</param>
        /// <param name="bones">The bones to use for the skeleton.</param>
        public Skeleton(string name, List<SkeletonBone> bones) : base(name)
        {
            Bones = bones;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Skeleton"/>.
        /// </summary>
        /// <param name="name">The name of the <see cref="Graphics3DObject"/> object.</param>
        /// <param name="boneCount">The number to allocate in the list.</param>
        public Skeleton(string name, int boneCount) : base(name)
        {
            Bones = new(boneCount);
        }

        /// <summary>
        /// Searches for a bone with the given name.
        /// </summary>
        /// <param name="boneName">The name of the bone to search for.</param>
        /// <returns>The first bone in the list that matches the name provided, otherwise null is returned.</returns>
        public SkeletonBone? FindBone(string? boneName) => Bones.Find(x => x.Name.Equals(boneName));

        /// <summary>
        /// Searches for a bone with the given name.
        /// </summary>
        /// <param name="boneName">The name of the bone to search for.</param>
        /// <returns>The index of the first bone in the list that matches the name provided, otherwise -1 is returned.</returns>
        public int FindBoneIndex(string? boneName) => Bones.FindIndex(x => x.Name.Equals(boneName));

        /// <summary>
        /// Searches for a bone with the given name.
        /// </summary>
        /// <param name="boneName">Name of the bone</param>
        /// <param name="bone">The <see cref="SkeletonBone"/> if found, otherwise null.</param>
        /// <returns>True if the bone is found, otherwise false</returns>
        public bool TryGetBone(string? boneName, [NotNullWhen(true)] out SkeletonBone? bone) => (bone = Bones.Find(x => x.Name.Equals(boneName))) != null;

        /// <summary>
        /// Searches for a bone with the given name.
        /// </summary>
        /// <param name="boneName">Name of the bone</param>
        /// <param name="bone">The index of the <see cref="SkeletonBone"/> if found, otherwise -1.</param>
        /// <returns>True if the bone is found, otherwise false</returns>
        public bool TryGetBoneIndex(string? boneName, out int boneIndex) => (boneIndex = Bones.FindIndex(x => x.Name.Equals(boneName))) != -1;


        /// <summary>
        /// Searches for a bone with the given name.
        /// </summary>
        /// <param name="boneName">Name of the bone</param>
        /// <returns>True of the bone if found, otherwise false</returns>
        public bool ContainsBone(string? boneName) => Bones.FindIndex(x => x.Name.Equals(boneName)) != -1;

        /// <summary>
        /// Adds the <see cref="SkeletonBone"/> to this skeleton and assignes the required data.
        /// </summary>
        /// <param name="bone">The bone to add to this skeleton.</param>
        public void AddBone(SkeletonBone bone)
        {
            Bones.Add(bone);
        }

        /// <summary>
        /// Enumerates the bones depth first from the root nodes.
        /// </summary>
        /// <returns>Current bone.</returns>
        public IEnumerable<SkeletonBone> EnumerateHierarchy()
        {
            var boneStack = new Stack<SkeletonBone>();

            // Push roots first
            foreach (var bone in Bones)
                if (bone.Parent == null)
                    boneStack.Push(bone);

            while (boneStack.Count > 0)
            {
                var currentBone = boneStack.Pop();

                yield return currentBone;

                foreach (var child in currentBone.Children)
                    if (child is SkeletonBone bone)
                        boneStack.Push(bone);
            }
        }

        //public IEnumerable<SkeletonBone> EnumerateRoots()
        //{
        //    foreach (var bone in Bones)
        //    {
        //        if (bone.Parent == null)
        //        {
        //            yield return bone;
        //        }
        //    }
        //}

        //public void GenerateLocalTransforms()
        //{
        //    foreach (var bone in EnumerateHierarchy())
        //    {
        //        bone.GenerateLocalTransform();
        //    }
        //}

        //public void GenerateGlobalTransforms()
        //{
        //    foreach (var bone in EnumerateHierarchy())
        //    {
        //        bone.GenerateWorldTransform();
        //    }
        //}

        ///// <summary>
        ///// Assigns the bone indices based off their index within the table. 
        ///// </summary>
        //public void AssignBoneIndices()
        //{
        //    for (int i = 0; i < Bones.Count; i++)
        //    {
        //        Bones[i].Index = i;
        //    }
        //}

        //public Skeleton CreateCopy()
        //{
        //    var newSkeleton = new Skeleton(Name);
        //    var bones = new SkeletonBone[Bones.Count];

        //    foreach(var bone in EnumerateHierarchy())
        //    {
        //        bones[bone.Index] = new SkeletonBone(bone.Name)
        //        {
        //            Index                   = bone.Index,
        //            Parent                  = bone.Parent != null ? bones.First(x => x?.Name == bone.Parent?.Name) : null,
        //            BaseLocalTranslation    = bone.BaseLocalTranslation,
        //            BaseLocalRotation       = bone.BaseLocalRotation,
        //            BaseWorldTranslation    = bone.BaseWorldTranslation,
        //            BaseWorldRotation       = bone.BaseWorldRotation,
        //            LocalRotation    = bone.BaseLocalRotation,
        //            LocalTranslation = bone.BaseLocalTranslation,
        //            WorldRotation    = bone.BaseWorldRotation,
        //            WorldTranslation = bone.BaseWorldTranslation,
        //            BaseScale               = bone.BaseScale,
        //            CanAnimate              = bone.CanAnimate
        //        };
        //    }

        //    newSkeleton.Bones = [..bones];
        //    return newSkeleton;
        //}

        //public void CreateAnimationTargets(SkeletonAnimation animation) => Bones.ForEach(x => animation.Tracks.Add(new(x.Name)));

        //public void InitializeAnimationTransforms()
        //{
        //    // TODO: Make it so we don't have to call this every frame...
        //    foreach (var bone in Bones)
        //    {
        //        bone.InitializeAnimationTransforms();
        //    }
        //}

        //public void Update()
        //{
        //    foreach (var bone in EnumerateHierarchy())
        //    {
        //        bone.GenerateCurrentWorldTransform();
        //    }
        //}
    }
}
