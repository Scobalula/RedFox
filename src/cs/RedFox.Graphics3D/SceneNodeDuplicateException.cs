using System;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Thrown when an operation would create a duplicate scene node.
    /// </summary>
    public class SceneNodeDuplicateException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNodeDuplicateException"/> class.
        /// </summary>
        public SceneNodeDuplicateException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNodeDuplicateException"/> class with an error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SceneNodeDuplicateException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNodeDuplicateException"/> class with an error message and cause.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that caused the current exception.</param>
        public SceneNodeDuplicateException(string message, Exception inner) : base(message, inner) { }
    }
}
