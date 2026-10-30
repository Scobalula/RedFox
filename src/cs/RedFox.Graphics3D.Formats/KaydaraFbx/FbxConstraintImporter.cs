using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Imports FBX parent, rotation, position and scale constraints, creating one constraint node per source.
/// </summary>
public static class FbxConstraintImporter
{
    /// <summary>
    /// Imports every supported constraint into the scene root.
    /// </summary>
    /// <param name="importer">The active scene importer.</param>
    public static void Import(FbxSceneImporter importer)
    {
        foreach (FbxObject constraint in importer.Graph.Objects)
        {
            if (constraint.ClassName != "Constraint")
                continue;

            SceneNode?				constrained	= null;
            List<FbxObject>			sources		= [];

            foreach (FbxConnection connection in importer.Graph.GetChildConnections(constraint.Id))
            {
                if (importer.Graph.GetObject(connection.ChildId) is not { ClassName: "Model" } model)
                    continue;

                if (connection.PropertyName.StartsWith("Constrained", StringComparison.OrdinalIgnoreCase))
                    constrained = importer.GetNode(model.Id);
                else if (connection.PropertyName.StartsWith("Source", StringComparison.OrdinalIgnoreCase))
                    sources.Add(model);
            }

            if (constrained is null)
                continue;

            foreach (FbxObject source in sources)
            {
                if (importer.GetNode(source.Id) is not SceneNode sourceNode || CreateConstraint(importer, constraint, constrained, source, sourceNode) is not ConstraintNode node)
                    continue;

                node.Weight = constraint.GetSingle($"{source.Name}.Weight", 100.0f) / 100.0f;
                node.SetParentUnsafe(importer.Scene);
            }
        }
    }

    private static ConstraintNode? CreateConstraint(FbxSceneImporter importer, FbxObject constraint, SceneNode constrained, FbxObject source, SceneNode sourceNode)
    {
        string name = importer.GetUniqueName($"{constrained.Name}_{constraint.Name}");

        return constraint.SubType switch
        {
            "Parent-Child" => new ParentConstraintNode(name, constrained, sourceNode)
            {
                TranslationOffset	= constraint.GetVector3($"{source.Name}.Offset T", Vector3.Zero),
                RotationOffset		= FbxTransform.EulerToQuaternion(constraint.GetVector3($"{source.Name}.Offset R", Vector3.Zero), FbxRotationOrder.EulerXYZ),
            },
            "Rotation From Rotations" => new OrientConstraintNode(name, constrained, sourceNode)
            {
                RotationOffset	= FbxTransform.EulerToQuaternion(constraint.GetVector3("Rotation", Vector3.Zero), FbxRotationOrder.EulerXYZ),
                SkipX			= !constraint.GetBoolean("AffectX", true),
                SkipY			= !constraint.GetBoolean("AffectY", true),
                SkipZ			= !constraint.GetBoolean("AffectZ", true),
            },
            "Position From Positions" => new PointConstraintNode(name, constrained, sourceNode)
            {
                TranslationOffset	= constraint.GetVector3("Translation", Vector3.Zero),
                SkipX				= !constraint.GetBoolean("AffectX", true),
                SkipY				= !constraint.GetBoolean("AffectY", true),
                SkipZ				= !constraint.GetBoolean("AffectZ", true),
            },
            "Scale From Scales" => new ScaleConstraintNode(name, constrained, sourceNode)
            {
                ScaleOffset	= constraint.GetVector3("Scaling", Vector3.One),
                SkipX		= !constraint.GetBoolean("AffectX", true),
                SkipY		= !constraint.GetBoolean("AffectY", true),
                SkipZ		= !constraint.GetBoolean("AffectZ", true),
            },
            _ => null,
        };
    }
}
