using System;

namespace RedFox.Graphics3D
{
    public class SceneNodeDuplicateException : Exception
    {
        public SceneNodeDuplicateException() { }
        public SceneNodeDuplicateException(string message) : base(message) { }
        public SceneNodeDuplicateException(string message, Exception inner) : base(message, inner) { }
    }
}
