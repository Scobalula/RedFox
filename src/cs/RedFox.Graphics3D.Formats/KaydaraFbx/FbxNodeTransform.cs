using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Holds the static transform properties of an FBX model and evaluates its local matrix for any translation, rotation and scale.
/// </summary>
public sealed class FbxNodeTransform
{
    private readonly Matrix4x4 _scalePivotInverse;
    private readonly Matrix4x4 _scalePivot;
    private readonly Matrix4x4 _scaleOffset;
    private readonly Matrix4x4 _rotationPivotInverse;
    private readonly Matrix4x4 _postRotationInverse;
    private readonly Matrix4x4 _preRotation;
    private readonly Matrix4x4 _rotationPivot;
    private readonly Matrix4x4 _rotationOffset;

    /// <summary>
    /// Gets the static local translation.
    /// </summary>
    public Vector3 Translation { get; }

    /// <summary>
    /// Gets the static local rotation in degrees.
    /// </summary>
    public Vector3 Rotation { get; }

    /// <summary>
    /// Gets the static local scale.
    /// </summary>
    public Vector3 Scaling { get; }

    /// <summary>
    /// Gets the rotation order of the local rotation.
    /// </summary>
    public FbxRotationOrder RotationOrder { get; }

    /// <summary>
    /// Gets how the node inherits its parent's transform.
    /// </summary>
    public FbxInheritType InheritType { get; }

    /// <summary>
    /// Gets the geometric transform applied to attached geometry only.
    /// </summary>
    public Matrix4x4 Geometric { get; }

    /// <summary>
    /// Initializes a new <see cref="FbxNodeTransform"/> from a model object.
    /// </summary>
    /// <param name="model">The FBX model object.</param>
    public FbxNodeTransform(FbxObject model)
    {
        bool rotationActive = model.GetBoolean("RotationActive", true);

        Translation		= model.GetVector3("Lcl Translation", Vector3.Zero);
        Rotation		= model.GetVector3("Lcl Rotation", Vector3.Zero);
        Scaling			= model.GetVector3("Lcl Scaling", Vector3.One);
        RotationOrder	= rotationActive ? (FbxRotationOrder)model.GetInt32("RotationOrder", 0) : FbxRotationOrder.EulerXYZ;
        InheritType		= (FbxInheritType)Math.Clamp(model.GetInt32("InheritType", 1), 0, 2);

        Vector3 rotationPivot	= model.GetVector3("RotationPivot", Vector3.Zero);
        Vector3 scalePivot		= model.GetVector3("ScalingPivot", Vector3.Zero);
        Vector3 preRotation		= rotationActive ? model.GetVector3("PreRotation", Vector3.Zero) : Vector3.Zero;
        Vector3 postRotation	= rotationActive ? model.GetVector3("PostRotation", Vector3.Zero) : Vector3.Zero;

        _scalePivotInverse		= Matrix4x4.CreateTranslation(-scalePivot);
        _scalePivot				= Matrix4x4.CreateTranslation(scalePivot);
        _scaleOffset			= Matrix4x4.CreateTranslation(model.GetVector3("ScalingOffset", Vector3.Zero));
        _rotationPivotInverse	= Matrix4x4.CreateTranslation(-rotationPivot);
        _postRotationInverse	= Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(FbxTransform.EulerToQuaternion(postRotation, FbxRotationOrder.EulerXYZ)));
        _preRotation			= Matrix4x4.CreateFromQuaternion(FbxTransform.EulerToQuaternion(preRotation, FbxRotationOrder.EulerXYZ));
        _rotationPivot			= Matrix4x4.CreateTranslation(rotationPivot);
        _rotationOffset			= Matrix4x4.CreateTranslation(model.GetVector3("RotationOffset", Vector3.Zero));

        Vector3 geometricTranslation	= model.GetVector3("GeometricTranslation", Vector3.Zero);
        Vector3 geometricRotation		= model.GetVector3("GeometricRotation", Vector3.Zero);
        Vector3 geometricScaling		= model.GetVector3("GeometricScaling", Vector3.One);

        Geometric = FbxTransform.Compose(geometricTranslation, FbxTransform.EulerToQuaternion(geometricRotation, FbxRotationOrder.EulerXYZ), geometricScaling);
    }

    /// <summary>
    /// Evaluates the local matrix using the static translation, rotation and scale.
    /// </summary>
    /// <returns>The local matrix.</returns>
    public Matrix4x4 GetLocalMatrix()
    {
        return GetLocalMatrix(Translation, Rotation, Scaling);
    }

    /// <summary>
    /// Evaluates the local matrix <c>T·Roff·Rp·Rpre·R·Rpost⁻¹·Rp⁻¹·Soff·Sp·S·Sp⁻¹</c> for the given values.
    /// </summary>
    /// <param name="translation">The local translation.</param>
    /// <param name="rotation">The local rotation in degrees.</param>
    /// <param name="scaling">The local scale.</param>
    /// <returns>The local matrix.</returns>
    public Matrix4x4 GetLocalMatrix(Vector3 translation, Vector3 rotation, Vector3 scaling)
    {
        Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(FbxTransform.EulerToQuaternion(rotation, RotationOrder));

        return _scalePivotInverse * Matrix4x4.CreateScale(scaling) * _scalePivot * _scaleOffset * _rotationPivotInverse * _postRotationInverse * rotationMatrix * _preRotation * _rotationPivot * _rotationOffset * Matrix4x4.CreateTranslation(translation);
    }
}
