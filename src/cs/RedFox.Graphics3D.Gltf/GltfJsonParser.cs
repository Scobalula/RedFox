using System.Text.Json;

namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Parses a glTF 2.0 JSON document into a <see cref="GltfDocument"/> using
/// <see cref="System.Text.Json.JsonDocument"/> for zero-allocation DOM traversal.
/// </summary>
public static class GltfJsonParser
{
    /// <summary>
    /// Parses the specified JSON byte span into a <see cref="GltfDocument"/>.
    /// </summary>
    /// <param name="json">The UTF-8 encoded JSON bytes.</param>
    /// <returns>A fully populated <see cref="GltfDocument"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the JSON is malformed or missing required properties.</exception>
    public static GltfDocument Parse(ReadOnlyMemory<byte> json)
    {
        using JsonDocument jsonDoc = JsonDocument.Parse(json);
        JsonElement root = jsonDoc.RootElement;
        GltfDocument doc = new();

        if (root.TryGetProperty("scene", out JsonElement sceneProp))
            doc.Scene = sceneProp.GetInt32();

        if (root.TryGetProperty("scenes", out JsonElement scenes))
            ParseScenes(scenes, doc);

        if (root.TryGetProperty("nodes", out JsonElement nodes))
            ParseNodes(nodes, doc);

        if (root.TryGetProperty("meshes", out JsonElement meshes))
            ParseMeshes(meshes, doc);

        if (root.TryGetProperty("accessors", out JsonElement accessors))
            ParseAccessors(accessors, doc);

        if (root.TryGetProperty("bufferViews", out JsonElement bufferViews))
            ParseBufferViews(bufferViews, doc);

        if (root.TryGetProperty("buffers", out JsonElement buffers))
            ParseBuffers(buffers, doc);

        if (root.TryGetProperty("materials", out JsonElement materials))
            ParseMaterials(materials, doc);

        if (root.TryGetProperty("textures", out JsonElement textures))
            ParseTextures(textures, doc);

        if (root.TryGetProperty("images", out JsonElement images))
            ParseImages(images, doc);

        if (root.TryGetProperty("samplers", out JsonElement samplers))
            ParseSamplers(samplers, doc);

        if (root.TryGetProperty("skins", out JsonElement skins))
            ParseSkins(skins, doc);

        if (root.TryGetProperty("animations", out JsonElement animations))
            ParseAnimations(animations, doc);

        if (root.TryGetProperty("cameras", out JsonElement cameras))
            ParseCameras(cameras, doc);

        return doc;
    }

    /// <summary>
    /// Parses the "scenes" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing scene definitions.</param>
    /// <param name="doc">The document to populate with parsed scenes.</param>
    public static void ParseScenes(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfScene scene = new();
            if (el.TryGetProperty("name", out JsonElement name))
                scene.Name = name.GetString();
            if (el.TryGetProperty("nodes", out JsonElement nodes))
                scene.Nodes = ReadIntArray(nodes);
            doc.Scenes.Add(scene);
        }
    }

    /// <summary>
    /// Parses the "nodes" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing node definitions.</param>
    /// <param name="doc">The document to populate with parsed nodes.</param>
    public static void ParseNodes(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfNode node = new();
            if (el.TryGetProperty("name", out JsonElement name))
                node.Name = name.GetString();
            if (el.TryGetProperty("children", out JsonElement children))
                node.Children = ReadIntArray(children);
            if (el.TryGetProperty("mesh", out JsonElement mesh))
                node.Mesh = mesh.GetInt32();
            if (el.TryGetProperty("skin", out JsonElement skin))
                node.Skin = skin.GetInt32();
            if (el.TryGetProperty("camera", out JsonElement camera))
                node.Camera = camera.GetInt32();
            if (el.TryGetProperty("translation", out JsonElement trans))
                node.Translation = ReadFloatArray(trans);
            if (el.TryGetProperty("rotation", out JsonElement rot))
                node.Rotation = ReadFloatArray(rot);
            if (el.TryGetProperty("scale", out JsonElement scale))
                node.Scale = ReadFloatArray(scale);
            if (el.TryGetProperty("matrix", out JsonElement matrix))
                node.Matrix = ReadFloatArray(matrix);
            if (el.TryGetProperty("weights", out JsonElement weights))
                node.Weights = ReadFloatArray(weights);
            doc.Nodes.Add(node);
        }
    }

    /// <summary>
    /// Parses the "meshes" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing mesh definitions.</param>
    /// <param name="doc">The document to populate with parsed meshes.</param>
    public static void ParseMeshes(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfMesh mesh = new();
            if (el.TryGetProperty("name", out JsonElement name))
                mesh.Name = name.GetString();
            if (el.TryGetProperty("weights", out JsonElement weights))
                mesh.Weights = ReadFloatArray(weights);

            if (el.TryGetProperty("primitives", out JsonElement prims))
            {
                foreach (JsonElement primEl in prims.EnumerateArray())
                {
                    GltfMeshPrimitive prim = new();

                    if (primEl.TryGetProperty("attributes", out JsonElement attrs))
                    {
                        foreach (JsonProperty attr in attrs.EnumerateObject())
                            prim.Attributes[attr.Name] = attr.Value.GetInt32();
                    }

                    if (primEl.TryGetProperty("indices", out JsonElement indices))
                        prim.Indices = indices.GetInt32();
                    if (primEl.TryGetProperty("material", out JsonElement mat))
                        prim.Material = mat.GetInt32();
                    if (primEl.TryGetProperty("mode", out JsonElement mode))
                        prim.Mode = mode.GetInt32();

                    if (primEl.TryGetProperty("targets", out JsonElement targets))
                    {
                        prim.Targets = [];
                        foreach (JsonElement targetEl in targets.EnumerateArray())
                        {
                            Dictionary<string, int> target = [];
                            foreach (JsonProperty tp in targetEl.EnumerateObject())
                                target[tp.Name] = tp.Value.GetInt32();
                            prim.Targets.Add(target);
                        }
                    }

                    mesh.Primitives.Add(prim);
                }
            }

            doc.Meshes.Add(mesh);
        }
    }

    /// <summary>
    /// Parses the "accessors" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing accessor definitions.</param>
    /// <param name="doc">The document to populate with parsed accessors.</param>
    public static void ParseAccessors(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfAccessor acc = new();
            if (el.TryGetProperty("bufferView", out JsonElement bv))
                acc.BufferView = bv.GetInt32();
            if (el.TryGetProperty("byteOffset", out JsonElement bo))
                acc.ByteOffset = bo.GetInt32();
            if (el.TryGetProperty("componentType", out JsonElement ct))
                acc.ComponentType = ct.GetInt32();
            if (el.TryGetProperty("normalized", out JsonElement norm))
                acc.Normalized = norm.GetBoolean();
            if (el.TryGetProperty("count", out JsonElement count))
                acc.Count = count.GetInt32();
            if (el.TryGetProperty("type", out JsonElement type))
                acc.Type = type.GetString() ?? GltfConstants.TypeScalar;
            if (el.TryGetProperty("min", out JsonElement min))
                acc.Min = ReadFloatArray(min);
            if (el.TryGetProperty("max", out JsonElement max))
                acc.Max = ReadFloatArray(max);
            if (el.TryGetProperty("name", out JsonElement name))
                acc.Name = name.GetString();
            doc.Accessors.Add(acc);
        }
    }

    /// <summary>
    /// Parses the "bufferViews" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing buffer view definitions.</param>
    /// <param name="doc">The document to populate with parsed buffer views.</param>
    public static void ParseBufferViews(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfBufferView bv = new();
            if (el.TryGetProperty("buffer", out JsonElement buf))
                bv.Buffer = buf.GetInt32();
            if (el.TryGetProperty("byteOffset", out JsonElement bo))
                bv.ByteOffset = bo.GetInt32();
            if (el.TryGetProperty("byteLength", out JsonElement bl))
                bv.ByteLength = bl.GetInt32();
            if (el.TryGetProperty("byteStride", out JsonElement bs))
                bv.ByteStride = bs.GetInt32();
            if (el.TryGetProperty("target", out JsonElement target))
                bv.Target = target.GetInt32();
            if (el.TryGetProperty("name", out JsonElement name))
                bv.Name = name.GetString();
            doc.BufferViews.Add(bv);
        }
    }

    /// <summary>
    /// Parses the "buffers" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing buffer definitions.</param>
    /// <param name="doc">The document to populate with parsed buffers.</param>
    public static void ParseBuffers(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfBuffer buf = new();
            if (el.TryGetProperty("byteLength", out JsonElement bl))
                buf.ByteLength = bl.GetInt32();
            if (el.TryGetProperty("uri", out JsonElement uri))
                buf.Uri = uri.GetString();
            if (el.TryGetProperty("name", out JsonElement name))
                buf.Name = name.GetString();
            doc.Buffers.Add(buf);
        }
    }

    /// <summary>
    /// Parses the "materials" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing material definitions.</param>
    /// <param name="doc">The document to populate with parsed materials.</param>
    public static void ParseMaterials(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfMaterial mat = new();
            if (el.TryGetProperty("name", out JsonElement name))
                mat.Name = name.GetString();
            if (el.TryGetProperty("doubleSided", out JsonElement ds))
                mat.DoubleSided = ds.GetBoolean();
            if (el.TryGetProperty("alphaMode", out JsonElement am))
                mat.AlphaMode = am.GetString() ?? "OPAQUE";
            if (el.TryGetProperty("alphaCutoff", out JsonElement ac))
                mat.AlphaCutoff = ac.GetSingle();

            if (el.TryGetProperty("pbrMetallicRoughness", out JsonElement pbr))
            {
                if (pbr.TryGetProperty("baseColorFactor", out JsonElement bcf))
                    mat.BaseColorFactor = ReadFloatArray(bcf);
                if (pbr.TryGetProperty("metallicFactor", out JsonElement mf))
                    mat.MetallicFactor = mf.GetSingle();
                if (pbr.TryGetProperty("roughnessFactor", out JsonElement rf))
                    mat.RoughnessFactor = rf.GetSingle();

                if (pbr.TryGetProperty("baseColorTexture", out JsonElement bct))
                {
                    if (bct.TryGetProperty("index", out JsonElement idx))
                        mat.BaseColorTextureIndex = idx.GetInt32();
                    if (bct.TryGetProperty("texCoord", out JsonElement tc))
                        mat.BaseColorTextureTexCoord = tc.GetInt32();
                }

                if (pbr.TryGetProperty("metallicRoughnessTexture", out JsonElement mrt))
                {
                    if (mrt.TryGetProperty("index", out JsonElement idx))
                        mat.MetallicRoughnessTextureIndex = idx.GetInt32();
                }
            }

            if (el.TryGetProperty("normalTexture", out JsonElement nt))
            {
                if (nt.TryGetProperty("index", out JsonElement idx))
                    mat.NormalTextureIndex = idx.GetInt32();
                if (nt.TryGetProperty("scale", out JsonElement sc))
                    mat.NormalTextureScale = sc.GetSingle();
            }

            if (el.TryGetProperty("occlusionTexture", out JsonElement ot))
            {
                if (ot.TryGetProperty("index", out JsonElement idx))
                    mat.OcclusionTextureIndex = idx.GetInt32();
                if (ot.TryGetProperty("strength", out JsonElement st))
                    mat.OcclusionTextureStrength = st.GetSingle();
            }

            if (el.TryGetProperty("emissiveFactor", out JsonElement ef))
                mat.EmissiveFactor = ReadFloatArray(ef);

            if (el.TryGetProperty("emissiveTexture", out JsonElement et))
            {
                if (et.TryGetProperty("index", out JsonElement idx))
                    mat.EmissiveTextureIndex = idx.GetInt32();
            }

            doc.Materials.Add(mat);
        }
    }

    /// <summary>
    /// Parses the "textures" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing texture definitions.</param>
    /// <param name="doc">The document to populate with parsed textures.</param>
    public static void ParseTextures(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfTexture tex = new();
            if (el.TryGetProperty("name", out JsonElement name))
                tex.Name = name.GetString();
            if (el.TryGetProperty("source", out JsonElement source))
                tex.Source = source.GetInt32();
            if (el.TryGetProperty("sampler", out JsonElement sampler))
                tex.Sampler = sampler.GetInt32();
            doc.Textures.Add(tex);
        }
    }

    /// <summary>
    /// Parses the "images" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing image definitions.</param>
    /// <param name="doc">The document to populate with parsed images.</param>
    public static void ParseImages(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfImage img = new();
            if (el.TryGetProperty("name", out JsonElement name))
                img.Name = name.GetString();
            if (el.TryGetProperty("uri", out JsonElement uri))
                img.Uri = uri.GetString();
            if (el.TryGetProperty("mimeType", out JsonElement mime))
                img.MimeType = mime.GetString();
            if (el.TryGetProperty("bufferView", out JsonElement bv))
                img.BufferView = bv.GetInt32();
            doc.Images.Add(img);
        }
    }

    /// <summary>
    /// Parses the "samplers" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing sampler definitions.</param>
    /// <param name="doc">The document to populate with parsed samplers.</param>
    public static void ParseSamplers(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfSampler sampler = new();
            if (el.TryGetProperty("name", out JsonElement name))
                sampler.Name = name.GetString();
            if (el.TryGetProperty("magFilter", out JsonElement mag))
                sampler.MagFilter = mag.GetInt32();
            if (el.TryGetProperty("minFilter", out JsonElement min))
                sampler.MinFilter = min.GetInt32();
            if (el.TryGetProperty("wrapS", out JsonElement ws))
                sampler.WrapS = ws.GetInt32();
            if (el.TryGetProperty("wrapT", out JsonElement wt))
                sampler.WrapT = wt.GetInt32();
            doc.Samplers.Add(sampler);
        }
    }

    /// <summary>
    /// Parses the "skins" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing skin definitions.</param>
    /// <param name="doc">The document to populate with parsed skins.</param>
    public static void ParseSkins(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfSkin skin = new();
            if (el.TryGetProperty("name", out JsonElement name))
                skin.Name = name.GetString();
            if (el.TryGetProperty("inverseBindMatrices", out JsonElement ibm))
                skin.InverseBindMatrices = ibm.GetInt32();
            if (el.TryGetProperty("skeleton", out JsonElement skeleton))
                skin.SkeletonRoot = skeleton.GetInt32();
            if (el.TryGetProperty("joints", out JsonElement joints))
                skin.Joints = ReadIntArray(joints);
            doc.Skins.Add(skin);
        }
    }

    /// <summary>
    /// Parses the "animations" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing animation definitions.</param>
    /// <param name="doc">The document to populate with parsed animations.</param>
    public static void ParseAnimations(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfAnimation anim = new();
            if (el.TryGetProperty("name", out JsonElement name))
                anim.Name = name.GetString();

            if (el.TryGetProperty("channels", out JsonElement channels))
            {
                foreach (JsonElement ch in channels.EnumerateArray())
                {
                    GltfAnimationChannel channel = new();
                    if (ch.TryGetProperty("sampler", out JsonElement s))
                        channel.Sampler = s.GetInt32();
                    if (ch.TryGetProperty("target", out JsonElement target))
                    {
                        if (target.TryGetProperty("node", out JsonElement node))
                            channel.TargetNode = node.GetInt32();
                        if (target.TryGetProperty("path", out JsonElement path))
                            channel.TargetPath = path.GetString() ?? string.Empty;
                    }
                    anim.Channels.Add(channel);
                }
            }

            if (el.TryGetProperty("samplers", out JsonElement samplers))
            {
                foreach (JsonElement sp in samplers.EnumerateArray())
                {
                    GltfAnimationSampler sampler = new();
                    if (sp.TryGetProperty("input", out JsonElement input))
                        sampler.Input = input.GetInt32();
                    if (sp.TryGetProperty("output", out JsonElement output))
                        sampler.Output = output.GetInt32();
                    if (sp.TryGetProperty("interpolation", out JsonElement interp))
                        sampler.Interpolation = interp.GetString() ?? GltfConstants.InterpolationLinear;
                    anim.Samplers.Add(sampler);
                }
            }

            doc.Animations.Add(anim);
        }
    }

    /// <summary>
    /// Parses the "cameras" array from the glTF JSON into the document.
    /// </summary>
    /// <param name="array">The JSON array element containing camera definitions.</param>
    /// <param name="doc">The document to populate with parsed cameras.</param>
    public static void ParseCameras(JsonElement array, GltfDocument doc)
    {
        foreach (JsonElement el in array.EnumerateArray())
        {
            GltfCamera cam = new();
            if (el.TryGetProperty("name", out JsonElement name))
                cam.Name = name.GetString();
            if (el.TryGetProperty("type", out JsonElement type))
                cam.Type = type.GetString() ?? "perspective";

            if (el.TryGetProperty("perspective", out JsonElement persp))
            {
                if (persp.TryGetProperty("yfov", out JsonElement yf))
                    cam.YFov = yf.GetSingle();
                if (persp.TryGetProperty("aspectRatio", out JsonElement ar))
                    cam.AspectRatio = ar.GetSingle();
                if (persp.TryGetProperty("znear", out JsonElement zn))
                    cam.ZNear = zn.GetSingle();
                if (persp.TryGetProperty("zfar", out JsonElement zf))
                    cam.ZFar = zf.GetSingle();
            }

            if (el.TryGetProperty("orthographic", out JsonElement ortho))
            {
                if (ortho.TryGetProperty("xmag", out JsonElement xm))
                    cam.XMag = xm.GetSingle();
                if (ortho.TryGetProperty("ymag", out JsonElement ym))
                    cam.YMag = ym.GetSingle();
                if (ortho.TryGetProperty("znear", out JsonElement zn))
                    cam.ZNear = zn.GetSingle();
                if (ortho.TryGetProperty("zfar", out JsonElement zf))
                    cam.ZFar = zf.GetSingle();
            }

            doc.Cameras.Add(cam);
        }
    }

    /// <summary>
    /// Reads a JSON array of numbers into a float array.
    /// </summary>
    /// <param name="array">The JSON array element containing numeric values.</param>
    /// <returns>A float array with the parsed values.</returns>
    public static float[] ReadFloatArray(JsonElement array)
    {
        float[] result = new float[array.GetArrayLength()];
        int i = 0;
        foreach (JsonElement el in array.EnumerateArray())
            result[i++] = el.GetSingle();
        return result;
    }

    /// <summary>
    /// Reads a JSON array of integers into an int array.
    /// </summary>
    /// <param name="array">The JSON array element containing integer values.</param>
    /// <returns>An int array with the parsed values.</returns>
    public static int[] ReadIntArray(JsonElement array)
    {
        int[] result = new int[array.GetArrayLength()];
        int i = 0;
        foreach (JsonElement el in array.EnumerateArray())
            result[i++] = el.GetInt32();
        return result;
    }
}
