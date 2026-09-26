using System.Numerics;
using CallOfFile;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.IwEngine;

internal static class XAnimReader
{
    public static void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? cancellationToken)
    {
        using XAssetTokenStream tokens = new(stream);
        tokens.Expect("ANIMATION");
        tokens.Expect("VERSION");
        int partCount = tokens.MoveTo("NUMPARTS").GetInt32();
        SkeletonAnimation animation = new(context.Name, partCount, TransformType.Absolute)
        {
            TransformSpace = TransformSpace.World,
            TransformType = TransformType.Absolute,
        };
        SkeletonAnimationTrack[] tracks = new SkeletonAnimationTrack[partCount];
        for (int i = 0; i < partCount; i++)
        {
            if (tokens.Expect("PART") is not TokenDataUIntString part)
                throw new InvalidDataException("Expected an XAnim part definition.");
            int partIndex = checked((int)part.IntegerValue);
            ValidateIndex(partIndex, partCount, "part");
            SkeletonAnimationTrack track = new(part.StringValue)
            {
                TransformSpace = TransformSpace.World,
                TransformType = TransformType.Absolute,
            };
            tracks[partIndex] = track;
            animation.Tracks.Add(track);
        }

        animation.Framerate = tokens.MoveTo("FRAMERATE").GetSingle();
        tokens.MoveTo("NUMFRAMES");
        while (tokens.Peek() is { } token)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            tokens.Read();

            if (token is TokenDataUIntString note && note.Token.Name == "FRAME")
                animation.CreateAction(note.StringValue).KeyFrames.Add(new(note.IntegerValue, null));
            if (token is not TokenDataInt frameToken || frameToken.Token.Name != "FRAME")
                continue;

            float frame = frameToken.Value;
            while (tokens.Peek() is { } partToken && partToken.Token.Name is not "FRAME" and not "NOTETRACKS")
            {
                if (partToken.Token.Name != "PART" || partToken is not TokenDataUInt part)
                {
                    tokens.Read();
                    continue;
                }

                int partIndex = checked((int)part.Value);
                tokens.Read();
                ValidateIndex(partIndex, partCount, "animated part");
                Vector3? offset = null;
                Vector3? scale = null;
                Vector3 x = Vector3.UnitX;
                Vector3 y = Vector3.UnitY;
                Vector3 z = Vector3.UnitZ;
                bool hasAxes = false;
                Quaternion? quaternion = null;
                while (tokens.Peek() is { } transform && transform.Token.Name is not "PART" and not "FRAME" and not "NOTETRACKS")
                {
                    tokens.Read();
                    switch (transform.Token.Name)
                    {
                        case "OFFSET": offset = transform.GetVector3(); break;
                        case "SCALE": scale = transform.GetVector3(); break;
                        case "X": x = transform.GetVector3(); hasAxes = true; break;
                        case "Y": y = transform.GetVector3(); hasAxes = true; break;
                        case "Z": z = transform.GetVector3(); hasAxes = true; break;
                        case "QUATERNION":
                            Vector4 value = transform.GetVector4();
                            quaternion = Quaternion.Normalize(new Quaternion(value.X, value.Y, value.Z, value.W));
                            break;
                    }
                }

                SkeletonAnimationTrack track = tracks[partIndex];
                if (offset.HasValue)
                    track.AddTranslationFrame(frame, offset.Value);
                if (quaternion.HasValue || hasAxes)
                    track.AddRotationFrame(frame, quaternion ?? XAssetTransform.CreateRotation(x, y, z));
                if (scale.HasValue)
                    track.AddScaleFrame(frame, scale.Value);
            }
        }

        scene.RootNode.AddNode(animation);
    }

    private static void ValidateIndex(int index, int count, string description)
    {
        if ((uint)index >= (uint)count)
            throw new InvalidDataException($"The XAnim {description} index {index} is outside the valid range 0-{count - 1}.");
    }
}
