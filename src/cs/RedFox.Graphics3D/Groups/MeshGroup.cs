using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Graphics3D.Groups;

public class MeshGroup : Group
{
    /// <summary>
    /// Initializes a new instance of <see cref="MeshGroup"/> with a generated name.
    /// </summary>
    public MeshGroup() : base() { }

    /// <summary>
    /// Initializes a new instance of <see cref="MeshGroup"/> with the specified name.
    /// </summary>
    /// <param name="name">The name to assign to the group.</param>
    public MeshGroup(string name) : base(name) { }

    /// <summary>
    /// Initializes a new instance of <see cref="MeshGroup"/> with the specified name and flags.
    /// </summary>
    /// <param name="name">The name to assign to the group.</param>
    /// <param name="flags">The flags that control node behavior.</param>
    public MeshGroup(string name, SceneNodeFlags flags) : base(name, flags) { }
}
