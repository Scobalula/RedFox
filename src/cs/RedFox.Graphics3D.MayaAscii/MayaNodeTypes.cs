namespace RedFox.Graphics3D.MayaAscii;

/// <summary>
/// Contains constant strings for Maya ASCII node type identifiers used in <c>createNode</c> commands.
/// These correspond to the built-in Maya dependency graph and DAG node types.
/// </summary>
public static class MayaNodeTypes
{
    /// <summary>
    /// The Maya transform node type, used that represents a DAG transform in the scene hierarchy.
    /// </summary>
    public const string Transform = "transform";

    /// <summary>
    /// The Maya joint node type, used for skeleton bones in the joint hierarchy.
    /// </summary>
    public const string Joint = "joint";

    /// <summary>
    /// The Maya mesh shape node type, containing polygon geometry data.
    /// </summary>
    public const string Mesh = "mesh";

    /// <summary>
    /// The Maya <c>groupId</c> node type, used to tag face sets for per-face material assignment.
    /// </summary>
    public const string GroupId = "groupId";

    /// <summary>
    /// The Maya <c>groupParts</c> node type, used to route geometry subset connections for shading groups.
    /// </summary>
    public const string GroupParts = "groupParts";

    /// <summary>
    /// The Maya <c>skinCluster</c> node type, representing a smooth skin deformer.
    /// </summary>
    public const string SkinCluster = "skinCluster";

    /// <summary>
    /// The Maya <c>dagPose</c> node type, representing a stored bind pose snapshot.
    /// </summary>
    public const string DagPose = "dagPose";

    /// <summary>
    /// The Maya <c>shadingEngine</c> node type, representing a shading group that connects materials to geometry.
    /// </summary>
    public const string ShadingEngine = "shadingEngine";

    /// <summary>
    /// The Maya <c>materialInfo</c> node type, which holds material metadata for a shading group.
    /// </summary>
    public const string MaterialInfo = "materialInfo";

    /// <summary>
    /// The Maya <c>lambert</c> shader node type, representing a basic diffuse shader.
    /// </summary>
    public const string Lambert = "lambert";

    /// <summary>
    /// The Maya <c>phong</c> shader node type, representing a Phong specular shader.
    /// </summary>
    public const string Phong = "phong";

    /// <summary>
    /// The Maya <c>file</c> texture node type, representing a file-based texture reference.
    /// </summary>
    public const string File = "file";

    /// <summary>
    /// The Maya <c>place2dTexture</c> node type, controlling UV placement for a texture.
    /// </summary>
    public const string Place2dTexture = "place2dTexture";

    /// <summary>
    /// The Maya <c>animCurveTL</c> node type for translation X/Y/Z animation curves (linear).
    /// </summary>
    public const string AnimCurveTL = "animCurveTL";

    /// <summary>
    /// The Maya <c>animCurveTA</c> node type for rotation X/Y/Z animation curves (angular).
    /// </summary>
    public const string AnimCurveTA = "animCurveTA";

    /// <summary>
    /// The Maya <c>animCurveTU</c> node type for scale and other unitless animation curves.
    /// </summary>
    public const string AnimCurveTU = "animCurveTU";

    /// <summary>
    /// The Maya <c>parentConstraint</c> node type, constraining both translation and rotation to a target.
    /// </summary>
    public const string ParentConstraint = "parentConstraint";

    /// <summary>
    /// The Maya <c>orientConstraint</c> node type, constraining only rotation to a target.
    /// </summary>
    public const string OrientConstraint = "orientConstraint";

    /// <summary>
    /// The Maya <c>tweak</c> node type, used as an intermediate deformer in the skin cluster pipeline for vertex edits.
    /// </summary>
    public const string Tweak = "tweak";

    /// <summary>
    /// The Maya <c>objectSet</c> node type, representing a set of objects or components for deformer membership.
    /// </summary>
    public const string ObjectSet = "objectSet";
}
