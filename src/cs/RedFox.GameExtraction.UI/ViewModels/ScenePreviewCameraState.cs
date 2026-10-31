using System.Numerics;
using RedFox.Graphics3D;

namespace RedFox.GameExtraction.UI.ViewModels;

internal readonly record struct ScenePreviewCameraState(Vector3 OrbitTarget, float YawRadians, float PitchRadians, float Distance, CameraProjection Projection, float OrthographicSize);
