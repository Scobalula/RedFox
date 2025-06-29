using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// A class to hold an animation action that executes an action during animation playback.
    /// </summary>
    [DebuggerDisplay("Name = {Name}")]
    public class AnimationAction(string name, string type)
    {
        /// <summary>
        /// Gets or Sets the name of the action.
        /// </summary>
        public string Name { get; set; } = name;

        /// <summary>
        /// Gets or Sets the type of the action.
        /// </summary>
        public string Type { get; set; } = type;

        /// <summary>
        /// Gets or Sets the key frames this action is triggered at.
        /// </summary>
        public List<AnimationKeyFrame<float, Action<Graphics3DScene>?>> KeyFrames { get; set; } = [];
    }
}
