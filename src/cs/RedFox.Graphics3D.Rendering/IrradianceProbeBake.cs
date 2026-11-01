using System.Diagnostics;
using System.Numerics;
using RedFox.Imaging.Primitives;
using RedFox.Graphics3D.Rendering.Handles;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering;

internal sealed class IrradianceProbeBake(IGraphicsDevice graphicsDevice) : IDisposable
{
    private const string ProjectionMaterialTypeName = "IrradianceProbeProjection";
    private const ImageFormat ProbeFormat = ImageFormat.R16G16B16A16Float;
    private const int FaceCount = 6;
    private const int FaceSize = 16;
    private const int ProbeTexels = 4;
    private const int BounceCount = 3;
    private const double FrameBudgetMilliseconds = 2.0;
    private const int MaxProbesPerAxis = 64;
    private const int AtlasCount = 3;
    private const int FaceAtlasSlot = 0;
    private const int FullscreenVertexCount = 3;
    private const float MissDistance = 2.0f;

    private static readonly Vector3[] FaceForwards = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
    private static readonly Vector3[] FaceUps = [Vector3.UnitY, Vector3.UnitY, -Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitY, Vector3.UnitY];

    private readonly IGpuTexture?[] _atlasTextures = new IGpuTexture?[AtlasCount];
    private readonly IGpuRenderTarget?[] _atlasTargets = new IGpuRenderTarget?[AtlasCount];
    private readonly List<SceneBounds> _meshBounds = [];
    private readonly List<MeshRenderHandle> _faceMeshes = [];
    private IGpuPipelineState? _projectionPipeline;
    private IGpuTexture? _faceTexture;
    private IGpuTexture? _faceDepthTexture;
    private IGpuRenderTarget? _faceTarget;
    private IrradianceProbeGrid _grid;
    private IrradianceProbeGrid _displayGrid;
    private SceneBounds _bounds;
    private int _displayIndex = -1;
    private int _bounce;
    private int _nextProbe;
    private int _key;
    private bool _hasKey;

    public bool IsBaking { get; private set; }

    public void Update(ICommandList commandList, IReadOnlyList<MeshRenderHandle> meshes, int key, bool ready, in Matrix4x4 sceneAxis, ProbeLightingQuality quality)
    {
        if (ready && (!_hasKey || key != _key))
        {
            _key	= key;
            _hasKey	= true;
            StartBake(meshes, sceneAxis, quality);
        }

        if (ready && IsBaking)
        {
            BakeProbes(commandList, meshes, sceneAxis);
        }

        commandList.SetIrradianceProbes(_displayIndex >= 0 ? _atlasTextures[_displayIndex] : null, _displayGrid);
    }

    public void Clear()
    {
        IsBaking		= false;
        _hasKey			= false;
        _displayIndex	= -1;
        ReleaseAtlases();
    }

    public void Dispose()
    {
        Clear();

        _faceTarget?.Dispose();
        _faceTarget = null;
        _faceTexture?.Dispose();
        _faceTexture = null;
        _faceDepthTexture?.Dispose();
        _faceDepthTexture = null;

        if (_projectionPipeline is not null && graphicsDevice.MaterialTypes is IMaterialPipelineProvider pipelineProvider)
        {
            pipelineProvider.ReleasePipeline(ProjectionMaterialTypeName, _projectionPipeline);
            _projectionPipeline = null;
        }
    }

    private void StartBake(IReadOnlyList<MeshRenderHandle> meshes, in Matrix4x4 sceneAxis, ProbeLightingQuality quality)
    {
        IsBaking	= false;
        _bounds		= SceneBounds.Invalid;

        _meshBounds.Clear();

        foreach (MeshRenderHandle mesh in meshes)
        {
            SceneBounds meshBounds = mesh.GetWorldBounds(sceneAxis);

            _meshBounds.Add(meshBounds);
            _bounds = _bounds.Include(meshBounds);
        }

        if (!_bounds.IsValid || _bounds.Radius <= 0.0f)
        {
            _displayIndex = -1;
            return;
        }

        int budget = quality switch
        {
            ProbeLightingQuality.Low => 256,
            ProbeLightingQuality.Medium => 1024,
            _ => 4096,
        };

        SceneBounds bounds = _bounds.Expand(_bounds.Radius * 0.02f);
        float longest = MathF.Max(bounds.Size.X, MathF.Max(bounds.Size.Y, bounds.Size.Z));
        Vector3 size = Vector3.Max(bounds.Size, new Vector3(longest * 0.05f));
        float estimate = MathF.Cbrt(size.X * size.Y * size.Z / budget);
        int countX = Math.Clamp((int)MathF.Ceiling(size.X / estimate) + 1, 2, MaxProbesPerAxis);
        int countY = Math.Clamp((int)MathF.Ceiling(size.Y / estimate) + 1, 2, MaxProbesPerAxis);
        int countZ = Math.Clamp((int)MathF.Ceiling(size.Z / estimate) + 1, 2, MaxProbesPerAxis);
        Vector3 spacing = size / new Vector3(countX - 1, countY - 1, countZ - 1);

        _grid = new IrradianceProbeGrid(bounds.Center - size * 0.5f, spacing, countX, countY, countZ, MathF.Max(spacing.X, MathF.Max(spacing.Y, spacing.Z)));

        for (int i = 0; i < AtlasCount; i++)
        {
            if (i == _displayIndex)
            {
                continue;
            }

            _atlasTargets[i]?.Dispose();
            _atlasTextures[i]?.Dispose();
            _atlasTextures[i]	= graphicsDevice.CreateTexture(countX * ProbeTexels, countY * countZ, ProbeFormat, TextureUsage.RenderTarget | TextureUsage.Sampled);
            _atlasTargets[i]	= graphicsDevice.CreateRenderTarget(_atlasTextures[i], null);
        }

        if (_faceTarget is null)
        {
            _faceTexture		= graphicsDevice.CreateTexture(FaceSize * FaceCount, FaceSize, ProbeFormat, TextureUsage.RenderTarget | TextureUsage.Sampled);
            _faceDepthTexture	= graphicsDevice.CreateTexture(FaceSize * FaceCount, FaceSize, ImageFormat.D32Float, TextureUsage.DepthStencil);
            _faceTarget			= graphicsDevice.CreateRenderTarget(_faceTexture, _faceDepthTexture);
        }

        _projectionPipeline	??= ((IMaterialPipelineProvider)graphicsDevice.MaterialTypes).AcquirePipeline(graphicsDevice, ProjectionMaterialTypeName);
        _bounce				= 0;
        _nextProbe			= 0;
        IsBaking			= true;
    }

    private void BakeProbes(ICommandList commandList, IReadOnlyList<MeshRenderHandle> meshes, in Matrix4x4 sceneAxis)
    {
        int probeCount = _grid.CountX * _grid.CountY * _grid.CountZ;
        int target = GetWorkingIndex(_bounce);

        commandList.SetIrradianceProbes(_bounce > 0 ? _atlasTextures[GetWorkingIndex(_bounce - 1)] : null, _grid);
        commandList.SetProbeCapture(true);

        long start = Stopwatch.GetTimestamp();

        while (_nextProbe < probeCount && Stopwatch.GetElapsedTime(start).TotalMilliseconds < FrameBudgetMilliseconds)
        {
            CaptureProbe(commandList, meshes, sceneAxis, _nextProbe, target);
            _nextProbe++;
        }

        commandList.SetProbeCapture(false);

        if (_nextProbe < probeCount)
        {
            return;
        }

        _nextProbe = 0;
        _bounce++;

        if (_bounce == BounceCount)
        {
            _displayIndex	= target;
            _displayGrid	= _grid;
            IsBaking		= false;
        }
    }

    private void CaptureProbe(ICommandList commandList, IReadOnlyList<MeshRenderHandle> meshes, in Matrix4x4 sceneAxis, int index, int target)
    {
        int x = index % _grid.CountX;
        int y = index / _grid.CountX % _grid.CountY;
        int z = index / (_grid.CountX * _grid.CountY);
        Vector3 position = _grid.Origin + new Vector3(x, y, z) * _grid.Spacing;
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, _grid.DistanceScale * 0.01f, _bounds.Radius * 4.0f);
        Vector2 faceSize = new(FaceSize, FaceSize);

        commandList.SetRenderTarget(_faceTarget);
        commandList.SetViewport(FaceSize * FaceCount, FaceSize);
        commandList.ClearRenderTarget(0.0f, 0.0f, 0.0f, 0.0f, depth: 1.0f);

        for (int face = 0; face < FaceCount; face++)
        {
            Matrix4x4 view = Matrix4x4.CreateLookAt(position, position + FaceForwards[face], FaceUps[face]);
            ViewFrustum frustum = ViewFrustum.FromViewProjection(view * projection);

            _faceMeshes.Clear();

            for (int i = 0; i < meshes.Count; i++)
            {
                if (frustum.Intersects(_meshBounds[i]))
                {
                    _faceMeshes.Add(meshes[i]);
                }
            }

            commandList.SetViewport(face * FaceSize, 0, FaceSize, FaceSize);
            SceneTraversal.Render(_faceMeshes, commandList, RenderFlags.Opaque, view, projection, sceneAxis, position, faceSize);
        }

        commandList.SetRenderTarget(_atlasTargets[target]);
        commandList.SetViewport(x * ProbeTexels, y + z * _grid.CountY, ProbeTexels, 1);
        commandList.SetPipelineState(_projectionPipeline!);
        commandList.SetUniformInt("FaceAtlas", FaceAtlasSlot);
        commandList.SetUniformInt("FaceSize", FaceSize);
        commandList.SetUniformFloat("MissDistance", MissDistance);
        commandList.BindTexture(FaceAtlasSlot, _faceTexture!);
        commandList.Draw(FullscreenVertexCount, 0);
    }

    private int GetWorkingIndex(int bounce)
    {
        int first = _displayIndex == 0 ? 1 : 0;
        int second = _displayIndex == 2 ? 1 : 2;

        return bounce % 2 == 0 ? first : second;
    }

    private void ReleaseAtlases()
    {
        for (int i = 0; i < AtlasCount; i++)
        {
            _atlasTargets[i]?.Dispose();
            _atlasTargets[i] = null;
            _atlasTextures[i]?.Dispose();
            _atlasTextures[i] = null;
        }
    }
}
