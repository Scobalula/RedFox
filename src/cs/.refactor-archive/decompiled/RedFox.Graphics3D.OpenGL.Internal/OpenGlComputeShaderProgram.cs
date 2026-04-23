using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Internal;

internal sealed class OpenGlComputeShaderProgram(GL gl, string computeSource) : OpenGlProgramBase(gl, new(ShaderType, string)[1] { (ShaderType.ComputeShader, computeSource) })
{
	private const uint InvalidProgramResourceIndex = uint.MaxValue;

	private readonly Dictionary<string, uint> _cachedStorageBindings = new Dictionary<string, uint>(StringComparer.Ordinal);

	private readonly HashSet<string> _missingStorageBindings = new HashSet<string>(StringComparer.Ordinal);

	public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
	{
		ThrowIfDisposed();
		base.Gl.DispatchCompute(groupCountX, groupCountY, groupCountZ);
	}

	public unsafe bool TryGetShaderStorageBlockBinding(string blockName, out uint binding)
	{
		ThrowIfDisposed();
		ArgumentException.ThrowIfNullOrWhiteSpace(blockName, "blockName");
		if (_cachedStorageBindings.TryGetValue(blockName, out var value))
		{
			binding = value;
			return true;
		}
		if (_missingStorageBindings.Contains(blockName))
		{
			binding = 0u;
			return false;
		}
		uint programResourceIndex = base.Gl.GetProgramResourceIndex(base.Handle, ProgramInterface.ShaderStorageBlock, blockName);
		if (programResourceIndex == uint.MaxValue)
		{
			_missingStorageBindings.Add(blockName);
			binding = 0u;
			return false;
		}
		int num = 0;
		int num2 = 1;
		ProgramResourceProperty programResourceProperty = ProgramResourceProperty.BufferBinding;
		base.Gl.GetProgramResource(base.Handle, ProgramInterface.ShaderStorageBlock, programResourceIndex, (uint)num2, &programResourceProperty, (uint)num2, null, &num);
		if (num < 0)
		{
			_missingStorageBindings.Add(blockName);
			binding = 0u;
			return false;
		}
		binding = (uint)num;
		_cachedStorageBindings[blockName] = binding;
		return true;
	}
}
