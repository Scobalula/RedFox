using System;
using System.Collections.Generic;
using System.Numerics;
using RedFox.Graphics3D.OpenGL.Rendering;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Internal;

internal sealed class OpenGlSkeletonBoneRenderHandle : IOpenGlSceneNodeRenderHandle, ISceneNodeRenderHandle, IDisposable
{
	private readonly SkeletonBone _bone;

	private readonly GL _gl;

	private readonly OpenGlRenderSettings _settings;

	private uint _vao;

	private uint _vbo;

	private int _vertexCount = -1;

	private bool _disposed;

	public OpenGlRenderLayer Layer => OpenGlRenderLayer.Overlay;

	public OpenGlSkeletonBoneRenderHandle(GL gl, SkeletonBone bone, OpenGlRenderSettings settings)
	{
		_gl = gl ?? throw new ArgumentNullException("gl");
		_bone = bone ?? throw new ArgumentNullException("bone");
		_settings = settings ?? throw new ArgumentNullException("settings");
	}

	public bool IsOwnedBy(GL gl)
	{
		ArgumentNullException.ThrowIfNull(gl, "gl");
		return _gl == gl;
	}

	public void Update()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		float[] array = BuildLineVertices(_bone, _settings);
		_vertexCount = array.Length / 13;
		if (_vao == 0)
		{
			_vao = _gl.GenVertexArray();
			_vbo = _gl.GenBuffer();
			_gl.BindVertexArray(_vao);
			_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
			_gl.BufferData(BufferTargetARB.ArrayBuffer, array, BufferUsageARB.DynamicDraw);
			_gl.EnableVertexAttribArray(0u);
			_gl.VertexAttribPointer(0u, 3, VertexAttribPointerType.Float, normalized: false, 52u, 0);
			_gl.EnableVertexAttribArray(1u);
			_gl.VertexAttribPointer(1u, 3, VertexAttribPointerType.Float, normalized: false, 52u, 12);
			_gl.EnableVertexAttribArray(2u);
			_gl.VertexAttribPointer(2u, 4, VertexAttribPointerType.Float, normalized: false, 52u, 24);
			_gl.EnableVertexAttribArray(3u);
			_gl.VertexAttribPointer(3u, 1, VertexAttribPointerType.Float, normalized: false, 52u, 40);
			_gl.EnableVertexAttribArray(4u);
			_gl.VertexAttribPointer(4u, 1, VertexAttribPointerType.Float, normalized: false, 52u, 44);
			_gl.EnableVertexAttribArray(5u);
			_gl.VertexAttribPointer(5u, 1, VertexAttribPointerType.Float, normalized: false, 52u, 48);
			_gl.BindVertexArray(0u);
		}
		else
		{
			_gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
			_gl.BufferData(BufferTargetARB.ArrayBuffer, array, BufferUsageARB.DynamicDraw);
			_gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0u);
		}
	}

	public void Render(OpenGlRenderContext context, in CameraView view)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (_vertexCount > 0 && _vao != 0)
		{
			context.LineShaderProgram.Use();
			context.LineShaderProgram.SetMatrix4("uModel", _bone.GetActiveWorldMatrix());
			context.LineShaderProgram.SetMatrix4("uSceneAxis", context.SceneAxisMatrix);
			context.LineShaderProgram.SetMatrix4("uView", view.ViewMatrix);
			context.LineShaderProgram.SetMatrix4("uProjection", view.ProjectionMatrix);
			context.LineShaderProgram.SetVector2("uViewportSize", context.ViewportSize);
			context.LineShaderProgram.SetFloat("uLineHalfWidthPx", MathF.Max(_settings.BoneLineWidth, 0.75f) * 0.5f);
			context.LineShaderProgram.SetVector3("uCameraPosition", view.Position);
			context.LineShaderProgram.SetFloat("uFadeStartDistance", 0f);
			context.LineShaderProgram.SetFloat("uFadeEndDistance", 0f);
			context.Gl.BindVertexArray(_vao);
			context.Gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_vertexCount);
			context.Gl.BindVertexArray(0u);
		}
	}

	public void Release()
	{
		if (!_disposed)
		{
			if (_vao != 0)
			{
				_gl.DeleteVertexArray(_vao);
				_vao = 0u;
			}
			if (_vbo != 0)
			{
				_gl.DeleteBuffer(_vbo);
				_vbo = 0u;
			}
			_vertexCount = -1;
			_disposed = true;
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_vao = 0u;
			_vbo = 0u;
			_vertexCount = -1;
			_disposed = true;
		}
	}

	private static float[] BuildLineVertices(SkeletonBone bone, OpenGlRenderSettings settings)
	{
		float num = MathF.Max(settings.BoneAxisSize, 0f);
		List<float> list = new List<float>(312);
		Vector3 start = Vector3.Zero;
		Matrix4x4 result = default(Matrix4x4);
		bool flag = bone.Parent != null && Matrix4x4.Invert(bone.GetActiveLocalMatrix(), out result);
		if (flag)
		{
			start = Vector3.Transform(Vector3.Zero, result);
			float num2 = start.Length();
			if (num2 > 1E-06f)
			{
				float y = num2 * MathF.Max(settings.BoneAxisScaleFromParent, 0f);
				num = MathF.Max(num, y);
			}
		}
		if (settings.BoneAxisMaxSize > 0f)
		{
			num = MathF.Min(num, settings.BoneAxisMaxSize);
		}
		if (num > 0f)
		{
			AddLineSegment(list, Vector3.Zero, Vector3.UnitX * num, settings.BoneAxisXColor, 1f);
			AddLineSegment(list, Vector3.Zero, Vector3.UnitY * num, settings.BoneAxisYColor, 1f);
			AddLineSegment(list, Vector3.Zero, Vector3.UnitZ * num, settings.BoneAxisZColor, 1f);
		}
		if (flag)
		{
			Vector4 color = (settings.UseBoneNameHashColor ? BuildBoneNameColor(bone.Name, settings) : settings.BoneConnectionColor);
			AddLineSegment(list, start, Vector3.Zero, color, 1f);
		}
		return list.ToArray();
	}

	private static Vector4 BuildBoneNameColor(string boneName, OpenGlRenderSettings settings)
	{
		string text = (string.IsNullOrWhiteSpace(boneName) ? "Bone" : boneName);
		uint num = 2166136261u;
		for (int i = 0; i < text.Length; i++)
		{
			num ^= text[i];
			num *= 16777619;
		}
		float hue = (float)(num % 360) / 360f;
		float saturation = Math.Clamp(settings.BoneNameColorSaturation, 0f, 1f);
		float value = Math.Clamp(settings.BoneNameColorValue, 0f, 1f);
		Vector3 vector = HsvToRgb(hue, saturation, value);
		return new Vector4(vector.X, vector.Y, vector.Z, settings.BoneConnectionColor.W);
	}

	private static Vector3 HsvToRgb(float hue, float saturation, float value)
	{
		if (saturation <= 0f)
		{
			return new Vector3(value, value, value);
		}
		float num = (hue - MathF.Floor(hue)) * 6f;
		int num2 = (int)num;
		float num3 = num - (float)num2;
		float num4 = value * (1f - saturation);
		float num5 = value * (1f - saturation * num3);
		float num6 = value * (1f - saturation * (1f - num3));
		if (1 == 0)
		{
		}
		Vector3 result = num2 switch
		{
			0 => new Vector3(value, num6, num4), 
			1 => new Vector3(num5, value, num4), 
			2 => new Vector3(num4, value, num6), 
			3 => new Vector3(num4, num5, value), 
			4 => new Vector3(num6, num4, value), 
			_ => new Vector3(value, num4, num5), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static void AddLineSegment(List<float> vertices, Vector3 start, Vector3 end, Vector4 color, float widthScale)
	{
		AddExpandedVertex(vertices, start, end, color, 0f, -1f, widthScale);
		AddExpandedVertex(vertices, start, end, color, 0f, 1f, widthScale);
		AddExpandedVertex(vertices, start, end, color, 1f, 1f, widthScale);
		AddExpandedVertex(vertices, start, end, color, 0f, -1f, widthScale);
		AddExpandedVertex(vertices, start, end, color, 1f, 1f, widthScale);
		AddExpandedVertex(vertices, start, end, color, 1f, -1f, widthScale);
	}

	private static void AddExpandedVertex(List<float> vertices, Vector3 start, Vector3 end, Vector4 color, float along, float side, float widthScale)
	{
		vertices.Add(start.X);
		vertices.Add(start.Y);
		vertices.Add(start.Z);
		vertices.Add(end.X);
		vertices.Add(end.Y);
		vertices.Add(end.Z);
		vertices.Add(color.X);
		vertices.Add(color.Y);
		vertices.Add(color.Z);
		vertices.Add(color.W);
		vertices.Add(along);
		vertices.Add(side);
		vertices.Add(widthScale);
	}

	void IOpenGlSceneNodeRenderHandle.Render(OpenGlRenderContext context, in CameraView view)
	{
		Render(context, in view);
	}
}
