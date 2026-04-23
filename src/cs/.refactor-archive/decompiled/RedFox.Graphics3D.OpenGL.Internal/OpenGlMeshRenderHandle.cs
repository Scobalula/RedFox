using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.OpenGL.Rendering;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Internal;

internal sealed class OpenGlMeshRenderHandle : IOpenGlSceneNodeRenderHandle, ISceneNodeRenderHandle, IDisposable
{
	private readonly GL _gl;

	private readonly Mesh _mesh;

	private readonly Dictionary<string, uint> _bufferByName = new Dictionary<string, uint>(StringComparer.Ordinal);

	private readonly List<uint> _ownedBuffers = new List<uint>();

	private uint _vertexArrayObject;

	private int _vertexCount;

	private int _indexCount;

	private int _skinInfluenceCount;

	private int _boneCount;

	private Matrix4x4[] _skinTransformsScratch = Array.Empty<Matrix4x4>();

	private bool _disposed;

	public OpenGlRenderLayer Layer => OpenGlRenderLayer.Opaque;

	private bool HasSkinningData => _skinInfluenceCount > 0 && _boneCount > 0 && _bufferByName.ContainsKey("Positions") && _bufferByName.ContainsKey("Normals") && _bufferByName.ContainsKey("BoneIndices") && _bufferByName.ContainsKey("BoneWeights") && _bufferByName.ContainsKey("SkinTransforms") && _bufferByName.ContainsKey("SkinnedPositions") && _bufferByName.ContainsKey("SkinnedNormals");

	public OpenGlMeshRenderHandle(GL gl, Mesh mesh)
	{
		_gl = gl ?? throw new ArgumentNullException("gl");
		_mesh = mesh ?? throw new ArgumentNullException("mesh");
	}

	public bool IsOwnedBy(GL gl)
	{
		ArgumentNullException.ThrowIfNull(gl, "gl");
		return _gl == gl;
	}

	public void Update()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (_mesh.Positions == null || _mesh.Positions.ElementCount == 0)
		{
			ReleaseGpuResources();
		}
		else if (_vertexArrayObject == 0)
		{
			BuildGpuResources();
		}
	}

	public void Render(OpenGlRenderContext context, in CameraView view)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (_vertexArrayObject == 0 || _mesh.Positions == null || _mesh.Positions.ElementCount == 0)
		{
			return;
		}
		OpenGlSurfaceMaterial openGlSurfaceMaterial = ResolveSurfaceMaterial(_mesh, context.Settings);
		Matrix4x4 value = _mesh.GetBindWorldMatrix();
		if (HasSkinningData && context.SkinningComputeProgram != null)
		{
			EnsureSkinTransformsScratchCapacity(_boneCount);
			int num = _mesh.CopySkinTransforms(_skinTransformsScratch);
			if (num >= _boneCount)
			{
				DispatchSkinning(context.SkinningComputeProgram, _skinTransformsScratch.AsSpan(0, _boneCount), context.Settings.SkinningMode);
				value = Matrix4x4.Identity;
			}
		}
		context.MeshShaderProgram.Use();
		context.MeshShaderProgram.SetMatrix4("uModel", value);
		context.MeshShaderProgram.SetMatrix4("uSceneAxis", context.SceneAxisMatrix);
		context.MeshShaderProgram.SetMatrix4("uView", view.ViewMatrix);
		context.MeshShaderProgram.SetMatrix4("uProjection", view.ProjectionMatrix);
		context.MeshShaderProgram.SetVector3("uCameraPosition", view.Position);
		context.MeshShaderProgram.SetVector4("uBaseColor", openGlSurfaceMaterial.BaseColor);
		context.MeshShaderProgram.SetFloat("uMaterialSpecularStrength", openGlSurfaceMaterial.SpecularStrength);
		context.MeshShaderProgram.SetFloat("uMaterialSpecularPower", openGlSurfaceMaterial.SpecularPower);
		Draw();
	}

	public void Release()
	{
		if (!_disposed)
		{
			ReleaseGpuResources();
			_disposed = true;
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_ownedBuffers.Clear();
			_bufferByName.Clear();
			_vertexArrayObject = 0u;
			_vertexCount = 0;
			_indexCount = 0;
			_skinInfluenceCount = 0;
			_boneCount = 0;
			_skinTransformsScratch = Array.Empty<Matrix4x4>();
			_disposed = true;
		}
	}

	private unsafe void BuildGpuResources()
	{
		ArgumentNullException.ThrowIfNull(_mesh.Positions, "_mesh.Positions");
		DataBuffer positions = _mesh.Positions;
		float[] array = BuildPositionBuffer(positions);
		float[] array2 = BuildNormalBuffer(_mesh.Normals, positions.ElementCount);
		uint[] array3 = BuildIndexBuffer(_mesh.FaceIndices);
		uint[] skinIndexData;
		float[] skinWeightData;
		int influenceCount;
		int boneCount;
		bool flag = TryBuildSkinningData(_mesh, positions.ElementCount, out skinIndexData, out skinWeightData, out influenceCount, out boneCount);
		_skinInfluenceCount = influenceCount;
		_boneCount = boneCount;
		float[] data = (flag ? ExpandToVec4(array, 1f) : array);
		float[] data2 = (flag ? ExpandToVec4(array2, 0f) : array2);
		_vertexArrayObject = _gl.GenVertexArray();
		_gl.BindVertexArray(_vertexArrayObject);
		CreateBuffer("SkinnedPositions", BufferTargetARB.ArrayBuffer, data, BufferUsageARB.DynamicDraw);
		uint stride = (flag ? 16u : 12u);
		_gl.EnableVertexAttribArray(0u);
		_gl.VertexAttribPointer(0u, 3, VertexAttribPointerType.Float, normalized: false, stride, null);
		CreateBuffer("SkinnedNormals", BufferTargetARB.ArrayBuffer, data2, BufferUsageARB.DynamicDraw);
		_gl.EnableVertexAttribArray(1u);
		_gl.VertexAttribPointer(1u, 3, VertexAttribPointerType.Float, normalized: false, stride, null);
		if (flag)
		{
			float[] data3 = ExpandToVec4(array, 1f);
			float[] data4 = ExpandToVec4(array2, 0f);
			CreateBuffer("Positions", BufferTargetARB.ShaderStorageBuffer, data3, BufferUsageARB.StaticDraw);
			CreateBuffer("Normals", BufferTargetARB.ShaderStorageBuffer, data4, BufferUsageARB.StaticDraw);
			CreateBuffer("BoneIndices", BufferTargetARB.ShaderStorageBuffer, skinIndexData, BufferUsageARB.StaticDraw);
			CreateBuffer("BoneWeights", BufferTargetARB.ShaderStorageBuffer, skinWeightData, BufferUsageARB.StaticDraw);
			uint num = _gl.GenBuffer();
			_ownedBuffers.Add(num);
			_bufferByName["SkinTransforms"] = num;
			_gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, num);
			nuint size = (nuint)(_boneCount * 16 * 4);
			_gl.BufferData(BufferTargetARB.ShaderStorageBuffer, size, null, BufferUsageARB.DynamicDraw);
		}
		if (array3.Length != 0)
		{
			uint buffer = CreateBuffer("FaceIndices", BufferTargetARB.ElementArrayBuffer, array3, BufferUsageARB.StaticDraw);
			_indexCount = array3.Length;
			_gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffer);
		}
		else
		{
			_indexCount = 0;
		}
		_vertexCount = positions.ElementCount;
		_gl.BindVertexArray(0u);
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0u);
		_gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0u);
	}

	private unsafe void Draw()
	{
		_gl.BindVertexArray(_vertexArrayObject);
		if (_indexCount > 0)
		{
			_gl.DrawElements(PrimitiveType.Triangles, (uint)_indexCount, DrawElementsType.UnsignedInt, null);
		}
		else
		{
			_gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_vertexCount);
		}
		_gl.BindVertexArray(0u);
	}

	private unsafe void DispatchSkinning(OpenGlComputeShaderProgram computeProgram, ReadOnlySpan<Matrix4x4> boneMatrices, SkinningMode skinningMode)
	{
		if (!HasSkinningData || boneMatrices.Length < _boneCount || !_bufferByName.TryGetValue("SkinTransforms", out var value) || value == 0)
		{
			return;
		}
		_gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, value);
		fixed (Matrix4x4* data = boneMatrices)
		{
			_gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(_boneCount * sizeof(Matrix4x4)), data, BufferUsageARB.DynamicDraw);
		}
		foreach (var (blockName, num2) in _bufferByName)
		{
			if (num2 == 0)
			{
				return;
			}
			if (computeProgram.TryGetShaderStorageBlockBinding(blockName, out var binding))
			{
				_gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, binding, num2);
			}
		}
		computeProgram.Use();
		computeProgram.SetInt("VertexCount", _vertexCount);
		computeProgram.SetInt("SkinInfluenceCount", _skinInfluenceCount);
		computeProgram.SetInt("SkinningMode", (int)skinningMode);
		uint groupCountX = (uint)((_vertexCount + 63) / 64);
		computeProgram.Dispatch(groupCountX, 1u, 1u);
		_gl.MemoryBarrier(8193u);
	}

	private unsafe uint CreateBuffer(string name, BufferTargetARB target, float[] data, BufferUsageARB usage)
	{
		uint num = _gl.GenBuffer();
		_ownedBuffers.Add(num);
		_bufferByName[name] = num;
		_gl.BindBuffer(target, num);
		fixed (float* data2 = data)
		{
			_gl.BufferData(target, (nuint)(data.Length * 4), data2, usage);
		}
		return num;
	}

	private unsafe uint CreateBuffer(string name, BufferTargetARB target, uint[] data, BufferUsageARB usage)
	{
		uint num = _gl.GenBuffer();
		_ownedBuffers.Add(num);
		_bufferByName[name] = num;
		_gl.BindBuffer(target, num);
		fixed (uint* data2 = data)
		{
			_gl.BufferData(target, (nuint)(data.Length * 4), data2, usage);
		}
		return num;
	}

	private void ReleaseGpuResources()
	{
		for (int i = 0; i < _ownedBuffers.Count; i++)
		{
			uint num = _ownedBuffers[i];
			if (num != 0)
			{
				_gl.DeleteBuffer(num);
			}
		}
		_ownedBuffers.Clear();
		_bufferByName.Clear();
		if (_vertexArrayObject != 0)
		{
			_gl.DeleteVertexArray(_vertexArrayObject);
			_vertexArrayObject = 0u;
		}
		_vertexCount = 0;
		_indexCount = 0;
		_skinInfluenceCount = 0;
		_boneCount = 0;
		_skinTransformsScratch = Array.Empty<Matrix4x4>();
	}

	private void EnsureSkinTransformsScratchCapacity(int requiredBoneCount)
	{
		if (requiredBoneCount > 0 && _skinTransformsScratch.Length < requiredBoneCount)
		{
			_skinTransformsScratch = new Matrix4x4[requiredBoneCount];
		}
	}

	private static OpenGlSurfaceMaterial ResolveSurfaceMaterial(Mesh mesh, OpenGlRenderSettings settings)
	{
		Vector4 baseColor = Vector4.One;
		float specularStrength = settings.SpecularStrength;
		float specularPower = settings.SpecularPower;
		List<Material> materials = mesh.Materials;
		if (materials != null && materials.Count > 0)
		{
			Material material = mesh.Materials[0];
			if (material.DiffuseColor.HasValue)
			{
				baseColor = material.DiffuseColor.Value;
			}
			if (material.SpecularStrength.HasValue)
			{
				specularStrength = material.SpecularStrength.Value;
			}
			if (material.Shininess.HasValue)
			{
				specularPower = material.Shininess.Value;
			}
		}
		return new OpenGlSurfaceMaterial(baseColor, specularStrength, specularPower);
	}

	private static float[] BuildPositionBuffer(DataBuffer positions)
	{
		float[] array = new float[positions.ElementCount * 3];
		for (int i = 0; i < positions.ElementCount; i++)
		{
			Vector3 vector = positions.GetVector3(i, 0);
			int num = i * 3;
			array[num] = vector.X;
			array[num + 1] = vector.Y;
			array[num + 2] = vector.Z;
		}
		return array;
	}

	private static float[] BuildNormalBuffer(DataBuffer? normals, int vertexCount)
	{
		float[] array = new float[vertexCount * 3];
		for (int i = 0; i < vertexCount; i++)
		{
			Vector3 vector = ResolveNormal(normals, i);
			int num = i * 3;
			array[num] = vector.X;
			array[num + 1] = vector.Y;
			array[num + 2] = vector.Z;
		}
		return array;
	}

	private static bool TryBuildSkinningData(Mesh mesh, int vertexCount, out uint[] skinIndexData, out float[] skinWeightData, out int influenceCount, out int boneCount)
	{
		skinIndexData = Array.Empty<uint>();
		skinWeightData = Array.Empty<float>();
		influenceCount = 0;
		boneCount = 0;
		if (!mesh.HasSkinning || mesh.BoneIndices == null || mesh.BoneWeights == null)
		{
			return false;
		}
		IReadOnlyList<SkeletonBone> skinnedBones = mesh.SkinnedBones;
		if (skinnedBones == null || skinnedBones.Count <= 0)
		{
			return false;
		}
		DataBuffer boneIndices = mesh.BoneIndices;
		DataBuffer boneWeights = mesh.BoneWeights;
		influenceCount = Math.Min(boneIndices.ValueCount, boneWeights.ValueCount);
		boneCount = skinnedBones.Count;
		if (influenceCount <= 0 || boneCount <= 0)
		{
			return false;
		}
		skinIndexData = new uint[vertexCount * influenceCount];
		skinWeightData = new float[vertexCount * influenceCount];
		for (int i = 0; i < vertexCount; i++)
		{
			for (int j = 0; j < influenceCount; j++)
			{
				int num = i * influenceCount + j;
				uint num2 = boneIndices.Get<uint>(i, j, 0);
				if (num2 >= boneCount)
				{
					throw new InvalidDataException($"Mesh '{mesh.Name}' contains an invalid skin index {num2} at vertex {i}.");
				}
				skinIndexData[num] = num2;
				skinWeightData[num] = boneWeights.Get<float>(i, j, 0);
			}
		}
		return true;
	}

	private static float[] ExpandToVec4(float[] source, float w)
	{
		int num = source.Length / 3;
		float[] array = new float[num * 4];
		for (int i = 0; i < num; i++)
		{
			int num2 = i * 3;
			int num3 = i * 4;
			array[num3] = source[num2];
			array[num3 + 1] = source[num2 + 1];
			array[num3 + 2] = source[num2 + 2];
			array[num3 + 3] = w;
		}
		return array;
	}

	private static uint[] BuildIndexBuffer(DataBuffer? faceIndices)
	{
		if (faceIndices == null || faceIndices.ElementCount == 0)
		{
			return Array.Empty<uint>();
		}
		List<uint> list = new List<uint>(faceIndices.ElementCount * faceIndices.ValueCount * faceIndices.ComponentCount);
		for (int i = 0; i < faceIndices.ElementCount; i++)
		{
			for (int j = 0; j < faceIndices.ValueCount; j++)
			{
				for (int k = 0; k < faceIndices.ComponentCount; k++)
				{
					list.Add(faceIndices.Get<uint>(i, j, k));
				}
			}
		}
		return list.ToArray();
	}

	private static Vector3 ResolveNormal(DataBuffer? normals, int vertexIndex)
	{
		if (normals == null || normals.ElementCount == 0 || vertexIndex >= normals.ElementCount)
		{
			return Vector3.UnitY;
		}
		Vector3 vector = normals.GetVector3(vertexIndex, 0);
		if (vector.LengthSquared() < 1E-10f)
		{
			return Vector3.UnitY;
		}
		return Vector3.Normalize(vector);
	}

	void IOpenGlSceneNodeRenderHandle.Render(OpenGlRenderContext context, in CameraView view)
	{
		Render(context, in view);
	}
}
