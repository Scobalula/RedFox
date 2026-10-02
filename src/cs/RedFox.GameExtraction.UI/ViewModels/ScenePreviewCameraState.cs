using System.Numerics;

namespace RedFox.GameExtraction.UI.ViewModels;

internal readonly record struct ScenePreviewCameraState(Vector3 OrbitTarget, float YawRadians, float PitchRadians, float Distance);
