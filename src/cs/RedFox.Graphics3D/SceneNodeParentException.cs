using System;

namespace RedFox.Graphics3D
{
    public class SceneNodeParentException : Exception
    {
        public SceneNodeParentException() { }
        public SceneNodeParentException(string message) : base(message) { }
        public SceneNodeParentException(string message, Exception inner) : base(message, inner) { }
    }
}
