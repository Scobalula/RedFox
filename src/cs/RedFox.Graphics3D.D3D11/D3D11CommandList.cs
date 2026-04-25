using System.Numerics;
using RedFox.Graphics3D.Rendering.Backend;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents the placeholder D3D11 command list for the backend skeleton.
/// </summary>
public sealed class D3D11CommandList : ICommandList
{
    /// <summary>
    /// Initializes a new instance of the <see cref="D3D11CommandList"/> class.
    /// </summary>
    public D3D11CommandList()
    {
    }

    /// <inheritdoc/>
    public void Reset()
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetViewport(int width, int height)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetRenderTarget(IGpuRenderTarget? renderTarget)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void ClearRenderTarget(float red, float green, float blue, float alpha, float depth)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetPipelineState(IGpuPipelineState pipelineState)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetSceneAxis(Matrix4x4 sceneAxis)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetFrontFaceWinding(FaceWinding faceWinding)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetAmbientColor(Vector3 ambientColor)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetUseViewBasedLighting(bool enabled)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetSkinningMode(SkinningMode skinningMode)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void ResetLights(Vector3 fallbackDirection, Vector3 fallbackColor, float fallbackIntensity)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void AppendLight(Vector3 direction, Vector3 color, float intensity)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void BindBuffer(int slot, IGpuBuffer buffer)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void BindTexture(int slot, IGpuTexture texture)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetUniformInt(ReadOnlySpan<char> name, int value)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetUniformFloat(ReadOnlySpan<char> name, float value)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetUniformVector2(ReadOnlySpan<char> name, Vector2 value)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetUniformVector3(ReadOnlySpan<char> name, Vector3 value)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetUniformVector4(ReadOnlySpan<char> name, Vector4 value)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void SetUniformMatrix4x4(ReadOnlySpan<char> name, Matrix4x4 value)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void Draw(int vertexCount, int startVertex)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void DrawIndexed(int indexCount, int startIndex, int baseVertex)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void Dispatch(int groupCountX, int groupCountY, int groupCountZ)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void MemoryBarrier()
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void PushDebugGroup(ReadOnlySpan<char> name)
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }

    /// <inheritdoc/>
    public void PopDebugGroup()
    {
        throw D3D11BackendSkeleton.NotImplemented();
    }
}