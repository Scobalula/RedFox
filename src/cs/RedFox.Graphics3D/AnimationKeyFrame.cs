using System.Diagnostics;
using System.Numerics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// A struct to store a value stored a particular frame in a <see cref="Animation"/>.
    /// </summary>
    /// <remarks>
    /// Initializes a new <see cref="AnimationKeyFrame{TFrame, TValue}"/> at the given frame with the given value.
    /// </remarks>
    /// <typeparam name="TFrame">The type to use for the frame.</typeparam>
    /// <typeparam name="TValue">The type to use for the value.</typeparam>
    /// <param name="frame">The frame the value is stored at.</param>
    /// <param name="value">The value stored at this frame.</param>
    [DebuggerDisplay("Frame = {Frame} Value = {Value}")]
    public struct AnimationKeyFrame<TFrame, TValue>(TFrame frame, TValue value) where TFrame : INumber<TFrame>
    {
        /// <summary>
        /// Gets or Sets the frame the value is stored at.
        /// </summary>
        public TFrame Frame { get; set; } = frame;

        /// <summary>
        /// Gets or Sets the value stored at this frame.
        /// </summary>
        public TValue Value { get; set; } = value;
    }
}
