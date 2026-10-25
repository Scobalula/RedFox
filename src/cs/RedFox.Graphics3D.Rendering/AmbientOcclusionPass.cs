using System.Numerics;
using RedFox.Imaging.Primitives;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering;

internal sealed class AmbientOcclusionPass(IGraphicsDevice graphicsDevice) : RenderPass
{
    private const string	AmbientOcclusionMapName		= "AmbientOcclusionMap";
    private const string	OcclusionMaterialTypeName	= "AmbientOcclusion";
    private const string	BlurMaterialTypeName		= "AmbientOcclusionBlur";
    private const int		OcclusionInputSlot			= 0;
    private const int		FullscreenVertexCount		= 3;

    private IGpuPipelineState?	_occlusionPipeline;
    private IGpuPipelineState?	_blurPipeline;
    private IGpuTexture?		_occlusionTexture;
    private IGpuTexture?		_blurTexture;
    private IGpuRenderTarget?	_occlusionTarget;
    private IGpuRenderTarget?	_blurTarget;
    private int					_width			= 1;
    private int					_height			= 1;
    private Scene?				_boundsScene;
    private long				_boundsVersion	= -1;
    private float				_sceneRadius;

    public AmbientOcclusionQuality Quality { get; set; } = AmbientOcclusionQuality.High;

    public float Radius { get; set; } = 0.08f;

    public float Intensity { get; set; } = 1.5f;

    public override RenderPassPhase Phase => RenderPassPhase.Prepass;

    public override void Resize(int width, int height)
    {
        if (_width != width || _height != height)
        {
            ReleaseTargets();
        }

        _width	= width;
        _height	= height;
    }

    protected override void ExecutePass(RenderFrameContext context)
    {
        ICommandList	commandList	= context.GetRequired<ICommandList>();
        Matrix4x4		projection	= context.View.ProjectionMatrix;
        float			radius		= GetSceneRadius(context.Scene) * Radius;

        if (radius <= 0.0f || !Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection))
        {
            return;
        }

        int sliceCount = Quality switch
        {
            AmbientOcclusionQuality.Low		=> 2,
            AmbientOcclusionQuality.Medium	=> 3,
            AmbientOcclusionQuality.High	=> 4,
            _								=> 6,
        };

        int stepCount = Quality switch
        {
            AmbientOcclusionQuality.Low		=> 4,
            AmbientOcclusionQuality.Medium	=> 6,
            AmbientOcclusionQuality.High	=> 8,
            _								=> 12,
        };

        if (_occlusionTarget is null)
        {
            _occlusionTexture	= graphicsDevice.CreateTexture(_width, _height, ImageFormat.R8Unorm, TextureUsage.RenderTarget | TextureUsage.Sampled);
            _blurTexture		= graphicsDevice.CreateTexture(_width, _height, ImageFormat.R8Unorm, TextureUsage.RenderTarget | TextureUsage.Sampled);
            _occlusionTarget	= graphicsDevice.CreateRenderTarget(_occlusionTexture, null);
            _blurTarget			= graphicsDevice.CreateRenderTarget(_blurTexture, null);
        }

        IMaterialPipelineProvider pipelineProvider = (IMaterialPipelineProvider)graphicsDevice.MaterialTypes;

        commandList.SetViewport(_width, _height);
        commandList.SetRenderTarget(_occlusionTarget);
        commandList.SetPipelineState(_occlusionPipeline ??= pipelineProvider.AcquirePipeline(graphicsDevice, OcclusionMaterialTypeName));
        commandList.SetUniformMatrix4x4("Projection", projection);
        commandList.SetUniformMatrix4x4("InverseProjection", inverseProjection);
        commandList.SetUniformFloat("Radius", radius);
        commandList.SetUniformFloat("Intensity", Intensity);
        commandList.SetUniformInt("SliceCount", sliceCount);
        commandList.SetUniformInt("StepCount", stepCount);
        commandList.Draw(FullscreenVertexCount, 0);

        commandList.SetPipelineState(_blurPipeline ??= pipelineProvider.AcquirePipeline(graphicsDevice, BlurMaterialTypeName));
        commandList.SetUniformMatrix4x4("InverseProjection", inverseProjection);
        commandList.SetUniformInt("OcclusionMap", OcclusionInputSlot);

        commandList.SetRenderTarget(_blurTarget);
        commandList.BindTexture(OcclusionInputSlot, _occlusionTexture!);
        commandList.SetUniformVector2("BlurDirection", Vector2.UnitX);
        commandList.Draw(FullscreenVertexCount, 0);

        commandList.SetRenderTarget(_occlusionTarget);
        commandList.BindTexture(OcclusionInputSlot, _blurTexture!);
        commandList.SetUniformVector2("BlurDirection", Vector2.UnitY);
        commandList.Draw(FullscreenVertexCount, 0);

        commandList.SetGlobalTexture(AmbientOcclusionMapName, _occlusionTexture);
    }

    protected override void DisposeResources()
    {
        ReleaseTargets();

        IMaterialPipelineProvider pipelineProvider = (IMaterialPipelineProvider)graphicsDevice.MaterialTypes;

        if (_occlusionPipeline is not null)
        {
            pipelineProvider.ReleasePipeline(OcclusionMaterialTypeName, _occlusionPipeline);
            _occlusionPipeline = null;
        }

        if (_blurPipeline is not null)
        {
            pipelineProvider.ReleasePipeline(BlurMaterialTypeName, _blurPipeline);
            _blurPipeline = null;
        }
    }

    private float GetSceneRadius(Scene scene)
    {
        if (!ReferenceEquals(_boundsScene, scene) || _boundsVersion != scene.Version)
        {
            _sceneRadius	= SceneBoundsCalculator.TryCompute(scene, out SceneBounds bounds, node => node is Mesh) ? bounds.Radius : 0.0f;
            _boundsScene	= scene;
            _boundsVersion	= scene.Version;
        }

        return _sceneRadius;
    }

    private void ReleaseTargets()
    {
        _occlusionTarget?.Dispose();
        _occlusionTarget = null;
        _blurTarget?.Dispose();
        _blurTarget = null;
        _occlusionTexture?.Dispose();
        _occlusionTexture = null;
        _blurTexture?.Dispose();
        _blurTexture = null;
    }
}
