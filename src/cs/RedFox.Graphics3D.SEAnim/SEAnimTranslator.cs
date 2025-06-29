using RedFox.Graphics3D.Skeletal;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace RedFox.Graphics3D.SEAnim
{
    /// <summary>
    /// A class to handle translating data from SEAnim files.
    /// </summary>
    public sealed class SEAnimTranslator : Graphics3DTranslator
    {
        /// <summary>
        /// SEAnim Magic
        /// </summary>
        public static readonly byte[] Magic = [0x53, 0x45, 0x41, 0x6E, 0x69, 0x6D];

        /// <inheritdoc/>
        public override string Name => "SEAnimTranslator";

        /// <inheritdoc/>
        public override string[] Extensions { get; } =
        [
            ".seanim"
        ];

        /// <inheritdoc/>
        public override bool SupportsReading => true;

        /// <inheritdoc/>
        public override bool SupportsWriting => true;

        /// <inheritdoc/>
        public override void Read(Stream stream, string filePath, Graphics3DScene scene)
        {
            using var reader = new BinaryReader(stream);

            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var magic = reader.ReadChars(6);
            var version = reader.ReadInt16();
            var sizeofHeader = reader.ReadInt16();
            var transformType = TransformType.Unknown;

            switch (reader.ReadByte())
            {
                case 0: transformType = TransformType.Absolute; break;
                case 1: transformType = TransformType.Additive; break;
                case 2: transformType = TransformType.Relative; break;
            }

            var flags         = reader.ReadByte();
            var dataFlags     = reader.ReadByte();
            var dataPropFlags = reader.ReadByte();
            var reserved      = reader.ReadUInt16();
            var frameRate     = reader.ReadSingle();
            var frameCount    = reader.ReadInt32();
            var boneCount     = reader.ReadInt32();
            var modCount      = reader.ReadByte();
            var reserved0     = reader.ReadByte();
            var reserved1     = reader.ReadByte();
            var reserved2     = reader.ReadByte();
            var noteCount     = reader.ReadInt32();

            var skelAnim = new SkeletonAnimation($"{fileName}", null, boneCount, transformType)
            {
                Framerate = frameRate,
                TransformType = transformType
            };

            for (int i = 0; i < boneCount; i++)
            {
                skelAnim.Targets.Add(new(ReadUTF8String(reader)));

                if ((dataFlags & 1) != 0)
                    skelAnim.Targets[i].TranslationFrames = [];
                if ((dataFlags & 2) != 0)
                    skelAnim.Targets[i].RotationFrames = [];
                if ((dataFlags & 4) != 0)
                    skelAnim.Targets[i].ScaleFrames = [];
            }

            for (int i = 0; i < modCount; i++)
            {
                var boneIndex = boneCount <= 0xFF ? reader.ReadByte() : reader.ReadUInt16();

                switch (reader.ReadByte())
                {
                    case 0: skelAnim.Targets[boneIndex].ChildTransformType = TransformType.Absolute; break;
                    case 1: skelAnim.Targets[boneIndex].ChildTransformType = TransformType.Additive; break;
                    case 2: skelAnim.Targets[boneIndex].ChildTransformType = TransformType.Relative; break;
                }
            }

            foreach (var bone in skelAnim.Targets)
            {
                reader.ReadByte();

                if ((dataFlags & 1) != 0)
                {
                    int keyCount;

                    if (frameCount <= 0xFF)
                        keyCount = reader.ReadByte();
                    else if (frameCount <= 0xFFFF)
                        keyCount = reader.ReadUInt16();
                    else
                        keyCount = reader.ReadInt32();

                    for (int f = 0; f < keyCount; f++)
                    {
                        int frame;

                        if (frameCount <= 0xFF)
                            frame = reader.ReadByte();
                        else if (frameCount <= 0xFFFF)
                            frame = reader.ReadUInt16();
                        else
                            frame = reader.ReadInt32();

                        if ((dataPropFlags & (1 << 0)) == 0)
                            bone.TranslationFrames!.Add(new(
                                frame,
                                new(reader.ReadSingle(),
                                    reader.ReadSingle(),
                                    reader.ReadSingle())));
                        else
                            bone.TranslationFrames!.Add(new(
                                frame,
                                new((float)reader.ReadDouble(),
                                    (float)reader.ReadDouble(),
                                    (float)reader.ReadDouble())));
                    }
                }

                if ((dataFlags & 2) != 0)
                {
                    int keyCount;

                    if (frameCount <= 0xFF)
                        keyCount = reader.ReadByte();
                    else if (frameCount <= 0xFFFF)
                        keyCount = reader.ReadUInt16();
                    else
                        keyCount = reader.ReadInt32();

                    for (int f = 0; f < keyCount; f++)
                    {
                        int frame;

                        if (frameCount <= 0xFF)
                            frame = reader.ReadByte();
                        else if (frameCount <= 0xFFFF)
                            frame = reader.ReadUInt16();
                        else
                            frame = reader.ReadInt32();

                        if ((dataPropFlags & (1 << 0)) == 0)
                            bone.RotationFrames!.Add(new(
                                frame, 
                                new(reader.ReadSingle(),
                                    reader.ReadSingle(),
                                    reader.ReadSingle(),
                                    reader.ReadSingle())));
                        else
                            bone.RotationFrames!.Add(new(
                                frame,
                                new((float)reader.ReadDouble(),
                                    (float)reader.ReadDouble(),
                                    (float)reader.ReadDouble(),
                                    (float)reader.ReadDouble())));
                    }
                }

                if ((dataFlags & 4) != 0)
                {
                    int keyCount;

                    if (frameCount <= 0xFF)
                        keyCount = reader.ReadByte();
                    else if (frameCount <= 0xFFFF)
                        keyCount = reader.ReadUInt16();
                    else
                        keyCount = reader.ReadInt32();

                    for (int f = 0; f < keyCount; f++)
                    {
                        int frame;

                        if (frameCount <= 0xFF)
                            frame = reader.ReadByte();
                        else if (frameCount <= 0xFFFF)
                            frame = reader.ReadUInt16();
                        else
                            frame = reader.ReadInt32();

                        if ((dataPropFlags & (1 << 0)) == 0)
                            bone.ScaleFrames!.Add(new(
                                frame,
                                new(reader.ReadSingle(),
                                    reader.ReadSingle(),
                                    reader.ReadSingle())));
                        else
                            bone.ScaleFrames!.Add(new(
                                frame,
                                new((float)reader.ReadDouble(),
                                    (float)reader.ReadDouble(),
                                    (float)reader.ReadDouble())));
                    }
                }
            }

            for (int i = 0; i < noteCount; i++)
            {
                int frame;

                if (frameCount <= 0xFF)
                    frame = reader.ReadByte();
                else if (frameCount <= 0xFFFF)
                    frame = reader.ReadUInt16();
                else
                    frame = reader.ReadInt32();

                skelAnim.CreateAction(ReadUTF8String(reader)).KeyFrames.Add(new(frame, null));
            }

            scene.Objects.Add(skelAnim);
        }

        /// <inheritdoc/>
        public override void Write(Stream stream, string filePath, Graphics3DScene scene)
        {
            // Determine bones with different types
            var boneModifiers = new Dictionary<int, byte>();

            var data          = scene.GetFirstInstance<SkeletonAnimation>() ?? throw new InvalidDataException();

            var frameCount    = data.GetAnimationFrameCount();
            var actionCount   = data.GetAnimationActionCount();
            var targetCount   = data.Targets.Count;
            var transformType = data.TransformType;
            int index         = 0;

            var animationType = data.TransformType;

            foreach (var bone in data.Targets)
            {
                if (bone.ChildTransformType != TransformType.Parent && bone.ChildTransformType != animationType)
                {
                    // Convert to SEAnim Type
                    switch (bone.ChildTransformType)
                    {
                        case TransformType.Absolute: boneModifiers[index] = 0; break;
                        case TransformType.Additive: boneModifiers[index] = 1; break;
                        case TransformType.Relative: boneModifiers[index] = 2; break;
                    }
                }

                index++;
            }

            using var writer = new BinaryWriter(stream);

            writer.Write(Magic);
            writer.Write((ushort)0x1);
            writer.Write((ushort)0x1C);

            // Convert to SEAnim Type

            switch (transformType)
            {
                case TransformType.Absolute: writer.Write((byte)0); break;
                case TransformType.Additive: writer.Write((byte)1); break;
                default: writer.Write((byte)2); break;
            }

            writer.Write((byte)0);

            byte flags = 0;

            if (data != null && data.HasTranslationFrames())
                flags |= 1;
            if (data != null && data.HasRotationFrames())
                flags |= 2;
            if (data != null && data.HasScalesFrames())
                flags |= 4;
            if (actionCount > 0)
                flags |= 64;

            writer.Write(flags);
            writer.Write((byte)0);
            writer.Write((ushort)0);
            writer.Write(data != null ? data.Framerate : 30.0f);
            writer.Write((int)frameCount);
            writer.Write(targetCount);
            writer.Write((byte)boneModifiers.Count);
            writer.Write((byte)0);
            writer.Write((ushort)0);
            writer.Write(actionCount);

            if (data != null)
            {
                var targets = data.Targets;

                foreach (var bone in targets)
                {
                    writer.Write(Encoding.UTF8.GetBytes(bone.BoneName.Replace('.', '_')));
                    writer.Write((byte)0);
                }

                foreach (var modifier in boneModifiers)
                {
                    if (targetCount <= 0xFF)
                        writer.Write((byte)modifier.Key);
                    else if (targetCount <= 0xFFFF)
                        writer.Write((ushort)modifier.Key);
                    else
                        throw new NotSupportedException();

                    writer.Write(modifier.Value);
                }

                foreach (var bone in targets)
                {
                    writer.Write((byte)0);

                    // TranslationFrames
                    if ((flags & 1) != 0)
                    {
                        if (frameCount <= 0xFF)
                            writer.Write((byte)bone.TranslationFrameCount);
                        else if (frameCount <= 0xFFFF)
                            writer.Write((ushort)bone.TranslationFrameCount);
                        else
                            writer.Write(bone.TranslationFrameCount);

                        if (bone.TranslationFrames != null)
                        {
                            foreach (var frame in bone.TranslationFrames)
                            {
                                if (frameCount <= 0xFF)
                                    writer.Write((byte)frame.Frame);
                                else if (frameCount <= 0xFFFF)
                                    writer.Write((ushort)frame.Frame);
                                else
                                    writer.Write((int)frame.Frame);

                                writer.Write(frame.Value.X);
                                writer.Write(frame.Value.Y);
                                writer.Write(frame.Value.Z);
                            }
                        }
                    }

                    // RotationFrames
                    if ((flags & 2) != 0)
                    {
                        if (frameCount <= 0xFF)
                            writer.Write((byte)bone.RotationFrameCount);
                        else if (frameCount <= 0xFFFF)
                            writer.Write((ushort)bone.RotationFrameCount);
                        else
                            writer.Write(bone.RotationFrameCount);

                        if (bone.RotationFrames != null)
                        {
                            foreach (var frame in bone.RotationFrames)
                            {
                                if (frameCount <= 0xFF)
                                    writer.Write((byte)frame.Frame);
                                else if (frameCount <= 0xFFFF)
                                    writer.Write((ushort)frame.Frame);
                                else
                                    writer.Write((int)frame.Frame);

                                writer.Write(frame.Value.X);
                                writer.Write(frame.Value.Y);
                                writer.Write(frame.Value.Z);
                                writer.Write(frame.Value.W);
                            }
                        }
                    }

                    // ScaleFrames
                    if ((flags & 4) != 0)
                    {
                        if (frameCount <= 0xFF)
                            writer.Write((byte)bone.ScaleFrameCount);
                        else if (frameCount <= 0xFFFF)
                            writer.Write((ushort)bone.ScaleFrameCount);
                        else
                            writer.Write(bone.ScaleFrameCount);

                        if(bone.ScaleFrames != null)
                        {
                            foreach (var frame in bone.ScaleFrames)
                            {
                                if (frameCount <= 0xFF)
                                    writer.Write((byte)frame.Frame);
                                else if (frameCount <= 0xFFFF)
                                    writer.Write((ushort)frame.Frame);
                                else
                                    writer.Write((int)frame.Frame);

                                writer.Write(frame.Value.X);
                                writer.Write(frame.Value.Y);
                                writer.Write(frame.Value.Z);
                            }
                        }
                    }
                }
            }

            if (data?.Actions != null)
            {
                foreach (var action in data.Actions)
                {
                    foreach (var frame in action.KeyFrames)
                    {
                        if (frameCount <= 0xFF)
                            writer.Write((byte)frame.Frame);
                        else if (frameCount <= 0xFFFF)
                            writer.Write((ushort)frame.Frame);
                        else
                            writer.Write((int)frame.Frame);

                        writer.Write(Encoding.UTF8.GetBytes(action.Name));
                        writer.Write((byte)0);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override bool IsValid(Graphics3DScene scene, Span<byte> startOfFile, Stream stream, string? filePath, string? ext)
        {
            return !string.IsNullOrWhiteSpace(ext) && Extensions.Contains(ext);
        }

        /// <summary>
        /// Reads a UTF8 string from the file
        /// </summary>
        internal static string ReadUTF8String(BinaryReader reader)
        {
            var output = new StringBuilder(32);

            while (true)
            {
                var c = reader.ReadByte();
                if (c == 0)
                    break;
                output.Append(Convert.ToChar(c));
            }

            return output.ToString();
        }
    }
}