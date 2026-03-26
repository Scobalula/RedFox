using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Graphics3D
{
    public class SceneRoot : SceneNode
    {
        public Scene Owner { get; internal set; }

        internal SceneRoot(Scene owner) : base(owner.Name)
        {
            Owner = owner;
        }

        public void RemoveAll<T>() where T : SceneNode
        {
            var nodesToRemove = GetDescendants<T>();

            foreach (var node in nodesToRemove)
            {
                node.MoveTo(null);
            }
        }
    }
}
