using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    public abstract class Animation(string name) : Graphics3DObject(name)
    {
        /// <summary>
        /// Gets or Sets the animation actions.
        /// </summary>
        public List<AnimationAction>? Actions { get; set; }

        /// <summary>
        /// Gets or Sets the framerate of the animation.
        /// </summary>
        public float Framerate { get; set; }

        public abstract float GetAnimationFrameCount();

        /// <summary>
        /// Calculates the total number of actions in this animation.
        /// </summary>
        /// <returns>The total number of actions in this animation.</returns>
        public int GetAnimationActionCount() => Actions == null ? 0 : Actions.Sum(x => x.KeyFrames.Count);

        /// <summary>
        /// Creates a new instance of an <see cref="AnimationAction"/> within this animation, if one already exists with this name, then that action is returned.
        /// </summary>
        /// <param name="name">Name of the action.</param>
        /// <returns>A new action that is added to this animation if it doesn't exist, otherwise an existing action with the given name.</returns>
        public AnimationAction CreateAction(string name)
        {
            Actions ??= [];
            var idx = Actions.FindIndex(x => x.Name == name);

            if (idx != -1)
                return Actions[idx];

            var nAction = new AnimationAction(name, "Default");
            Actions.Add(nAction);

            return nAction;
        }

        /// <summary>
        /// Creates a new instance of an <see cref="AnimationAction"/> within this animation, if one already exists with this name, then that action is returned.
        /// </summary>
        /// <param name="name">Name of the action.</param>
        /// <returns>A new action that is added to this animation if it doesn't exist, otherwise an existing action with the given name.</returns>
        public AnimationAction CreateAction(string name, IEnumerable<AnimationKeyFrame<float, Action<Graphics3DScene>?>> keyFrames)
        {
            Actions ??= [];
            var idx = Actions.FindIndex(x => x.Name == name);

            if (idx != -1)
                return Actions[idx];

            var nAction = new AnimationAction(name, "Default");
            nAction.KeyFrames.AddRange(keyFrames);
            Actions.Add(nAction);
            

            return nAction;
        }
    }
}
