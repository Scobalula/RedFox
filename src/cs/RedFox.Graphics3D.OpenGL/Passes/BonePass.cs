using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using RedFox.Graphics3D.OpenGL.Viewing;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

/// <summary>
/// Renders skeleton bones as GPU-driven 3D lines and point sprites.  The
/// world matrix for every bone is uploaded to a floating-point texture each
/// frame; the vertex shader samples that texture to position the geometry, so
/// no CPU-side clipping or screen-space arithmetic is required.
/// </summary>
public sealed class BonePass : IRenderPass
{
    // ------------------------------------------------------------------
    // Per-skeleton GPU state
    // ------------------------------------------------------------------

    private sealed class SkeletonCache
    {
        public uint Vao;
        public uint Vbo;
        public uint BoneWorldMatrixTexture;
        public int  MatrixTexWidth;
        public int  MatrixTexHeight;

        // Stable bone ordering – index i in this array maps to bone index i
        // in the vertex data and in the matrix texture.
        public SkeletonBone[] Bones = [];

        // Draw ranges stored as (firstVertex, vertexCount) pairs.
        public int ConnectionFirst;
        public int ConnectionCount;
        public int AxisFirst;
        public int AxisCount;
        public int JointFirst;
        public int JointCount;
    }

    // ------------------------------------------------------------------
    // Fields
    // ------------------------------------------------------------------

    private GL      _gl       = null!;
    private GLShader _shader  = null!;
    private int     _maxTextureSize = 2048;
    private bool    _isOpenGles;
    private bool    _initialized;

    private readonly Dictionary<Skeleton, SkeletonCache> _caches =
        new(ReferenceEqualityComparer.Instance);

    // ------------------------------------------------------------------
    // Colours (baked into vertex data)
    // ------------------------------------------------------------------

    private static readonly Vector4 ColorConnection = new(1.00f, 0.75f, 0.10f, 1.0f);
    private static readonly Vector4 ColorAxisX      = new(1.00f, 0.25f, 0.25f, 1.0f);
    private static readonly Vector4 ColorAxisY      = new(0.25f, 0.85f, 0.25f, 1.0f);
    private static readonly Vector4 ColorAxisZ      = new(0.25f, 0.55f, 1.00f, 1.0f);
    private static readonly Vector4 ColorJoint      = new(1.00f, 0.75f, 0.10f, 1.0f);

    // ------------------------------------------------------------------
    // IRenderPass
    // ------------------------------------------------------------------

    public string Name    => "Bones";
    public bool   Enabled { get; set; } = true;

    public void Initialize(GLRenderer renderer)
    {
        _gl         = renderer.GL;
        _isOpenGles = renderer.IsOpenGles;

        (string vert, string frag) = ShaderSource.LoadProgram(_gl, "bone");
        _shader = new GLShader(_gl, vert, frag);

        _gl.GetInteger(GLEnum.MaxTextureSize, out _maxTextureSize);
        _maxTextureSize = Math.Max(_maxTextureSize, 1);

        _initialized = true;
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled || !renderer.ShowBones)
            return;

        Camera? camera = renderer.ActiveCamera;
        if (camera is null)
            return;

        float axisScale = ComputeAxisScale(scene, renderer.SceneTransform);

        _shader.Use();
        _shader.SetUniform("uView",       camera.GetViewMatrix());
        _shader.SetUniform("uProjection", camera.GetProjectionMatrix());
        _shader.SetUniform("uScene",      renderer.SceneTransform);
        _shader.SetUniform("uAxisScale",  axisScale);

        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        // Depth clipping (primitive assembly) is distinct from the depth test
        // (fragment stage) and still fires even with DepthTest disabled.
        // GL_DEPTH_CLAMP clamps out-of-range Z to the clip boundary instead of
        // discarding the primitive, keeping bone overlays fully visible.
        if (!_isOpenGles)
            _gl.Enable(GLEnum.DepthClamp);

        foreach (Skeleton skeleton in scene.RootNode.EnumerateDescendants<Skeleton>())
            RenderSkeleton(skeleton);

        if (!_isOpenGles)
            _gl.Disable(GLEnum.DepthClamp);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
        _gl.BindVertexArray(0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Dispose()
    {
        _shader?.Dispose();

        foreach (SkeletonCache cache in _caches.Values)
            DeleteCache(cache);

        _caches.Clear();
    }

    // ------------------------------------------------------------------
    // Per-skeleton rendering
    // ------------------------------------------------------------------

    private void RenderSkeleton(Skeleton skeleton)
    {
        SkeletonCache cache = GetOrBuildCache(skeleton);
        if (cache.BoneWorldMatrixTexture == 0 || cache.Bones.Length == 0)
            return;

        UploadBoneWorldMatrices(cache);

        _shader.SetUniform("uBoneWorldMatrixTexture",     0);
        _shader.SetUniform("uBoneWorldMatrixTextureSize", new Vector2(cache.MatrixTexWidth, cache.MatrixTexHeight));
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, cache.BoneWorldMatrixTexture);

        _gl.BindVertexArray(cache.Vao);

        if (cache.ConnectionCount > 0)
            _gl.DrawArrays(PrimitiveType.Lines, cache.ConnectionFirst, (uint)cache.ConnectionCount);

        if (cache.AxisCount > 0)
            _gl.DrawArrays(PrimitiveType.Lines, cache.AxisFirst, (uint)cache.AxisCount);

        if (cache.JointCount > 0)
        {
            _gl.PointSize(5.0f);
            _gl.DrawArrays(PrimitiveType.Points, cache.JointFirst, (uint)cache.JointCount);
            _gl.PointSize(1.0f);
        }
    }

    // ------------------------------------------------------------------
    // Cache management
    // ------------------------------------------------------------------

    private SkeletonCache GetOrBuildCache(Skeleton skeleton)
    {
        if (_caches.TryGetValue(skeleton, out SkeletonCache? existing))
        {
            int currentBoneCount = CountBones(skeleton);
            if (existing.Bones.Length == currentBoneCount)
                return existing;

            // Topology changed (e.g. hot-reload) – rebuild.
            DeleteCache(existing);
            _caches.Remove(skeleton);
        }

        SkeletonCache cache = BuildCache(skeleton);
        _caches[skeleton] = cache;
        return cache;
    }

    private unsafe SkeletonCache BuildCache(Skeleton skeleton)
    {
        SkeletonBone[] bones = CollectBones(skeleton);
        if (bones.Length == 0)
            return new SkeletonCache { Bones = bones };

        // -------------------------------------------------------------------
        // Build vertex data (8 floats per vertex):
        //   [ boneIndex | localOffset.xyz | color.rgba ]
        // Sections: connections (GL_LINES) → axes (GL_LINES) → joints (GL_POINTS)
        // -------------------------------------------------------------------
        Dictionary<SkeletonBone, int> boneIndex = [];
        for (int i = 0; i < bones.Length; i++)
            boneIndex[bones[i]] = i;

        List<float> conn  = [];
        List<float> axes  = [];
        List<float> joint = [];

        foreach (SkeletonBone bone in bones)
        {
            int idx = boneIndex[bone];

            // Parent → this bone connection line
            if (bone.Parent is SkeletonBone parentBone && boneIndex.TryGetValue(parentBone, out int parentIdx))
            {
                AppendVertex(conn, parentIdx, Vector3.Zero, ColorConnection);
                AppendVertex(conn, idx,       Vector3.Zero, ColorConnection);
            }

            // Local-axis indicators (unit vectors; scaled by uAxisScale in the shader)
            AppendVertex(axes, idx, Vector3.Zero,  ColorAxisX);
            AppendVertex(axes, idx, Vector3.UnitX, ColorAxisX);
            AppendVertex(axes, idx, Vector3.Zero,  ColorAxisY);
            AppendVertex(axes, idx, Vector3.UnitY, ColorAxisY);
            AppendVertex(axes, idx, Vector3.Zero,  ColorAxisZ);
            AppendVertex(axes, idx, Vector3.UnitZ, ColorAxisZ);

            // Joint point
            AppendVertex(joint, idx, Vector3.Zero, ColorJoint);
        }

        float[] vertexData = [.. conn, .. axes, .. joint];

        int connFirst  = 0;
        int connCount  = conn.Count  / 8;
        int axisFirst  = connCount;
        int axisCount  = axes.Count  / 8;
        int jointFirst = axisFirst + axisCount;
        int jointCount = joint.Count / 8;

        // Upload static geometry
        uint vao = _gl.GenVertexArray();
        uint vbo = _gl.GenBuffer();
        _gl.BindVertexArray(vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        fixed (float* ptr = vertexData)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertexData.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
        }

        const int Stride = 8 * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 1, VertexAttribPointerType.Float, false, Stride, 0);                   // boneIndex
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, Stride, 1 * sizeof(float));   // localOffset
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, Stride, 4 * sizeof(float));   // color

        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        // Create bone world-matrix texture (initial data zeroed; filled each frame)
        (int texW, int texH) = ComputeTextureDimensions(bones.Length * 4);
        uint matTex = CreateFloatTexture(texW, texH, new float[texW * texH * 4]);

        return new SkeletonCache
        {
            Vao                    = vao,
            Vbo                    = vbo,
            BoneWorldMatrixTexture = matTex,
            MatrixTexWidth         = texW,
            MatrixTexHeight        = texH,
            Bones                  = bones,
            ConnectionFirst        = connFirst,
            ConnectionCount        = connCount,
            AxisFirst              = axisFirst,
            AxisCount              = axisCount,
            JointFirst             = jointFirst,
            JointCount             = jointCount,
        };
    }

    private void DeleteCache(SkeletonCache cache)
    {
        try
        {
            if (cache.Vao != 0) _gl.DeleteVertexArray(cache.Vao);
            if (cache.Vbo != 0) _gl.DeleteBuffer(cache.Vbo);
            if (cache.BoneWorldMatrixTexture != 0) _gl.DeleteTexture(cache.BoneWorldMatrixTexture);
        }
        catch { }
    }

    // ------------------------------------------------------------------
    // Per-frame bone matrix upload
    // ------------------------------------------------------------------

    private unsafe void UploadBoneWorldMatrices(SkeletonCache cache)
    {
        int count    = cache.Bones.Length;
        float[] data = new float[cache.MatrixTexWidth * cache.MatrixTexHeight * 4];

        for (int i = 0; i < count; i++)
            WriteMatrixTexels(cache.Bones[i].GetActiveWorldMatrix(), data, i * 4);

        _gl.BindTexture(TextureTarget.Texture2D, cache.BoneWorldMatrixTexture);
        fixed (float* ptr = data)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                (uint)cache.MatrixTexWidth, (uint)cache.MatrixTexHeight,
                PixelFormat.Rgba, PixelType.Float, ptr);
        }
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    // ------------------------------------------------------------------
    // Bone collection (depth-first, stable order)
    // ------------------------------------------------------------------

    private static SkeletonBone[] CollectBones(Skeleton skeleton)
    {
        List<SkeletonBone> bones = [];
        CollectBonesRecursive(skeleton, bones);
        return [.. bones];
    }

    private static void CollectBonesRecursive(SceneNode node, List<SkeletonBone> bones)
    {
        foreach (SceneNode child in node.EnumerateChildren())
        {
            if (child is SkeletonBone bone)
            {
                bones.Add(bone);
                CollectBonesRecursive(bone, bones);
            }
        }
    }

    private static int CountBones(Skeleton skeleton)
    {
        int count = 0;
        foreach (SkeletonBone _ in skeleton.EnumerateDescendants<SkeletonBone>())
            count++;
        return count;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static float ComputeAxisScale(Scene scene, Matrix4x4 sceneTransform)
    {
        if (SceneBounds.TryGetBounds(scene, sceneTransform, out SceneBoundsInfo bounds) && bounds.Radius > 1e-6f)
            return MathF.Max(bounds.Radius * 0.04f, 1e-4f);

        return 1.0f;
    }

    private static void AppendVertex(List<float> buf, int boneIndex, Vector3 localOffset, Vector4 color)
    {
        buf.Add(boneIndex);
        buf.Add(localOffset.X);
        buf.Add(localOffset.Y);
        buf.Add(localOffset.Z);
        buf.Add(color.X);
        buf.Add(color.Y);
        buf.Add(color.Z);
        buf.Add(color.W);
    }

    private static void WriteMatrixTexels(Matrix4x4 m, float[] dest, int texelBase)
    {
        WriteTexel(dest, texelBase + 0, m.M11, m.M12, m.M13, m.M14);
        WriteTexel(dest, texelBase + 1, m.M21, m.M22, m.M23, m.M24);
        WriteTexel(dest, texelBase + 2, m.M31, m.M32, m.M33, m.M34);
        WriteTexel(dest, texelBase + 3, m.M41, m.M42, m.M43, m.M44);
    }

    private static void WriteTexel(float[] dest, int texelIndex, float x, float y, float z, float w)
    {
        int b = texelIndex * 4;
        dest[b]     = x;
        dest[b + 1] = y;
        dest[b + 2] = z;
        dest[b + 3] = w;
    }

    private unsafe uint CreateFloatTexture(int width, int height, float[] data)
    {
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        fixed (float* ptr = data)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba32f,
                (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.Float, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return tex;
    }

    private (int Width, int Height) ComputeTextureDimensions(int texelCount)
    {
        int safe  = Math.Max(texelCount, 1);
        int width = Math.Min(_maxTextureSize, Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(safe))));
        int height = (safe + width - 1) / width;
        return (width, height);
    }
}
