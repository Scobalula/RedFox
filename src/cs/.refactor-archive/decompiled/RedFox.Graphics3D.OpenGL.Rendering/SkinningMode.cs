namespace RedFox.Graphics3D.OpenGL.Rendering;

/// <summary>
/// Selects the skinning blend algorithm used by GPU skinning.
/// </summary>
public enum SkinningMode
{
	/// <summary>
	/// Uses weighted matrix blending.
	/// </summary>
	Linear,
	/// <summary>
	/// Uses dual-quaternion blending for rotation/translation.
	/// </summary>
	DualQuaternion
}
