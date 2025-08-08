using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    public class Transform
    {
        /// <summary>
        /// Gets or Sets the local position.
        /// </summary>
        public Nullable<Vector3> LocalPosition { get; set; }

        /// <summary>
        /// Gets or Sets the local rotation.
        /// </summary>
        public Nullable<Quaternion> LocalRotation { get; set; }

        /// <summary>
        /// Gets or Sets the local position.
        /// </summary>
        public Nullable<Vector3> WorldPosition { get; set; }

        /// <summary>
        /// Gets or Sets the local position.
        /// </summary>
        public Nullable<Quaternion> WorldRotation { get; set; }

        public Nullable<Vector3> Scale { get; set; }

        public void CopyTo(Transform other)
        {
            other.LocalPosition = LocalPosition;
            other.LocalRotation = LocalRotation;
            other.WorldPosition = WorldPosition;
            other.WorldRotation = WorldRotation;
            other.Scale = Scale;
        }

        public void SetLocalPosition(Vector3 value)
        {
            LocalPosition = value;
            WorldPosition = null;
        }

        public void SetLocalRotation(Quaternion value)
        {
            LocalRotation = value;
            WorldRotation = null;
        }

        public void SetWorldPosition(Vector3 value)
        {
            LocalPosition = null;
            WorldPosition = value;
        }

        public void SetWorldRotation(Quaternion value)
        {
            LocalRotation = null;
            WorldRotation = value;
        }

        public void Invalidate()
        {
            LocalPosition = null;
            LocalRotation = null;
            WorldPosition = null;
            WorldRotation = null;
            Scale = null;
        }
    }
}
