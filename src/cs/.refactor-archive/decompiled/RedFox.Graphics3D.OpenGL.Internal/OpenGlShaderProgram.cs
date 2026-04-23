using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Internal;

internal sealed class OpenGlShaderProgram : OpenGlProgramBase
{
	public OpenGlShaderProgram(GL gl, string vertexSource, string fragmentSource)
		: base(gl, new(ShaderType, string)[2]
		{
			(ShaderType.VertexShader, vertexSource),
			(ShaderType.FragmentShader, fragmentSource)
		})
	{
	}

	public unsafe void SetMatrix4(string uniformName, Matrix4x4 value)
	{
		int uniformLocation = GetUniformLocation(uniformName);
		if (uniformLocation >= 0)
		{
			float* value2 = (float*)Unsafe.AsPointer(in value.M11);
			base.Gl.UniformMatrix4(uniformLocation, 1u, transpose: false, value2);
		}
	}

	public void SetVector3(string uniformName, Vector3 value)
	{
		int uniformLocation = GetUniformLocation(uniformName);
		if (uniformLocation >= 0)
		{
			base.Gl.Uniform3(uniformLocation, value.X, value.Y, value.Z);
		}
	}

	public void SetVector2(string uniformName, Vector2 value)
	{
		int uniformLocation = GetUniformLocation(uniformName);
		if (uniformLocation >= 0)
		{
			base.Gl.Uniform2(uniformLocation, value.X, value.Y);
		}
	}

	public void SetVector4(string uniformName, Vector4 value)
	{
		int uniformLocation = GetUniformLocation(uniformName);
		if (uniformLocation >= 0)
		{
			base.Gl.Uniform4(uniformLocation, value.X, value.Y, value.Z, value.W);
		}
	}

	public void SetFloat(string uniformName, float value)
	{
		int uniformLocation = GetUniformLocation(uniformName);
		if (uniformLocation >= 0)
		{
			base.Gl.Uniform1(uniformLocation, value);
		}
	}
}
