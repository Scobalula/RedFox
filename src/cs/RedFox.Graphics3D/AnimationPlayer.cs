using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    public class AnimationPlayer(string name) : Graphics3DObject(name)
    {
        /// <summary>
        /// Gets or Sets the layers.
        /// </summary>
        public List<AnimationSampler> Layers { get; set; } = [];

        /// <summary>
        /// Gets the number of frames in the animation.
        /// </summary>
        public float FrameCount { get; set; }

        /// <summary>
        /// Gets or Sets the playback framerate.
        /// </summary>
        public float FrameRate { get; set; }

        /// <summary>
        /// Gets the length of the animation.
        /// </summary>
        public float Length => FrameCount / FrameRate;

        /// <summary>
        /// Gets the length of each frame.
        /// </summary>
        public float FrameTime => 1.0f / FrameRate;

        /// <summary>
        /// Gets or Sets the solvers.
        /// </summary>
        public List<AnimationSamplerSolver> Solvers { get; set; } = [];

        public AnimationSampler? AddLayer(AnimationSampler? sampler)
        {
            if (sampler is not null)
            {
                Layers.Add(sampler);
            }

            return sampler;
        }

        public AnimationPlayer WithSubLayer(AnimationSampler? layer)
        {
            if (layer is not null)
            {
                FrameCount = Math.Max(layer.FrameCount, FrameCount);
                Layers.Add(layer);
            }

            return this;
        }

        public AnimationPlayer WithSubLayer(AnimationSampler? layer, float blend)
        {
            if (layer is not null)
            {
                layer.Weights.Add(new(0, 0));
                layer.Weights.Add(new((int)(layer.FrameCount * blend), 1));

                FrameCount = Math.Max(layer.FrameCount, FrameCount);
                Layers.Add(layer);
            }
            return this;
        }

        public void AddSolver(AnimationSamplerSolver? solver) => WithSolver(solver);

        public AnimationPlayer WithSolver(AnimationSamplerSolver? solver)
        {
            if (solver is not null)
            {
                Solvers.Add(solver);
            }

            return this;
        }

        public void Update(float time) => Update(time, AnimationSampleType.AbsoluteFrameTime);

        public void Update(float time, AnimationSampleType type)
        {
            foreach (var layer in Layers)
            {
                layer.Update(time, type);
            }

            foreach (var solver in Solvers)
            {
                solver.Update(time);
            }
        }

        public (float, float) GetFrameRange()
        {
            var finalMinFrame = float.MaxValue;
            var finalMaxFrame = float.MinValue; 
            foreach(var layer in Layers)
            {
                var (minFrame, maxFrame) = layer.Animation.GetAnimationFrameRange();

                minFrame += layer.StartFrame;
                maxFrame += layer.StartFrame;

                finalMinFrame = Math.Min(minFrame, finalMinFrame);
                finalMaxFrame = Math.Max(maxFrame, finalMaxFrame);
            }

            return (finalMinFrame, finalMaxFrame);
        }

        public bool IsObjectAnimation(string name)
        {
            foreach (var layer in Layers)
            {
                if (layer.IsObjectAnimated(name))
                {
                    return true;
                }
            }

            return false;
        }

        //public AnimationSampler GetLayer(string name) => Layers.TryGetValue(name, out var sampler) ? sampler : throw new Exception();

        //public AnimationSampler? TryGetLayer(string name) => Layers.TryGetValue(name, out var sampler) ? sampler : null;
        //public bool TryGetLayer(string name,[NotNullWhen(true)] out AnimationSampler? sampler) => (sampler = GetLayer(name)) != null;

        //public AnimationSamplerSolver? GetSolver(string name) => Solvers != null ? Solvers.TryGetValue(name, out var solver) ? solver : null : null;

        //public bool TryGetSolver(string name, [NotNullWhen(true)] out AnimationSamplerSolver? solver) => (solver = GetSolver(name)) != null;
    }
}
