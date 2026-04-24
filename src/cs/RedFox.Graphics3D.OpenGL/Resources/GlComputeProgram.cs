using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;

namespace RedFox.Graphics3D.OpenGL.Resources;

/// <summary>
/// Compiled OpenGL compute shader program. Exposes <see cref="Dispatch"/> and
/// helpers for resolving shader-storage block bindings.
/// </summary>
internal sealed class GlComputeProgram : GlProgramBase
{
    private const uint InvalidProgramResourceIndex = 0xFFFFFFFFu;
    private readonly Dictionary<string, uint> _cachedStorageBindings = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingStorageBindings = new(StringComparer.Ordinal);

    /// <summary>
    /// Compiles and links a compute-shader program.
    /// </summary>
    /// <param name="gl">The owning GL context.</param>
    /// <param name="computeSource">The compute-shader GLSL source.</param>
    public GlComputeProgram(GL gl, string computeSource)
        : base(gl, new[] { (ShaderType.ComputeShader, computeSource) })
    {
    }

    /// <summary>
    /// Dispatches the compute shader with the supplied workgroup counts.
    /// </summary>
    /// <param name="groupCountX">Workgroup count along the X dimension.</param>
    /// <param name="groupCountY">Workgroup count along the Y dimension.</param>
    /// <param name="groupCountZ">Workgroup count along the Z dimension.</param>
    public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        ThrowIfDisposed();
        Gl.DispatchCompute(groupCountX, groupCountY, groupCountZ);
    }

    /// <summary>
    /// Resolves the binding point of a named shader-storage block, caching the result.
    /// </summary>
    /// <param name="blockName">The shader-storage block name.</param>
    /// <param name="binding">The resolved binding point when present.</param>
    /// <returns><see langword="true"/> when the block was resolved; otherwise <see langword="false"/>.</returns>
    public unsafe bool TryGetShaderStorageBlockBinding(string blockName, out uint binding)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(blockName);

        if (_cachedStorageBindings.TryGetValue(blockName, out uint cachedBinding))
        {
            binding = cachedBinding;
            return true;
        }

        if (_missingStorageBindings.Contains(blockName))
        {
            binding = 0;
            return false;
        }

        uint resourceIndex = Gl.GetProgramResourceIndex(Handle, ProgramInterface.ShaderStorageBlock, blockName);
        if (resourceIndex == InvalidProgramResourceIndex)
        {
            _missingStorageBindings.Add(blockName);
            binding = 0;
            return false;
        }

        int bindingValue = 0;
        int propertyCount = 1;
        ProgramResourceProperty property = ProgramResourceProperty.BufferBinding;
        Gl.GetProgramResource(
            Handle,
            ProgramInterface.ShaderStorageBlock,
            resourceIndex,
            (uint)propertyCount,
            &property,
            (uint)propertyCount,
            null,
            &bindingValue);

        if (bindingValue < 0)
        {
            _missingStorageBindings.Add(blockName);
            binding = 0;
            return false;
        }

        binding = (uint)bindingValue;
        _cachedStorageBindings[blockName] = binding;
        return true;
    }
}
