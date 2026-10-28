namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Writes the camera and light node attributes of exported models.
/// </summary>
public static class FbxNodeAttributeExporter
{
    private const double FilmWidth	= 1.417;
    private const double FilmHeight	= 0.945;

    /// <summary>
    /// Writes a camera attribute, deriving the focal length from the vertical field of view.
    /// </summary>
    /// <param name="attribute">The <c>NodeAttribute</c> node.</param>
    /// <param name="properties">The attribute's <c>Properties70</c> node.</param>
    /// <param name="camera">The camera to write.</param>
    public static void WriteCamera(FbxNode attribute, FbxNode properties, Camera camera)
    {
        double fieldOfView	= Math.Clamp(camera.FieldOfView, 1.0f, 179.0f);
        double focalLength	= FilmHeight * 25.4 / (2.0 * Math.Tan(fieldOfView * Math.PI / 360.0));
        double aspect		= camera.AspectRatio > 0.0f ? camera.AspectRatio : FilmWidth / FilmHeight;

        FbxSceneExporter.AddNumber(properties, "FieldOfView", "A", fieldOfView);
        FbxSceneExporter.AddNumber(properties, "FocalLength", "A", focalLength);
        FbxSceneExporter.AddInteger(properties, "ApertureMode", "enum", 2);
        FbxSceneExporter.AddProperty(properties, "FilmWidth", "double", "Number", string.Empty, new FbxProperty(FilmHeight * aspect));
        FbxSceneExporter.AddProperty(properties, "FilmHeight", "double", "Number", string.Empty, new FbxProperty(FilmHeight));
        FbxSceneExporter.AddProperty(properties, "FilmAspectRatio", "double", "Number", string.Empty, new FbxProperty(aspect));
        FbxSceneExporter.AddProperty(properties, "NearPlane", "double", "Number", string.Empty, new FbxProperty((double)camera.NearPlane));
        FbxSceneExporter.AddProperty(properties, "FarPlane", "double", "Number", string.Empty, new FbxProperty((double)camera.FarPlane));
        FbxSceneExporter.AddInteger(properties, "CameraProjectionType", "enum", camera.Projection == CameraProjection.Orthographic ? 1 : 0);
        FbxSceneExporter.AddProperty(properties, "OrthoZoom", "double", "Number", string.Empty, new FbxProperty((double)camera.OrthographicSize));

        attribute.AddChild("TypeFlags", new FbxProperty("Camera"));
        attribute.AddChild("GeometryVersion", new FbxProperty(124));
        attribute.AddChild("Position", new FbxProperty(0.0)).Properties.AddRange([new FbxProperty(0.0), new FbxProperty(0.0)]);
        attribute.AddChild("Up", new FbxProperty(0.0)).Properties.AddRange([new FbxProperty(1.0), new FbxProperty(0.0)]);
        attribute.AddChild("LookAt", new FbxProperty(1.0)).Properties.AddRange([new FbxProperty(0.0), new FbxProperty(0.0)]);
        attribute.AddChild("ShowInfoOnMoving", new FbxProperty(1));
        attribute.AddChild("ShowAudio", new FbxProperty(0));
        attribute.AddChild("AudioColor", new FbxProperty(0.0)).Properties.AddRange([new FbxProperty(1.0), new FbxProperty(0.0)]);
        attribute.AddChild("CameraOrthoZoom", new FbxProperty(1.0));
    }

    /// <summary>
    /// Writes a light attribute.
    /// </summary>
    /// <param name="attribute">The <c>NodeAttribute</c> node.</param>
    /// <param name="properties">The attribute's <c>Properties70</c> node.</param>
    /// <param name="light">The light to write.</param>
    public static void WriteLight(FbxNode attribute, FbxNode properties, Light light)
    {
        int type = light.Type switch
        {
            LightType.Directional	=> 1,
            LightType.Spot			=> 2,
            LightType.Area			=> 3,
            _						=> 0,
        };

        FbxSceneExporter.AddInteger(properties, "LightType", "enum", type);
        FbxSceneExporter.AddVector3(properties, "Color", "Color", "A", light.Color);
        FbxSceneExporter.AddNumber(properties, "Intensity", "A", light.Intensity * 100.0);
        FbxSceneExporter.AddInteger(properties, "CastLight", "bool", light.Enabled ? 1 : 0);
        FbxSceneExporter.AddNumber(properties, "InnerAngle", "A", light.InnerConeAngle);
        FbxSceneExporter.AddNumber(properties, "OuterAngle", "A", light.OuterConeAngle);

        attribute.AddChild("TypeFlags", new FbxProperty("Light"));
        attribute.AddChild("GeometryVersion", new FbxProperty(124));
    }
}
