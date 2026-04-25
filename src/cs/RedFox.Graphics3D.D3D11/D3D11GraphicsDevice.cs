using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Materials;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using D3D11Blend = Silk.NET.Direct3D11.Blend;
using D3D11BlendOp = Silk.NET.Direct3D11.BlendOp;
using D3D11BufferUsage = Silk.NET.Direct3D11.Usage;
using D3D11ComparisonFunc = Silk.NET.Direct3D11.ComparisonFunc;
using D3D11CullMode = Silk.NET.Direct3D11.CullMode;
using D3D11DepthWriteMask = Silk.NET.Direct3D11.DepthWriteMask;
using D3D11FillMode = Silk.NET.Direct3D11.FillMode;
using BackendBlendOp = RedFox.Graphics3D.Rendering.Backend.BlendOp;
using BackendCullMode = RedFox.Graphics3D.Rendering.Backend.CullMode;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents the concrete Direct3D 11 graphics device implementation.
/// </summary>
public sealed unsafe class D3D11GraphicsDevice : IGraphicsDevice
{
    private readonly List<D3D11CommandList> _commandLists = [];
    private readonly D3D11Context _context;
    private bool _disposed;

    internal D3D11GraphicsDevice(D3D11Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        MaterialTypes = new D3D11MaterialTypeRegistry();
    }

    /// <inheritdoc/>
    public bool SupportsCompute => true;

    /// <inheritdoc/>
    public IMaterialTypeRegistry MaterialTypes { get; }

    /// <inheritdoc/>
    public IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage)
    {
        return CreateBuffer(sizeBytes, stride, usage, ReadOnlySpan<byte>.Empty);
    }

    /// <inheritdoc/>
    public unsafe IGpuBuffer CreateBuffer(int sizeBytes, int stride, BufferUsage usage, ReadOnlySpan<byte> initialData)
    {
        ThrowIfDisposed();

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        }

        if (stride <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }

        BufferDesc desc = new()
        {
            ByteWidth = usage.HasFlag(BufferUsage.Uniform) ? D3D11Helpers.AlignTo16(sizeBytes) : checked((uint)sizeBytes),
            Usage = IsDynamicBuffer(usage) ? D3D11BufferUsage.Dynamic : D3D11BufferUsage.Default,
            BindFlags = GetBindFlags(usage),
            CPUAccessFlags = IsDynamicBuffer(usage) ? (uint)CpuAccessFlag.Write : 0,
            MiscFlags = HasStructuredView(usage) ? (uint)ResourceMiscFlag.BufferAllowRawViews : 0,
            StructureByteStride = 0,
        };

        ComPtr<ID3D11Buffer> buffer = default;
        string createBufferContext = $"ID3D11Device::CreateBuffer(size={sizeBytes}, stride={stride}, usage={usage}, bind=0x{desc.BindFlags:X}, misc=0x{desc.MiscFlags:X})";
        if (initialData.IsEmpty)
        {
            D3D11Helpers.ThrowIfFailed(
                _context.Device.Get().CreateBuffer(ref desc, (SubresourceData*)null, ref buffer),
                createBufferContext);
        }
        else
        {
            if (initialData.Length > sizeBytes)
            {
                throw new ArgumentException("Initial buffer data exceeds the requested buffer size.", nameof(initialData));
            }

            fixed (byte* initialDataPointer = initialData)
            {
                SubresourceData subresourceData = new()
                {
                    PSysMem = initialDataPointer,
                };
                D3D11Helpers.ThrowIfFailed(
                    _context.Device.Get().CreateBuffer(ref desc, ref subresourceData, ref buffer),
                    createBufferContext);
            }
        }

        CreateBufferViews(buffer, sizeBytes, stride, usage, out ComPtr<ID3D11ShaderResourceView> shaderResourceView, out ComPtr<ID3D11UnorderedAccessView> unorderedAccessView);
        return new D3D11Buffer(buffer, shaderResourceView, unorderedAccessView, sizeBytes, stride, usage);
    }

    /// <inheritdoc/>
    public unsafe void UpdateBuffer(IGpuBuffer buffer, ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        D3D11Buffer d3dBuffer = buffer as D3D11Buffer
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11Buffer)}.");
        if (data.Length > d3dBuffer.SizeBytes)
        {
            throw new ArgumentException("Buffer update data exceeds the allocated buffer size.", nameof(data));
        }

        if (data.IsEmpty)
        {
            return;
        }

        fixed (byte* dataPointer = data)
        {
            if (IsDynamicBuffer(d3dBuffer.Usage))
            {
                MappedSubresource mappedSubresource = default;
                D3D11Helpers.ThrowIfFailed(
                    _context.DeviceContext.Get().Map((ID3D11Resource*)d3dBuffer.Handle, 0, Map.WriteDiscard, 0, ref mappedSubresource),
                    "ID3D11DeviceContext::Map");
                System.Buffer.MemoryCopy(dataPointer, mappedSubresource.PData, d3dBuffer.SizeBytes, data.Length);
                _context.DeviceContext.Get().Unmap((ID3D11Resource*)d3dBuffer.Handle, 0);
                return;
            }

            _context.DeviceContext.Get().UpdateSubresource((ID3D11Resource*)d3dBuffer.Handle, 0, (Box*)null, dataPointer, 0, 0);
        }
    }

    /// <inheritdoc/>
    public IGpuShader CreateShader(ReadOnlySpan<byte> utf8Source, ShaderStage stage)
    {
        ThrowIfDisposed();
        if (utf8Source.IsEmpty)
        {
            throw new ArgumentException("Shader source cannot be empty.", nameof(utf8Source));
        }

        return new D3D11Shader(CompileShader(utf8Source, stage), stage);
    }

    internal IGpuShader CreateShaderFromFile(string shaderPath, ShaderStage stage)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderPath);
        byte[] source = File.ReadAllBytes(shaderPath);
        return new D3D11Shader(CompileShaderFromFile(source, stage, shaderPath), stage);
    }

    /// <inheritdoc/>
    public IGpuPipelineState CreatePipelineState(
        IGpuShader vertexShader,
        IGpuShader fragmentShader,
        ReadOnlySpan<VertexAttribute> vertexAttributes,
        BackendCullMode cullMode,
        FaceWinding faceWinding,
        bool wireframe,
        bool blend,
        BlendFactor sourceBlendFactor,
        BlendFactor destinationBlendFactor,
        BackendBlendOp blendOperation,
        bool depthTest,
        bool depthWrite,
        CompareFunc depthCompareFunc,
        PrimitiveTopology primitiveTopology)
    {
        ThrowIfDisposed();

        D3D11Shader d3dVertexShader = vertexShader as D3D11Shader
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11Shader)} for vertex shader.");
        D3D11Shader d3dFragmentShader = fragmentShader as D3D11Shader
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11Shader)} for fragment shader.");

        if (d3dVertexShader.Stage != ShaderStage.Vertex)
        {
            throw new InvalidOperationException("The supplied vertex shader is not a vertex-stage shader.");
        }

        if (d3dFragmentShader.Stage != ShaderStage.Fragment)
        {
            throw new InvalidOperationException("The supplied fragment shader is not a fragment-stage shader.");
        }

        ComPtr<ID3D11VertexShader> d3dVertexShaderHandle = default;
        ComPtr<ID3D11PixelShader> d3dPixelShaderHandle = default;
        ComPtr<ID3D11InputLayout> inputLayout = default;
        ComPtr<ID3D11RasterizerState> counterClockwiseRasterizerState = default;
        ComPtr<ID3D11RasterizerState> clockwiseRasterizerState = default;
        ComPtr<ID3D11BlendState> blendState = default;
        ComPtr<ID3D11DepthStencilState> depthStencilState = default;

        ReadOnlySpan<byte> vertexBytecode = d3dVertexShader.Bytecode;
        fixed (byte* vertexBytecodePointer = vertexBytecode)
        {
            ID3D11VertexShader* vertexShaderPointer = null;
            D3D11Helpers.ThrowIfFailed(
                _context.Device.Get().CreateVertexShader(vertexBytecodePointer, (nuint)vertexBytecode.Length, (ID3D11ClassLinkage*)null, &vertexShaderPointer),
                "ID3D11Device::CreateVertexShader");
            d3dVertexShaderHandle = new ComPtr<ID3D11VertexShader>(vertexShaderPointer);
            inputLayout = CreateInputLayout(vertexAttributes, vertexBytecodePointer, vertexBytecode.Length);
        }

        ReadOnlySpan<byte> fragmentBytecode = d3dFragmentShader.Bytecode;
        fixed (byte* fragmentBytecodePointer = fragmentBytecode)
        {
            ID3D11PixelShader* pixelShaderPointer = null;
            D3D11Helpers.ThrowIfFailed(
                _context.Device.Get().CreatePixelShader(fragmentBytecodePointer, (nuint)fragmentBytecode.Length, (ID3D11ClassLinkage*)null, &pixelShaderPointer),
                "ID3D11Device::CreatePixelShader");
            d3dPixelShaderHandle = new ComPtr<ID3D11PixelShader>(pixelShaderPointer);
        }

        counterClockwiseRasterizerState = CreateRasterizerState(cullMode, wireframe, true);
        clockwiseRasterizerState = CreateRasterizerState(cullMode, wireframe, false);
        blendState = CreateBlendState(blend, sourceBlendFactor, destinationBlendFactor, blendOperation);
        depthStencilState = CreateDepthStencilState(depthTest, depthWrite, depthCompareFunc);

        return new D3D11PipelineState(
            d3dVertexShaderHandle,
            d3dPixelShaderHandle,
            inputLayout,
            counterClockwiseRasterizerState,
            clockwiseRasterizerState,
            blendState,
            depthStencilState,
            vertexAttributes,
            primitiveTopology);
    }

    /// <inheritdoc/>
    public IGpuPipelineState CreatePipelineState(IGpuShader computeShader)
    {
        ThrowIfDisposed();

        D3D11Shader d3dComputeShader = computeShader as D3D11Shader
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11Shader)} for compute shader.");

        if (d3dComputeShader.Stage != ShaderStage.Compute)
        {
            throw new InvalidOperationException("The supplied compute shader is not a compute-stage shader.");
        }

        ComPtr<ID3D11ComputeShader> d3dComputeShaderHandle = default;
        ReadOnlySpan<byte> computeBytecode = d3dComputeShader.Bytecode;
        fixed (byte* computeBytecodePointer = computeBytecode)
        {
            ID3D11ComputeShader* computeShaderPointer = null;
            D3D11Helpers.ThrowIfFailed(
                _context.Device.Get().CreateComputeShader(computeBytecodePointer, (nuint)computeBytecode.Length, (ID3D11ClassLinkage*)null, &computeShaderPointer),
                "ID3D11Device::CreateComputeShader");
            d3dComputeShaderHandle = new ComPtr<ID3D11ComputeShader>(computeShaderPointer);
        }

        return new D3D11PipelineState(d3dComputeShaderHandle);
    }

    /// <inheritdoc/>
    public IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage)
    {
        return CreateTexture(width, height, format, usage, ReadOnlySpan<byte>.Empty);
    }

    /// <inheritdoc/>
    public unsafe IGpuTexture CreateTexture(int width, int height, ImageFormat format, TextureUsage usage, ReadOnlySpan<byte> pixels)
    {
        ThrowIfDisposed();

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Format dxgiFormat = D3D11Helpers.GetDxgiFormat(format);
        if (dxgiFormat == Format.FormatUnknown)
        {
            throw new NotSupportedException($"D3D11 does not support texture format '{format}'.");
        }

        Texture2DDesc desc = new()
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = dxgiFormat,
            SampleDesc = new SampleDesc(1, 0),
            Usage = D3D11BufferUsage.Default,
            BindFlags = GetTextureBindFlags(usage),
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };

        ComPtr<ID3D11Texture2D> texture = default;
        if (pixels.IsEmpty)
        {
            D3D11Helpers.ThrowIfFailed(
                _context.Device.Get().CreateTexture2D(ref desc, (SubresourceData*)null, ref texture),
                "ID3D11Device::CreateTexture2D");
        }
        else
        {
            fixed (byte* pixelPointer = pixels)
            {
                SubresourceData subresourceData = new()
                {
                    PSysMem = pixelPointer,
                    SysMemPitch = (uint)(width * GetBytesPerPixel(format)),
                };
                D3D11Helpers.ThrowIfFailed(
                    _context.Device.Get().CreateTexture2D(ref desc, ref subresourceData, ref texture),
                    "ID3D11Device::CreateTexture2D");
            }
        }

        return new D3D11Texture(texture, width, height, format, usage);
    }

    /// <inheritdoc/>
    public bool SupportsFormat(ImageFormat format, TextureUsage usage)
    {
        return D3D11Helpers.GetDxgiFormat(format) != Format.FormatUnknown;
    }

    /// <inheritdoc/>
    public IGpuRenderTarget CreateRenderTarget(IGpuTexture colorTexture, IGpuTexture? depthTexture)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(colorTexture);

        D3D11Texture d3dColorTexture = colorTexture as D3D11Texture
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11Texture)} for color texture.");
        D3D11Texture? d3dDepthTexture = depthTexture as D3D11Texture;

        ComPtr<ID3D11RenderTargetView> renderTargetView = default;
        ComPtr<ID3D11DepthStencilView> depthStencilView = default;
        ID3D11RenderTargetView* renderTargetViewPointer = null;
        D3D11Helpers.ThrowIfFailed(
            _context.Device.Get().CreateRenderTargetView((ID3D11Resource*)d3dColorTexture.Handle, (RenderTargetViewDesc*)null, &renderTargetViewPointer),
            "ID3D11Device::CreateRenderTargetView");
        renderTargetView = new ComPtr<ID3D11RenderTargetView>(renderTargetViewPointer);

        if (d3dDepthTexture is not null)
        {
            ID3D11DepthStencilView* depthStencilViewPointer = null;
            D3D11Helpers.ThrowIfFailed(
                _context.Device.Get().CreateDepthStencilView((ID3D11Resource*)d3dDepthTexture.Handle, (DepthStencilViewDesc*)null, &depthStencilViewPointer),
                "ID3D11Device::CreateDepthStencilView");
            depthStencilView = new ComPtr<ID3D11DepthStencilView>(depthStencilViewPointer);
        }

        return new D3D11RenderTarget(renderTargetView, depthStencilView);
    }

    /// <inheritdoc/>
    public ICommandList CreateCommandList()
    {
        ThrowIfDisposed();
        D3D11CommandList commandList = new(_context);
        _commandLists.Add(commandList);
        return commandList;
    }

    /// <inheritdoc/>
    public void Submit(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        for (int commandListIndex = 0; commandListIndex < _commandLists.Count; commandListIndex++)
        {
            _commandLists[commandListIndex].Dispose();
        }

        _commandLists.Clear();
        _context.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static bool IsDynamicBuffer(BufferUsage usage)
    {
        if (HasStructuredView(usage))
        {
            return false;
        }

        return usage.HasFlag(BufferUsage.DynamicWrite) || usage.HasFlag(BufferUsage.CpuWrite);
    }

    private static bool HasStructuredView(BufferUsage usage)
    {
        return usage.HasFlag(BufferUsage.ShaderStorage) || usage.HasFlag(BufferUsage.Structured);
    }

    private static uint GetBindFlags(BufferUsage usage)
    {
        uint flags = 0;
        if (usage.HasFlag(BufferUsage.Vertex))
        {
            flags |= (uint)BindFlag.VertexBuffer;
        }

        if (usage.HasFlag(BufferUsage.Index))
        {
            flags |= (uint)BindFlag.IndexBuffer;
        }

        if (usage.HasFlag(BufferUsage.Uniform))
        {
            flags |= (uint)BindFlag.ConstantBuffer;
        }

        if (HasStructuredView(usage))
        {
            flags |= (uint)BindFlag.ShaderResource;
            flags |= (uint)BindFlag.UnorderedAccess;
        }

        return flags == 0 ? (uint)BindFlag.VertexBuffer : flags;
    }

    private void CreateBufferViews(
        ComPtr<ID3D11Buffer> buffer,
        int sizeBytes,
        int stride,
        BufferUsage usage,
        out ComPtr<ID3D11ShaderResourceView> shaderResourceView,
        out ComPtr<ID3D11UnorderedAccessView> unorderedAccessView)
    {
        shaderResourceView = default;
        unorderedAccessView = default;
        if (!HasStructuredView(usage))
        {
            return;
        }

        if (sizeBytes % sizeof(uint) != 0)
        {
            throw new ArgumentException("Raw D3D11 buffer views require a byte size evenly divisible by four.", nameof(sizeBytes));
        }

        uint elementCount = checked((uint)(sizeBytes / sizeof(uint)));
        ShaderResourceViewDesc shaderResourceViewDesc = new()
        {
            Format = Format.FormatR32Typeless,
            ViewDimension = D3DSrvDimension.D3D11SrvDimensionBufferex,
            BufferEx = new BufferexSrv
            {
                FirstElement = 0,
                NumElements = elementCount,
                Flags = (uint)BufferexSrvFlag.Raw,
            },
        };

        ID3D11ShaderResourceView* shaderResourceViewPointer = null;
        D3D11Helpers.ThrowIfFailed(
            _context.Device.Get().CreateShaderResourceView((ID3D11Resource*)buffer.Handle, ref shaderResourceViewDesc, &shaderResourceViewPointer),
            "ID3D11Device::CreateShaderResourceView(buffer)");
        shaderResourceView = new ComPtr<ID3D11ShaderResourceView>(shaderResourceViewPointer);

        UnorderedAccessViewDesc unorderedAccessViewDesc = new()
        {
            Format = Format.FormatR32Typeless,
            ViewDimension = UavDimension.Buffer,
            Buffer = new BufferUav
            {
                FirstElement = 0,
                NumElements = elementCount,
                Flags = (uint)BufferUavFlag.Raw,
            },
        };

        ID3D11UnorderedAccessView* unorderedAccessViewPointer = null;
        D3D11Helpers.ThrowIfFailed(
            _context.Device.Get().CreateUnorderedAccessView((ID3D11Resource*)buffer.Handle, ref unorderedAccessViewDesc, &unorderedAccessViewPointer),
            "ID3D11Device::CreateUnorderedAccessView(buffer)");
        unorderedAccessView = new ComPtr<ID3D11UnorderedAccessView>(unorderedAccessViewPointer);
    }

    private static uint GetTextureBindFlags(TextureUsage usage)
    {
        uint flags = 0;
        if (usage.HasFlag(TextureUsage.Sampled))
        {
            flags |= (uint)BindFlag.ShaderResource;
        }

        if (usage.HasFlag(TextureUsage.RenderTarget))
        {
            flags |= (uint)BindFlag.RenderTarget;
        }

        if (usage.HasFlag(TextureUsage.DepthStencil))
        {
            flags |= (uint)BindFlag.DepthStencil;
        }

        return flags;
    }

    private static int GetBytesPerPixel(ImageFormat format)
    {
        return format switch
        {
            ImageFormat.D24UnormS8Uint => 4,
            ImageFormat.D32Float => 4,
            ImageFormat.R8G8B8A8Unorm => 4,
            ImageFormat.R8G8B8A8UnormSrgb => 4,
            ImageFormat.B8G8R8A8Unorm => 4,
            ImageFormat.B8G8R8A8UnormSrgb => 4,
            _ => 4,
        };
    }

    private static byte[] CompileShader(ReadOnlySpan<byte> source, ShaderStage stage)
    {
        D3DCompiler compiler = D3DCompiler.GetApi();
        ID3D10Blob* shaderBlob = null;
        ID3D10Blob* errorBlob = null;
        byte[] entryPointBytes = Encoding.UTF8.GetBytes("Main\0");
        byte[] profileBytes = Encoding.UTF8.GetBytes(D3D11Helpers.GetShaderProfile(stage) + "\0");

        fixed (byte* sourcePointer = source)
        fixed (byte* entryPointPointer = entryPointBytes)
        fixed (byte* profilePointer = profileBytes)
        {
            int result = compiler.Compile(
                sourcePointer,
                (nuint)source.Length,
                (byte*)null,
                (D3DShaderMacro*)null,
                (ID3DInclude*)null,
                entryPointPointer,
                profilePointer,
                0,
                0,
                &shaderBlob,
                &errorBlob);

            if (result < 0)
            {
                string message = ReadBlobText(errorBlob);
                if (errorBlob is not null)
                {
                    errorBlob->Release();
                }
                throw new D3D11Exception(string.IsNullOrWhiteSpace(message)
                    ? $"HLSL compilation failed with HRESULT 0x{result:X8}."
                    : message);
            }
        }

        try
        {
            ReadOnlySpan<byte> bytecode = new(shaderBlob->GetBufferPointer(), checked((int)shaderBlob->GetBufferSize()));
            return bytecode.ToArray();
        }
        finally
        {
            shaderBlob->Release();
            if (errorBlob is not null)
            {
                errorBlob->Release();
            }
        }
    }

    private static byte[] CompileShaderFromFile(ReadOnlySpan<byte> source, ShaderStage stage, string shaderPath)
    {
        string hash = ComputeShaderHash(source, stage);
        string shaderDirectory = Path.GetDirectoryName(shaderPath) ?? AppContext.BaseDirectory;
        string shaderName = Path.GetFileNameWithoutExtension(shaderPath);
        string cachePath = Path.Combine(shaderDirectory, $"{shaderName}.{hash}.dxbc");
        if (File.Exists(cachePath))
        {
            return File.ReadAllBytes(cachePath);
        }

        byte[] bytecode = CompileShader(source, stage);
        try
        {
            File.WriteAllBytes(cachePath, bytecode);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return bytecode;
    }

    private static string ComputeShaderHash(ReadOnlySpan<byte> source, ShaderStage stage)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(source);
        hash.AppendData(Encoding.UTF8.GetBytes(D3D11Helpers.GetShaderProfile(stage)));
        byte[] digest = hash.GetHashAndReset();
        return Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant();
    }

    private ComPtr<ID3D11InputLayout> CreateInputLayout(ReadOnlySpan<VertexAttribute> vertexAttributes, byte* vertexBytecodePointer, int vertexBytecodeLength)
    {
        InputElementDesc[] inputElements = new InputElementDesc[vertexAttributes.Length];
        IntPtr[] semanticPointers = new IntPtr[vertexAttributes.Length];
        try
        {
            for (int attributeIndex = 0; attributeIndex < vertexAttributes.Length; attributeIndex++)
            {
                VertexAttribute attribute = vertexAttributes[attributeIndex];
                Format attributeFormat = D3D11Helpers.GetAttributeFormat(attribute);
                if (attributeFormat == Format.FormatUnknown)
                {
                    throw new NotSupportedException($"D3D11 does not support vertex attribute '{attribute.Name}' with type '{attribute.Type}' and {attribute.ComponentCount} component(s).");
                }

                string semanticName = D3D11Helpers.GetSemanticName(attribute, out uint semanticIndex);
                semanticPointers[attributeIndex] = Marshal.StringToHGlobalAnsi(semanticName);
                inputElements[attributeIndex] = new InputElementDesc
                {
                    SemanticName = (byte*)semanticPointers[attributeIndex],
                    SemanticIndex = semanticIndex,
                    Format = attributeFormat,
                    InputSlot = (uint)attributeIndex,
                    AlignedByteOffset = (uint)attribute.OffsetBytes,
                    InputSlotClass = InputClassification.PerVertexData,
                    InstanceDataStepRate = 0,
                };
            }

            ComPtr<ID3D11InputLayout> inputLayout = default;
            fixed (InputElementDesc* inputElementsPointer = inputElements)
            {
                D3D11Helpers.ThrowIfFailed(
                    _context.Device.Get().CreateInputLayout(inputElementsPointer, (uint)inputElements.Length, vertexBytecodePointer, (nuint)vertexBytecodeLength, ref inputLayout),
                    "ID3D11Device::CreateInputLayout");
            }

            return inputLayout;
        }
        finally
        {
            for (int semanticPointerIndex = 0; semanticPointerIndex < semanticPointers.Length; semanticPointerIndex++)
            {
                if (semanticPointers[semanticPointerIndex] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(semanticPointers[semanticPointerIndex]);
                }
            }
        }
    }

    private ComPtr<ID3D11RasterizerState> CreateRasterizerState(BackendCullMode cullMode, bool wireframe, bool frontCounterClockwise)
    {
        RasterizerDesc desc = new()
        {
            FillMode = wireframe ? D3D11FillMode.Wireframe : D3D11FillMode.Solid,
            CullMode = cullMode switch
            {
                BackendCullMode.Front => D3D11CullMode.Front,
                BackendCullMode.Back => D3D11CullMode.Back,
                _ => D3D11CullMode.None,
            },
            FrontCounterClockwise = D3D11Helpers.ToBool32(frontCounterClockwise),
            DepthClipEnable = D3D11Helpers.ToBool32(true),
        };

        ComPtr<ID3D11RasterizerState> rasterizerState = default;
        D3D11Helpers.ThrowIfFailed(
            _context.Device.Get().CreateRasterizerState(ref desc, ref rasterizerState),
            "ID3D11Device::CreateRasterizerState");
        return rasterizerState;
    }

    private ComPtr<ID3D11BlendState> CreateBlendState(bool blend, BlendFactor sourceBlendFactor, BlendFactor destinationBlendFactor, BackendBlendOp blendOperation)
    {
        RenderTargetBlendDesc renderTargetBlendDesc = new()
        {
            BlendEnable = D3D11Helpers.ToBool32(blend),
            SrcBlend = GetBlendFactor(sourceBlendFactor),
            DestBlend = GetBlendFactor(destinationBlendFactor),
            BlendOp = GetBlendOperation(blendOperation),
            SrcBlendAlpha = D3D11Blend.One,
            DestBlendAlpha = D3D11Blend.Zero,
            BlendOpAlpha = D3D11BlendOp.Add,
            RenderTargetWriteMask = (byte)ColorWriteEnable.All,
        };

        BlendDesc desc = new()
        {
            AlphaToCoverageEnable = D3D11Helpers.ToBool32(false),
            IndependentBlendEnable = D3D11Helpers.ToBool32(false),
        };
        desc.RenderTarget.Element0 = renderTargetBlendDesc;

        ComPtr<ID3D11BlendState> blendState = default;
        D3D11Helpers.ThrowIfFailed(
            _context.Device.Get().CreateBlendState(ref desc, ref blendState),
            "ID3D11Device::CreateBlendState");
        return blendState;
    }

    private ComPtr<ID3D11DepthStencilState> CreateDepthStencilState(bool depthTest, bool depthWrite, CompareFunc depthCompareFunc)
    {
        DepthStencilDesc desc = new()
        {
            DepthEnable = D3D11Helpers.ToBool32(depthTest),
            DepthWriteMask = depthWrite ? D3D11DepthWriteMask.All : D3D11DepthWriteMask.Zero,
            DepthFunc = GetDepthFunction(depthCompareFunc),
            StencilEnable = D3D11Helpers.ToBool32(false),
        };

        ComPtr<ID3D11DepthStencilState> depthStencilState = default;
        D3D11Helpers.ThrowIfFailed(
            _context.Device.Get().CreateDepthStencilState(ref desc, ref depthStencilState),
            "ID3D11Device::CreateDepthStencilState");
        return depthStencilState;
    }

    private static string ReadBlobText(ID3D10Blob* blob)
    {
        if (blob is null)
        {
            return string.Empty;
        }

        ReadOnlySpan<byte> bytes = new(blob->GetBufferPointer(), checked((int)blob->GetBufferSize()));
        return Encoding.UTF8.GetString(bytes).TrimEnd('\0', '\r', '\n');
    }

    private static D3D11Blend GetBlendFactor(BlendFactor blendFactor)
    {
        return blendFactor switch
        {
            BlendFactor.Zero => D3D11Blend.Zero,
            BlendFactor.SourceColor => D3D11Blend.SrcColor,
            BlendFactor.InverseSourceColor => D3D11Blend.InvSrcColor,
            BlendFactor.SourceAlpha => D3D11Blend.SrcAlpha,
            BlendFactor.InverseSourceAlpha => D3D11Blend.InvSrcAlpha,
            BlendFactor.DestinationColor => D3D11Blend.DestColor,
            BlendFactor.InverseDestinationColor => D3D11Blend.InvDestColor,
            BlendFactor.DestinationAlpha => D3D11Blend.DestAlpha,
            BlendFactor.InverseDestinationAlpha => D3D11Blend.InvDestAlpha,
            _ => D3D11Blend.One,
        };
    }

    private static D3D11BlendOp GetBlendOperation(BackendBlendOp blendOperation)
    {
        return blendOperation switch
        {
            BackendBlendOp.Subtract => D3D11BlendOp.Subtract,
            BackendBlendOp.ReverseSubtract => D3D11BlendOp.RevSubtract,
            BackendBlendOp.Minimum => D3D11BlendOp.Min,
            BackendBlendOp.Maximum => D3D11BlendOp.Max,
            _ => D3D11BlendOp.Add,
        };
    }

    private static D3D11ComparisonFunc GetDepthFunction(CompareFunc compareFunc)
    {
        return compareFunc switch
        {
            CompareFunc.Never => D3D11ComparisonFunc.Never,
            CompareFunc.Less => D3D11ComparisonFunc.Less,
            CompareFunc.Equal => D3D11ComparisonFunc.Equal,
            CompareFunc.Greater => D3D11ComparisonFunc.Greater,
            CompareFunc.GreaterOrEqual => D3D11ComparisonFunc.GreaterEqual,
            CompareFunc.NotEqual => D3D11ComparisonFunc.NotEqual,
            CompareFunc.Always => D3D11ComparisonFunc.Always,
            _ => D3D11ComparisonFunc.LessEqual,
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}