using System;
using System.Diagnostics;
using System.Numerics;

namespace RedFox.Graphics3D.Skeletal
{
    public class SkeletonBone : Graphics3DObject
    {
        /// <summary>
        /// Internal bone parent value.
        /// </summary>
        private SkeletonBone? _parent;

        /// <summary>
        /// Gets or Sets the index of the bone within the <see cref="Skeleton"/>.
        /// </summary>
        public int Index { get; internal set; }

        /// <summary>
        /// Gets or Sets the bones that are children of this bone.
        /// </summary>
        public List<SkeletonBone> Children { get; set; } = [];

        /// <summary>
        /// Gets or Sets the bone position relative to its parent.
        /// </summary>
        public Vector3 BaseLocalTranslation { get; set; }

        /// <summary>
        /// Gets or Sets the bone rotation relative to its parent.
        /// </summary>
        public Quaternion BaseLocalRotation { get; set; }

        /// <summary>
        /// Gets or Sets the bone position relative to the origin.
        /// </summary>
        public Vector3 BaseWorldTranslation { get; set; }

        /// <summary>
        /// Gets or Sets the bone rotation relative to the origin.
        /// </summary>
        public Quaternion BaseWorldRotation { get; set; }

        /// <summary>
        /// Gets or Sets the current bone position relative to its parent.
        /// </summary>
        public Vector3 LocalTranslation { get; set; }

        /// <summary>
        /// Gets or Sets the current bone rotation relative to its parent.
        /// </summary>
        public Quaternion LocalRotation { get; set; }

        /// <summary>
        /// Gets or Sets the current bone position relative to the origin.
        /// </summary>
        public Vector3 WorldTranslation { get; set; }

        /// <summary>
        /// Gets or Sets the current bone rotation relative to the origin.
        /// </summary>
        public Quaternion WorldRotation { get; set; }

        /// <summary>
        /// Gets or Sets the scale.
        /// </summary>
        public Vector3 BaseScale { get; set; }

        /// <summary>
        /// Gets or Sets the scale.
        /// </summary>
        public Vector3 Scale { get; set; }

        /// <summary>
        /// Gets or Sets if this bone can be animated.
        /// </summary>
        public bool CanAnimate { get; set; }

        /// <summary>
        /// Gets or Sets the parent of this bone.
        /// </summary>
        public SkeletonBone? Parent
        {
            get
            {
                return _parent;
            }
            set
            {
                _parent?.Children.Remove(this);
                _parent = value;
                _parent?.Children.Add(this);
            }
        }

        public SkeletonBone(string boneName) : base(boneName)
        {
            BaseLocalTranslation = Vector3.Zero;
            BaseLocalRotation = Quaternion.Identity;
            BaseWorldTranslation = Vector3.Zero;
            BaseWorldRotation = Quaternion.Identity;
        }

        public SkeletonBone(string boneName, Matrix4x4 localMatrix, Matrix4x4 globalMatrix) : base(boneName)
        {
            BaseLocalTranslation = localMatrix.Translation;
            BaseLocalRotation = Quaternion.CreateFromRotationMatrix(localMatrix);
            BaseWorldTranslation = globalMatrix.Translation;
            BaseWorldRotation = Quaternion.CreateFromRotationMatrix(globalMatrix);
        }

        /// <summary>
        /// Checks if this bone is a descendant of the given bone.
        /// </summary>
        /// <param name="bone">Parent to check for.</param>
        /// <returns>True if it is, otherwise false.</returns>
        public bool IsDescendantOf(SkeletonBone? bone)
        {
            if (bone == null)
                return false;

            var current = Parent;

            while (current is not null)
            {
                if (current == bone)
                    return true;

                current = current.Parent;
            }

            return false;
        }

        /// <summary>
        /// Checks if this bone is a descendant of the given bone by name.
        /// </summary>
        /// <param name="boneName">Name to check for.</param>
        /// <returns>True if it is, otherwise false.</returns>
        public bool IsDescendantOf(string? boneName) =>
            IsDescendantOf(boneName, StringComparison.CurrentCulture);

        /// <summary>
        /// Checks if this bone is a descendant of the given bone by name.
        /// </summary>
        /// <param name="boneName">Name to check for.</param>
        /// <param name="comparisonType">One of the enumeration values that specifies how the strings will be compared.</param>
        /// <returns>True if it is, otherwise false.</returns>
        public bool IsDescendantOf(string? boneName, StringComparison comparisonType)
        {
            if (string.IsNullOrWhiteSpace(boneName))
                return false;
            var current = Parent;

            while (current is not null)
            {
                if (current.Name.Equals(boneName, comparisonType))
                    return true;

                current = current.Parent;
            }

            return false;
        }

        public void GenerateLocalTransform()
        {
            if (Parent != null)
            {
                BaseLocalRotation = Quaternion.Conjugate(Parent.BaseWorldRotation) * BaseWorldRotation;
                BaseLocalTranslation = Vector3.Transform(BaseWorldTranslation - Parent.BaseWorldTranslation, Quaternion.Conjugate(Parent.BaseWorldRotation));
            }
            else
            {
                BaseLocalTranslation = BaseWorldTranslation;
                BaseLocalRotation = BaseWorldRotation;
            }
        }

        public void GenerateWorldTransform()
        {
            if (Parent != null)
            {
                BaseWorldRotation = Parent.BaseWorldRotation * BaseLocalRotation;
                BaseWorldTranslation = Vector3.Transform(BaseLocalTranslation, Parent.BaseWorldRotation) + Parent.BaseWorldTranslation;
            }
            else
            {
                BaseWorldTranslation = BaseLocalTranslation;
                BaseWorldRotation = BaseLocalRotation;
            }
        }

        public void GenerateWorldTransforms()
        {
            GenerateWorldTransform();

            foreach (var child in Children)
            {
                child.GenerateWorldTransforms();
            }
        }

        public void GenerateCurrentLocalTransform()
        {
            if (Parent != null)
            {
                LocalRotation = Quaternion.Conjugate(Parent.WorldRotation) * WorldRotation;
                LocalTranslation = Vector3.Transform(WorldTranslation - Parent.WorldTranslation, Quaternion.Conjugate(Parent.WorldRotation));
            }
            else
            {
                LocalTranslation = WorldTranslation;
                LocalRotation = WorldRotation;
            }
        }

        public void GenerateCurrentWorldTransform()
        {
            if (Parent != null)
            {
                WorldRotation = Parent.WorldRotation * LocalRotation;
                WorldTranslation = Vector3.Transform(LocalTranslation, Parent.WorldRotation) + Parent.WorldTranslation;
            }
            else
            {
                WorldTranslation = LocalTranslation;
                WorldRotation = LocalRotation;
            }
        }

        public void GenerateCurrentWorldTransforms()
        {
            GenerateCurrentWorldTransform();

            foreach (var child in Children)
            {
                child.GenerateCurrentWorldTransforms();
            }
        }

        public IEnumerable<SkeletonBone> EnumerateParents()
        {
            var parent = Parent;

            while (parent != null)
            {
                yield return parent;
                parent = parent.Parent;
            }
        }

        public void InitializeAnimationTransforms()
        {
            LocalRotation    = BaseLocalRotation;
            LocalTranslation = BaseLocalTranslation;
            WorldTranslation = BaseWorldTranslation;
            WorldRotation    = BaseWorldRotation;
        }

        public void SetRotation(Quaternion quat, TransformSpace space)
        {
            if(space == TransformSpace.World)
            {

            }
            else
            {

            }
        }
    }
}