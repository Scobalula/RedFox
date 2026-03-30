namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Defines constants used throughout glTF 2.0 parsing and serialization,
/// including magic bytes, component types, accessor element types, and buffer view targets.
/// </summary>
public static class GltfConstants
{
    /// <summary>
    /// The magic number identifying a GLB binary container (ASCII "glTF").
    /// </summary>
    public const uint GlbMagic = 0x46546C67;

    /// <summary>
    /// The glTF version supported by this implementation.
    /// </summary>
    public const uint GltfVersion = 2;

    /// <summary>
    /// The GLB chunk type for JSON content.
    /// </summary>
    public const uint ChunkTypeJson = 0x4E4F534A;

    /// <summary>
    /// The GLB chunk type for binary buffer content.
    /// </summary>
    public const uint ChunkTypeBin = 0x004E4942;

    /// <summary>
    /// The size of the GLB file header in bytes (magic + version + length).
    /// </summary>
    public const int GlbHeaderSize = 12;

    /// <summary>
    /// The size of a GLB chunk header in bytes (length + type).
    /// </summary>
    public const int GlbChunkHeaderSize = 8;

    /// <summary>
    /// Component type constant for signed byte (8-bit).
    /// </summary>
    public const int ComponentTypeByte = 5120;

    /// <summary>
    /// Component type constant for unsigned byte (8-bit).
    /// </summary>
    public const int ComponentTypeUnsignedByte = 5121;

    /// <summary>
    /// Component type constant for signed short (16-bit).
    /// </summary>
    public const int ComponentTypeShort = 5122;

    /// <summary>
    /// Component type constant for unsigned short (16-bit).
    /// </summary>
    public const int ComponentTypeUnsignedShort = 5123;

    /// <summary>
    /// Component type constant for unsigned integer (32-bit).
    /// </summary>
    public const int ComponentTypeUnsignedInt = 5125;

    /// <summary>
    /// Component type constant for floating-point (32-bit).
    /// </summary>
    public const int ComponentTypeFloat = 5126;

    /// <summary>
    /// Buffer view target for array buffer (vertex attributes).
    /// </summary>
    public const int TargetArrayBuffer = 34962;

    /// <summary>
    /// Buffer view target for element array buffer (index data).
    /// </summary>
    public const int TargetElementArrayBuffer = 34963;

    /// <summary>
    /// The accessor type string for a scalar value.
    /// </summary>
    public const string TypeScalar = "SCALAR";

    /// <summary>
    /// The accessor type string for a two-component vector.
    /// </summary>
    public const string TypeVec2 = "VEC2";

    /// <summary>
    /// The accessor type string for a three-component vector.
    /// </summary>
    public const string TypeVec3 = "VEC3";

    /// <summary>
    /// The accessor type string for a four-component vector.
    /// </summary>
    public const string TypeVec4 = "VEC4";

    /// <summary>
    /// The accessor type string for a 2x2 matrix.
    /// </summary>
    public const string TypeMat2 = "MAT2";

    /// <summary>
    /// The accessor type string for a 3x3 matrix.
    /// </summary>
    public const string TypeMat3 = "MAT3";

    /// <summary>
    /// The accessor type string for a 4x4 matrix.
    /// </summary>
    public const string TypeMat4 = "MAT4";

    /// <summary>
    /// Animation interpolation mode: linear.
    /// </summary>
    public const string InterpolationLinear = "LINEAR";

    /// <summary>
    /// Animation interpolation mode: step (no interpolation).
    /// </summary>
    public const string InterpolationStep = "STEP";

    /// <summary>
    /// Animation interpolation mode: cubic spline.
    /// </summary>
    public const string InterpolationCubicSpline = "CUBICSPLINE";

    /// <summary>
    /// Animation channel target path for translation.
    /// </summary>
    public const string PathTranslation = "translation";

    /// <summary>
    /// Animation channel target path for rotation.
    /// </summary>
    public const string PathRotation = "rotation";

    /// <summary>
    /// Animation channel target path for scale.
    /// </summary>
    public const string PathScale = "scale";

    /// <summary>
    /// Animation channel target path for morph target weights.
    /// </summary>
    public const string PathWeights = "weights";

    /// <summary>
    /// Primitive topology: triangles.
    /// </summary>
    public const int ModeTriangles = 4;

    /// <summary>
    /// Returns the number of components for the given accessor type string.
    /// </summary>
    /// <param name="type">The accessor type string (e.g., "SCALAR", "VEC3", "MAT4").</param>
    /// <returns>The number of scalar components in the type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type string is not recognized.</exception>
    public static int GetComponentCount(string type) => type switch
    {
        TypeScalar => 1,
        TypeVec2 => 2,
        TypeVec3 => 3,
        TypeVec4 => 4,
        TypeMat2 => 4,
        TypeMat3 => 9,
        TypeMat4 => 16,
        _ => throw new InvalidOperationException($"Unknown glTF accessor type: {type}")
    };

    /// <summary>
    /// Returns the byte size of a single component for the given component type.
    /// </summary>
    /// <param name="componentType">The glTF component type constant.</param>
    /// <returns>The size in bytes of one component.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the component type is not recognized.</exception>
    public static int GetComponentSize(int componentType) => componentType switch
    {
        ComponentTypeByte => 1,
        ComponentTypeUnsignedByte => 1,
        ComponentTypeShort => 2,
        ComponentTypeUnsignedShort => 2,
        ComponentTypeUnsignedInt => 4,
        ComponentTypeFloat => 4,
        _ => throw new InvalidOperationException($"Unknown glTF component type: {componentType}")
    };

    /// <summary>
    /// Returns the accessor type string for the given number of components.
    /// </summary>
    /// <param name="componentCount">The number of scalar components (1, 2, 3, or 4).</param>
    /// <returns>The accessor type string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the component count is not supported.</exception>
    public static string GetAccessorType(int componentCount) => componentCount switch
    {
        1 => TypeScalar,
        2 => TypeVec2,
        3 => TypeVec3,
        4 => TypeVec4,
        16 => TypeMat4,
        _ => throw new ArgumentOutOfRangeException(nameof(componentCount), $"No accessor type for {componentCount} components.")
    };
}
