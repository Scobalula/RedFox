using System;
using System.Numerics;

namespace RedFox.Graphics3D.Rendering.Backend;

/// <summary>
/// Represents a backend command list used to record or immediately issue GPU work.
/// </summary>
public interface ICommandList
{
    /// <summary>
    /// Resets the command list to an empty state for a new frame.
    /// </summary>
    void Reset();

    /// <summary>
    /// Sets the active viewport dimensions in pixels.
    /// </summary>
    /// <param name="width">The viewport width in pixels.</param>
    /// <param name="height">The viewport height in pixels.</param>
    void SetViewport(int width, int height);

    /// <summary>
    /// Sets the active render target.
    /// </summary>
    /// <param name="renderTarget">The render target to bind, or <see langword="null"/> for the default target.</param>
    void SetRenderTarget(IGpuRenderTarget? renderTarget);

    /// <summary>
    /// Clears the active render target and depth buffer.
    /// </summary>
    /// <param name="red">The red clear component.</param>
    /// <param name="green">The green clear component.</param>
    /// <param name="blue">The blue clear component.</param>
    /// <param name="alpha">The alpha clear component.</param>
    /// <param name="depth">The depth clear value.</param>
    void ClearRenderTarget(float red, float green, float blue, float alpha, float depth);

    /// <summary>
    /// Sets the active pipeline state.
    /// </summary>
    /// <param name="pipelineState">The pipeline state to bind.</param>
    void SetPipelineState(IGpuPipelineState pipelineState);

    /// <summary>
    /// Binds a GPU buffer to the supplied slot.
    /// </summary>
    /// <param name="slot">The backend-defined binding slot.</param>
    /// <param name="buffer">The buffer to bind.</param>
    void BindBuffer(int slot, IGpuBuffer buffer);

    /// <summary>
    /// Binds a GPU texture to the supplied slot.
    /// </summary>
    /// <param name="slot">The backend-defined binding slot.</param>
    /// <param name="texture">The texture to bind.</param>
    void BindTexture(int slot, IGpuTexture texture);

    /// <summary>
    /// Sets an integer uniform value.
    /// </summary>
    /// <param name="name">The uniform name.</param>
    /// <param name="value">The uniform value.</param>
    void SetUniformInt(ReadOnlySpan<char> name, int value);

    /// <summary>
    /// Sets a floating-point uniform value.
    /// </summary>
    /// <param name="name">The uniform name.</param>
    /// <param name="value">The uniform value.</param>
    void SetUniformFloat(ReadOnlySpan<char> name, float value);

    /// <summary>
    /// Sets a <see cref="Vector2"/> uniform value.
    /// </summary>
    /// <param name="name">The uniform name.</param>
    /// <param name="value">The uniform value.</param>
    void SetUniformVector2(ReadOnlySpan<char> name, Vector2 value);

    /// <summary>
    /// Sets a <see cref="Vector3"/> uniform value.
    /// </summary>
    /// <param name="name">The uniform name.</param>
    /// <param name="value">The uniform value.</param>
    void SetUniformVector3(ReadOnlySpan<char> name, Vector3 value);

    /// <summary>
    /// Sets a <see cref="Vector4"/> uniform value.
    /// </summary>
    /// <param name="name">The uniform name.</param>
    /// <param name="value">The uniform value.</param>
    void SetUniformVector4(ReadOnlySpan<char> name, Vector4 value);

    /// <summary>
    /// Sets a <see cref="Matrix4x4"/> uniform value.
    /// </summary>
    /// <param name="name">The uniform name.</param>
    /// <param name="value">The uniform value.</param>
    void SetUniformMatrix4x4(ReadOnlySpan<char> name, Matrix4x4 value);

    /// <summary>
    /// Draws non-indexed primitives.
    /// </summary>
    /// <param name="vertexCount">The number of vertices to draw.</param>
    /// <param name="startVertex">The starting vertex offset.</param>
    void Draw(int vertexCount, int startVertex);

    /// <summary>
    /// Draws indexed primitives.
    /// </summary>
    /// <param name="indexCount">The number of indices to draw.</param>
    /// <param name="startIndex">The starting index offset.</param>
    /// <param name="baseVertex">The base vertex offset added to each index.</param>
    void DrawIndexed(int indexCount, int startIndex, int baseVertex);

    /// <summary>
    /// Dispatches compute workgroups.
    /// </summary>
    /// <param name="groupCountX">The X workgroup count.</param>
    /// <param name="groupCountY">The Y workgroup count.</param>
    /// <param name="groupCountZ">The Z workgroup count.</param>
    void Dispatch(int groupCountX, int groupCountY, int groupCountZ);

    /// <summary>
    /// Issues a backend-defined memory barrier for recently submitted GPU work.
    /// </summary>
    void MemoryBarrier();

    /// <summary>
    /// Pushes a debug group onto the backend command stream.
    /// </summary>
    /// <param name="name">The debug group name.</param>
    void PushDebugGroup(ReadOnlySpan<char> name);

    /// <summary>
    /// Pops the most recent debug group from the backend command stream.
    /// </summary>
    void PopDebugGroup();
}