using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.GLFW;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System;
using D3D11Api = Silk.NET.Direct3D11.D3D11;
using GlfwApi = Silk.NET.GLFW.Glfw;
using GlfwWindowHandle = Silk.NET.GLFW.WindowHandle;

namespace RedFox.Graphics3D.D3D11;

internal sealed unsafe class D3D11Context : IDisposable
{
    private const uint BufferUsageRenderTargetOutput = 0x20;
    private const uint DefaultSwapChainBufferCount = 2;
    private readonly IWindow _window;
    private bool _disposed;
    private ComPtr<ID3D11DepthStencilView> _defaultDepthStencilView;
    private ComPtr<ID3D11Texture2D> _defaultDepthTexture;
    private ComPtr<ID3D11Texture2D> _defaultBackBuffer;
    private ComPtr<ID3D11RenderTargetView> _defaultRenderTargetView;
    private ComPtr<IDXGISwapChain> _swapChain;

    private D3D11Context(
        IWindow window,
        D3D11Api d3d,
        DXGI dxgi,
        ComPtr<ID3D11Device> device,
        ComPtr<ID3D11DeviceContext> deviceContext,
        ComPtr<IDXGIFactory1> factory,
        ComPtr<IDXGISwapChain> swapChain)
    {
        _window = window;
        D3D = d3d;
        Dxgi = dxgi;
        Device = device;
        DeviceContext = deviceContext;
        Factory = factory;
        _swapChain = swapChain;
    }

    public D3D11Api D3D { get; }

    public DXGI Dxgi { get; }

    public ComPtr<ID3D11Device> Device { get; }

    public ComPtr<ID3D11DeviceContext> DeviceContext { get; }

    public ComPtr<IDXGIFactory1> Factory { get; }

    public ID3D11RenderTargetView* DefaultRenderTargetView => _defaultRenderTargetView.Handle;

    public ID3D11DepthStencilView* DefaultDepthStencilView => _defaultDepthStencilView.Handle;

    public ID3D11Texture2D* DefaultBackBuffer => _defaultBackBuffer.Handle;

    public static D3D11Context Create(IWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        D3D11Api d3d = D3D11Api.GetApi(window);
        DXGI dxgi = DXGI.GetApi(window);
        IntPtr outputWindowHandle = GetOutputWindowHandle(window);
        ComPtr<ID3D11Device> device = default;
        ComPtr<ID3D11DeviceContext> deviceContext = default;
        D3DFeatureLevel featureLevel = D3DFeatureLevel.Level110;
        D3DFeatureLevel[] requestedFeatureLevels =
        [
            D3DFeatureLevel.Level111,
            D3DFeatureLevel.Level110,
        ];

        fixed (D3DFeatureLevel* requestedFeatureLevelsPointer = requestedFeatureLevels)
        {
            ID3D11Device* devicePointer = null;
            ID3D11DeviceContext* deviceContextPointer = null;
            int result = d3d.CreateDevice(
                (IDXGIAdapter*)null,
                D3DDriverType.Hardware,
                IntPtr.Zero,
                (uint)CreateDeviceFlag.BgraSupport,
                requestedFeatureLevelsPointer,
                (uint)requestedFeatureLevels.Length,
                (uint)D3D11Api.SdkVersion,
                &devicePointer,
                &featureLevel,
                &deviceContextPointer);
            D3D11Helpers.ThrowIfFailed(result, "D3D11CreateDevice");
            device = new ComPtr<ID3D11Device>(devicePointer);
            deviceContext = new ComPtr<ID3D11DeviceContext>(deviceContextPointer);
        }

        ComPtr<IDXGIFactory1> factory = dxgi.CreateDXGIFactory1<IDXGIFactory1>();
        SwapChainDesc swapChainDesc = CreateSwapChainDesc(window, outputWindowHandle);
        ComPtr<IDXGISwapChain> swapChain = default;
        D3D11Helpers.ThrowIfFailed(
            factory.Get().CreateSwapChain(device, ref swapChainDesc, ref swapChain),
            "IDXGIFactory1::CreateSwapChain");
        factory.Get().MakeWindowAssociation(outputWindowHandle, 2u);

        D3D11Context context = new(window, d3d, dxgi, device, deviceContext, factory, swapChain);
        Vector2D<int> framebufferSize = window.FramebufferSize;
        context.CreateDefaultTargets(Math.Max(1, framebufferSize.X), Math.Max(1, framebufferSize.Y));
        return context;
    }

    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int safeWidth = Math.Max(1, width);
        int safeHeight = Math.Max(1, height);

        ReleaseDefaultTargets();
        D3D11Helpers.ThrowIfFailed(
            _swapChain.Get().ResizeBuffers(DefaultSwapChainBufferCount, (uint)safeWidth, (uint)safeHeight, Format.FormatB8G8R8A8Unorm, 0),
            "IDXGISwapChain::ResizeBuffers");
        CreateDefaultTargets(safeWidth, safeHeight);
    }

    public void Present()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        D3D11Helpers.ThrowIfFailed(_swapChain.Get().Present(0, 0), "IDXGISwapChain::Present");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseDefaultTargets();
        _swapChain.Dispose();
        Factory.Dispose();
        DeviceContext.Dispose();
        Device.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static SwapChainDesc CreateSwapChainDesc(IWindow window, IntPtr outputWindowHandle)
    {
        Vector2D<int> framebufferSize = window.FramebufferSize;
        return new SwapChainDesc
        {
            BufferDesc = new ModeDesc
            {
                Width = (uint)Math.Max(1, framebufferSize.X),
                Height = (uint)Math.Max(1, framebufferSize.Y),
                RefreshRate = new Rational(60, 1),
                Format = Format.FormatB8G8R8A8Unorm,
                ScanlineOrdering = ModeScanlineOrder.Unspecified,
                Scaling = ModeScaling.Unspecified,
            },
            SampleDesc = new SampleDesc(1, 0),
            BufferUsage = BufferUsageRenderTargetOutput,
            BufferCount = DefaultSwapChainBufferCount,
            OutputWindow = outputWindowHandle,
            Windowed = D3D11Helpers.ToBool32(true),
            SwapEffect = SwapEffect.Discard,
            Flags = 0,
        };
    }

    private static IntPtr GetOutputWindowHandle(IWindow window)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("D3D11 rendering requires a Win32 window handle.");
        }

        GlfwApi glfw = GlfwApi.GetApi();
        GlfwNativeWindow nativeWindow = new(glfw, (GlfwWindowHandle*)window.Handle);
        if (nativeWindow.DXHandle is { } dxHandle && dxHandle != IntPtr.Zero)
        {
            return dxHandle;
        }

        if (nativeWindow.Win32 is { } win32 && win32.Hwnd != IntPtr.Zero)
        {
            return win32.Hwnd;
        }

        throw new D3D11Exception("Unable to resolve the Win32 HWND for the D3D11 swap chain.");
    }

    private void CreateDefaultTargets(int width, int height)
    {
        _defaultBackBuffer = _swapChain.Get().GetBuffer<ID3D11Texture2D>(0);
        D3D11Helpers.ThrowIfFailed(
            Device.Get().CreateRenderTargetView(_defaultBackBuffer, (RenderTargetViewDesc*)null, ref _defaultRenderTargetView),
            "ID3D11Device::CreateRenderTargetView");

        Texture2DDesc depthDesc = new()
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatD32Float,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.DepthStencil,
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        D3D11Helpers.ThrowIfFailed(
            Device.Get().CreateTexture2D(ref depthDesc, (SubresourceData*)null, ref _defaultDepthTexture),
            "ID3D11Device::CreateTexture2D(depth)");
        D3D11Helpers.ThrowIfFailed(
            Device.Get().CreateDepthStencilView(_defaultDepthTexture, (DepthStencilViewDesc*)null, ref _defaultDepthStencilView),
            "ID3D11Device::CreateDepthStencilView");
    }

    private void ReleaseDefaultTargets()
    {
        ID3D11RenderTargetView* nullRenderTarget = null;
        DeviceContext.Get().OMSetRenderTargets(1, &nullRenderTarget, null);
        _defaultDepthStencilView.Dispose();
        _defaultDepthTexture.Dispose();
        _defaultBackBuffer.Dispose();
        _defaultRenderTargetView.Dispose();
        _defaultDepthStencilView = default;
        _defaultDepthTexture = default;
        _defaultBackBuffer = default;
        _defaultRenderTargetView = default;
    }
}
