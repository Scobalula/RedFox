using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Internal;

/// <summary>
/// OpenGL render handle for a grid scene node.
/// Manages VAO/VBO geometry and tracks when rebuild is needed.
/// </summary>
internal sealed class OpenGlGridRenderHandle : IOpenGlSceneNodeRenderHandle, ISceneNodeRenderHandle, IDisposable
{
	private readonly GL _gl;

	private readonly Grid _grid;

	private uint _vao;

	private uint _vbo;

	private int _vertexCount = -1;

	private float _lastSize = float.NaN;

	private float _lastSpacing;

	private int _lastMajorStep;

	private float _lastLineWidth;

	private float _lastEdgeLineWidthScale;

	private Vector4 _lastMinorColor;

	private Vector4 _lastMajorColor;

	private Vector4 _lastAxisXColor;

	private Vector4 _lastAxisZColor;

	private bool _disposed;

	/// <summary>
	/// Gets the vertex array object.
	/// </summary>
	public uint Vao => _vao;

	/// <summary>
	/// Gets the vertex count.
	/// </summary>
	public int VertexCount => _vertexCount;

	public OpenGlRenderLayer Layer => OpenGlRenderLayer.Transparent;

	public OpenGlGridRenderHandle(GL gl, Grid grid)
	{
		_gl = gl ?? throw new ArgumentNullException("gl");
		_grid = grid ?? throw new ArgumentNullException("grid");
	}

	public bool IsOwnedBy(GL gl)
	{
		ArgumentNullException.ThrowIfNull(gl, "gl");
		return _gl == gl;
	}

	/// <summary>
	/// Checks if geometry is current and rebuild is needed.
	/// </summary>
	public bool IsGeometryStale(Grid grid)
	{
		return _vertexCount < 0 || _vao == 0 || _lastSize != grid.Size || _lastSpacing != grid.Spacing || _lastMajorStep != grid.MajorStep || _lastLineWidth != grid.LineWidth || _lastEdgeLineWidthScale != grid.EdgeLineWidthScale || _lastMinorColor != grid.MinorColor || _lastMajorColor != grid.MajorColor || _lastAxisXColor != grid.AxisXColor || _lastAxisZColor != grid.AxisZColor;
	}

	/// <summary>
	/// Rebuilds grid geometry from grid settings.
	/// </summary>
	public void RebuildGeometry(GL gl, Grid grid)
	{
		ThrowIfDisposed();
		float[] array = BuildLineVertices(grid);
		_vertexCount = array.Length / 13;
		if (_vao == 0)
		{
			_vao = gl.GenVertexArray();
			_vbo = gl.GenBuffer();
			gl.BindVertexArray(_vao);
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
			gl.BufferData(BufferTargetARB.ArrayBuffer, array, BufferUsageARB.DynamicDraw);
			gl.EnableVertexAttribArray(0u);
			gl.VertexAttribPointer(0u, 3, VertexAttribPointerType.Float, normalized: false, 52u, 0);
			gl.EnableVertexAttribArray(1u);
			gl.VertexAttribPointer(1u, 3, VertexAttribPointerType.Float, normalized: false, 52u, 12);
			gl.EnableVertexAttribArray(2u);
			gl.VertexAttribPointer(2u, 4, VertexAttribPointerType.Float, normalized: false, 52u, 24);
			gl.EnableVertexAttribArray(3u);
			gl.VertexAttribPointer(3u, 1, VertexAttribPointerType.Float, normalized: false, 52u, 40);
			gl.EnableVertexAttribArray(4u);
			gl.VertexAttribPointer(4u, 1, VertexAttribPointerType.Float, normalized: false, 52u, 44);
			gl.EnableVertexAttribArray(5u);
			gl.VertexAttribPointer(5u, 1, VertexAttribPointerType.Float, normalized: false, 52u, 48);
			gl.BindVertexArray(0u);
		}
		else
		{
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
			gl.BufferData(BufferTargetARB.ArrayBuffer, array, BufferUsageARB.DynamicDraw);
			gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0u);
		}
		_lastSize = grid.Size;
		_lastSpacing = grid.Spacing;
		_lastMajorStep = grid.MajorStep;
		_lastLineWidth = grid.LineWidth;
		_lastEdgeLineWidthScale = grid.EdgeLineWidthScale;
		_lastMinorColor = grid.MinorColor;
		_lastMajorColor = grid.MajorColor;
		_lastAxisXColor = grid.AxisXColor;
		_lastAxisZColor = grid.AxisZColor;
	}

	/// <summary>
	/// Disposes GPU resources.
	/// </summary>
	public void Update()
	{
		ThrowIfDisposed();
		if (IsGeometryStale(_grid))
		{
			RebuildGeometry(_gl, _grid);
		}
	}

	public void Render(OpenGlRenderContext context, in CameraView view)
	{
		ThrowIfDisposed();
		if (_vertexCount > 0 && _vao != 0)
		{
			Grid grid = _grid;
			float num = ((grid.FadeStartDistance > 0f) ? grid.FadeStartDistance : (grid.Size * 0.375f));
			float num2 = ((grid.FadeEndDistance > num) ? grid.FadeEndDistance : (grid.Size * 1.25f));
			context.LineShaderProgram.Use();
			context.LineShaderProgram.SetMatrix4("uModel", Matrix4x4.Identity);
			context.LineShaderProgram.SetMatrix4("uSceneAxis", Matrix4x4.Identity);
			context.LineShaderProgram.SetMatrix4("uView", view.ViewMatrix);
			context.LineShaderProgram.SetMatrix4("uProjection", view.ProjectionMatrix);
			context.LineShaderProgram.SetVector2("uViewportSize", context.ViewportSize);
			context.LineShaderProgram.SetFloat("uLineHalfWidthPx", MathF.Max(grid.LineWidth, 0.75f) * 0.5f);
			context.LineShaderProgram.SetVector3("uCameraPosition", view.Position);
			context.LineShaderProgram.SetFloat("uFadeStartDistance", grid.FadeEnabled ? num : 0f);
			context.LineShaderProgram.SetFloat("uFadeEndDistance", grid.FadeEnabled ? num2 : 0f);
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

	private float[] BuildLineVertices(Grid grid)
	{
		float num = MathF.Max(grid.Size, grid.Spacing);
		float num2 = MathF.Max(grid.Spacing, 0.0001f);
		int num3 = Math.Max(1, grid.MajorStep);
		Vector4 minorColor = grid.MinorColor;
		Vector4 majorColor = grid.MajorColor;
		Vector4 axisXColor = grid.AxisXColor;
		Vector4 axisZColor = grid.AxisZColor;
		int num4 = (int)MathF.Ceiling(num / num2);
		float num5 = (float)num4 * num2;
		int capacity = 2 * (2 * num4 + 1) * 6 * 13;
		List<float> list = new List<float>(capacity);
		for (int i = -num4; i <= num4; i++)
		{
			float x = (float)i * num2;
			int num6 = Math.Abs(i);
			bool flag = i == 0;
			bool flag2 = num6 == num4;
			bool flag3 = num6 == num4 - 1 && grid.EdgeLineWidthScale > 1f;
			bool flag4 = !flag && !flag3 && num6 % num3 == 0;
			Vector4 color = (flag ? axisZColor : (flag4 ? majorColor : minorColor));
			float widthScale = (flag2 ? MathF.Max(grid.EdgeLineWidthScale, 1f) : 1f);
			AddLineSegment(list, new Vector3(x, 0f, 0f - num5), new Vector3(x, 0f, num5), color, widthScale);
		}
		for (int j = -num4; j <= num4; j++)
		{
			float z = (float)j * num2;
			int num7 = Math.Abs(j);
			bool flag5 = j == 0;
			bool flag6 = num7 == num4;
			bool flag7 = num7 == num4 - 1 && grid.EdgeLineWidthScale > 1f;
			bool flag8 = !flag5 && !flag7 && num7 % num3 == 0;
			Vector4 color2 = (flag5 ? axisXColor : (flag8 ? majorColor : minorColor));
			float widthScale2 = (flag6 ? MathF.Max(grid.EdgeLineWidthScale, 1f) : 1f);
			AddLineSegment(list, new Vector3(0f - num5, 0f, z), new Vector3(num5, 0f, z), color2, widthScale2);
		}
		return list.ToArray();
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

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
	}

	void IOpenGlSceneNodeRenderHandle.Render(OpenGlRenderContext context, in CameraView view)
	{
		Render(context, in view);
	}
}
