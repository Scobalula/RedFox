using System.Text.Json;

namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Serializes a <see cref="GltfDocument"/> to UTF-8 JSON bytes using
/// <see cref="Utf8JsonWriter"/> for high-performance, allocation-efficient output.
/// </summary>
public static class GltfJsonWriter
{
    /// <summary>
    /// Serializes the specified <see cref="GltfDocument"/> to a UTF-8 byte array.
    /// </summary>
    /// <param name="doc">The document to serialize.</param>
    /// <returns>The JSON representation as a UTF-8 byte array.</returns>
    public static byte[] Write(GltfDocument doc)
    {
        using MemoryStream ms = new();
        using (Utf8JsonWriter w = new(ms))
        {
            w.WriteStartObject();

            WriteAsset(w);

            if (doc.Scene >= 0)
                w.WriteNumber("scene", doc.Scene);

            if (doc.Scenes.Count > 0)
                WriteScenes(w, doc);
            if (doc.Nodes.Count > 0)
                WriteNodes(w, doc);
            if (doc.Meshes.Count > 0)
                WriteMeshes(w, doc);
            if (doc.Accessors.Count > 0)
                WriteAccessors(w, doc);
            if (doc.BufferViews.Count > 0)
                WriteBufferViews(w, doc);
            if (doc.Buffers.Count > 0)
                WriteBuffers(w, doc);
            if (doc.Materials.Count > 0)
                WriteMaterials(w, doc);
            if (doc.Textures.Count > 0)
                WriteTextures(w, doc);
            if (doc.Images.Count > 0)
                WriteImages(w, doc);
            if (doc.Samplers.Count > 0)
                WriteSamplers(w, doc);
            if (doc.Skins.Count > 0)
                WriteSkins(w, doc);
            if (doc.Animations.Count > 0)
                WriteAnimations(w, doc);
            if (doc.Cameras.Count > 0)
                WriteCameras(w, doc);

            w.WriteEndObject();
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Writes the glTF asset metadata block to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    public static void WriteAsset(Utf8JsonWriter w)
    {
        w.WriteStartObject("asset");
        w.WriteString("version", "2.0");
        w.WriteString("generator", "RedFox");
        w.WriteEndObject();
    }

    /// <summary>
    /// Writes the scenes array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing scenes to serialize.</param>
    public static void WriteScenes(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("scenes");
        foreach (GltfScene scene in doc.Scenes)
        {
            w.WriteStartObject();
            if (scene.Name is not null)
                w.WriteString("name", scene.Name);
            if (scene.Nodes.Length > 0)
                WriteIntArray(w, "nodes", scene.Nodes);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the nodes array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing nodes to serialize.</param>
    public static void WriteNodes(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("nodes");
        foreach (GltfNode node in doc.Nodes)
        {
            w.WriteStartObject();
            if (node.Name is not null)
                w.WriteString("name", node.Name);
            if (node.Children is { Length: > 0 })
                WriteIntArray(w, "children", node.Children);
            if (node.Mesh >= 0)
                w.WriteNumber("mesh", node.Mesh);
            if (node.Skin >= 0)
                w.WriteNumber("skin", node.Skin);
            if (node.Camera >= 0)
                w.WriteNumber("camera", node.Camera);
            if (node.Translation is not null)
                WriteFloatArray(w, "translation", node.Translation);
            if (node.Rotation is not null)
                WriteFloatArray(w, "rotation", node.Rotation);
            if (node.Scale is not null)
                WriteFloatArray(w, "scale", node.Scale);
            if (node.Matrix is not null)
                WriteFloatArray(w, "matrix", node.Matrix);
            if (node.Weights is not null)
                WriteFloatArray(w, "weights", node.Weights);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the meshes array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing meshes to serialize.</param>
    public static void WriteMeshes(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("meshes");
        foreach (GltfMesh mesh in doc.Meshes)
        {
            w.WriteStartObject();
            if (mesh.Name is not null)
                w.WriteString("name", mesh.Name);

            w.WriteStartArray("primitives");
            foreach (GltfMeshPrimitive prim in mesh.Primitives)
            {
                w.WriteStartObject();

                w.WriteStartObject("attributes");
                foreach (KeyValuePair<string, int> attr in prim.Attributes)
                    w.WriteNumber(attr.Key, attr.Value);
                w.WriteEndObject();

                if (prim.Indices >= 0)
                    w.WriteNumber("indices", prim.Indices);
                if (prim.Material >= 0)
                    w.WriteNumber("material", prim.Material);
                if (prim.Mode != GltfConstants.ModeTriangles)
                    w.WriteNumber("mode", prim.Mode);

                if (prim.Targets is { Count: > 0 })
                {
                    w.WriteStartArray("targets");
                    foreach (Dictionary<string, int> target in prim.Targets)
                    {
                        w.WriteStartObject();
                        foreach (KeyValuePair<string, int> tp in target)
                            w.WriteNumber(tp.Key, tp.Value);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }

                w.WriteEndObject();
            }
            w.WriteEndArray();

            if (mesh.Weights is { Length: > 0 })
                WriteFloatArray(w, "weights", mesh.Weights);

            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the accessors array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing accessors to serialize.</param>
    public static void WriteAccessors(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("accessors");
        foreach (GltfAccessor acc in doc.Accessors)
        {
            w.WriteStartObject();
            if (acc.BufferView >= 0)
                w.WriteNumber("bufferView", acc.BufferView);
            if (acc.ByteOffset > 0)
                w.WriteNumber("byteOffset", acc.ByteOffset);
            w.WriteNumber("componentType", acc.ComponentType);
            if (acc.Normalized)
                w.WriteBoolean("normalized", true);
            w.WriteNumber("count", acc.Count);
            w.WriteString("type", acc.Type);
            if (acc.Min is not null)
                WriteFloatArray(w, "min", acc.Min);
            if (acc.Max is not null)
                WriteFloatArray(w, "max", acc.Max);
            if (acc.Name is not null)
                w.WriteString("name", acc.Name);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the bufferViews array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing buffer views to serialize.</param>
    public static void WriteBufferViews(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("bufferViews");
        foreach (GltfBufferView bv in doc.BufferViews)
        {
            w.WriteStartObject();
            w.WriteNumber("buffer", bv.Buffer);
            if (bv.ByteOffset > 0)
                w.WriteNumber("byteOffset", bv.ByteOffset);
            w.WriteNumber("byteLength", bv.ByteLength);
            if (bv.ByteStride > 0)
                w.WriteNumber("byteStride", bv.ByteStride);
            if (bv.Target > 0)
                w.WriteNumber("target", bv.Target);
            if (bv.Name is not null)
                w.WriteString("name", bv.Name);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the buffers array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing buffers to serialize.</param>
    public static void WriteBuffers(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("buffers");
        foreach (GltfBuffer buf in doc.Buffers)
        {
            w.WriteStartObject();
            w.WriteNumber("byteLength", buf.ByteLength);
            if (buf.Uri is not null)
                w.WriteString("uri", buf.Uri);
            if (buf.Name is not null)
                w.WriteString("name", buf.Name);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the materials array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing materials to serialize.</param>
    public static void WriteMaterials(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("materials");
        foreach (GltfMaterial mat in doc.Materials)
        {
            w.WriteStartObject();
            if (mat.Name is not null)
                w.WriteString("name", mat.Name);

            w.WriteStartObject("pbrMetallicRoughness");
            WriteFloatArray(w, "baseColorFactor", mat.BaseColorFactor);
            w.WriteNumber("metallicFactor", mat.MetallicFactor);
            w.WriteNumber("roughnessFactor", mat.RoughnessFactor);

            if (mat.BaseColorTextureIndex >= 0)
            {
                w.WriteStartObject("baseColorTexture");
                w.WriteNumber("index", mat.BaseColorTextureIndex);
                if (mat.BaseColorTextureTexCoord > 0)
                    w.WriteNumber("texCoord", mat.BaseColorTextureTexCoord);
                w.WriteEndObject();
            }

            if (mat.MetallicRoughnessTextureIndex >= 0)
            {
                w.WriteStartObject("metallicRoughnessTexture");
                w.WriteNumber("index", mat.MetallicRoughnessTextureIndex);
                w.WriteEndObject();
            }

            w.WriteEndObject(); // pbrMetallicRoughness

            if (mat.NormalTextureIndex >= 0)
            {
                w.WriteStartObject("normalTexture");
                w.WriteNumber("index", mat.NormalTextureIndex);
                if (Math.Abs(mat.NormalTextureScale - 1f) > 1e-6f)
                    w.WriteNumber("scale", mat.NormalTextureScale);
                w.WriteEndObject();
            }

            if (mat.OcclusionTextureIndex >= 0)
            {
                w.WriteStartObject("occlusionTexture");
                w.WriteNumber("index", mat.OcclusionTextureIndex);
                if (Math.Abs(mat.OcclusionTextureStrength - 1f) > 1e-6f)
                    w.WriteNumber("strength", mat.OcclusionTextureStrength);
                w.WriteEndObject();
            }

            if (mat.EmissiveFactor[0] != 0f || mat.EmissiveFactor[1] != 0f || mat.EmissiveFactor[2] != 0f)
                WriteFloatArray(w, "emissiveFactor", mat.EmissiveFactor);

            if (mat.EmissiveTextureIndex >= 0)
            {
                w.WriteStartObject("emissiveTexture");
                w.WriteNumber("index", mat.EmissiveTextureIndex);
                w.WriteEndObject();
            }

            if (mat.AlphaMode != "OPAQUE")
                w.WriteString("alphaMode", mat.AlphaMode);
            if (mat.AlphaMode == "MASK" && Math.Abs(mat.AlphaCutoff - 0.5f) > 1e-6f)
                w.WriteNumber("alphaCutoff", mat.AlphaCutoff);
            if (mat.DoubleSided)
                w.WriteBoolean("doubleSided", true);

            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the textures array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing textures to serialize.</param>
    public static void WriteTextures(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("textures");
        foreach (GltfTexture tex in doc.Textures)
        {
            w.WriteStartObject();
            if (tex.Source >= 0)
                w.WriteNumber("source", tex.Source);
            if (tex.Sampler >= 0)
                w.WriteNumber("sampler", tex.Sampler);
            if (tex.Name is not null)
                w.WriteString("name", tex.Name);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the images array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing images to serialize.</param>
    public static void WriteImages(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("images");
        foreach (GltfImage img in doc.Images)
        {
            w.WriteStartObject();
            if (img.Uri is not null)
                w.WriteString("uri", img.Uri);
            if (img.MimeType is not null)
                w.WriteString("mimeType", img.MimeType);
            if (img.BufferView >= 0)
                w.WriteNumber("bufferView", img.BufferView);
            if (img.Name is not null)
                w.WriteString("name", img.Name);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the samplers array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing samplers to serialize.</param>
    public static void WriteSamplers(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("samplers");
        foreach (GltfSampler sampler in doc.Samplers)
        {
            w.WriteStartObject();
            if (sampler.MagFilter > 0)
                w.WriteNumber("magFilter", sampler.MagFilter);
            if (sampler.MinFilter > 0)
                w.WriteNumber("minFilter", sampler.MinFilter);
            w.WriteNumber("wrapS", sampler.WrapS);
            w.WriteNumber("wrapT", sampler.WrapT);
            if (sampler.Name is not null)
                w.WriteString("name", sampler.Name);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the skins array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing skins to serialize.</param>
    public static void WriteSkins(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("skins");
        foreach (GltfSkin skin in doc.Skins)
        {
            w.WriteStartObject();
            if (skin.Name is not null)
                w.WriteString("name", skin.Name);
            if (skin.InverseBindMatrices >= 0)
                w.WriteNumber("inverseBindMatrices", skin.InverseBindMatrices);
            if (skin.SkeletonRoot >= 0)
                w.WriteNumber("skeleton", skin.SkeletonRoot);
            WriteIntArray(w, "joints", skin.Joints);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the animations array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing animations to serialize.</param>
    public static void WriteAnimations(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("animations");
        foreach (GltfAnimation anim in doc.Animations)
        {
            w.WriteStartObject();
            if (anim.Name is not null)
                w.WriteString("name", anim.Name);

            w.WriteStartArray("channels");
            foreach (GltfAnimationChannel ch in anim.Channels)
            {
                w.WriteStartObject();
                w.WriteNumber("sampler", ch.Sampler);
                w.WriteStartObject("target");
                if (ch.TargetNode >= 0)
                    w.WriteNumber("node", ch.TargetNode);
                w.WriteString("path", ch.TargetPath);
                w.WriteEndObject();
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteStartArray("samplers");
            foreach (GltfAnimationSampler sp in anim.Samplers)
            {
                w.WriteStartObject();
                w.WriteNumber("input", sp.Input);
                w.WriteNumber("output", sp.Output);
                if (sp.Interpolation != GltfConstants.InterpolationLinear)
                    w.WriteString("interpolation", sp.Interpolation);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes the cameras array to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="doc">The document containing cameras to serialize.</param>
    public static void WriteCameras(Utf8JsonWriter w, GltfDocument doc)
    {
        w.WriteStartArray("cameras");
        foreach (GltfCamera cam in doc.Cameras)
        {
            w.WriteStartObject();
            if (cam.Name is not null)
                w.WriteString("name", cam.Name);
            w.WriteString("type", cam.Type);

            if (cam.Type == "perspective")
            {
                w.WriteStartObject("perspective");
                w.WriteNumber("yfov", cam.YFov);
                if (cam.AspectRatio > 0)
                    w.WriteNumber("aspectRatio", cam.AspectRatio);
                w.WriteNumber("znear", cam.ZNear);
                if (cam.ZFar > 0)
                    w.WriteNumber("zfar", cam.ZFar);
                w.WriteEndObject();
            }
            else if (cam.Type == "orthographic")
            {
                w.WriteStartObject("orthographic");
                w.WriteNumber("xmag", cam.XMag);
                w.WriteNumber("ymag", cam.YMag);
                w.WriteNumber("znear", cam.ZNear);
                w.WriteNumber("zfar", cam.ZFar);
                w.WriteEndObject();
            }

            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes a named float array property to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="name">The JSON property name.</param>
    /// <param name="values">The float values to write.</param>
    public static void WriteFloatArray(Utf8JsonWriter w, string name, float[] values)
    {
        w.WriteStartArray(name);
        foreach (float v in values)
            w.WriteNumberValue(v);
        w.WriteEndArray();
    }

    /// <summary>
    /// Writes a named integer array property to the JSON writer.
    /// </summary>
    /// <param name="w">The JSON writer to write to.</param>
    /// <param name="name">The JSON property name.</param>
    /// <param name="values">The integer values to write.</param>
    public static void WriteIntArray(Utf8JsonWriter w, string name, int[] values)
    {
        w.WriteStartArray(name);
        foreach (int v in values)
            w.WriteNumberValue(v);
        w.WriteEndArray();
    }
}
