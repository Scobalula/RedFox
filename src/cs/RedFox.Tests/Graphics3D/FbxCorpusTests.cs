using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Formats.KaydaraFbx;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using Xunit.Abstractions;

namespace RedFox.Tests.Graphics3D;

public sealed class FbxCorpusTests(ITestOutputHelper output)
{
    private static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void FbxCorpus_RealFiles_LoadWithoutErrors()
    {
        if (GetCorpusDirectory() is not string directory)
            return;

        string[]						files		= [.. Directory.EnumerateFiles(directory, "*.fbx", SearchOption.AllDirectories).Where(path => !IsFuzzPath(path)).Order(StringComparer.Ordinal)];
        List<string>					failures	= [];
        Dictionary<string, TimeSpan>	timings		= [];
        Stopwatch						total		= Stopwatch.StartNew();

        foreach (string file in files)
        {
            Stopwatch	timer	= Stopwatch.StartNew();
            Task<Scene>	task	= Task.Run(() => ReadScene(file));

            if (Task.WhenAny(task, Task.Delay(FileTimeout)).Result != task)
            {
                failures.Add($"{Path.GetFileName(file)}: timed out");
                continue;
            }

            bool expectedFailure = Path.GetFileName(file).Contains("_fail", StringComparison.Ordinal) && task.Exception?.InnerException is InvalidDataException;

            timings[Path.GetFileName(file)] = timer.Elapsed;

            if (task.Exception is not null && !expectedFailure)
                failures.Add($"{Path.GetFileName(file)}: {task.Exception.InnerException?.GetType().Name}: {task.Exception.InnerException?.Message}");
        }

        output.WriteLine($"Loaded {files.Length - failures.Count}/{files.Length} files in {total.Elapsed.TotalSeconds:F2}s");

        foreach (KeyValuePair<string, TimeSpan> timing in timings.OrderByDescending(pair => pair.Value).Take(10))
            output.WriteLine($"  {timing.Value.TotalMilliseconds,8:F1} ms  {timing.Key}");

        foreach (string failure in failures)
            output.WriteLine(failure);

        Assert.Empty(failures);
    }

    [Fact]
    public void FbxCorpus_FuzzFiles_FailWithInvalidDataOnly()
    {
        if (GetCorpusDirectory() is not string directory)
            return;

        string[]		files		= [.. Directory.EnumerateFiles(Path.Combine(directory, "fuzz"), "*.fbx").Order(StringComparer.Ordinal)];
        List<string>	failures	= [];
        int				rejected	= 0;
        Stopwatch		total		= Stopwatch.StartNew();

        foreach (string file in files)
        {
            Task<Scene> task = Task.Run(() => ReadScene(file));

            if (Task.WhenAny(task, Task.Delay(FileTimeout)).Result != task)
            {
                failures.Add($"{Path.GetFileName(file)}: timed out");
                continue;
            }

            if (task.Exception?.InnerException is InvalidDataException)
                rejected++;
            else if (task.Exception is not null)
                failures.Add($"{Path.GetFileName(file)}: {task.Exception.InnerException?.GetType().Name}: {task.Exception.InnerException?.Message}");
        }

        output.WriteLine($"Fuzz: {files.Length} files, {rejected} rejected, {failures.Count} unexpected, {total.Elapsed.TotalSeconds:F2}s");

        foreach (string failure in failures)
            output.WriteLine(failure);

        Assert.Empty(failures);
    }

    [Fact]
    public void FbxCorpus_ReferenceObjFiles_MatchEvaluatedPositions()
    {
        if (GetCorpusDirectory() is not string directory)
            return;

        string[]	objFiles	= [.. Directory.EnumerateFiles(directory, "*.obj").Order(StringComparer.Ordinal)];
        string[]	fbxFiles	= [.. Directory.EnumerateFiles(directory, "*.fbx").Order(StringComparer.Ordinal)];
        int			compared	= 0;
        int			matched		= 0;

        foreach (string objFile in objFiles)
        {
            string objName = Path.GetFileNameWithoutExtension(objFile);

            foreach (string fbxFile in fbxFiles)
            {
                if (!TryGetReferenceFrame(Path.GetFileNameWithoutExtension(fbxFile), objName, out int frame))
                    continue;

                compared++;
                string result;

                try
                {
                    Scene			scene		= ReadScene(fbxFile);
                    List<Vector3>	expected	= ReadObjPositions(objFile);

                    ApplyAnimationFrame(scene, frame, true);
                    result = ComparePositions(expected, GetEvaluatedPositions(scene), out bool isMatch);

                    if (!isMatch && frame != 0)
                    {
                        ApplyAnimationFrame(scene, frame, false);
                        string absolute = ComparePositions(expected, GetEvaluatedPositions(scene), out isMatch);
                        result = isMatch ? absolute : result;
                    }

                    if (isMatch)
                        matched++;
                }
                catch (Exception exception)
                {
                    result = $"{exception.GetType().Name}: {exception.Message}";
                }

                output.WriteLine($"{Path.GetFileName(fbxFile)} vs {objName} @ {frame}: {result}");
            }
        }

        output.WriteLine($"Matched {matched}/{compared} reference comparisons");
    }

    [Fact]
    public void FbxCorpus_RealFiles_RoundTripPreservesEvaluatedPositions()
    {
        if (GetCorpusDirectory() is not string directory)
            return;

        string[]		files		= [.. Directory.EnumerateFiles(directory, "*.fbx").Where(path => !path.Contains("_fail", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
        List<string>	failures	= [];
        Stopwatch		total		= Stopwatch.StartNew();

        foreach (string file in files)
        {
            try
            {
                Scene source = ReadScene(file);

                foreach (string target in (ReadOnlySpan<string>)["roundtrip.fbx", "roundtrip.ascii.fbx"])
                {
                    SceneTranslatorManager manager = new();
                    manager.Register(new FbxTranslator());

                    using MemoryStream stream = new();
                    manager.Write(stream, target, source, new SceneTranslatorOptions(), token: null);

                    stream.Position = 0;
                    Scene reloaded = manager.Read(stream, target, new SceneTranslatorOptions(), token: null);

                    List<Vector3> expected = GetEvaluatedPositions(source);

                    if (expected.Count == 0 || !expected.TrueForAll(static position => float.IsFinite(position.X + position.Y + position.Z)))
                        continue;

                    string result = ComparePositions(expected, GetEvaluatedPositions(reloaded), out bool isMatch);

                    if (!isMatch)
                        failures.Add($"{Path.GetFileName(file)} -> {target}: {result}");
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{Path.GetFileName(file)}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        output.WriteLine($"Round-tripped {files.Length} files in {total.Elapsed.TotalSeconds:F2}s, {failures.Count} failures");

        foreach (string failure in failures)
            output.WriteLine(failure);

        Assert.Empty(failures);
    }

    private static string? GetCorpusDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("REDFOX_FBX_DIR");
        return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory) ? null : directory;
    }

    private static bool IsFuzzPath(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}fuzz{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static Scene ReadScene(string path)
    {
        SceneTranslatorManager manager = new();
        manager.Register(new FbxTranslator());
        return manager.Read(path, new SceneTranslatorOptions(), token: null);
    }

    private static bool TryGetReferenceFrame(string fbxName, string objName, out int frame)
    {
        frame = 0;
        int formatIndex = Math.Max(fbxName.IndexOf("_ascii", StringComparison.Ordinal), fbxName.IndexOf("_binary", StringComparison.Ordinal));

        if (formatIndex < 5 || !char.IsAsciiDigit(fbxName[formatIndex - 1]) || fbxName[formatIndex - 5] != '_')
            return false;

        string	baseName	= fbxName[..(formatIndex - 5)];
        string	variant		= fbxName[(fbxName.IndexOf('_', formatIndex + 1) is int variantIndex and > 0 ? variantIndex : fbxName.Length)..];
        string	expected	= baseName + variant;

        if (!objName.StartsWith(expected, StringComparison.Ordinal))
            return false;

        string suffix = objName[expected.Length..];

        if (suffix.Length == 0)
            return true;

        if (suffix == "_0_obj")
            return true;

        return suffix[0] == '_' && int.TryParse(suffix.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out frame);
    }

    private static void ApplyAnimationFrame(Scene scene, int frame, bool relative)
    {
        foreach (MorphAnimation morphAnimation in scene.EnumerateDescendants<MorphAnimation>())
        {
            foreach (MorphAnimationTrack track in morphAnimation.Tracks)
            {
                float start = relative && track.WeightCurve is { KeyFrameCount: > 0 } curve ? curve.StartTime : 0.0f;

                foreach (Mesh mesh in scene.EnumerateDescendants<Mesh>())
                {
                    if (mesh.Morph?.IndexOfTarget(track.Name) is int index and >= 0)
                        mesh.Morph.Weights[index] = track.SampleWeight(start + frame);
                }
            }
        }

        SkeletonAnimation? animation = scene.EnumerateDescendants<SkeletonAnimation>().FirstOrDefault();

        if (animation is null)
            return;

        Dictionary<string, SceneNode> nodes = new(StringComparer.Ordinal);

        foreach (SceneNode node in scene.EnumerateDescendants<SceneNode>())
            nodes.TryAdd(node.Name, node);

        float time = (relative ? animation.GetAnimationFrameRange().Item1 : 0.0f) + frame;

        foreach (SkeletonAnimationTrack track in animation.Tracks)
        {
            if (!nodes.TryGetValue(track.Name, out SceneNode? node))
                continue;

            if (track.TranslationCurve is { KeyFrameCount: > 0 } translation)
                node.LiveTransform.LocalPosition = translation.SampleVector3(time);

            if (track.RotationCurve is { KeyFrameCount: > 0 } rotation)
                node.LiveTransform.LocalRotation = rotation.SampleQuaternion(time);

            if (track.ScaleCurve is { KeyFrameCount: > 0 } scale)
                node.LiveTransform.Scale = scale.SampleVector3(time);
        }
    }

    private static List<Vector3> GetEvaluatedPositions(Scene scene)
    {
        List<Vector3> positions = [];

        foreach (Mesh mesh in scene.EnumerateDescendants<Mesh>())
        {
            if (mesh.Positions is null)
                continue;

            Matrix4x4 world = mesh.GetActiveWorldMatrix();

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                Vector3 position = mesh.GetVertexPosition(i);
                positions.Add(mesh.Skin is not null && HasWeights(mesh.Skin, i) ? position : Vector3.Transform(position, world));
            }
        }

        return positions;
    }

    private static bool HasWeights(Skin skin, int vertex)
    {
        for (int i = 0; i < skin.InfluenceCount; i++)
        {
            if (skin.BoneWeights.Get<float>(vertex, i, 0) > 0.0f)
                return true;
        }

        return false;
    }

    private static List<Vector3> ReadObjPositions(string path)
    {
        List<Vector3> positions = [];

        foreach (string line in File.ReadLines(path))
        {
            if (!line.StartsWith("v ", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            positions.Add(new Vector3(float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture)));
        }

        return positions;
    }

    private static string ComparePositions(List<Vector3> expected, List<Vector3> actual, out bool isMatch)
    {
        isMatch = false;

        if (expected.Count == 0 || actual.Count == 0)
            return $"empty (expected {expected.Count}, actual {actual.Count})";

        Vector3 min = expected[0];
        Vector3 max = expected[0];

        foreach (Vector3 position in expected)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        float	tolerance		= MathF.Max(Vector3.Distance(min, max) * 0.002f, 1e-4f);
        float	actualCovered	= 0.0f;
        float	expectedCovered	= 0.0f;

        foreach (float scale in (ReadOnlySpan<float>)[1.0f, 0.01f, 100.0f])
        {
            List<Vector3>	scaled			= actual.ConvertAll(position => position * scale);
            float			scaledActual	= GetCoverage(scaled, expected, tolerance);
            float			scaledExpected	= GetCoverage(expected, scaled, tolerance);

            if (scaledActual + scaledExpected > actualCovered + expectedCovered)
            {
                actualCovered	= scaledActual;
                expectedCovered	= scaledExpected;
            }
        }

        isMatch = actualCovered >= 0.99f && expectedCovered >= 0.99f;

        StringBuilder builder = new();
        builder.Append(isMatch ? "OK" : "MISMATCH");
        builder.Append(CultureInfo.InvariantCulture, $" (actual→ref {actualCovered:P1}, ref→actual {expectedCovered:P1}, {actual.Count} vs {expected.Count} verts)");

        if (!isMatch)
            builder.Append(CultureInfo.InvariantCulture, $" ref [{min} .. {max}] actual [{GetMin(actual)} .. {GetMax(actual)}]");
        return builder.ToString();
    }

    private static Vector3 GetMin(List<Vector3> positions)
    {
        return positions.Aggregate(Vector3.Min);
    }

    private static Vector3 GetMax(List<Vector3> positions)
    {
        return positions.Aggregate(Vector3.Max);
    }

    private static float GetCoverage(List<Vector3> source, List<Vector3> target, float tolerance)
    {
        Dictionary<Vector3, List<Vector3>> grid = [];

        foreach (Vector3 position in target)
        {
            Vector3 cell = GetCell(position, tolerance);

            if (!grid.TryGetValue(cell, out List<Vector3>? bucket))
                grid[cell] = bucket = [];

            bucket.Add(position);
        }

        int covered = 0;

        foreach (Vector3 position in source)
        {
            if (HasNeighbour(grid, position, tolerance))
                covered++;
        }

        return covered / (float)source.Count;
    }

    private static bool HasNeighbour(Dictionary<Vector3, List<Vector3>> grid, Vector3 position, float tolerance)
    {
        Vector3 cell = GetCell(position, tolerance);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!grid.TryGetValue(cell + new Vector3(dx, dy, dz), out List<Vector3>? bucket))
                        continue;

                    foreach (Vector3 candidate in bucket)
                    {
                        if (Vector3.DistanceSquared(candidate, position) <= tolerance * tolerance)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private static Vector3 GetCell(Vector3 position, float tolerance)
    {
        Vector3 scaled = position / tolerance;
        return new Vector3(MathF.Floor(scaled.X), MathF.Floor(scaled.Y), MathF.Floor(scaled.Z));
    }
}
