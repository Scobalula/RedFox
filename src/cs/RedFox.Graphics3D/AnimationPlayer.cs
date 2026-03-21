using System.Diagnostics;

namespace RedFox.Graphics3D;

/// <summary>
/// Orchestrates multi-layer animation playback by evaluating a stack of
/// <see cref="AnimationSampler"/> layers followed by post-process
/// <see cref="AnimationSamplerSolver"/> instances (constraints, IK, etc.).
/// <para>
/// Layers are evaluated in list order. Each layer's
/// <see cref="AnimationSampler.BlendMode"/> and
/// <see cref="AnimationSampler.CurrentWeight"/> control how its results
/// combine with previous layers. Use <see cref="AnimationSampler.Mask"/>
/// on individual layers to restrict them to specific bones or nodes.
/// </para>
/// </summary>
[DebuggerDisplay("AnimationPlayer: {Name}, Layers = {Layers.Count}")]
public class AnimationPlayer(string name) : SceneNode(name)
{
    /// <summary>
    /// Gets or sets the stack of animation sampler layers.
    /// Layers are evaluated in order — later layers blend on top of earlier ones.
    /// </summary>
    public List<AnimationSampler> Layers { get; set; } = [];

    /// <summary>
    /// Gets or sets the total frame count across all layers.
    /// Automatically updated by <see cref="WithSubLayer(AnimationSampler?)"/>.
    /// </summary>
    public float FrameCount { get; set; }

    /// <summary>
    /// Gets or sets the playback framerate in frames per second.
    /// </summary>
    public float FrameRate { get; set; }

    /// <summary>
    /// Gets the total animation length in seconds.
    /// </summary>
    public float Length => FrameRate > 0f ? FrameCount / FrameRate : 0f;

    /// <summary>
    /// Gets the duration of a single frame in seconds.
    /// </summary>
    public float FrameTime => FrameRate > 0f ? 1.0f / FrameRate : 0f;

    /// <summary>
    /// Gets or sets the post-process solvers (IK, constraints) that run
    /// after all animation layers have been applied.
    /// </summary>
    public List<AnimationSamplerSolver> Solvers { get; set; } = [];

    // ------------------------------------------------------------------
    // Layer management
    // ------------------------------------------------------------------

    /// <summary>
    /// Adds a sampler as a new layer if it is not <see langword="null"/>.
    /// </summary>
    /// <param name="sampler">The sampler to add.</param>
    /// <returns>The added sampler, or <see langword="null"/> if input was null.</returns>
    public AnimationSampler? AddLayer(AnimationSampler? sampler)
    {
        if (sampler is not null)
            Layers.Add(sampler);
        return sampler;
    }

    /// <summary>
    /// Adds a layer and updates <see cref="FrameCount"/> to encompass it.
    /// Returns this player for fluent chaining.
    /// </summary>
    /// <param name="layer">The sampler layer to add.</param>
    /// <returns>This <see cref="AnimationPlayer"/> for chaining.</returns>
    public AnimationPlayer WithSubLayer(AnimationSampler? layer)
    {
        if (layer is not null)
        {
            FrameCount = Math.Max(layer.FrameCount, FrameCount);
            Layers.Add(layer);
        }
        return this;
    }

    /// <summary>
    /// Adds a layer with a linear blend-in ramp. The layer's weight starts at 0
    /// and reaches 1 at <c>layer.FrameCount * blend</c> frames.
    /// </summary>
    /// <param name="layer">The sampler layer to add.</param>
    /// <param name="blend">Blend-in factor (0..1) — fraction of the layer's
    /// duration over which weight ramps from 0 to 1.</param>
    /// <returns>This <see cref="AnimationPlayer"/> for chaining.</returns>
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

    /// <summary>
    /// Adds a layer with explicit blend mode and constant weight.
    /// </summary>
    /// <param name="layer">The sampler layer.</param>
    /// <param name="blendMode">How this layer combines with previous layers.</param>
    /// <param name="weight">Constant weight for this layer (0..1).</param>
    /// <returns>This <see cref="AnimationPlayer"/> for chaining.</returns>
    public AnimationPlayer WithSubLayer(AnimationSampler? layer, AnimationBlendMode blendMode)
        => WithSubLayer(layer, blendMode, weight: 1.0f);

    public AnimationPlayer WithSubLayer(AnimationSampler? layer, AnimationBlendMode blendMode, float weight)
    {
        if (layer is not null)
        {
            layer.BlendMode = blendMode;
            layer.CurrentWeight = weight;
            FrameCount = Math.Max(layer.FrameCount, FrameCount);
            Layers.Add(layer);
        }
        return this;
    }

    /// <summary>
    /// Adds a layer restricted to nodes in the given mask.
    /// </summary>
    /// <param name="layer">The sampler layer.</param>
    /// <param name="mask">Mask defining which nodes the layer can affect.</param>
    /// <returns>This <see cref="AnimationPlayer"/> for chaining.</returns>
    public AnimationPlayer WithSubLayer(AnimationSampler? layer, AnimationMask mask)
    {
        if (layer is not null)
        {
            layer.Mask = mask;
            FrameCount = Math.Max(layer.FrameCount, FrameCount);
            Layers.Add(layer);
        }
        return this;
    }

    /// <summary>
    /// Adds a post-process solver if it is not <see langword="null"/>.
    /// </summary>
    public void AddSolver(AnimationSamplerSolver? solver) => WithSolver(solver);

    /// <summary>
    /// Adds a solver and returns this player for fluent chaining.
    /// </summary>
    public AnimationPlayer WithSolver(AnimationSamplerSolver? solver)
    {
        if (solver is not null)
            Solvers.Add(solver);
        return this;
    }

    /// <summary>
    /// Updates all layers and solvers at the specified frame time.
    /// Layers are evaluated in order; solvers run afterward.
    /// </summary>
    /// <param name="time">Absolute frame time.</param>
    public new void Update(float time) => Update(time, AnimationSampleType.AbsoluteFrameTime);

    /// <summary>
    /// Updates all layers and solvers using the specified time interpretation.
    /// </summary>
    /// <param name="time">Time value whose meaning depends on <paramref name="type"/>.</param>
    /// <param name="type">How to interpret the time parameter.</param>
    public void Update(float time, AnimationSampleType type)
    {
        foreach (var layer in Layers)
            layer.Update(time, type);

        foreach (var solver in Solvers)
            solver.Solve(time);
    }

    /// <summary>
    /// Resets all layers and solvers to their initial state.
    /// </summary>
    public void ResetAll()
    {
        foreach (var layer in Layers)
            layer.Reset();
    }

    /// <summary>
    /// Gets the minimum and maximum frame range across all layers,
    /// accounting for each layer's <see cref="AnimationSampler.StartFrame"/>.
    /// </summary>
    public (float Min, float Max) GetFrameRange()
    {
        var finalMin = float.MaxValue;
        var finalMax = float.MinValue;

        foreach (var layer in Layers)
        {
            var (minFrame, maxFrame) = layer.Animation.GetAnimationFrameRange();
            finalMin = Math.Min(minFrame + layer.StartFrame, finalMin);
            finalMax = Math.Max(maxFrame + layer.StartFrame, finalMax);
        }

        return (finalMin, finalMax);
    }

    /// <summary>
    /// Checks whether any layer animates the specified object (bone/node name).
    /// </summary>
    /// <param name="name">The node name to check.</param>
    /// <returns><see langword="true"/> if any layer animates this object.</returns>
    public bool IsObjectAnimated(string name)
    {
        foreach (var layer in Layers)
        {
            if (layer.IsObjectAnimated(name))
                return true;
        }
        return false;
    }
}
