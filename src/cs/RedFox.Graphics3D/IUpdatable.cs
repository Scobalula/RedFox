
namespace RedFox.Graphics3D
{
    /// <summary>
    /// Defines an interface for objects that require per-frame updates during rendering.
    /// Implementing classes should perform any time-dependent logic such as animation
    /// advancement, transform recalculation, or state transitions within the
    /// <see cref="Update"/> method.
    /// </summary>
    public interface IUpdatable
    {
        /// <summary>
        /// Updates the object state for the current frame.
        /// </summary>
        /// <param name="deltaTime">
        /// The elapsed time in seconds since the last frame. Used for time-dependent
        /// calculations such as animation interpolation and physics simulation.
        /// </param>
        void Update(float deltaTime);
    }
}
