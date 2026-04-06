using System.Numerics;

namespace RedFox.Graphics3D.OpenGL;

public sealed class RenderSettings
{
    public Camera? ActiveCamera { get; set; }
    public bool ShowBones { get; set; } = true;
    public bool ShowWireframe { get; set; }
    public bool ShowSkybox { get; set; } = true;
    public bool EnableBackFaceCulling { get; set; }
    public RendererShadingMode ShadingMode { get; set; } = RendererShadingMode.Pbr;
    public bool IsOpenGles { get; set; }
    public Matrix4x4 SceneTransform { get; set; } = Matrix4x4.Identity;
    public Matrix3x3 SceneNormalMatrix => Matrix3x3.FromModelMatrix(SceneTransform);
    public RendererColor BackgroundColor { get; set; } = new(0.12f, 0.12f, 0.14f, 1.0f);
    public float EnvironmentMapExposure { get; set; } = 1.0f;
    public float EnvironmentMapReflectionIntensity { get; set; } = 1.0f;
    public bool EnvironmentMapBlurEnabled { get; set; }
    public float EnvironmentMapBlurRadius { get; set; } = 4.0f;
    public bool EnableIBL { get; set; } = true;
    public bool EnableShadows { get; set; } = true;
    public ShadowQuality ShadowQuality { get; set; } = ShadowQuality.Ultra;
    public float ShadowSoftness { get; set; } = 1.0f;
    public float ShadowIntensity { get; set; } = 1;
    public bool AutoDetectShadowLight { get; set; } = true;
    public int RequestedMsaaSamples { get; set; } = 4;

    public Vector3 TransformPoint(Vector3 value) => Vector3.Transform(value, SceneTransform);

    public Vector3 TransformDirection(Vector3 value)
    {
        Vector3 transformed = Vector3.TransformNormal(value, SceneTransform);
        return transformed.LengthSquared() > 1e-12f
            ? Vector3.Normalize(transformed)
            : value;
    }
}
