using RedFox.Imaging.Primitives;

namespace RedFox.Graphics3D.Rendering;

internal sealed class DepthPrepass(IGraphicsDevice graphicsDevice, IReadOnlyList<IRenderHandle> handles) : RenderPass
{
    private const string SceneDepthMapName = "SceneDepthMap";

    private IGpuTexture?		_depthTexture;
    private IGpuRenderTarget?	_renderTarget;
    private int					_width	= 1;
    private int					_height	= 1;

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
        CameraView		view		= context.View;

        if (_renderTarget is null)
        {
            _depthTexture	= graphicsDevice.CreateTexture(_width, _height, ImageFormat.D32Float, TextureUsage.DepthStencil | TextureUsage.Sampled);
            _renderTarget	= graphicsDevice.CreateRenderTarget(null, _depthTexture);
        }

        commandList.SetViewport(_width, _height);
        commandList.SetRenderTarget(_renderTarget);
        commandList.ClearRenderTarget(0.0f, 0.0f, 0.0f, 0.0f, depth: 1.0f);
        SceneTraversal.Render(handles, commandList, RenderFlags.DepthPrepass, view.ViewMatrix, view.ProjectionMatrix, SceneRenderer.GetSceneAxisMatrix(context.Scene.UpAxis), view.Position, context.ViewportSize);
        commandList.SetGlobalTexture(SceneDepthMapName, _depthTexture);
    }

    protected override void DisposeResources()
    {
        ReleaseTargets();
    }

    private void ReleaseTargets()
    {
        _renderTarget?.Dispose();
        _renderTarget = null;
        _depthTexture?.Dispose();
        _depthTexture = null;
    }
}
