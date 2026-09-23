using System;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Thrown when a requested scene node cannot be found.
    /// </summary>
    public class SceneNodeNotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNodeNotFoundException"/> class.
        /// </summary>
        public SceneNodeNotFoundException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNodeNotFoundException"/> class with an error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SceneNodeNotFoundException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNodeNotFoundException"/> class with an error message and cause.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that caused the current exception.</param>
        public SceneNodeNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
}
