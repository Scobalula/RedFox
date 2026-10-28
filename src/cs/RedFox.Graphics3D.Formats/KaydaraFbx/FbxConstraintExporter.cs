namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Writes parent, orient, point and scale constraints as FBX constraint objects.
/// </summary>
public static class FbxConstraintExporter
{
    /// <summary>
    /// Writes every selected constraint whose constrained and source nodes are exported.
    /// </summary>
    /// <param name="exporter">The active scene exporter.</param>
    public static void Export(FbxSceneExporter exporter)
    {
        foreach (ConstraintNode constraint in exporter.Selection.GetDescendants<ConstraintNode>())
        {
            if (!exporter.ModelIds.TryGetValue(constraint.ConstrainedNode, out long constrainedId) || !exporter.ModelIds.TryGetValue(constraint.SourceNode, out long sourceId))
                continue;

            string? type = constraint switch
            {
                ParentConstraintNode	=> "Parent-Child",
                OrientConstraintNode	=> "Rotation From Rotations",
                PointConstraintNode		=> "Position From Positions",
                ScaleConstraintNode		=> "Scale From Scales",
                _						=> null,
            };

            if (type is null)
                continue;

            long	constraintId	= exporter.AddObject("Constraint", constraint.Name, type, out FbxNode node);
            FbxNode	properties		= new("Properties70");
            string	source			= constraint.SourceNode.Name;
            bool	isParent		= constraint is ParentConstraintNode;

            node.AddChild("Type", new FbxProperty(type));
            node.AddChild("MultiLayer", new FbxProperty(0));
            node.Children.Add(properties);

            FbxSceneExporter.AddInteger(properties, "Active", "bool", 1);
            FbxSceneExporter.AddNumber(properties, "Weight", "A", 100.0);
            FbxSceneExporter.AddNumber(properties, $"{source}.Weight", "A", constraint.Weight * 100.0);

            switch (constraint)
            {
                case ParentConstraintNode parent:
                    FbxSceneExporter.AddVector3(properties, $"{source}.Offset T", "Vector", "A", parent.TranslationOffset);
                    FbxSceneExporter.AddVector3(properties, $"{source}.Offset R", "Vector", "A", FbxTransform.QuaternionToEulerXyz(parent.RotationOffset));
                    break;
                case OrientConstraintNode orient:
                    FbxSceneExporter.AddVector3(properties, "Rotation", "Vector", "A", FbxTransform.QuaternionToEulerXyz(orient.RotationOffset));
                    WriteAxes(properties, orient.SkipX, orient.SkipY, orient.SkipZ);
                    break;
                case PointConstraintNode point:
                    FbxSceneExporter.AddVector3(properties, "Translation", "Vector", "A", point.TranslationOffset);
                    WriteAxes(properties, point.SkipX, point.SkipY, point.SkipZ);
                    break;
                case ScaleConstraintNode scale:
                    FbxSceneExporter.AddVector3(properties, "Scaling", "Vector", "A", scale.ScaleOffset);
                    WriteAxes(properties, scale.SkipX, scale.SkipY, scale.SkipZ);
                    break;
            }

            exporter.Connect(constrainedId, constraintId, isParent ? "Constrained object (Child)" : "Constrained Object");
            exporter.Connect(sourceId, constraintId, isParent ? "Source (Parent)" : "Source");
        }
    }

    private static void WriteAxes(FbxNode properties, bool skipX, bool skipY, bool skipZ)
    {
        FbxSceneExporter.AddInteger(properties, "AffectX", "bool", skipX ? 0 : 1);
        FbxSceneExporter.AddInteger(properties, "AffectY", "bool", skipY ? 0 : 1);
        FbxSceneExporter.AddInteger(properties, "AffectZ", "bool", skipZ ? 0 : 1);
    }
}
