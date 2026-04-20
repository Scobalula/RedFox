using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// The root node of a scene graph.
    /// </summary>
    public class SceneRoot : SceneNode
    {
        /// <summary>
        /// Gets the scene that owns this root node.
        /// </summary>
        public Scene Owner { get; internal set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="owner">The scene that owns this root.</param>
        internal SceneRoot(Scene owner) : base(owner.Name)
        {
            Owner = owner;
        }

        /// <summary>
        /// Removes all nodes of the specified type from the scene.
        /// </summary>
        /// <typeparam name="T">The node type to remove.</typeparam>
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
