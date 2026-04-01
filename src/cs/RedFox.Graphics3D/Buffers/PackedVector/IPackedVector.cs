using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Defines a packed vector type that can pack and unpack <see cref="Vector4"/> values.
    /// </summary>
    /// <typeparam name="TSelf">The concrete packed vector type implementing this interface.</typeparam>
    public interface IPackedVector<TSelf> where TSelf : unmanaged, IPackedVector<TSelf>
    {
        /// <summary>
        /// Gets the number of components stored in this packed vector type.
        /// </summary>
        static abstract int ComponentCount { get; }

        /// <summary>
        /// Packs a <see cref="Vector4"/> into this packed vector instance.
        /// </summary>
        /// <param name="value">The vector to pack.</param>
        void Pack(Vector4 value);

        /// <summary>
        /// Unpacks this packed vector instance into a <see cref="Vector4"/>.
        /// </summary>
        /// <returns>The unpacked vector.</returns>
        Vector4 Unpack();
    }
}
