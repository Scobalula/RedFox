namespace RedFox.Graphics3D
{
    /// <summary>
    /// Controls which transform space is preserved when reparenting a scene node.
    /// </summary>
    public enum ReparentTransformMode
    {
        /// <summary>
        /// Preserve world transforms by invalidating local transform caches.
        /// </summary>
        PreserveWorld,

        /// <summary>
        /// Preserve local transforms by invalidating world transform caches.
        /// </summary>
        PreserveLocal,

        /// <summary>
        /// Preserve whichever values are already present without invalidation.
        /// </summary>
        PreserveExisting,
    }
}
