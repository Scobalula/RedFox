namespace RedFox.Graphics3D;

/// <summary>
/// Abstract base class for post-animation solvers that run after all
/// <see cref="AnimationSampler"/> layers have been evaluated. Solvers can
/// modify scene node transforms to satisfy constraints (e.g. look-at, aim)
/// or solve inverse kinematics chains.
/// <para>
/// Each solver carries its own weight, which can be animated via a
/// <see cref="WeightCurve"/> (DataBuffer-backed) or a legacy keyframe list.
/// </para>
/// </summary>
public abstract class AnimationSamplerSolver(string name) : SceneNode(name)
{
    /// <summary>
    /// Gets or sets the legacy weight keyframe list. Used for backward
    /// compatibility; prefer <see cref="WeightCurve"/> for new code.
    /// </summary>
    public List<AnimationKeyFrame<float, float>> Weights { get; set; } = [];

    /// <summary>
    /// Gets or sets a DataBuffer-backed weight curve. When set, takes
    /// priority over the <see cref="Weights"/> list.
    /// </summary>
    public AnimationCurve? WeightCurve { get; set; }

    /// <summary>
    /// Gets or sets the current weight of this solver, controlling how
    /// strongly it influences the final pose. Ranges from 0 (no effect)
    /// to 1 (full effect).
    /// </summary>
    public float CurrentWeight { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets whether this solver is enabled. When disabled, 
    /// <see cref="Solve(float)"/> returns immediately without modifying
    /// any transforms.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Updates the solver's weight from the weight curve at the given time,
    /// then executes the solver logic if enabled and weight is greater than zero.
    /// Called by <see cref="AnimationPlayer"/> after all layers have been applied.
    /// </summary>
    /// <param name="time">The current absolute frame time.</param>
    public void Solve(float time)
    {
        if (!IsEnabled) return;

        // Update weight from curve
        if (WeightCurve is not null && WeightCurve.KeyFrameCount > 0)
        {
            CurrentWeight = WeightCurve.SampleScalar(time);
        }
        else if (Weights.Count > 0)
        {
            var (i0, i1) = Animation.GetFramePairIndex(Weights, time, 0f);
            if (i0 >= 0)
            {
                CurrentWeight = i0 == i1
                    ? Weights[i0].Value
                    : float.Lerp(Weights[i0].Value, Weights[i1].Value,
                        (time - Weights[i0].Frame) / (Weights[i1].Frame - Weights[i0].Frame));
            }
        }

        if (CurrentWeight <= 0f) return;

        OnSolve(time);
    }

    /// <summary>
    /// When overridden in a derived class, performs the actual solver logic
    /// (constraint evaluation, IK chain solving, etc.). The solver's
    /// <see cref="CurrentWeight"/> has already been updated when this is called.
    /// </summary>
    /// <param name="time">The current absolute frame time.</param>
    protected abstract void OnSolve(float time);
}
