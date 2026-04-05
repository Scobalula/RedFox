using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class SkeletonRenderHandle : RenderHandle
{
    private readonly Skeleton _skeleton;
    private readonly int _maxTextureSize;

    private uint _vao;
    private uint _vbo;
    private uint _boneWorldMatrixTexture;
    private int _matrixTexWidth = 1;
    private int _matrixTexHeight = 1;
    private SkeletonBone[] _bones = [];
    private int _connectionFirst;
    private int _connectionCount;
    private int _axisFirst;
    private int _axisCount;
    private int _jointFirst;
    private int _jointCount;

    private static readonly Vector4 ColorConnection = new(1.00f, 0.75f, 0.10f, 1.0f);
    private static readonly Vector4 ColorAxisX = new(1.00f, 0.25f, 0.25f, 1.0f);
    private static readonly Vector4 ColorAxisY = new(0.25f, 0.85f, 0.25f, 1.0f);
    private static readonly Vector4 ColorAxisZ = new(0.25f, 0.55f, 1.00f, 1.0f);
    private static readonly Vector4 ColorJoint = new(1.00f, 0.75f, 0.10f, 1.0f);

    public Skeleton Skeleton => _skeleton;
    public int BoneCount => _bones.Length;

    public SkeletonRenderHandle(Skeleton skeleton, int maxTextureSize = 2048)
    {
        _skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
        _maxTextureSize = maxTextureSize;
    }

    protected override void OnInitialize(GL gl)
    {
        SkeletonBone[] bones = CollectBones(_skeleton);
        _bones = bones;

        if (bones.Length == 0)
            return;

        Dictionary<SkeletonBone, int> boneIndex = [];
        for (int i = 0; i < bones.Length; i++)
            boneIndex[bones[i]] = i;

        List<float> conn = [];
        List<float> axes = [];
        List<float> joint = [];

        foreach (SkeletonBone bone in bones)
        {
            int idx = boneIndex[bone];

            if (bone.Parent is SkeletonBone parentBone && boneIndex.TryGetValue(parentBone, out int parentIdx))
            {
                AppendVertex(conn, parentIdx, Vector3.Zero, ColorConnection);
                AppendVertex(conn, idx, Vector3.Zero, ColorConnection);
            }

            AppendVertex(axes, idx, Vector3.Zero, ColorAxisX);
            AppendVertex(axes, idx, Vector3.UnitX, ColorAxisX);
            AppendVertex(axes, idx, Vector3.Zero, ColorAxisY);
            AppendVertex(axes, idx, Vector3.UnitY, ColorAxisY);
            AppendVertex(axes, idx, Vector3.Zero, ColorAxisZ);
            AppendVertex(axes, idx, Vector3.UnitZ, ColorAxisZ);

            AppendVertex(joint, idx, Vector3.Zero, ColorJoint);
        }

        float[] vertexData = [.. conn, .. axes, .. joint];

        _connectionFirst = 0;
        _connectionCount = conn.Count / 8;
        _axisFirst = _connectionCount;
        _axisCount = axes.Count / 8;
        _jointFirst = _axisFirst + _axisCount;
        _jointCount = joint.Count / 8;

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            fixed (float* ptr = vertexData)
            {
                gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(vertexData.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }
        }

        const int stride = 8 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 1, VertexAttribPointerType.Float, false, stride, 0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 1 * sizeof(float));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float));

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        (_matrixTexWidth, _matrixTexHeight) =
            GlBufferOperations.ComputeTextureDimensions(_maxTextureSize, bones.Length * 4);
        _boneWorldMatrixTexture = GlBufferOperations.CreateFloatTexture(
            gl, _matrixTexWidth, _matrixTexHeight,
            new float[_matrixTexWidth * _matrixTexHeight * 4]);
    }

    protected override void OnUpdate(GL gl, float deltaTime)
    {
        if (_boneWorldMatrixTexture == 0 || _bones.Length == 0)
            return;

        float[] data = new float[_matrixTexWidth * _matrixTexHeight * 4];
        for (int i = 0; i < _bones.Length; i++)
            GlBufferOperations.WriteMatrixTexels(_bones[i].GetActiveWorldMatrix(), data, i * 4);

        GlBufferOperations.UploadFloatTexture(gl, _boneWorldMatrixTexture,
            _matrixTexWidth, _matrixTexHeight, data);
    }

    public void Draw(GL gl, GLShader shader)
    {
        if (_boneWorldMatrixTexture == 0 || _bones.Length == 0)
            return;

        shader.SetUniform("uBoneWorldMatrixTexture", 0);
        shader.SetUniform("uBoneWorldMatrixTextureSize", new Vector2(_matrixTexWidth, _matrixTexHeight));
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _boneWorldMatrixTexture);

        gl.BindVertexArray(_vao);

        if (_connectionCount > 0)
            gl.DrawArrays(PrimitiveType.Lines, _connectionFirst, (uint)_connectionCount);

        if (_axisCount > 0)
            gl.DrawArrays(PrimitiveType.Lines, _axisFirst, (uint)_axisCount);

        if (_jointCount > 0)
        {
            gl.PointSize(5.0f);
            gl.DrawArrays(PrimitiveType.Points, _jointFirst, (uint)_jointCount);
            gl.PointSize(1.0f);
        }
    }

    protected override void OnDispose(GL gl)
    {
        _skeleton.GraphicsHandle = null;

        try
        {
            if (_vao != 0) gl.DeleteVertexArray(_vao);
            if (_vbo != 0) gl.DeleteBuffer(_vbo);
            if (_boneWorldMatrixTexture != 0) gl.DeleteTexture(_boneWorldMatrixTexture);
        }
        catch { }
    }

    public bool NeedsRebuild()
    {
        int currentBoneCount = 0;
        foreach (SkeletonBone _ in _skeleton.EnumerateDescendants<SkeletonBone>())
            currentBoneCount++;
        return currentBoneCount != _bones.Length;
    }

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
}
