using RedFox.Graphics3D.Skeletal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    [DebuggerDisplay("Name = {Name}")]
    public abstract class Graphics3DObject
    {
        /// <summary>
        /// Internal bone parent value.
        /// </summary>
        private readonly HashSet<Graphics3DObject> _children = [];

        public string Name { get; set; }

        /// <summary>
        /// Gets or Sets the parent of this object.
        /// </summary>
        public Graphics3DObject? Parent { get; internal set; }

        /// <summary>
        /// Gets or Sets the bones that are children of this bone.
        /// </summary>
        public IReadOnlyCollection<Graphics3DObject> Children => _children;

        /// <summary>
        /// Gets or Sets the base transform.
        /// </summary>
        public Transform BaseTransform { get; set; } = new();

        /// <summary>
        /// Gets or Sets the active transform.
        /// </summary>
        public Transform LiveTransform { get; set; } = new();

        // Graphics3DObject.cs
        // -------------------

        public void CalculateLocalTransform()
        {
            if (Parent is not null)
            {
                LiveTransform.LocalPosition =
                    Vector3.Transform(
                        GetActiveWorldPosition() - Parent.GetActiveWorldPosition(),
                        Quaternion.Conjugate(Parent.GetActiveWorldRotation()));

                LiveTransform.LocalRotation =
                    Quaternion.Normalize(
                        Quaternion.Conjugate(Parent.GetActiveWorldRotation()) *
                        GetActiveWorldRotation());
            }
            else
            {
                LiveTransform.LocalPosition = GetActiveWorldPosition();
                LiveTransform.LocalRotation = Quaternion.Normalize(
                    GetActiveWorldRotation());
            }
        }

        public void CalculateWorldTransform()
        {
            if (Parent is not null)
            {
                LiveTransform.WorldPosition =
                    Vector3.Transform(
                        GetLiveLocalPosition(),
                        Parent.GetActiveWorldRotation()) +
                    Parent.GetActiveWorldPosition();

                LiveTransform.WorldRotation = Quaternion.Normalize(
                    Parent.GetActiveWorldRotation() * GetLiveLocalRotation());
            }
            else
            {
                LiveTransform.WorldPosition = GetLiveLocalPosition();
                LiveTransform.WorldRotation =
                    Quaternion.Normalize(GetLiveLocalRotation());
            }
        }

        public void SetLiveWorldPosition(Vector3 value)
        {
            LiveTransform.WorldPosition = value;
            LiveTransform.LocalPosition = null;

            CalculateLocalTransform();

            foreach (var child in Children)
            {
                child.LiveTransform.WorldPosition = null;
                child.LiveTransform.WorldRotation = null;
            }
        }

        public void SetLiveWorldRotation(Quaternion value)
        {
            value = Quaternion.Normalize(value);

            LiveTransform.WorldRotation = value;

            if (Parent is not null)
                LiveTransform.LocalRotation =
                    Quaternion.Normalize(
                        Quaternion.Conjugate(Parent.GetActiveWorldRotation()) * value);
            else
                LiveTransform.LocalRotation = value;

            foreach (var child in Children)
            {
                child.LiveTransform.WorldRotation = null;
                child.LiveTransform.WorldPosition = null;
            }
        }

        public Vector3 GetBaseLocalPosition()
        {
            if (BaseTransform.LocalPosition.HasValue)
            {
                return BaseTransform.LocalPosition.Value;
            }

            if (!BaseTransform.WorldPosition.HasValue)
            {
                BaseTransform.WorldPosition = Parent != null? Parent.GetBaseWorldPosition() : Vector3.Zero;
            }

            if (Parent is not null)
            {
                BaseTransform.LocalPosition = Vector3.Transform(GetBaseWorldPosition() - Parent.GetBaseWorldPosition(), Quaternion.Conjugate(Parent.GetBaseWorldRotation()));
            }
            else
            {
                BaseTransform.LocalPosition = GetBaseWorldPosition();
            }

            return BaseTransform.LocalPosition.Value;
        }

        public Vector3 GetBaseWorldPosition()
        {
            if (BaseTransform.WorldPosition.HasValue)
            {
                return BaseTransform.WorldPosition.Value;
            }

            if (!BaseTransform.LocalPosition.HasValue)
            {
                BaseTransform.LocalPosition = Vector3.Zero;
            }

            if (Parent is not null)
            {
                BaseTransform.WorldPosition = Vector3.Transform(GetBaseLocalPosition(), Parent.GetBaseWorldRotation()) + Parent.GetBaseWorldPosition();
            }
            else
            {
                BaseTransform.WorldPosition = GetBaseLocalPosition();
            }

            return BaseTransform.WorldPosition.Value;
        }

        public Quaternion GetBaseLocalRotation()
        {
            if (BaseTransform.LocalRotation.HasValue)
            {
                return BaseTransform.LocalRotation.Value;
            }

            if (!BaseTransform.WorldRotation.HasValue)
            {
                BaseTransform.WorldRotation = Parent != null ? Parent.GetBaseWorldRotation() : Quaternion.Identity;
            }

            if (Parent is not null)
            {
                BaseTransform.LocalRotation = Quaternion.Conjugate(Parent.GetBaseWorldRotation()) * GetBaseWorldRotation();
            }
            else
            {
                BaseTransform.LocalRotation = GetBaseWorldRotation();
            }

            return BaseTransform.LocalRotation.Value;
        }

        public Quaternion GetBaseWorldRotation()
        {
            if (BaseTransform.WorldRotation.HasValue)
            {
                return BaseTransform.WorldRotation.Value;
            }

            if (!BaseTransform.LocalRotation.HasValue)
            {
                BaseTransform.LocalRotation = Quaternion.Identity;
            }

            if (Parent is not null)
            {
                BaseTransform.WorldRotation = Parent.GetBaseWorldRotation() * GetBaseLocalRotation();
            }
            else
            {
                BaseTransform.WorldRotation = GetBaseLocalRotation();
            }

            return BaseTransform.WorldRotation.Value;
        }

        public Vector3 GetLiveLocalPosition()
        {
            if (LiveTransform.LocalPosition.HasValue)
            {
                return LiveTransform.LocalPosition.Value;
            }

            if (LiveTransform.WorldPosition.HasValue)
            {
                if (Parent is not null)
                {
                    LiveTransform.LocalPosition = Vector3.Transform(GetActiveWorldPosition() - Parent.GetActiveWorldPosition(), Quaternion.Conjugate(Parent.GetActiveWorldRotation()));
                }
                else
                {
                    LiveTransform.LocalPosition = GetActiveWorldPosition();
                }
            }
            else
            {
                LiveTransform.LocalPosition = GetBaseLocalPosition();
            }
            
            return LiveTransform.LocalPosition.Value;
        }

        public Vector3 GetActiveWorldPosition()
        {
            if (LiveTransform.WorldPosition.HasValue)
            {
                return LiveTransform.WorldPosition.Value;
            }

            if (Parent is not null)
            {
                LiveTransform.WorldPosition = Vector3.Transform(GetLiveLocalPosition(), Parent.GetActiveWorldRotation()) + Parent.GetActiveWorldPosition();
            }
            else
            {
                LiveTransform.WorldPosition = GetLiveLocalPosition();
            }

            return LiveTransform.WorldPosition.Value;
        }

        public Quaternion GetLiveLocalRotation()
        {
            if (LiveTransform.LocalRotation.HasValue)
            {
                return LiveTransform.LocalRotation.Value;
            }

            if (LiveTransform.WorldRotation.HasValue)
            {
                if (Parent is not null)
                {
                    LiveTransform.LocalRotation = Quaternion.Conjugate(Parent.GetActiveWorldRotation()) * GetActiveWorldRotation();
                }
                else
                {
                    LiveTransform.LocalRotation = GetActiveWorldRotation();
                }
            }
            else
            {
                LiveTransform.LocalRotation = GetBaseLocalRotation();
            }

            return LiveTransform.LocalRotation.Value;
        }

        public Quaternion GetActiveWorldRotation()
        {
            if (LiveTransform.WorldRotation.HasValue)
            {
                return LiveTransform.WorldRotation.Value;
            }

            if (!LiveTransform.LocalRotation.HasValue)
            {
                LiveTransform.LocalRotation = GetBaseLocalRotation();
            }

            if (Parent is not null)
            {
                LiveTransform.WorldRotation = Parent.GetActiveWorldRotation() * GetLiveLocalRotation();
            }
            else
            {
                LiveTransform.WorldRotation = GetLiveLocalRotation();
            }

            return LiveTransform.WorldRotation.Value;
        }

        public void SetLiveLocalPosition(Vector3 value)
        {
            LiveTransform.LocalPosition = value;

            if (Parent is not null)
            {
                LiveTransform.WorldPosition = Vector3.Transform(GetLiveLocalPosition(), Parent.GetActiveWorldRotation()) + Parent.GetActiveWorldPosition();
            }
            else
            {
                LiveTransform.WorldPosition = GetLiveLocalPosition();
            }
        }

        public void SetLiveLocalRotation(Quaternion value)
        {
            value = Quaternion.Normalize(value);

            LiveTransform.LocalRotation = value;
            CalculateWorldTransform();

            foreach (var child in Children)
            {
                child.CalculateWorldTransform();
            }
        }

        //public void SetLiveWorldPosition(Vector3 value)
        //{
        //    LiveTransform.WorldPosition = value;

        //    if (Parent is not null)
        //    {
        //        LiveTransform.WorldPosition = Vector3.Transform(GetLiveLocalPosition(), Parent.GetActiveWorldRotation()) + Parent.GetActiveWorldPosition();
        //    }
        //    else
        //    {
        //        LiveTransform.WorldPosition = GetLiveLocalPosition();
        //    }
        //}

        //public void SetLiveWorldRotation(Quaternion value)
        //{
        //    LiveTransform.WorldRotation = value;

        //    CalculateLocalTransform();

        //    foreach (var child in Children)
        //    {
        //        child.LiveTransform.WorldRotation = null;
        //        child.LiveTransform.WorldPosition = null;
        //    }
        //}


        //public void CalculateLocalTransform()
        //{
        //    if (Parent is not null)
        //    {
        //        LiveTransform.LocalPosition = Vector3.Transform(GetActiveWorldPosition() - Parent.GetActiveWorldPosition(), Quaternion.Conjugate(Parent.GetActiveWorldRotation()));
        //        LiveTransform.LocalRotation = Quaternion.Conjugate(Parent.GetActiveWorldRotation()) * GetActiveWorldRotation();
        //    }
        //    else
        //    {
        //        LiveTransform.LocalPosition = GetActiveWorldPosition();
        //        LiveTransform.LocalRotation = GetActiveWorldRotation();
        //    }
        //}

        //public void CalculateWorldTransform()
        //{
        //    if (Parent is not null)
        //    {
        //        LiveTransform.WorldPosition = Vector3.Transform(GetLiveLocalPosition(), Parent.GetActiveWorldRotation()) + Parent.GetActiveWorldPosition();
        //        LiveTransform.WorldRotation = Parent.GetActiveWorldRotation() * GetLiveLocalRotation();
        //    }
        //    else
        //    {
        //        LiveTransform.WorldPosition = GetLiveLocalPosition();
        //        LiveTransform.WorldRotation = GetLiveLocalRotation();
        //    }
        //}

        public Graphics3DObject()
        {
            Name = $"{nameof(Graphics3DObject)}_{GetHashCode():X}";
        }

        public Graphics3DObject(string name)
        {
            Name = name;
        }


        /// <summary>
        /// Moves the this directory into the provided directory.
        /// </summary>
        /// <param name="newParent">The directory to move this directory into.</param>
        public void MoveTo(Graphics3DObject? newParent)
        {
            if (newParent == Parent)
                return;

            Parent?._children.Remove(this);
            Parent = newParent;

            if (newParent is not null && !newParent._children.Add(this))
            {
                //throw new IOException($"Directory: {newParent.FullPath} already contains a directory with the name: {Name}");
            }

            // Invalidate world
            BaseTransform.WorldPosition = null;
            BaseTransform.WorldRotation = null;
            LiveTransform.WorldPosition = null;
            LiveTransform.WorldRotation = null;
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



        public void SetRotation(Quaternion quat, TransformSpace space)
        {
            if (space == TransformSpace.World)
            {

            }
            else
            {

            }
        }
    }
}
