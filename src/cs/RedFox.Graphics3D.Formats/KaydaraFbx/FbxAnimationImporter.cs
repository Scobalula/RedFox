using System.Numerics;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Imports FBX animation stacks and FBX 6 takes by baking every frame through the full FBX transform evaluation.
/// </summary>
public static class FbxAnimationImporter
{
    private const int	MaxFrames	= 1 << 16;
    private const long	MaxSamples	= 1L << 26;

    private static readonly string[] TransformProperties = ["Lcl Translation", "Lcl Rotation", "Lcl Scaling"];

    /// <summary>
    /// Imports every animation stack, or the FBX 6 takes when the document has no stacks.
    /// </summary>
    /// <param name="importer">The active scene importer.</param>
    public static void Import(FbxSceneImporter importer)
    {
        double	frameRate	= GetFrameRate(importer.Graph.Settings);
        bool	hasStacks	= false;
        long	budget		= MaxSamples;

        foreach (FbxObject stack in importer.Graph.Objects)
        {
            if (stack.ClassName != "AnimationStack")
                continue;

            List<FbxAnimationLayer> layers = [];

            foreach (FbxObject layer in importer.Graph.GetChildren(stack.Id, "AnimationLayer"))
                layers.Add(ReadLayer(importer, layer, layers.Count == 0));

            long start	= GetTime(stack, "LocalStart", GetTime(stack, "ReferenceStart", 0));
            long stop	= GetTime(stack, "LocalStop", GetTime(stack, "ReferenceStop", 0));

            hasStacks = true;
            ImportTake(importer, stack.Name, layers, start, stop, frameRate, ref budget);
        }

        if (hasStacks || importer.Graph.Document.FirstNode("Takes") is not FbxNode takes)
            return;

        foreach (FbxNode take in takes.ChildrenNamed("Take"))
        {
            string	name	= take.Properties.Count > 0 && take.Properties[0].Type == FbxPropertyType.String ? take.Properties[0].AsString() : "Take";
            long[]	range	= take.FirstChild("LocalTime")?.GetInt64s() ?? [];

            ImportTake(importer, name, [ReadLegacyTake(importer, take)], range.Length > 1 ? range[0] : 0, range.Length > 1 ? range[1] : 0, frameRate, ref budget);
        }
    }

    /// <summary>
    /// Returns the frame rate selected by the <c>TimeMode</c> and <c>CustomFrameRate</c> global settings.
    /// </summary>
    /// <param name="settings">The global settings object.</param>
    /// <returns>The frame rate in frames per second.</returns>
    public static double GetFrameRate(FbxObject settings)
    {
        return settings.GetInt32("TimeMode", 0) switch
        {
            1	=> 120.0,
            2	=> 100.0,
            3	=> 60.0,
            4	=> 50.0,
            5	=> 48.0,
            8	or 9 => 30000.0 / 1001.0,
            10	=> 25.0,
            11	=> 24.0,
            12	=> 1000.0,
            13	=> 24000.0 / 1001.0,
            14	=> settings.GetDouble("CustomFrameRate", 24.0) is > 0.0 and var custom ? custom : 24.0,
            15	=> 96.0,
            16	=> 72.0,
            17	=> 60000.0 / 1001.0,
            18	=> 120000.0 / 1001.0,
            _	=> 30.0,
        };
    }

    private static long GetTime(FbxObject fbxObject, string name, long fallback)
    {
        return fbxObject.TryGetValue(name, 0, out FbxProperty value) && value.IsNumber ? value.AsInt64() : fallback;
    }

    private static FbxAnimationLayer ReadLayer(FbxSceneImporter importer, FbxObject layerObject, bool isBase)
    {
        FbxAnimationLayer layer = new(layerObject.GetSingle("Weight", 100.0f) / 100.0f, !isBase && layerObject.GetInt32("BlendMode", 1) == 0);

        foreach (FbxObject curveNode in importer.Graph.GetChildren(layerObject.Id, "AnimationCurveNode"))
        {
            FbxAnimationCurve?[] curves = new FbxAnimationCurve?[3];

            foreach (FbxConnection connection in importer.Graph.GetChildConnections(curveNode.Id))
            {
                if (importer.Graph.GetObject(connection.ChildId) is { ClassName: "AnimationCurve" } curve && GetComponent(connection.PropertyName) is int component)
                    curves[component] = FbxAnimationCurve.Read(curve);
            }

            foreach (FbxConnection connection in importer.Graph.GetParentConnections(curveNode.Id))
            {
                if (connection.ConnectionType != "OP" || importer.Graph.GetObject(connection.ParentId) is not FbxObject target)
                    continue;

                Vector3				fallback	= target.GetVector3(connection.PropertyName, new Vector3(target.GetSingle(connection.PropertyName, 0.0f)));
                Vector3				defaults	= new(curveNode.GetSingle("d|X", curveNode.GetSingle($"d|{connection.PropertyName}", fallback.X)), curveNode.GetSingle("d|Y", fallback.Y), curveNode.GetSingle("d|Z", fallback.Z));
                FbxAnimationChannel	channel		= layer.GetOrCreate(target.Id, connection.PropertyName, defaults);

                for (int i = 0; i < curves.Length; i++)
                    channel.Curves[i] = curves[i] ?? channel.Curves[i];
            }
        }

        return layer;
    }

    private static int? GetComponent(string propertyName)
    {
        return propertyName switch
        {
            "d|X" => 0,
            "d|Y" => 1,
            "d|Z" => 2,
            _ when propertyName.StartsWith("d|", StringComparison.Ordinal) => 0,
            _ => null,
        };
    }

    private static FbxAnimationLayer ReadLegacyTake(FbxSceneImporter importer, FbxNode take)
    {
        FbxAnimationLayer layer = new(1.0f, false);

        foreach (FbxNode model in take.ChildrenNamed("Model"))
        {
            if (model.Properties.Count == 0 || model.Properties[0].Type != FbxPropertyType.String || !importer.Graph.TryGetIdByName(model.Properties[0].AsString(), out long modelId) || importer.GetNode(modelId) is null)
                continue;

            FbxNode? transform = model.ChildrenNamed("Channel").FirstOrDefault(static channel => channel.Properties.Count > 0 && channel.Properties[0].AsString() == "Transform");

            if (transform is null)
                continue;

            FbxNodeTransform staticTransform = importer.GetTransform(modelId);

            foreach (FbxNode property in transform.ChildrenNamed("Channel"))
            {
                string name = property.Properties.Count == 0 ? string.Empty : property.Properties[0].AsString();

                FbxAnimationChannel? channel = name switch
                {
                    "T"	=> layer.GetOrCreate(modelId, "Lcl Translation", staticTransform.Translation),
                    "R"	=> layer.GetOrCreate(modelId, "Lcl Rotation", staticTransform.Rotation),
                    "S"	=> layer.GetOrCreate(modelId, "Lcl Scaling", staticTransform.Scaling),
                    _	=> null,
                };

                if (channel is null)
                    continue;

                foreach (FbxNode component in property.ChildrenNamed("Channel"))
                {
                    int index = component.Properties.Count == 0 ? -1 : component.Properties[0].AsString() switch
                    {
                        "X"	=> 0,
                        "Y"	=> 1,
                        "Z"	=> 2,
                        _	=> -1,
                    };

                    if (index < 0)
                        continue;

                    if (component.TryGetChildProperty("Default", out FbxProperty value) && value.IsNumber)
                    {
                        Vector3 defaults = channel.Default;
                        defaults[index] = value.AsSingle();
                        channel.Default = defaults;
                    }

                    if (component.FirstChild("Key") is FbxNode keys)
                        channel.Curves[index] = FbxAnimationCurve.ReadLegacy(keys);
                }
            }
        }

        return layer;
    }

    private static void ImportTake(FbxSceneImporter importer, string name, List<FbxAnimationLayer> layers, long start, long stop, double frameRate, ref long budget)
    {
        HashSet<long>	animated		= [];
        List<long>		morphChannels	= [];
        long			firstKey		= long.MaxValue;
        long			lastKey			= long.MinValue;

        foreach (FbxAnimationLayer layer in layers)
        {
            foreach ((long id, Dictionary<string, FbxAnimationChannel> properties) in layer.Channels)
            {
                if (importer.GetNode(id) is not null && TransformProperties.Any(properties.ContainsKey))
                    animated.Add(id);
                else if (properties.ContainsKey("DeformPercent") && !morphChannels.Contains(id))
                    morphChannels.Add(id);

                foreach (FbxAnimationChannel channel in properties.Values)
                {
                    foreach (FbxAnimationCurve? curve in channel.Curves)
                    {
                        if (curve is null)
                            continue;

                        firstKey	= Math.Min(firstKey, curve.FirstTime);
                        lastKey		= Math.Max(lastKey, curve.LastTime);
                    }
                }
            }
        }

        if (animated.Count == 0 && morphChannels.Count == 0)
            return;

        if (stop <= start && lastKey >= firstKey)
        {
            start	= firstKey;
            stop	= lastKey;
        }

        double	ticksPerFrame	= FbxAnimationCurve.TicksPerSecond / frameRate;
        long	firstFrame		= (long)Math.Round(start / ticksPerFrame);
        long	lastFrame		= Math.Max(firstFrame, Math.Min((long)Math.Round(stop / ticksPerFrame), firstFrame + MaxFrames));

        if (animated.Count > 0)
            ImportTransforms(importer, name, layers, animated, firstFrame, lastFrame, ticksPerFrame, (float)frameRate, ref budget);

        if (morphChannels.Count > 0)
            Consume(ref budget, (lastFrame - firstFrame + 1) * morphChannels.Count);

        if (morphChannels.Count > 0)
            ImportMorphs(importer, name, layers, morphChannels, firstFrame, lastFrame, ticksPerFrame, (float)frameRate);
    }

    private static void ImportTransforms(FbxSceneImporter importer, string name, List<FbxAnimationLayer> layers, HashSet<long> animated, long firstFrame, long lastFrame, double ticksPerFrame, float frameRate, ref long budget)
    {
        List<long>									evaluated	= [];
        HashSet<long>								dynamic		= [];
        Dictionary<long, SkeletonAnimationTrack>	tracks		= [];
        Dictionary<long, Matrix4x4>					worlds		= [];
        Dictionary<long, Vector3>					scales		= [];
        Dictionary<long, Quaternion>				rotations	= [];

        foreach (long id in importer.ModelOrder)
        {
            long parentId	= importer.GetParentId(id);
            bool isDynamic	= animated.Contains(id) || dynamic.Contains(parentId);

            if (!isDynamic)
                continue;

            dynamic.Add(id);
            evaluated.Add(id);

            if (animated.Contains(id) || importer.GetTransform(id).InheritType != FbxInheritType.RSrs)
            {
                tracks[id] = new SkeletonAnimationTrack(importer.GetNode(id)!.Name)
                {
                    TransformSpace	= TransformSpace.Local,
                    TransformType	= TransformType.Absolute,
                };
            }
        }

        Consume(ref budget, (lastFrame - firstFrame + 1) * evaluated.Count);

        for (long frame = firstFrame; frame <= lastFrame; frame++)
        {
            long time = (long)Math.Round(frame * ticksPerFrame);

            foreach (long id in evaluated)
            {
                FbxNodeTransform	transform	= importer.GetTransform(id);
                long				parentId	= importer.GetParentId(id);
                Vector3				translation	= Evaluate(layers, id, "Lcl Translation", transform.Translation, time);
                Vector3				rotation	= Evaluate(layers, id, "Lcl Rotation", transform.Rotation, time);
                Vector3				scaling		= Evaluate(layers, id, "Lcl Scaling", transform.Scaling, time);
                Matrix4x4			parentWorld	= worlds.TryGetValue(parentId, out Matrix4x4 world) ? world : importer.GetWorldMatrix(parentId);
                Vector3				parentScale	= scales.TryGetValue(parentId, out Vector3 scale) ? scale : parentId == FbxObjectGraph.RootId ? Vector3.One : importer.GetTransform(parentId).Scaling;
                Matrix4x4			local		= importer.ResolveLocalMatrix(id, transform.GetLocalMatrix(translation, rotation, scaling), parentWorld, parentScale, out world);

                worlds[id] = world;
                scales[id] = scaling;

                if (!tracks.TryGetValue(id, out SkeletonAnimationTrack? track))
                    continue;

                FbxTransform.Decompose(local, out Vector3 localTranslation, out Quaternion localRotation, out Vector3 localScale);

                if (rotations.TryGetValue(id, out Quaternion previous) && Quaternion.Dot(previous, localRotation) < 0.0f)
                    localRotation = -localRotation;

                rotations[id] = localRotation;

                track.AddTranslationFrame(frame, localTranslation);
                track.AddRotationFrame(frame, localRotation);
                track.AddScaleFrame(frame, localScale);
            }
        }

        SkeletonAnimation animation = new(importer.GetUniqueName(name))
        {
            Framerate		= frameRate,
            TransformType	= TransformType.Absolute,
            TransformSpace	= TransformSpace.Local,
        };

        animation.Tracks.AddRange(tracks.Values);
        animation.SetParentUnsafe(importer.Scene);
    }

    private static void ImportMorphs(FbxSceneImporter importer, string name, List<FbxAnimationLayer> layers, List<long> channels, long firstFrame, long lastFrame, double ticksPerFrame, float frameRate)
    {
        MorphAnimation animation = new(importer.GetUniqueName($"{name}_Morph"))
        {
            Framerate = frameRate,
        };

        foreach (long id in channels)
        {
            if (importer.Graph.GetObject(id) is not FbxObject channel)
                continue;

            MorphAnimationTrack	track	= animation.GetOrCreateTrack(channel.Name);
            float				weight	= channel.GetSingle("DeformPercent", 0.0f);

            for (long frame = firstFrame; frame <= lastFrame; frame++)
                track.AddWeightFrame(frame, Evaluate(layers, id, "DeformPercent", new Vector3(weight), (long)Math.Round(frame * ticksPerFrame)).X / 100.0f);
        }

        animation.SetParentUnsafe(importer.Scene);
    }

    private static void Consume(ref long budget, long samples)
    {
        budget -= samples;

        if (budget < 0)
            throw new InvalidDataException("FBX animation exceeds the supported number of sampled frames.");
    }

    private static Vector3 Evaluate(List<FbxAnimationLayer> layers, long id, string property, Vector3 value, long time)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            FbxAnimationLayer layer = layers[i];

            if (layer.Get(id, property) is not FbxAnimationChannel channel)
                continue;

            Vector3 sample = channel.Evaluate(time);

            if (!layer.IsAdditive)
                value = Vector3.Lerp(value, sample, layer.Weight);
            else if (property == "Lcl Scaling")
                value *= Vector3.Lerp(Vector3.One, sample, layer.Weight);
            else
                value += sample * layer.Weight;
        }

        return value;
    }
}
