using System.Numerics;
using System.Text;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.IO;

namespace RedFox.Graphics3D.Formats.Wilt;

/// <summary>
/// Reads and writes Wilt model files.
/// </summary>
public sealed class WiltModelTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "WiltModel";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".wiltmodel"];

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => "WLTM"u8;

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        using BinaryReader reader = new(stream, Encoding.UTF8, true);

        scene.UpAxis = reader.ReadWiltHeader(MagicValue);

        MeshGroup		model		= scene.RootNode.AddNode<MeshGroup>(context.Name);
        SkeletonBone[]	bones		= [];
        Material[]		materials	= [];

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            token?.ThrowIfCancellationRequested();

            WiltSection	section	= (WiltSection)reader.ReadUInt32();
            uint		size	= reader.ReadUInt32();
            long		end		= reader.BaseStream.Position + size;
            if (size > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Wilt section exceeds the file boundary.");
            using SubStream sectionStream = new(reader.BaseStream, reader.BaseStream.Position, size, true);
            using BinaryReader sectionReader = new(sectionStream, Encoding.UTF8, true);

            switch (section)
            {
                case WiltSection.Meta:			sectionReader.ReadWiltMetadata(scene);							break;
                case WiltSection.Bones:			bones = ReadBones(sectionReader, scene, context);				break;
                case WiltSection.Materials:		materials = ReadMaterials(sectionReader, model, context.SourceDirectoryPath);				break;
                case WiltSection.Meshes:		ReadMeshes(sectionReader, model, bones, materials);			break;
                case WiltSection.Hair:			ReadHair(sectionReader, model, materials);						break;
                case WiltSection.IKHandles:		ReadIKHandles(sectionReader, model, bones);					break;
                case WiltSection.Constraints:	ReadConstraints(sectionReader, model, bones);					break;
            }

            reader.BaseStream.Position = end;
        }
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);

        SceneTranslationSelection	selection	= context.GetSelection(scene);
        SkeletonBone[]				bones		= selection.GetDescendants<SkeletonBone>();
        Mesh[]						meshes		= selection.GetDescendants<Mesh>();
        Hair[]						hair		= selection.GetDescendants<Hair>();
        IKHandleNode[]				ikHandles	= selection.GetDescendants<IKHandleNode>();
        ConstraintNode[]			constraints	= selection.GetDescendants<ConstraintNode>();
        Material[]					materials	= [.. selection.GetDescendants<Material>().Concat(meshes.SelectMany(mesh => mesh.Materials ?? [])).Concat(hair.Select(strands => strands.Material).OfType<Material>()).Distinct()];

        Dictionary<SkeletonBone, int>	boneTable		= bones.Select((bone, index) => (bone, index)).ToDictionary();
        Dictionary<Material, int>		materialTable	= materials.Select((material, index) => (material, index)).ToDictionary();

        writer.WriteWiltHeader(MagicValue, scene.UpAxis);
        writer.WriteWiltMetadata(scene.Attributes);
        WriteBones(writer, bones, boneTable);
        WriteMaterials(writer, materials, context);
        WriteMeshes(writer, meshes, boneTable, materialTable, token);

        if (hair.Length > 0)
            WriteHair(writer, hair, materialTable);
        if (ikHandles.Length > 0)
            WriteIKHandles(writer, ikHandles, boneTable);
        if (constraints.Length > 0)
            WriteConstraints(writer, constraints, boneTable);
    }

    private static void WriteBones(BinaryWriter writer, SkeletonBone[] bones, IReadOnlyDictionary<SkeletonBone, int> boneTable)
    {
        long		section		= writer.BeginWiltSection(WiltSection.Bones);
        SceneNode[]	boneNodes	= bones;

        Dictionary<SkeletonBone, SkeletonBone?> parents = SkeletonExportTransforms.BuildExportedParentMap(bones, boneNodes);
        writer.Write((uint)bones.Length);

        foreach (SkeletonBone bone in bones)
        {
            writer.WriteWiltString(bone.Name);
            SkeletonBone? parent = parents[bone];
            writer.Write(parent is null ? -1 : boneTable[parent]);
            SkeletonExportTransforms.DecomposeMatrix(SkeletonExportTransforms.GetRelativeBindMatrix(bone, parent), out Vector3 scale, out Quaternion rotation, out Vector3 position);
            writer.WriteStruct(position);
            writer.WriteStruct(rotation);
            writer.WriteStruct(scale);
        }

        writer.EndWiltSection(section);
    }

    private static SkeletonBone[] ReadBones(BinaryReader reader, Scene scene, SceneTranslationContext context)
    {
        int				count	= checked((int)reader.ReadUInt32());
        if (count > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Invalid Wilt bone count.");
        SkeletonBone[]	bones	= new SkeletonBone[count];
        int[]			parents	= new int[count];

        for (int i = 0; i < count; i++)
        {
            bones[i]	= new SkeletonBone(reader.ReadWiltString());
            parents[i]	= reader.ReadInt32();

            bones[i].BindTransform.LocalPosition	= reader.ReadStruct<Vector3>();
            bones[i].BindTransform.LocalRotation	= reader.ReadStruct<Quaternion>();
            bones[i].BindTransform.Scale			= reader.ReadStruct<Vector3>();
        }

        byte[] states = new byte[count];
        for (int i = 0; i < count; i++)
        {
            int current = i;
            while (current != -1 && states[current] == 0)
            {
                states[current] = 1;
                int parent = parents[current];
                if (parent < -1 || parent >= count)
                    throw new InvalidDataException("Invalid Wilt bone parent.");
                current = parent;
            }
            if (current != -1 && states[current] == 1)
                throw new InvalidDataException("Cyclic Wilt hierarchy.");
            current = i;
            while (current != -1 && states[current] == 1)
            {
                states[current]	= 2;
                current			= parents[current];
            }
        }

        if (count > 0)
            scene.RootNode.AddNode(new Skeleton($"{context.Name}_Skeleton")).LinkHierarchyUnsafe(bones, parents);

        return bones;
    }

    private static void WriteMaterials(BinaryWriter writer, Material[] materials, SceneTranslationContext context)
    {
        long section = writer.BeginWiltSection(WiltSection.Materials);

        writer.Write((uint)materials.Length);

        foreach (Material material in materials)
        {
            byte flags = (byte)((material.DoubleSided ? 1 : 0) | (material.SpecularMaskInDiffuseAlpha ? 2 : 0) | (material.UseSpecularColorMap ? 4 : 0));

            writer.WriteWiltString(material.Name);
            writer.WriteWiltString(material.Type ?? string.Empty);
            writer.Write(flags);
            writer.Write(checked((ushort)material.Textures.Count));

            foreach (MaterialTextureBinding binding in material.Textures)
            {
                writer.WriteWiltString(binding.SamplerUniform);
                writer.WriteWiltString(binding.Texture.GetPortableFilePath(context.TargetDirectoryPath));
                writer.Write((byte)GetTextureUsage(material, binding.Texture));
            }

            (string Name, object? Value)[] parameters = GetMaterialParameters(material);

            writer.Write(checked((ushort)parameters.Length));

            foreach ((string name, object? value) in parameters)
            {
                writer.WriteWiltString(name);
                writer.WriteWiltValue(value);
            }
        }

        writer.EndWiltSection(section);
    }

    private static Material[] ReadMaterials(BinaryReader reader, MeshGroup model, string? sourceDirectory)
    {
        uint count = reader.ReadUInt32();
        if (count > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Invalid Wilt material count.");
        Material[] materials = new Material[checked((int)count)];

        for (int i = 0; i < materials.Length; i++)
        {
            Material	material	= model.AddNode(new Material(reader.ReadWiltString()));
            string		type		= reader.ReadWiltString();
            byte		flags		= reader.ReadByte();

            material.Type						= type.Length > 0 ? type : null;
            material.DoubleSided				= (flags & 1) != 0;
            material.SpecularMaskInDiffuseAlpha	= (flags & 2) != 0;
            material.UseSpecularColorMap		= (flags & 4) != 0;

            ushort textureCount = reader.ReadUInt16();

            for (int t = 0; t < textureCount; t++)
            {
                string				slotKey	= reader.ReadWiltString();
                Texture				texture	= material.AddNode(new Texture(reader.ReadWiltString()));
                WiltTextureUsage	usage	= (WiltTextureUsage)reader.ReadByte();

                texture.ResolvedFilePath = FilePathResolver.ResolveReferencePath(texture.FilePath, sourceDirectory);
                SetMapName(material, usage, slotKey);
                material.Connect(slotKey, texture);
            }

            ushort parameterCount = reader.ReadUInt16();

            for (int p = 0; p < parameterCount; p++)
                SetMaterialParameter(material, reader.ReadWiltString(), reader.ReadWiltValue());

            materials[i] = material;
        }

        return materials;
    }

    private static void WriteMeshes(BinaryWriter writer, Mesh[] meshes, Dictionary<SkeletonBone, int> boneTable, Dictionary<Material, int> materialTable, CancellationToken? token)
    {
        long section = writer.BeginWiltSection(WiltSection.Meshes);

        writer.Write((uint)meshes.Length);

        foreach (Mesh mesh in meshes)
        {
            token?.ThrowIfCancellationRequested();

            (WiltVertexSemantic Semantic, DataBuffer? Buffer)[] streams = [(WiltVertexSemantic.Position,	mesh.Positions), (WiltVertexSemantic.Normal,		mesh.Normals), (WiltVertexSemantic.Tangent,	mesh.Tangents), (WiltVertexSemantic.BiTangent,	mesh.BiTangents), (WiltVertexSemantic.UV,			mesh.UVLayers), (WiltVertexSemantic.Color,		mesh.ColorLayers),];

            writer.WriteWiltString(mesh.Name);
            writer.Write((uint)mesh.VertexCount);
            writer.Write((byte)streams.Count(stream => stream.Buffer is not null));

            foreach ((WiltVertexSemantic semantic, DataBuffer? buffer) in streams)
            {
                if (buffer is null)
                    continue;

                writer.Write((byte)semantic);
                writer.WriteWiltBuffer(buffer);
            }

            writer.WriteWiltBuffer(mesh.FaceIndices);

            List<Material> meshMaterials = mesh.Materials ?? [];

            writer.Write(checked((ushort)meshMaterials.Count));

            for (int i = 0; i < meshMaterials.Count; i++)
            {
                (int startIndex, int indexCount) = mesh.MaterialIndexRanges?[i] ?? (0, mesh.IndexCount);

                writer.Write(materialTable[meshMaterials[i]]);
                writer.Write((uint)startIndex);
                writer.Write((uint)indexCount);
            }

            writer.Write(mesh.Skin is not null);

            if (mesh.Skin is Skin skin)
            {
                int[] palette = skin.GetBoneTableIndices(boneTable);

                writer.Write((byte)skin.SkinningMode);
                writer.Write((uint)palette.Length);
                writer.WriteStructArray(palette);
                writer.Write(skin.InverseBindMatrices is not null);

                foreach (Matrix4x4 matrix in skin.InverseBindMatrices ?? [])
                    writer.WriteStruct(matrix);

                writer.WriteWiltBuffer(skin.BoneIndices);
                writer.WriteWiltBuffer(skin.BoneWeights);
            }

            writer.Write(mesh.Morph is not null);

            if (mesh.Morph is Morph morph)
            {
                writer.Write((uint)morph.TargetCount);

                foreach (string targetName in morph.TargetNames)
                    writer.WriteWiltString(targetName);

                writer.WriteStructArray(morph.Weights);
                writer.WriteWiltBuffer(morph.DeltaPositions);
                writer.WriteWiltBuffer(morph.DeltaNormals);
                writer.WriteWiltBuffer(morph.DeltaTangents);
            }
        }

        writer.EndWiltSection(section);
    }

    private static void ReadMeshes(BinaryReader reader, MeshGroup model, SkeletonBone[] bones, Material[] materials)
    {
        uint count = reader.ReadUInt32();

        for (uint i = 0; i < count; i++)
        {
            Mesh mesh = model.AddNode<Mesh>(reader.ReadWiltString());

            reader.ReadUInt32();

            byte streamCount = reader.ReadByte();

            for (int s = 0; s < streamCount; s++)
            {
                WiltVertexSemantic	semantic	= (WiltVertexSemantic)reader.ReadByte();
                DataBuffer?			buffer		= reader.ReadWiltBuffer();

                switch (semantic)
                {
                    case WiltVertexSemantic.Position:	mesh.Positions		= buffer; break;
                    case WiltVertexSemantic.Normal:		mesh.Normals		= buffer; break;
                    case WiltVertexSemantic.Tangent:	mesh.Tangents		= buffer; break;
                    case WiltVertexSemantic.BiTangent:	mesh.BiTangents		= buffer; break;
                    case WiltVertexSemantic.UV:			mesh.UVLayers		= buffer; break;
                    case WiltVertexSemantic.Color:		mesh.ColorLayers	= buffer; break;
                    default:							throw new InvalidDataException($"Unsupported Wilt vertex semantic: {semantic}.");
                }
            }

            mesh.FaceIndices = reader.ReadWiltBuffer();

            ushort rangeCount = reader.ReadUInt16();

            if (rangeCount > 0)
            {
                mesh.Materials				= new(rangeCount);
                mesh.MaterialIndexRanges	= new(rangeCount);

                for (int r = 0; r < rangeCount; r++)
                {
                    mesh.Materials.Add(materials[reader.ReadInt32()]);
                    mesh.MaterialIndexRanges.Add((checked((int)reader.ReadUInt32()), checked((int)reader.ReadUInt32())));
                }
            }

            if (reader.ReadBoolean())
            {
                SkinningMode	skinningMode= (SkinningMode)reader.ReadByte();
                int				paletteCount= checked((int)reader.ReadUInt32());
                if ((long)paletteCount * sizeof(int) > reader.BaseStream.Length - reader.BaseStream.Position)
                    throw new InvalidDataException("Invalid Wilt palette count.");
                SkeletonBone[] palette = new SkeletonBone[paletteCount];
                for (int p = 0; p < palette.Length; p++)
                    palette[p] = bones[reader.ReadInt32()];
                Matrix4x4[]?	inverseBinds	= reader.ReadBoolean() ? reader.ReadStructArray<Matrix4x4>(palette.Length).ToArray() : null;
                DataBuffer		boneIndices		= reader.ReadWiltBuffer() ?? throw new InvalidDataException($"Wilt mesh '{mesh.Name}' skin has no bone indices.");
                DataBuffer		boneWeights		= reader.ReadWiltBuffer() ?? throw new InvalidDataException($"Wilt mesh '{mesh.Name}' skin has no bone weights.");

                mesh.Skin = new Skin(palette, boneIndices, boneWeights)
                {
                    SkinningMode		= skinningMode,
                    InverseBindMatrices	= inverseBinds,
                };
            }

            if (reader.ReadBoolean())
            {
                string[] targetNames = new string[checked((int)reader.ReadUInt32())];

                for (int t = 0; t < targetNames.Length; t++)
                    targetNames[t] = reader.ReadWiltString();

                float[]	weights	= reader.ReadStructArray<float>(targetNames.Length).ToArray();
                Morph	morph	= new(targetNames, reader.ReadWiltBuffer(), reader.ReadWiltBuffer(), reader.ReadWiltBuffer());

                weights.CopyTo(morph.Weights);
                mesh.Morph = morph;
            }
        }
    }

    private static void WriteHair(BinaryWriter writer, Hair[] hair, Dictionary<Material, int> materialTable)
    {
        long section = writer.BeginWiltSection(WiltSection.Hair);

        writer.Write((uint)hair.Length);

        foreach (Hair strands in hair)
        {
            writer.WriteWiltString(strands.Name);
            writer.Write(strands.Material is null ? -1 : materialTable[strands.Material]);
            writer.Write((uint)strands.StrandSegments.Length);
            writer.WriteStructArray(strands.StrandSegments);
            writer.WriteWiltBuffer(strands.Particles);
        }

        writer.EndWiltSection(section);
    }

    private static void ReadHair(BinaryReader reader, MeshGroup model, Material[] materials)
    {
        uint count = reader.ReadUInt32();

        for (uint i = 0; i < count; i++)
        {
            Hair	hair			= model.AddNode<Hair>(reader.ReadWiltString());
            int		materialIndex	= reader.ReadInt32();

            hair.Material		= materialIndex < 0 ? null : materials[materialIndex];
            hair.StrandSegments	= reader.ReadStructArray<int>(checked((int)reader.ReadUInt32())).ToArray();
            hair.Particles		= reader.ReadWiltBuffer();
        }
    }

    private static void WriteIKHandles(BinaryWriter writer, IKHandleNode[] ikHandles, Dictionary<SkeletonBone, int> boneTable)
    {
        long section = writer.BeginWiltSection(WiltSection.IKHandles);

        writer.Write((uint)ikHandles.Length);

        foreach (IKHandleNode handle in ikHandles)
        {
            writer.WriteWiltString(handle.Name);
            writer.Write(GetBoneIndex(boneTable, handle.StartNode));
            writer.Write(GetBoneIndex(boneTable, handle.EndNode));
            writer.Write(GetBoneIndex(boneTable, handle.TargetNode));
            writer.Write(GetBoneIndex(boneTable, handle.PoleVectorNode));
            writer.Write(GetBoneIndex(boneTable, handle.PoleNode));
            writer.WriteStruct(handle.TargetOffset);
            writer.Write(handle.UseTargetRotation);
            writer.Write(handle.Weight);
        }

        writer.EndWiltSection(section);
    }

    private static void ReadIKHandles(BinaryReader reader, MeshGroup model, SkeletonBone[] bones)
    {
        uint count = reader.ReadUInt32();

        for (uint i = 0; i < count; i++)
        {
            string			name	= reader.ReadWiltString();
            SkeletonBone	start	= bones[reader.ReadInt32()];
            SkeletonBone	end		= bones[reader.ReadInt32()];

            model.AddNode(new IKHandleNode(name, start, end)
            {
                TargetNode			= GetBone(bones, reader.ReadInt32()),
                PoleVectorNode		= GetBone(bones, reader.ReadInt32()),
                PoleNode			= GetBone(bones, reader.ReadInt32()),
                TargetOffset		= reader.ReadStruct<Vector3>(),
                UseTargetRotation	= reader.ReadBoolean(),
                Weight				= reader.ReadSingle(),
            });
        }
    }

    private static void WriteConstraints(BinaryWriter writer, ConstraintNode[] constraints, Dictionary<SkeletonBone, int> boneTable)
    {
        long section = writer.BeginWiltSection(WiltSection.Constraints);

        writer.Write((uint)constraints.Length);

        foreach (ConstraintNode constraint in constraints)
        {
            WiltConstraintType	type;
            Vector3				translationOffset	= Vector3.Zero;
            Quaternion			rotationOffset		= Quaternion.Identity;
            Vector3				scaleOffset			= Vector3.One;
            byte				skipMask			= 0;

            switch (constraint)
            {
                case PointConstraintNode point:
                    type				= WiltConstraintType.Point;
                    translationOffset	= point.TranslationOffset;
                    skipMask			= GetSkipMask(point.SkipX, point.SkipY, point.SkipZ);
                    break;
                case OrientConstraintNode orient:
                    type				= WiltConstraintType.Orient;
                    rotationOffset		= orient.RotationOffset;
                    skipMask			= GetSkipMask(orient.SkipX, orient.SkipY, orient.SkipZ);
                    break;
                case ScaleConstraintNode scale:
                    type				= WiltConstraintType.Scale;
                    scaleOffset			= scale.ScaleOffset;
                    skipMask			= GetSkipMask(scale.SkipX, scale.SkipY, scale.SkipZ);
                    break;
                case ParentConstraintNode parent:
                    type				= WiltConstraintType.Parent;
                    translationOffset	= parent.TranslationOffset;
                    rotationOffset		= parent.RotationOffset;
                    break;
                default:
                    throw new InvalidDataException($"Wilt cannot store constraints of type {constraint.GetType().Name}.");
            }

            writer.Write((byte)type);
            writer.WriteWiltString(constraint.Name);
            writer.Write(GetBoneIndex(boneTable, constraint.ConstrainedNode));
            writer.Write(GetBoneIndex(boneTable, constraint.SourceNode));
            writer.Write(constraint.Weight);
            writer.WriteStruct(translationOffset);
            writer.WriteStruct(rotationOffset);
            writer.WriteStruct(scaleOffset);
            writer.Write(skipMask);
        }

        writer.EndWiltSection(section);
    }

    private static void ReadConstraints(BinaryReader reader, MeshGroup model, SkeletonBone[] bones)
    {
        uint count = reader.ReadUInt32();

        for (uint i = 0; i < count; i++)
        {
            WiltConstraintType	type				= (WiltConstraintType)reader.ReadByte();
            string				name				= reader.ReadWiltString();
            SkeletonBone		constrained			= bones[reader.ReadInt32()];
            SkeletonBone		source				= bones[reader.ReadInt32()];
            float				weight				= reader.ReadSingle();
            Vector3				translationOffset	= reader.ReadStruct<Vector3>();
            Quaternion			rotationOffset		= reader.ReadStruct<Quaternion>();
            Vector3				scaleOffset			= reader.ReadStruct<Vector3>();
            byte				skipMask			= reader.ReadByte();
            bool				skipX				= (skipMask & 1) != 0;
            bool				skipY				= (skipMask & 2) != 0;
            bool				skipZ				= (skipMask & 4) != 0;

            ConstraintNode constraint = type switch
            {
                WiltConstraintType.Point	=> new PointConstraintNode(name, constrained, source)
                {
                    TranslationOffset	= translationOffset,
                    SkipX				= skipX,
                    SkipY				= skipY,
                    SkipZ				= skipZ,
                },
                WiltConstraintType.Orient	=> new OrientConstraintNode(name, constrained, source)
                {
                    RotationOffset	= rotationOffset,
                    SkipX			= skipX,
                    SkipY			= skipY,
                    SkipZ			= skipZ,
                },
                WiltConstraintType.Scale	=> new ScaleConstraintNode(name, constrained, source)
                {
                    ScaleOffset	= scaleOffset,
                    SkipX		= skipX,
                    SkipY		= skipY,
                    SkipZ		= skipZ,
                },
                WiltConstraintType.Parent	=> new ParentConstraintNode(name, constrained, source)
                {
                    TranslationOffset	= translationOffset,
                    RotationOffset		= rotationOffset,
                },
                _							=> throw new InvalidDataException($"Unsupported Wilt constraint type: {type}."),
            };

            constraint.Weight = weight;
            model.AddNode(constraint);
        }
    }

    private static WiltTextureUsage GetTextureUsage(Material material, Texture texture)
    {
        (WiltTextureUsage Usage, string? MapName)[] maps = [(WiltTextureUsage.Diffuse,			material.DiffuseMapName), (WiltTextureUsage.Normal,			material.NormalMapName), (WiltTextureUsage.Specular,			material.SpecularMapName), (WiltTextureUsage.Metallic,			material.MetallicMapName), (WiltTextureUsage.Gloss,			material.GlossMapName), (WiltTextureUsage.Roughness,		material.RoughnessMapName), (WiltTextureUsage.Emissive,			material.EmissiveMapName), (WiltTextureUsage.EmissiveMask,		material.EmissiveMaskMapName), (WiltTextureUsage.AmbientOcclusion,	material.AmbientOcclusionMapName), (WiltTextureUsage.Cavity,			material.CavityMapName), (WiltTextureUsage.Anisotropy,		material.AnisotropyMapName),];

        foreach ((WiltTextureUsage usage, string? mapName) in maps)
        {
            if (mapName is not null && material.TryGetTexture(mapName, out Texture? mapTexture) && ReferenceEquals(mapTexture, texture))
                return usage;
        }

        return WiltTextureUsage.None;
    }

    private static void SetMapName(Material material, WiltTextureUsage usage, string slotKey)
    {
        switch (usage)
        {
            case WiltTextureUsage.None:																break;
            case WiltTextureUsage.Diffuse:			material.DiffuseMapName				= slotKey;	break;
            case WiltTextureUsage.Normal:			material.NormalMapName				= slotKey;	break;
            case WiltTextureUsage.Specular:			material.SpecularMapName			= slotKey;	break;
            case WiltTextureUsage.Metallic:			material.MetallicMapName			= slotKey;	break;
            case WiltTextureUsage.Gloss:			material.GlossMapName				= slotKey;	break;
            case WiltTextureUsage.Roughness:		material.RoughnessMapName			= slotKey;	break;
            case WiltTextureUsage.Emissive:			material.EmissiveMapName			= slotKey;	break;
            case WiltTextureUsage.EmissiveMask:		material.EmissiveMaskMapName		= slotKey;	break;
            case WiltTextureUsage.AmbientOcclusion:	material.AmbientOcclusionMapName	= slotKey;	break;
            case WiltTextureUsage.Cavity:			material.CavityMapName				= slotKey;	break;
            case WiltTextureUsage.Anisotropy:		material.AnisotropyMapName			= slotKey;	break;
            default:								throw new InvalidDataException($"Unsupported Wilt texture usage: {usage}.");
        }
    }

    private static (string Name, object? Value)[] GetMaterialParameters(Material material)
    {
        (string Name, object? Value)[] parameters = [("diffuse_color",		material.DiffuseColor), ("specular_color",		material.SpecularColor), ("metallic_color",		material.MetallicColor), ("gloss_color",			material.GlossColor), ("roughness_color",		material.RoughnessColor), ("emissive_color",		material.EmissiveColor), ("ao_color",			material.AmbientOcclusionColor), ("cavity_color",		material.CavityColor), ("anisotropy_color",	material.AnisotropyColor), ("shininess",			material.Shininess), ("specular_strength",	material.SpecularStrength), ("metallic",			material.Metallic), ("roughness",			material.Roughness),];

        return [.. parameters.Where(parameter => parameter.Value is not null), .. (material.Attributes ?? []).Select(attribute => (attribute.Key, (object?)attribute.Value))];
    }

    private static void SetMaterialParameter(Material material, string name, object? value)
    {
        switch (name)
        {
            case "diffuse_color":		material.DiffuseColor			= (Vector4?)value;	break;
            case "specular_color":		material.SpecularColor			= (Vector4?)value;	break;
            case "metallic_color":		material.MetallicColor			= (Vector4?)value;	break;
            case "gloss_color":			material.GlossColor				= (Vector4?)value;	break;
            case "roughness_color":		material.RoughnessColor			= (Vector4?)value;	break;
            case "emissive_color":		material.EmissiveColor			= (Vector4?)value;	break;
            case "ao_color":			material.AmbientOcclusionColor	= (Vector4?)value;	break;
            case "cavity_color":		material.CavityColor			= (Vector4?)value;	break;
            case "anisotropy_color":	material.AnisotropyColor		= (Vector4?)value;	break;
            case "shininess":			material.Shininess				= (float?)value;	break;
            case "specular_strength":	material.SpecularStrength		= (float?)value;	break;
            case "metallic":			material.Metallic				= (float?)value;	break;
            case "roughness":			material.Roughness				= (float?)value;	break;
            default:					(material.Attributes ??= [])[name] = value ?? throw new InvalidDataException($"Wilt material parameter '{name}' has no value."); break;
        }
    }

    private static int GetBoneIndex(Dictionary<SkeletonBone, int> boneTable, SceneNode? node)
    {
        if (node is null)
            return -1;

        if (node is SkeletonBone bone && boneTable.TryGetValue(bone, out int index))
            return index;

        throw new InvalidDataException($"Wilt can only reference exported bones, but '{node.Name}' is not one.");
    }

    private static SkeletonBone? GetBone(SkeletonBone[] bones, int index) => index == -1 ? null : bones[index];

    private static byte GetSkipMask(bool skipX, bool skipY, bool skipZ) => (byte)((skipX ? 1 : 0) | (skipY ? 2 : 0) | (skipZ ? 4 : 0));
}
