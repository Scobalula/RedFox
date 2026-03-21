namespace RedFox.Graphics3D.IO
{
    /// <summary>
    /// Specifies how imported skeleton data is merged into an existing scene.
    /// </summary>
    public enum BoneConnectionMode
    {
        /// <summary>
        /// Do not perform skeleton-specific merge logic.
        /// Imported nodes are moved into the destination scene root.
        /// </summary>
        None,

        /// <summary>
        /// Merge imported bones into a destination skeleton by bone name.
        /// </summary>
        MergeByName,

        /// <summary>
        /// Attach an imported skeleton root (or a specific imported source bone)
        /// under a destination target bone.
        /// </summary>
        ConnectToTarget,

        /// <summary>
        /// Merge duplicate root nodes by name.
        /// </summary>
        MergeRoots,
    }

    /// <summary>
    /// Specifies how generic node name collisions are resolved while merging.
    /// </summary>
    public enum DuplicateNodeMode
    {
        /// <summary>
        /// Throw on duplicate node names.
        /// </summary>
        Throw,

        /// <summary>
        /// Rename the incoming node to a unique name.
        /// </summary>
        RenameIncoming,

        /// <summary>
        /// Ignore the incoming duplicate node.
        /// </summary>
        SkipIncoming,

        /// <summary>
        /// Merge incoming children into the existing node, then drop incoming parent.
        /// </summary>
        MergeChildren,
    }

    /// <summary>
    /// Specifies how duplicate bone names are resolved while merging.
    /// </summary>
    public enum BoneDuplicateMode
    {
        /// <summary>
        /// Merge incoming duplicate bones into existing destination bones.
        /// </summary>
        Merge,

        /// <summary>
        /// Rename incoming duplicate bones to unique names.
        /// </summary>
        RenameIncoming,

        /// <summary>
        /// Ignore incoming duplicate bones.
        /// </summary>
        SkipIncoming,

        /// <summary>
        /// Throw on duplicate bone names.
        /// </summary>
        Throw,
    }

    /// <summary>
    /// Provides configuration settings for scene translation and manager-side merge behavior.
    /// </summary>
    public class SceneTranslatorOptions
    {
        /// <summary>
        /// Gets or sets how imported skeletons are connected to an existing scene.
        /// </summary>
        public BoneConnectionMode BoneConnection { get; set; } = BoneConnectionMode.None;

        /// <summary>
        /// Gets or sets the destination skeleton name when a specific target skeleton
        /// should be used for bone merging/attachment. When null, the first destination
        /// skeleton is used.
        /// </summary>
        public string? TargetSkeletonName { get; set; }

        /// <summary>
        /// Gets or sets the destination bone name used by
        /// <see cref="BoneConnectionMode.ConnectToTarget"/>.
        /// </summary>
        public string? TargetAttachBone { get; set; }

        /// <summary>
        /// Gets or sets the imported source bone name used by
        /// <see cref="BoneConnectionMode.ConnectToTarget"/>.
        /// When null, all imported skeleton root children are attached.
        /// </summary>
        public string? SourceAttachBone { get; set; }

        /// <summary>
        /// Gets or sets an optional prefix applied to imported bone names before merge.
        /// </summary>
        public string? BoneNamespace { get; set; }

        /// <summary>
        /// Gets or sets the string comparison used for node and bone name matching.
        /// </summary>
        public StringComparison NameComparison { get; set; } = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Gets or sets how generic node duplicates are handled.
        /// </summary>
        public DuplicateNodeMode NodeDuplicates { get; set; } = DuplicateNodeMode.Throw;

        /// <summary>
        /// Gets or sets how duplicate bones are handled.
        /// </summary>
        public BoneDuplicateMode BoneDuplicates { get; set; } = BoneDuplicateMode.Merge;

        /// <summary>
        /// Gets or sets the suffix format used when a duplicate is renamed.
        /// The format should include a numeric placeholder (e.g. <c>"_{0}"</c>).
        /// </summary>
        public string DuplicateNameSuffixFormat { get; set; } = "_{0}";

        /// <summary>
        /// Gets or sets the first numeric suffix used when renaming duplicates.
        /// </summary>
        public int DuplicateNameStartIndex { get; set; } = 1;

        /// <summary>
        /// Gets or sets how import-time reparent operations preserve transforms.
        /// This is used while stitching incoming nodes into an existing destination scene.
        /// </summary>
        public ReparentTransformMode ImportReparentTransformMode { get; set; } = ReparentTransformMode.PreserveExisting;

        /// <summary>
        /// Gets or sets whether to collapse duplicate bones in mesh skin palettes
        /// after remapping.
        /// </summary>
        public bool CollapseDuplicateSkinnedBones { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to rebase mesh positions/normals when a mesh skin
        /// palette entry is remapped from one bone to a different destination bone.
        /// </summary>
        public bool RebaseSkinnedMeshVerticesForRemappedBones { get; set; }
    }
}
