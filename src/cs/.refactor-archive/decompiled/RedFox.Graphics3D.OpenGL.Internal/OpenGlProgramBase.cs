using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Internal;

internal abstract class OpenGlProgramBase : IDisposable
{
	private readonly Dictionary<string, int> _uniformLocations;

	private bool _disposed;

	protected GL Gl { get; }

	protected uint Handle { get; private set; }

	protected OpenGlProgramBase(GL gl, (ShaderType ShaderType, string Source)[] shaders)
	{
		ArgumentNullException.ThrowIfNull(gl, "gl");
		ArgumentNullException.ThrowIfNull(shaders, "shaders");
		if (shaders.Length == 0)
		{
			throw new ArgumentException("At least one shader source is required.", "shaders");
		}
		Gl = gl;
		_uniformLocations = new Dictionary<string, int>(StringComparer.Ordinal);
		uint[] array = new uint[shaders.Length];
		int num = 0;
		try
		{
			for (int i = 0; i < shaders.Length; i++)
			{
				var (shaderType, text) = shaders[i];
				ArgumentException.ThrowIfNullOrWhiteSpace(text, "source");
				uint num2 = CompileShader(shaderType, text);
				array[num++] = num2;
			}
			Handle = Gl.CreateProgram();
			for (int j = 0; j < num; j++)
			{
				Gl.AttachShader(Handle, array[j]);
			}
			Gl.LinkProgram(Handle);
			Gl.GetProgram(Handle, GLEnum.LinkStatus, out var @params);
			if (@params == 0)
			{
				string programInfoLog = Gl.GetProgramInfoLog(Handle);
				throw new InvalidOperationException("OpenGL program link failed: " + programInfoLog);
			}
		}
		catch
		{
			if (Handle != 0)
			{
				Gl.DeleteProgram(Handle);
				Handle = 0u;
			}
			throw;
		}
		finally
		{
			for (int k = 0; k < num; k++)
			{
				uint num3 = array[k];
				if (num3 != 0)
				{
					if (Handle != 0)
					{
						Gl.DetachShader(Handle, num3);
					}
					Gl.DeleteShader(num3);
				}
			}
		}
	}

	public void Use()
	{
		ThrowIfDisposed();
		Gl.UseProgram(Handle);
	}

	public void SetInt(string uniformName, int value)
	{
		int uniformLocation = GetUniformLocation(uniformName);
		if (uniformLocation >= 0)
		{
			Gl.Uniform1(uniformLocation, value);
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			if (Handle != 0)
			{
				Gl.DeleteProgram(Handle);
				Handle = 0u;
			}
			_disposed = true;
		}
	}

	protected void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
	}

	protected int GetUniformLocation(string uniformName)
	{
		ThrowIfDisposed();
		if (_uniformLocations.TryGetValue(uniformName, out var value))
		{
			return value;
		}
		value = Gl.GetUniformLocation(Handle, uniformName);
		_uniformLocations.Add(uniformName, value);
		return value;
	}

	private uint CompileShader(ShaderType shaderType, string source)
	{
		uint num = Gl.CreateShader(shaderType);
		Gl.ShaderSource(num, source);
		Gl.CompileShader(num);
		Gl.GetShader(num, ShaderParameterName.CompileStatus, out var @params);
		if (@params == 0)
		{
			string shaderInfoLog = Gl.GetShaderInfoLog(num);
			Gl.DeleteShader(num);
			throw new InvalidOperationException($"OpenGL shader compile failed ({shaderType}): {shaderInfoLog}");
		}
		return num;
	}
}
