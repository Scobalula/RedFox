

namespace RedFox.Compression.Oodle
{
    /// <summary>
    /// Oodle threading phase.
    /// </summary>
    public enum OodleThreading
    {
        /// <summary>
        /// Uses threading phase 0.
        /// </summary>
        Phase0 = 0,

        /// <summary>
        /// Uses threading phase 1.
        /// </summary>
        Phase1 = 1,

        /// <summary>
        /// Uses threading phase 2.
        /// </summary>
        Phase2 = 2,

        /// <summary>
        /// Disables threading.
        /// </summary>
        None   = 3,
    }
}
