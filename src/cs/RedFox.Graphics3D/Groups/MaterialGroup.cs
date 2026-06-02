using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Graphics3D.Groups;

/// <summary>
/// Represents a group of material nodes in the scene graph for logical grouping.
/// </summary>
public class MaterialGroup : Group
{
    /// <summary>
    /// Initializes a new instance of <see cref="MaterialGroup"/> with a generated name.
    /// </summary>
    public MaterialGroup() : base() { }

    /// <summary>
    /// Initializes a new instance of <see cref="MaterialGroup"/> with the specified name.
    /// </summary>
    /// <param name="name">The name to assign to the group.</param>
    public MaterialGroup(string name) : base(name) { }

    /// <summary>
    /// Initializes a new instance of <see cref="MaterialGroup"/> with the specified name and flags.
    /// </summary>
    /// <param name="name">The name to assign to the group.</param>
    /// <param name="flags">The flags that control node behavior.</param>
    public MaterialGroup(string name, SceneNodeFlags flags) : base(name, flags) { }
}
