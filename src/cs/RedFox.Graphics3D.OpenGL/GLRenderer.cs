using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.OpenGL.Passes;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class GLRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly List<IRenderPass> _passes = [];
    private GLEquirectangularEnvironmentMap? _environmentMap;
    private GLEnvironmentResources? _environmentResources;

    private GLShader? _lineShader;
    private uint _boneLineVao;
    private uint _boneLineVbo;
    private bool _isInitialized;
    private int _maxTextureSize = 2048;

    public GLRenderer(GL gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
    }

    public GL GL => _gl;
    public Camera? ActiveCamera { get; set; }
    public bool ShowBones { get; set; } = true;
    public bool ShowWireframe { get; set; }
    public bool EnableBackFaceCulling { get; set; }
    public bool IsOpenGles { get; private set; }
    public RendererColor BackgroundColor { get; set; } = new(0.12f, 0.12f, 0.14f, 1.0f);
    public ImageTranslatorManager? ImageTranslatorManager { get; set; }
    public Matrix4x4 SceneTransform { get; set; } = Matrix4x4.Identity;
    public GLEquirectangularEnvironmentMap? EnvironmentMap
    {
        get => _environmentMap;
        set
        {
            if (ReferenceEquals(_environmentMap, value))
                return;

            _environmentMap?.Dispose();
            _environmentMap = value;
            SetEnvironmentResources(null);
        }
    }

    public GLEnvironmentResources? EnvironmentResources => _environmentResources;

    public EnvironmentMapFlipMode EnvironmentMapFlipMode { get; set; } = EnvironmentMapFlipMode.Auto;
    public float EnvironmentMapExposure { get; set; } = 1.0f;
    public float EnvironmentMapReflectionIntensity { get; set; } = 1.0f;
    public bool EnvironmentMapBlurEnabled { get; set; }
    public float EnvironmentMapBlurRadius { get; set; } = 4.0f;
    public bool EnableIBL { get; set; } = true;

    /// <summary>
    /// Gets the IBL precomputation pass if it has been added to the renderer.
    /// This pass generates the BRDF LUT and prefiltered environment map textures.
    /// </summary>
    public IblPrecomputePass? IblPrecomputePass => GetPass<IblPrecomputePass>();

    public IReadOnlyList<IRenderPass> Passes => _passes;
    public Matrix3x3 SceneNormalMatrix => ComputeNormalMatrix(SceneTransform);

    public Vector3 TransformPoint(Vector3 value) => Vector3.Transform(value, SceneTransform);

    public Vector3 TransformDirection(Vector3 value)
    {
        Vector3 transformed = Vector3.TransformNormal(value, SceneTransform);
        return transformed.LengthSquared() > 1e-12f
            ? Vector3.Normalize(transformed)
            : value;
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        IsOpenGles = ShaderSource.GetProfile(_gl) == ShaderProfile.OpenGles;
        _gl.GetInteger(GLEnum.MaxTextureSize, out _maxTextureSize);
        _maxTextureSize = Math.Max(_maxTextureSize, 1);

        _gl.Enable(EnableCap.DepthTest);
        if (!IsOpenGles)
            _gl.Enable(GLEnum.TextureCubeMapSeamless);
        _gl.FrontFace(FrontFaceDirection.Ccw);

        (string lineVertex, string lineFragment) = ShaderSource.LoadProgram(_gl, "line");
        _lineShader = new GLShader(_gl, lineVertex, lineFragment);
        _boneLineVao = _gl.GenVertexArray();
        _boneLineVbo = _gl.GenBuffer();

        if (_passes.Count == 0)
        {
            AddPass(new EnvironmentMapPass());
            AddPass(new GridPass());
            AddPass(new GeometryPass());
        }

        foreach (IRenderPass pass in _passes)
            pass.Initialize(this);

        _isInitialized = true;
    }

    public void AddPass(IRenderPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        _passes.Add(pass);

        if (_isInitialized)
            pass.Initialize(this);
    }

    public void InsertPass(int index, IRenderPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);

        if ((uint)index > (uint)_passes.Count)
            index = _passes.Count;

        _passes.Insert(index, pass);

        if (_isInitialized)
            pass.Initialize(this);
    }

    public bool RemovePass(string name)
    {
        for (int i = 0; i < _passes.Count; i++)
        {
            if (!_passes[i].Name.Equals(name, StringComparison.Ordinal))
                continue;

            _passes[i].Dispose();
            _passes.RemoveAt(i);
            return true;
        }

        return false;
    }

    public T? GetPass<T>() where T : class, IRenderPass => _passes.OfType<T>().FirstOrDefault();

    public void Render(Scene scene, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(scene);

        _gl.ClearColor(BackgroundColor.R, BackgroundColor.G, BackgroundColor.B, BackgroundColor.A);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (EnableBackFaceCulling)
        {
            _gl.Enable(EnableCap.CullFace);
            _gl.CullFace(GLEnum.Back);
        }
        else
        {
            _gl.Disable(EnableCap.CullFace);
        }

        if (ShowWireframe && !IsOpenGles)
            _gl.PolygonMode(GLEnum.FrontAndBack, PolygonMode.Line);

        foreach (IRenderPass pass in _passes)
        {
            if (pass.Enabled)
                pass.Render(this, scene, deltaTime);
        }

        if (ShowBones)
            RenderBones(scene);

        if (ShowWireframe && !IsOpenGles)
            _gl.PolygonMode(GLEnum.FrontAndBack, PolygonMode.Fill);
    }

    public GLMeshHandle? GetOrCreateMeshHandle(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        GLMeshHandle? handle = mesh.GraphicsHandle as GLMeshHandle;
        if (handle is not null)
            return handle;

        GLMeshHandle? uploadedHandle = UploadMesh(mesh);
        mesh.GraphicsHandle = uploadedHandle;
        return uploadedHandle;
    }

    public GLTextureHandle? GetOrCreateTextureHandle(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);

        GLTextureHandle? handle = texture.GraphicsHandle as GLTextureHandle;
        if (handle is not null)
            return handle;

        GLTextureHandle? uploadedHandle = UploadTexture(texture);
        texture.GraphicsHandle = uploadedHandle;
        return uploadedHandle;
    }

    public void UnloadMesh(Mesh mesh)
    {
        if (mesh.GraphicsHandle is GLMeshHandle handle)
        {
            handle.Delete(_gl);
            mesh.GraphicsHandle = null;
        }
    }

    public void UnloadTexture(Texture texture)
    {
        if (texture.GraphicsHandle is GLTextureHandle handle)
        {
            handle.Dispose();
            texture.GraphicsHandle = null;
        }
    }

    public void UpdateDynamicMeshData(Mesh mesh, GLMeshHandle handle)
    {
        if (!handle.HasMorphTargets || handle.MorphTargetCount == 0 || handle.BasePositions is null)
            return;

        float[] weights = ResolveMorphWeights(mesh, handle.MorphTargetCount);
        if (weights.All(static weight => MathF.Abs(weight) < 1e-6f))
        {
            UploadFloatBuffer(handle.PositionVBO, handle.BasePositions);

            if (handle.HasNormals && handle.BaseNormals is not null)
                UploadFloatBuffer(handle.NormalVBO, handle.BaseNormals);

            return;
        }

        float[] morphedPositions = new float[handle.BasePositions.Length];
        Array.Copy(handle.BasePositions, morphedPositions, morphedPositions.Length);
        ApplyMorphTargets(morphedPositions, handle.PositionMorphDeltas, handle.VertexCount, 3, weights);
        UploadFloatBuffer(handle.PositionVBO, morphedPositions);

        if (!handle.HasNormals || handle.BaseNormals is null)
            return;

        float[] morphedNormals = new float[handle.BaseNormals.Length];
        Array.Copy(handle.BaseNormals, morphedNormals, morphedNormals.Length);
        ApplyMorphTargets(morphedNormals, handle.NormalMorphDeltas, handle.VertexCount, 3, weights);
        NormalizeVectors(morphedNormals, 3);
        UploadFloatBuffer(handle.NormalVBO, morphedNormals);
    }

    public void UpdateSkinningData(Mesh mesh, GLMeshHandle handle)
    {
        if (!handle.HasSkinning || handle.BoneMatrixTexture == 0 || handle.BoneCount <= 0)
            return;

        Matrix4x4[] matrices = new Matrix4x4[handle.BoneCount];
        int count = mesh.CopySkinTransforms(matrices);
        if (count == 0)
            return;

        float[] textureData = new float[handle.BoneMatrixTextureWidth * handle.BoneMatrixTextureHeight * 4];
        for (int boneIndex = 0; boneIndex < count; boneIndex++)
            WriteMatrixTexels(matrices[boneIndex], textureData, boneIndex * 4);

        UploadFloatTexture(handle.BoneMatrixTexture, handle.BoneMatrixTextureWidth, handle.BoneMatrixTextureHeight, textureData);
    }

    private unsafe GLMeshHandle? UploadMesh(Mesh mesh)
    {
        if (mesh.Positions is null || mesh.VertexCount == 0)
            return null;

        uint vao = _gl.GenVertexArray();
        _gl.BindVertexArray(vao);

        float[] positions = ExtractVertexFloatBuffer(mesh.Positions, 0, 3);

        uint positionVbo = UploadFloatAttributeBuffer(positions, mesh.HasMorphTargets ? BufferUsageARB.DynamicDraw : BufferUsageARB.StaticDraw);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);

        float[]? normals = null;
        uint normalVbo = 0;
        if (mesh.Normals is not null)
        {
            normals = ExtractVertexFloatBuffer(mesh.Normals, 0, 3);
            normalVbo = UploadFloatAttributeBuffer(normals, mesh.HasMorphTargets ? BufferUsageARB.DynamicDraw : BufferUsageARB.StaticDraw);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 0, 0);
        }

        uint uvVbo = 0;
        if (mesh.UVLayers is not null)
        {
            uvVbo = UploadFloatAttributeBuffer(ExtractVertexFloatBuffer(mesh.UVLayers, 0, 2), BufferUsageARB.StaticDraw);
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 0, 0);
        }

        uint influenceRangeVbo = 0;
        uint influenceTexture = 0;
        uint boneMatrixTexture = 0;
        int boneCount = 0;
        int influenceTextureWidth = 1;
        int influenceTextureHeight = 1;
        int boneMatrixTextureWidth = 1;
        int boneMatrixTextureHeight = 1;
        bool hasSkinning = mesh.HasSkinning && mesh.SkinnedBones is not null;

        if (hasSkinning && mesh.BoneIndices is not null && mesh.BoneWeights is not null)
        {
            boneCount = mesh.SkinnedBones!.Count;
            (int[] influenceRanges, float[] influenceTextureData, int influenceEntryCount) =
                BuildSkinInfluenceTextureData(mesh.BoneIndices, mesh.BoneWeights, boneCount);

            influenceRangeVbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, influenceRangeVbo);
            fixed (int* ptr = influenceRanges)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(influenceRanges.Length * sizeof(int)), ptr, BufferUsageARB.StaticDraw);
            }

            _gl.EnableVertexAttribArray(3);
            _gl.VertexAttribIPointer(3, 2, VertexAttribIType.Int, 0, 0);

            (influenceTextureWidth, influenceTextureHeight) = ComputeTextureDimensions(Math.Max(influenceEntryCount, 1));
            influenceTexture = CreateFloatTexture(
                influenceTextureWidth,
                influenceTextureHeight,
                PadTextureData(influenceTextureData, influenceTextureWidth * influenceTextureHeight * 4));

            (boneMatrixTextureWidth, boneMatrixTextureHeight) = ComputeTextureDimensions(Math.Max(boneCount * 4, 1));
            boneMatrixTexture = CreateFloatTexture(
                boneMatrixTextureWidth,
                boneMatrixTextureHeight,
                new float[boneMatrixTextureWidth * boneMatrixTextureHeight * 4]);
        }

        uint elementBuffer = 0;
        int indexCount = 0;
        uint[]? indices = null;
        if (mesh.IsIndexed && mesh.FaceIndices is not null)
        {
            indices = ExtractIndexBuffer(mesh.FaceIndices);
            indexCount = indices.Length;
            elementBuffer = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, elementBuffer);
            fixed (uint* ptr = indices)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);
            }
        }

        bool frontFaceClockwise = DetermineFrontFaceClockwise(positions, normals, indices, mesh.VertexCount);

        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        return new GLMeshHandle(new GLMeshHandle.Descriptor
        {
            FrontFaceClockwise = frontFaceClockwise,
            VAO = vao,
            PositionVBO = positionVbo,
            NormalVBO = normalVbo,
            UVVBO = uvVbo,
            InfluenceRangeVBO = influenceRangeVbo,
            InfluenceTexture = influenceTexture,
            BoneMatrixTexture = boneMatrixTexture,
            EBO = elementBuffer,
            VertexCount = mesh.VertexCount,
            IndexCount = indexCount,
            IsIndexed = mesh.IsIndexed,
            HasNormals = mesh.Normals is not null,
            HasUVs = mesh.UVLayers is not null,
            HasSkinning = hasSkinning,
            BoneCount = boneCount,
            InfluenceTextureWidth = influenceTextureWidth,
            InfluenceTextureHeight = influenceTextureHeight,
            BoneMatrixTextureWidth = boneMatrixTextureWidth,
            BoneMatrixTextureHeight = boneMatrixTextureHeight,
            HasMorphTargets = mesh.HasMorphTargets,
            MorphTargetCount = mesh.MorphTargetCount,
            BasePositions = positions,
            BaseNormals = normals,
            PositionMorphDeltas = mesh.DeltaPositions is null ? null : ExtractMorphFloatBuffer(mesh.DeltaPositions, 3),
            NormalMorphDeltas = mesh.DeltaNormals is null ? null : ExtractMorphFloatBuffer(mesh.DeltaNormals, 3),
        });
    }

    private static bool DetermineFrontFaceClockwise(float[] positions, float[]? normals, uint[]? indices, int vertexCount)
    {
        const float epsilon = 1e-10f;

        if (normals is null || positions.Length < 9 || normals.Length < 9)
            return false;

        int triangleCount = indices is not null ? indices.Length / 3 : vertexCount / 3;
        if (triangleCount <= 0)
            return false;

        int positiveCount = 0;
        int negativeCount = 0;
        int sampleCount = Math.Min(triangleCount, 512);
        int triangleStep = Math.Max(triangleCount / sampleCount, 1);

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex += triangleStep)
        {
            int baseIndex = triangleIndex * 3;
            int i0 = indices is not null ? (int)indices[baseIndex] : baseIndex;
            int i1 = indices is not null ? (int)indices[baseIndex + 1] : baseIndex + 1;
            int i2 = indices is not null ? (int)indices[baseIndex + 2] : baseIndex + 2;

            Vector3 p0 = ReadVector3(positions, i0);
            Vector3 p1 = ReadVector3(positions, i1);
            Vector3 p2 = ReadVector3(positions, i2);

            Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
            if (faceNormal.LengthSquared() <= epsilon)
                continue;

            Vector3 avgNormal = ReadVector3(normals, i0) + ReadVector3(normals, i1) + ReadVector3(normals, i2);
            if (avgNormal.LengthSquared() <= epsilon)
                continue;

            float orientation = Vector3.Dot(faceNormal, avgNormal);
            if (orientation > epsilon)
                positiveCount++;
            else if (orientation < -epsilon)
                negativeCount++;
        }

        return negativeCount > positiveCount;
    }

    private static Vector3 ReadVector3(float[] values, int vertexIndex)
    {
        int offset = vertexIndex * 3;
        return new Vector3(values[offset], values[offset + 1], values[offset + 2]);
    }

    private unsafe GLTextureHandle? UploadTexture(Texture texture)
    {
        Image? image = texture.Data;

        if (image is null && texture.ImageLoader is not null)
            image = texture.ImageLoader.Load(texture.ResolvedFilePath, ImageTranslatorManager);

        string texturePath = texture.EffectiveFilePath;
        if (image is null && !string.IsNullOrWhiteSpace(texturePath) && File.Exists(texturePath))
            image = ImageTranslatorManager?.Read(texturePath);

        if (image is null)
            return null;

        if (ImageFormatInfo.IsBlockCompressed(image.Format) && GLTextureHandle.IsCompressedFormatSupported(image.Format))
            return new GLTextureHandle(_gl, image);

        return new GLTextureHandle(_gl, ConvertToRgba(image), image.Width, image.Height);
    }

    private unsafe uint UploadFloatAttributeBuffer(float[] data, BufferUsageARB usage)
    {
        uint buffer = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);

        fixed (float* ptr = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, usage);
        }

        return buffer;
    }

    private unsafe void UploadFloatBuffer(uint bufferId, float[] data)
    {
        if (bufferId == 0)
            return;

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, bufferId);
        fixed (float* ptr = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, BufferUsageARB.DynamicDraw);
        }
    }

    private unsafe uint CreateFloatTexture(int width, int height, float[] data)
    {
        uint texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        fixed (float* ptr = data)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba32f, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.Float, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return texture;
    }

    private unsafe void UploadFloatTexture(uint textureId, int width, int height, float[] data)
    {
        if (textureId == 0)
            return;

        _gl.BindTexture(TextureTarget.Texture2D, textureId);
        fixed (float* ptr = data)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.Float, ptr);
        }
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private (int Width, int Height) ComputeTextureDimensions(int texelCount)
    {
        int safeTexelCount = Math.Max(texelCount, 1);
        int width = Math.Min(_maxTextureSize, Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(safeTexelCount))));
        int height = (safeTexelCount + width - 1) / width;

        if (height > _maxTextureSize)
            throw new InvalidOperationException($"Texture data requires {height} rows, exceeding the maximum texture size {_maxTextureSize}.");

        return (width, height);
    }

    private static float[] PadTextureData(float[] source, int targetLength)
    {
        if (source.Length == targetLength)
            return source;

        float[] result = new float[targetLength];
        Array.Copy(source, result, source.Length);
        return result;
    }

    private static (int[] InfluenceRanges, float[] InfluenceTextureData, int InfluenceEntryCount) BuildSkinInfluenceTextureData(
        DataBuffer boneIndices,
        DataBuffer boneWeights,
        int boneCount)
    {
        int vertexCount = boneIndices.ElementCount;
        int influenceCount = Math.Min(boneIndices.ValueCount, boneWeights.ValueCount);
        int[] influenceRanges = new int[vertexCount * 2];
        List<float> influenceTextureData = [];

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            int startIndex = influenceTextureData.Count / 4;
            float totalWeight = 0.0f;
            List<(int BoneIndex, float Weight)> vertexInfluences = [];

            for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
            {
                float weight = boneWeights.Get<float>(vertexIndex, influenceIndex, 0);
                if (weight <= 1e-6f)
                    continue;

                int boneIndex = boneIndices.Get<int>(vertexIndex, influenceIndex, 0);
                if ((uint)boneIndex >= (uint)boneCount)
                    continue;

                vertexInfluences.Add((boneIndex, weight));
                totalWeight += weight;
            }

            influenceRanges[vertexIndex * 2] = startIndex;
            influenceRanges[(vertexIndex * 2) + 1] = vertexInfluences.Count;

            if (totalWeight <= 1e-6f)
                continue;

            float inverseTotalWeight = 1.0f / totalWeight;
            foreach ((int boneIndex, float weight) in vertexInfluences)
            {
                influenceTextureData.Add(boneIndex);
                influenceTextureData.Add(weight * inverseTotalWeight);
                influenceTextureData.Add(0.0f);
                influenceTextureData.Add(0.0f);
            }
        }

        return (influenceRanges, [.. influenceTextureData], influenceTextureData.Count / 4);
    }

    private static void WriteMatrixTexels(Matrix4x4 matrix, float[] destination, int texelBaseIndex)
    {
        WriteTexel(destination, texelBaseIndex + 0, matrix.M11, matrix.M12, matrix.M13, matrix.M14);
        WriteTexel(destination, texelBaseIndex + 1, matrix.M21, matrix.M22, matrix.M23, matrix.M24);
        WriteTexel(destination, texelBaseIndex + 2, matrix.M31, matrix.M32, matrix.M33, matrix.M34);
        WriteTexel(destination, texelBaseIndex + 3, matrix.M41, matrix.M42, matrix.M43, matrix.M44);
    }

    private static void WriteTexel(float[] destination, int texelIndex, float x, float y, float z, float w)
    {
        int baseIndex = texelIndex * 4;
        destination[baseIndex] = x;
        destination[baseIndex + 1] = y;
        destination[baseIndex + 2] = z;
        destination[baseIndex + 3] = w;
    }

    private static float[] ExtractVertexFloatBuffer(DataBuffer buffer, int valueIndex, int componentCount)
    {
        float[] result = new float[buffer.ElementCount * componentCount];

        for (int elementIndex = 0; elementIndex < buffer.ElementCount; elementIndex++)
        {
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                result[elementIndex * componentCount + componentIndex] = componentIndex < buffer.ComponentCount
                    ? buffer.Get<float>(elementIndex, Math.Min(valueIndex, buffer.ValueCount - 1), componentIndex)
                    : componentIndex == 3 ? 1.0f : 0.0f;
            }
        }

        return result;
    }

    private static float[] ExtractMorphFloatBuffer(DataBuffer buffer, int componentCount)
    {
        float[] result = new float[buffer.ElementCount * buffer.ValueCount * componentCount];

        for (int elementIndex = 0; elementIndex < buffer.ElementCount; elementIndex++)
        {
            for (int valueIndex = 0; valueIndex < buffer.ValueCount; valueIndex++)
            {
                int destinationBase = ((elementIndex * buffer.ValueCount) + valueIndex) * componentCount;
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    result[destinationBase + componentIndex] = componentIndex < buffer.ComponentCount
                        ? buffer.Get<float>(elementIndex, valueIndex, componentIndex)
                        : 0.0f;
                }
            }
        }

        return result;
    }

    private static uint[] ExtractIndexBuffer(DataBuffer buffer)
    {
        uint[] result = new uint[buffer.ElementCount];

        for (int elementIndex = 0; elementIndex < buffer.ElementCount; elementIndex++)
            result[elementIndex] = uint.CreateChecked(buffer.Get<int>(elementIndex, 0, 0));

        return result;
    }

    private static float[] ResolveMorphWeights(Mesh mesh, int morphTargetCount)
    {
        float[] result = new float[morphTargetCount];

        foreach (BlendShape blendShape in EnumerateBlendShapes(mesh))
        {
            if ((uint)blendShape.TargetIndex >= (uint)result.Length)
                continue;

            result[blendShape.TargetIndex] = blendShape.Weight;
        }

        return result;
    }

    private static IEnumerable<BlendShape> EnumerateBlendShapes(Mesh mesh)
    {
        foreach (BlendShape blendShape in mesh.EnumerateDescendants<BlendShape>())
            yield return blendShape;

        if (mesh.Scene is null)
            yield break;

        foreach (BlendShape blendShape in mesh.Scene.RootNode.EnumerateDescendants<BlendShape>())
        {
            if (blendShape.OwnerMesh == mesh && !blendShape.IsDescendantOf(mesh))
                yield return blendShape;
        }
    }

    private static void ApplyMorphTargets(float[] destination, float[]? deltas, int vertexCount, int componentCount, ReadOnlySpan<float> weights)
    {
        if (deltas is null)
            return;

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            int vertexBase = vertexIndex * componentCount;

            for (int targetIndex = 0; targetIndex < weights.Length; targetIndex++)
            {
                float weight = weights[targetIndex];
                if (MathF.Abs(weight) < 1e-6f)
                    continue;

                int deltaBase = ((vertexIndex * weights.Length) + targetIndex) * componentCount;
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    destination[vertexBase + componentIndex] += deltas[deltaBase + componentIndex] * weight;
            }
        }
    }

    private static void NormalizeVectors(float[] values, int componentCount)
    {
        for (int i = 0; i < values.Length; i += componentCount)
        {
            float lengthSquared = 0.0f;
            for (int c = 0; c < componentCount; c++)
                lengthSquared += values[i + c] * values[i + c];

            if (lengthSquared <= 1e-12f)
                continue;

            float inverseLength = 1.0f / MathF.Sqrt(lengthSquared);
            for (int c = 0; c < componentCount; c++)
                values[i + c] *= inverseLength;
        }
    }

    private static byte[] ConvertToRgba(Image image)
    {
        return image.DecodeSlice<byte>();
    }

    private unsafe void RenderBones(Scene scene)
    {
        if (_lineShader is null || ActiveCamera is null || _boneLineVao == 0 || _boneLineVbo == 0)
            return;

        List<Vector3> points = [];
        foreach (Skeleton skeleton in scene.RootNode.EnumerateDescendants<Skeleton>())
            CollectBoneLines(skeleton, points);

        if (points.Count == 0)
            return;

        float[] data = new float[points.Count * 3];
        for (int i = 0; i < points.Count; i++)
        {
            data[(i * 3)] = points[i].X;
            data[(i * 3) + 1] = points[i].Y;
            data[(i * 3) + 2] = points[i].Z;
        }

        _gl.BindVertexArray(_boneLineVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _boneLineVbo);
        fixed (float* ptr = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, BufferUsageARB.DynamicDraw);
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);

        _lineShader.Use();
        _lineShader.SetUniform("uViewProjection", ActiveCamera.GetViewMatrix() * ActiveCamera.GetProjectionMatrix());
        _lineShader.SetUniform("uModel", Matrix4x4.Identity);
        _lineShader.SetUniform("uScene", SceneTransform);
        _lineShader.SetUniform("uLineColor", new Vector4(0.0f, 1.0f, 0.3f, 1.0f));

        _gl.Disable(EnableCap.DepthTest);
        _gl.LineWidth(1.5f);
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)points.Count);
        _gl.Enable(EnableCap.DepthTest);
        _gl.BindVertexArray(0);
    }

    private static void CollectBoneLines(SceneNode node, List<Vector3> points)
    {
        if (node is SkeletonBone bone)
        {
            if (bone.Parent is SkeletonBone or Skeleton)
            {
                points.Add(bone.Parent.GetActiveWorldMatrix().Translation);
                points.Add(bone.GetActiveWorldMatrix().Translation);
            }
        }

        foreach (SceneNode child in node.EnumerateChildren())
            CollectBoneLines(child, points);
    }

    private static Matrix3x3 ComputeNormalMatrix(Matrix4x4 model)
    {
        if (Matrix4x4.Invert(model, out Matrix4x4 inverseModel))
        {
            Matrix4x4 transposed = Matrix4x4.Transpose(inverseModel);
            return new Matrix3x3(
                transposed.M11, transposed.M12, transposed.M13,
                transposed.M21, transposed.M22, transposed.M23,
                transposed.M31, transposed.M32, transposed.M33);
        }

        return Matrix3x3.Identity;
    }

    public void Dispose()
    {
        foreach (IRenderPass pass in _passes)
            pass.Dispose();
        _passes.Clear();

        SetEnvironmentResources(null);

        _lineShader?.Dispose();
        _environmentMap?.Dispose();
        _environmentMap = null;

        try
        {
            if (_boneLineVao != 0)
                _gl.DeleteVertexArray(_boneLineVao);

            if (_boneLineVbo != 0)
                _gl.DeleteBuffer(_boneLineVbo);
        }
        catch
        {
        }
    }

    internal void SetEnvironmentResources(GLEnvironmentResources? resources)
    {
        if (ReferenceEquals(_environmentResources, resources))
            return;

        _environmentResources?.Dispose();
        _environmentResources = resources;
    }
}

public readonly struct RendererColor(float r, float g, float b, float a)
{
    public float R { get; } = r;
    public float G { get; } = g;
    public float B { get; } = b;
    public float A { get; } = a;
}
