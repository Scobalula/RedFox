using System;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Thrown when a scene-node parenting operation is invalid.
    /// </summary>
    public class SceneNodeParentException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNodeParentException"/> class.
        /// </summary>
        public SceneNodeParentException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNodeParentException"/> class with an error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SceneNodeParentException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNodeParentException"/> class with an error message and cause.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that caused the current exception.</param>
        public SceneNodeParentException(string message, Exception inner) : base(message, inner) { }
    }
}
