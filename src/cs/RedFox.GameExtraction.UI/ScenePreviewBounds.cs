using RedFox.Graphics3D;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Scene bounds with and without skeleton bones, prepared before a scene is attached to the preview control.
/// </summary>
/// <param name="WithBones">Bounds including skeleton bones.</param>
/// <param name="WithoutBones">Bounds excluding skeleton bones.</param>
public readonly record struct ScenePreviewBounds(SceneBounds WithBones, SceneBounds WithoutBones);
