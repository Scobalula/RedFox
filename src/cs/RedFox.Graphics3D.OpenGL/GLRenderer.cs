using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.OpenGL.Passes;
using RedFox.Graphics3D.OpenGL.Shaders;
using RedFox.Graphics2D;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class GLRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly List<IRenderPass> _passes = [];
    private readonly Dictionary<Mesh, GLMeshHandle> _meshHandles = [];
    private readonly Dictionary<Texture, GLTextureHandle> _textureHandles = [];

    private GLShader? _lineShader;
    private uint _boneLineVAO;
    private uint _boneLineVBO;
    private int _boneLineVertexCount;

    public GL GL => _gl;
    public Camera? ActiveCamera { get; set; }
    public bool ShowBones { get; set; } = true;
    public bool ShowWireframe { get; set; }
    public RendererColor BackgroundColor { get; set; } = new(0.12f, 0.12f, 0.14f, 1.0f);
    public IReadOnlyList<IRenderPass> Passes => _passes;
    public ImageTranslatorManager? ImageTranslatorManager { get; set; }

    public GLRenderer(GL gl)
    {
        _gl = gl;
    }

    public void Initialize()
    {
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(CullFaceMode.Back);
        _gl.FrontFace(FrontFaceDirection.CounterClockwise);
        _gl.Enable(EnableCap.LineSmooth);
        _gl.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

        _lineShader = new GLShader(_gl, ShaderSource.LineVertex, ShaderSource.LineFragment);
        _boneLineVAO = _gl.GenVertexArray();
        _boneLineVBO = _gl.GenBuffer();

        AddPass(new GeometryPass());

        foreach (var pass in _passes)
            pass.Initialize(this);
    }

    public void AddPass(IRenderPass pass) => _passes.Add(pass);

    public bool RemovePass(string name)
    {
        for (int i = 0; i < _passes.Count; i++)
        {
            if (_passes[i].Name == name)
            {
                _passes[i].Dispose();
                _passes.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public T? GetPass<T>() where T : class, IRenderPass => _passes.OfType<T>().FirstOrDefault();

    public void Render(Scene scene, float deltaTime)
    {
        _gl.ClearColor(BackgroundColor.R, BackgroundColor.G, BackgroundColor.B, BackgroundColor.A);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (ShowWireframe)
            _gl.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

        foreach (var pass in _passes)
        {
            if (pass.Enabled)
                pass.Render(this, scene, deltaTime);
        }

        if (ShowBones)
            RenderBones(scene);

        if (ShowWireframe)
            _gl.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
    }

    public GLMeshHandle? GetOrCreateMeshHandle(Mesh mesh)
    {
        if (_meshHandles.TryGetValue(mesh, out var existing))
            return existing;

        var handle = UploadMesh(mesh);
        if (handle == null) return null;

        _meshHandles[mesh] = handle;
        mesh.GraphicsHandle = handle;
        return handle;
    }

    public GLTextureHandle? GetOrCreateTextureHandle(Texture texture)
    {
        if (_textureHandles.TryGetValue(texture, out var existing))
            return existing;

        var handle = UploadTexture(texture);
        if (handle == null) return null;

        _textureHandles[texture] = handle;
        texture.GraphicsHandle = handle;
        return handle;
    }

    public void UnloadMesh(Mesh mesh)
    {
        if (_meshHandles.TryGetValue(mesh, out var handle))
        {
            handle.Delete(_gl);
            _meshHandles.Remove(mesh);
            mesh.GraphicsHandle = null;
        }
    }

    private unsafe GLMeshHandle? UploadMesh(Mesh mesh)
    {
        if (mesh.Positions == null || mesh.VertexCount == 0)
            return null;

        uint vao = _gl.GenVertexArray();
        _gl.BindVertexArray(vao);

        uint posVbo = UploadAttributeBuffer(mesh.Positions, 3);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);

        uint normVbo = 0;
        if (mesh.Normals != null)
        {
            normVbo = UploadAttributeBuffer(mesh.Normals, 3);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 0, 0);
        }

        uint uvVbo = 0;
        if (mesh.UVLayers != null)
        {
            uvVbo = UploadAttributeBuffer(mesh.UVLayers, 2);
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 0, 0);
        }

        uint boneIdxVbo = 0;
        uint boneWeightVbo = 0;
        int skinInfluenceCount = 0;
        bool hasSkinning = mesh.HasSkinning && mesh.SkinnedBones != null;

        if (hasSkinning && mesh.BoneIndices != null)
        {
            skinInfluenceCount = mesh.SkinInfluenceCount;
            var indexData = ExtractIntBuffer(mesh.BoneIndices, skinInfluenceCount);
            boneIdxVbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTarget.ArrayBuffer, boneIdxVbo);
            fixed (int* ptr = indexData)
            {
                _gl.BufferData(BufferTarget.ArrayBuffer, (nuint)(indexData.Length * sizeof(int)), ptr, BufferUsage.StaticDraw);
            }
            _gl.EnableVertexAttribArray(3);
            _gl.VertexAttribIPointer(3, skinInfluenceCount, VertexAttribIPointerType.Int, 0, 0);

            if (mesh.BoneWeights != null)
            {
                var weightData = ExtractFloatBuffer(mesh.BoneWeights, skinInfluenceCount);
                boneWeightVbo = _gl.GenBuffer();
                _gl.BindBuffer(BufferTarget.ArrayBuffer, boneWeightVbo);
                fixed (float* ptr = weightData)
                {
                    _gl.BufferData(BufferTarget.ArrayBuffer, (nuint)(weightData.Length * sizeof(float)), ptr, BufferUsage.StaticDraw);
                }
                _gl.EnableVertexAttribArray(4);
                _gl.VertexAttribPointer(4, skinInfluenceCount, VertexAttribPointerType.Float, false, 0, 0);
            }
        }

        uint ebo = 0;
        int indexCount = 0;
        bool isIndexed = mesh.FaceIndices != null;

        if (isIndexed)
        {
            var faceData = ExtractIntBuffer(mesh.FaceIndices!, 1);
            indexCount = faceData.Length;
            ebo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            fixed (int* ptr = faceData)
            {
                _gl.BufferData(BufferTarget.ElementArrayBuffer, (nuint)(faceData.Length * sizeof(int)), ptr, BufferUsage.StaticDraw);
            }
        }

        _gl.BindVertexArray(0);

        return new GLMeshHandle(
            vao, posVbo, normVbo, uvVbo, boneIdxVbo, boneWeightVbo, ebo,
            mesh.VertexCount, indexCount, isIndexed,
            mesh.Normals != null, mesh.UVLayers != null, hasSkinning, skinInfluenceCount);
    }

    private unsafe uint UploadAttributeBuffer(DataBuffer buffer, int componentsPerVertex)
    {
        var data = ExtractFloatBuffer(buffer, componentsPerVertex);
        uint vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        fixed (float* ptr = data)
        {
            _gl.BufferData(BufferTarget.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, BufferUsage.StaticDraw);
        }
        return vbo;
    }

    private static float[] ExtractFloatBuffer(DataBuffer buffer, int valuesPerElement)
    {
        int elementCount = buffer.ElementCount;
        int componentCount = Math.Max(buffer.ComponentCount, 1);
        int actualValues = Math.Max(valuesPerElement, componentCount);
        var data = new float[elementCount * actualValues];

        for (int i = 0; i < elementCount; i++)
        {
            for (int v = 0; v < actualValues; v++)
            {
                for (int c = 0; c < componentCount; c++)
                {
                    int srcIdx = i * valuesPerElement * componentCount + v * componentCount + c;
                    int dstIdx = i * actualValues + v;
                    if (c == 0)
                        data[dstIdx] = buffer.Get<float>(i, Math.Min(v, Math.Max(0, buffer.ValueCount - 1)), c);
                }
            }
        }
        return data;
    }

    private static int[] ExtractIntBuffer(DataBuffer buffer, int valuesPerElement)
    {
        int elementCount = buffer.ElementCount;
        int actualValues = Math.Max(valuesPerElement, 1);
        var data = new int[elementCount * actualValues];

        for (int i = 0; i < elementCount; i++)
        {
            for (int v = 0; v < actualValues; v++)
            {
                data[i * actualValues + v] = buffer.Get<int>(i, Math.Min(v, Math.Max(0, buffer.ValueCount - 1)), 0);
            }
        }
        return data;
    }

    private unsafe GLTextureHandle? UploadTexture(Texture texture)
    {
        Image? image = texture.Data;

        if (image == null && texture.ImageLoader != null)
        {
            image = texture.ImageLoader.Load();
        }

        if (image == null && !string.IsNullOrEmpty(texture.FilePath) && File.Exists(texture.FilePath))
        {
            image = ImageTranslatorManager?.Read(texture.FilePath);
        }

        if (image == null) return null;

        byte[] rgbaData = ConvertToRGBA(image);
        return new GLTextureHandle(_gl, rgbaData, image.Width, image.Height);
    }

    private static byte[] ConvertToRGBA(Image image)
    {
        var slice = image.GetSlice(0, 0, 0);
        int pixelCount = image.Width * image.Height;
        byte[] data = new byte[pixelCount * 4];

        int bpp = image.Format.BitsPerPixel();
        int srcStride = bpp / 8;

        var srcData = image.PixelData;
        int srcOffset = slice.Offset;

        for (int i = 0; i < pixelCount; i++)
        {
            int src = srcOffset + i * srcStride;
            int dst = i * 4;

            if (srcStride >= 4)
            {
                data[dst + 0] = srcData[src + 0];
                data[dst + 1] = srcData[src + 1];
                data[dst + 2] = srcData[src + 2];
                data[dst + 3] = srcData[src + 3];
            }
            else if (srcStride == 3)
            {
                data[dst + 0] = srcData[src + 0];
                data[dst + 1] = srcData[src + 1];
                data[dst + 2] = srcData[src + 2];
                data[dst + 3] = 255;
            }
            else
            {
                data[dst + 0] = 200;
                data[dst + 1] = 200;
                data[dst + 2] = 200;
                data[dst + 3] = 255;
            }
        }

        return data;
    }

    private unsafe void RenderBones(Scene scene)
    {
        if (_boneLineVAO == 0 || _lineShader == null || ActiveCamera == null) return;

        var lineVerts = new List<Vector3>();

        foreach (var skeleton in scene.RootNode.EnumerateDescendants<Skeleton>())
            CollectBoneLines(skeleton, lineVerts);

        _boneLineVertexCount = lineVerts.Count;

        if (_boneLineVertexCount == 0) return;

        _gl.BindVertexArray(_boneLineVAO);
        _gl.BindBuffer(BufferTarget.ArrayBuffer, _boneLineVBO);

        var data = new float[_boneLineVertexCount * 3];
        for (int i = 0; i < _boneLineVertexCount; i++)
        {
            data[i * 3] = lineVerts[i].X;
            data[i * 3 + 1] = lineVerts[i].Y;
            data[i * 3 + 2] = lineVerts[i].Z;
        }

        fixed (float* ptr = data)
        {
            _gl.BufferData(BufferTarget.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, BufferUsage.DynamicDraw);
        }
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);

        _lineShader.Use();
        var vp = ActiveCamera.GetProjectionMatrix() * ActiveCamera.GetViewMatrix();
        _lineShader.SetUniform("uViewProjection", vp);
        _lineShader.SetUniform("uModel", Matrix4x4.Identity);
        _lineShader.SetUniform("uLineColor", new Vector4(0.0f, 1.0f, 0.3f, 1.0f));

        _gl.BindVertexArray(_boneLineVAO);
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_boneLineVertexCount);
        _gl.BindVertexArray(0);
    }

    private static void CollectBoneLines(SceneNode node, List<Vector3> points)
    {
        if (node is SkeletonBone)
        {
            var worldPos = node.GetActiveWorldPosition();

            if (node.Parent is SkeletonBone or Skeleton)
            {
                var parentPos = node.Parent!.GetActiveWorldPosition();
                points.Add(parentPos);
                points.Add(worldPos);
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    if (child is SkeletonBone)
                        CollectBoneLines(child, points);
                }
            }
        }
        else
        {
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                    CollectBoneLines(child, points);
            }
        }
    }

    public void Dispose()
    {
        foreach (var pass in _passes)
            pass.Dispose();
        _passes.Clear();

        foreach (var handle in _meshHandles.Values)
            handle.Delete(_gl);
        _meshHandles.Clear();

        foreach (var handle in _textureHandles.Values)
            handle.Dispose();
        _textureHandles.Clear();

        _lineShader?.Dispose();

        if (_boneLineVAO != 0) _gl.DeleteVertexArray(_boneLineVAO);
        if (_boneLineVBO != 0) _gl.DeleteBuffer(_boneLineVBO);
    }
}

public readonly struct RendererColor(float r, float g, float b, float a)
{
    public readonly float R = r;
    public readonly float G = g;
    public readonly float B = b;
    public readonly float A = a;
}
