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
                skelAnim.Tracks.Add(new(ReadUTF8String(reader)));

                if ((dataFlags & 1) != 0)
                    skelAnim.Tracks[i].TranslationCurve = new(TransformSpace.Local, transformType);
                if ((dataFlags & 2) != 0)
                    skelAnim.Tracks[i].RotationCurve = new(TransformSpace.Local, transformType == TransformType.Additive ? TransformType.Additive : TransformType.Absolute);
                if ((dataFlags & 4) != 0)
                    skelAnim.Tracks[i].ScaleCurve = new(TransformSpace.Local, transformType);
            }

            for (int i = 0; i < modCount; i++)
            {
                var boneIndex = boneCount <= 0xFF ? reader.ReadByte() : reader.ReadUInt16();

                switch (reader.ReadByte())
                {
                    case 0: skelAnim.Tracks[boneIndex].TransformType = TransformType.Absolute; break;
                    case 1: skelAnim.Tracks[boneIndex].TransformType = TransformType.Additive; break;
                    case 2: skelAnim.Tracks[boneIndex].TransformType = TransformType.Relative; break;
                }
            }

            foreach (var bone in skelAnim.Tracks)
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
                            bone.AddTranslationFrame(
                                frame,
                                new(reader.ReadSingle(),
                                    reader.ReadSingle(),
                                    reader.ReadSingle()));
                        else
                            bone.AddTranslationFrame(
                                frame,
                                new((float)reader.ReadDouble(),
                                    (float)reader.ReadDouble(),
                                    (float)reader.ReadDouble()));
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
                            bone.AddRotationFrame(
                                frame, 
                                new(reader.ReadSingle(),
                                    reader.ReadSingle(),
                                    reader.ReadSingle(),
                                    reader.ReadSingle()));
                        else
                            bone.AddRotationFrame(
                                frame,
                                new((float)reader.ReadDouble(),
                                    (float)reader.ReadDouble(),
                                    (float)reader.ReadDouble(),
                                    (float)reader.ReadDouble()));
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
                            bone.AddScaleFrame(
                                frame,
                                new(reader.ReadSingle(),
                                    reader.ReadSingle(),
                                    reader.ReadSingle()));
                        else
                            bone.AddScaleFrame(
                                frame,
                                new((float)reader.ReadDouble(),
                                    (float)reader.ReadDouble(),
                                    (float)reader.ReadDouble()));
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

            var data = scene.GetFirstInstance<SkeletonAnimation>() ?? throw new InvalidDataException();

            var (_, maxFrame) = data.GetAnimationFrameRange();
            var frameCount = (int)maxFrame;
            var actionCount = data.GetAnimationActionCount();
            var targetCount = data.Tracks.Count;
            var transformType = data.TransformType;
            int index = 0;

            var animationType = data.TransformType;

            //foreach (var bone in data.Tracks)
            //{
            //    if (bone.ChildTransformType != TransformType.Parent && bone.ChildTransformType != animationType)
            //    {
            //        // Convert to SEAnim Type
            //        switch (bone.ChildTransformType)
            //        {
            //            case TransformType.Absolute: boneModifiers[index] = 0; break;
            //            case TransformType.Additive: boneModifiers[index] = 1; break;
            //            case TransformType.Relative: boneModifiers[index] = 2; break;
            //        }
            //    }

            //    index++;
            //}

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

            if (data != null && data.Tracks.Any(x => x.TranslationCurve is not null))
                flags |= 1;
            if (data != null && data.Tracks.Any(x => x.RotationCurve is not null))
                flags |= 2;
            if (data != null && data.Tracks.Any(x => x.ScaleCurve is not null))
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
                var Tracks = data.Tracks;

                foreach (var bone in Tracks)
                {
                    writer.Write(Encoding.UTF8.GetBytes(bone.Name.Replace('.', '_')));
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

                foreach (var bone in Tracks)
                {
                    writer.Write((byte)0);

                    // TranslationFrames
                    if ((flags & 1) != 0)
                    {
                        var translationFrameCount = bone.TranslationCurve is null ? 0 : bone.TranslationCurve.KeyFrames.Count;

                        if (frameCount <= 0xFF)
                            writer.Write((byte)translationFrameCount);
                        else if (frameCount <= 0xFFFF)
                            writer.Write((ushort)translationFrameCount);
                        else
                            writer.Write(translationFrameCount);

                        if (bone.TranslationCurve != null)
                        {
                            foreach (var frame in bone.TranslationCurve.KeyFrames)
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
                        var rotationFrameCount = bone.RotationCurve is null ? 0 : bone.RotationCurve.KeyFrames.Count;

                        if (frameCount <= 0xFF)
                            writer.Write((byte)rotationFrameCount);
                        else if (frameCount <= 0xFFFF)
                            writer.Write((ushort)rotationFrameCount);
                        else
                            writer.Write(rotationFrameCount);

                        if (bone.RotationCurve != null)
                        {
                            foreach (var frame in bone.RotationCurve.KeyFrames)
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
                        var scaleFrameCount = bone.ScaleCurve is null ? 0 : bone.ScaleCurve.KeyFrames.Count;

                        if (frameCount <= 0xFF)
                            writer.Write((byte)scaleFrameCount);
                        else if (frameCount <= 0xFFFF)
                            writer.Write((ushort)scaleFrameCount);
                        else
                            writer.Write(scaleFrameCount);

                        if (bone.ScaleCurve != null)
                        {
                            foreach (var frame in bone.ScaleCurve.KeyFrames)
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