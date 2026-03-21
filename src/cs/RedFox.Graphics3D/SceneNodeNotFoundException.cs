using System;

namespace RedFox.Graphics3D
{
    public class SceneNodeNotFoundException : Exception
    {
        public SceneNodeNotFoundException() { }
        public SceneNodeNotFoundException(string message) : base(message) { }
        public SceneNodeNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
}
