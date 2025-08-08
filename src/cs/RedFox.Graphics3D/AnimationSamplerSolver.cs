using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// An interface that describes an animation solver for use during sampling.
    /// </summary>
    public abstract class AnimationSamplerSolver(string name) : Graphics3DObject(name)
    {
        /// <summary>
        /// Gets or Sets the weights for this layer.
        /// </summary>
        public List<AnimationKeyFrame<float, float>> Weights { get; set; } = [];

        /// <summary>
        /// Gets or Sets the current weight
        /// </summary>
        public float CurrentWeight { get; set; } = 1.0f;

        /// <summary>
        /// Updates this solver at the given time.
        /// </summary>
        /// <param name="time">Absolute frame time to update the solver at.</param>
        public abstract void Update(float time);
    }
}
