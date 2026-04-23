using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.OpenGL.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Silk.NET.OpenGL;

namespace RedFox.Rendering.OpenGL.Handles;

/// <summary>
/// OpenGL render handle for a <see cref="Mesh"/> scene node. Owns the mesh's GPU buffers
/// and exposes both <see cref="DispatchSkinning"/> (compute pass) and
/// <see cref="DrawOpaque"/> (geometry pass).
/// </summary>
internal sealed class OpenGlMeshHandle : ISceneNodeRenderHandle
{
    private readonly OpenGlContext _context;

    private readonly Dictionary<string, uint> _bufferByName = new(StringComparer.Ordinal);
    private readonly List<uint> _ownedBuffers = new();

    public Mesh Mesh { get; private set; }


    private uint _vertexArrayObject;
    private int _vertexCount;
    private int _indexCount;
    private int _skinInfluenceCount;
    private int _boneCount;
    private Matrix4x4[] _skinTransformsScratch = Array.Empty<Matrix4x4>();
    private bool _disposed;

    public OpenGlMeshHandle(OpenGlContext context, Mesh mesh)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
    }

    public bool IsOwnedBy(OpenGlContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ReferenceEquals(_context, context);
    }

    public bool HasGeometry => _vertexArrayObject != 0 && _vertexCount > 0;

    public void Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Mesh.Positions is null || Mesh.Positions.ElementCount == 0)
        {
            ReleaseGpuResources();
            return;
        }

        if (_vertexArrayObject != 0)
        {
            return;
        }

        BuildGpuResources();
    }

    public bool HasSkinningData =>
        _skinInfluenceCount > 0
        && _boneCount > 0
        && _bufferByName.ContainsKey("Positions")
        && _bufferByName.ContainsKey("Normals")
        && _bufferByName.ContainsKey("BoneIndices")
        && _bufferByName.ContainsKey("BoneWeights")
        && _bufferByName.ContainsKey("SkinTransforms")
        && _bufferByName.ContainsKey("SkinnedPositions")
        && _bufferByName.ContainsKey("SkinnedNormals");

    /// <summary>
    /// Dispatches the GPU skinning compute shader for this mesh. Returns <see langword="true"/>
    /// when skinning was performed (so the geometry pass should treat <see cref="Mesh.GetBindWorldMatrix"/>
    /// as identity).
    /// </summary>
    public unsafe bool DispatchSkinning(GlComputeProgram computeProgram, SkinningMode skinningMode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!HasSkinningData)
        {
            return false;
        }

        EnsureSkinTransformsScratchCapacity(_boneCount);
        int copiedTransforms = Mesh.CopySkinTransforms(_skinTransformsScratch);
        if (copiedTransforms < _boneCount)
        {
            return false;
        }

        if (!_bufferByName.TryGetValue("SkinTransforms", out uint boneTransformsBuffer) || boneTransformsBuffer == 0)
        {
            return false;
        }

        GL gl = _context.Gl;

        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, boneTransformsBuffer);
        fixed (Matrix4x4* matrixPointer = _skinTransformsScratch)
        {
            gl.BufferData(
                BufferTargetARB.ShaderStorageBuffer,
                (nuint)(_boneCount * sizeof(Matrix4x4)),
                matrixPointer,
                BufferUsageARB.DynamicDraw);
        }

        foreach ((string bufferName, uint bufferHandle) in _bufferByName)
        {
            if (bufferHandle == 0)
            {
                return false;
            }

            if (!computeProgram.TryGetShaderStorageBlockBinding(bufferName, out uint shaderBindingPoint))
            {
                continue;
            }

            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, shaderBindingPoint, bufferHandle);
        }

        computeProgram.Use();
        computeProgram.SetInt("VertexCount", _vertexCount);
        computeProgram.SetInt("SkinInfluenceCount", _skinInfluenceCount);
        computeProgram.SetInt("SkinningMode", (int)skinningMode);

        uint dispatchX = (uint)((_vertexCount + 63) / 64);
        computeProgram.Dispatch(dispatchX, 1, 1);
        _context.StorageMemoryBarrier();
        return true;
    }

    /// <summary>
    /// Draws the mesh using the supplied shared mesh shader program. Caller is responsible
    /// for setting per-frame uniforms (view/projection/lights). This method sets per-mesh
    /// uniforms (model, base color, specular) and issues the draw call.
    /// </summary>
    public unsafe void DrawOpaque(GlShaderProgram meshShaderProgram, OpenGlRenderSettings settings, Matrix4x4 sceneAxisMatrix, in CameraView view, bool skinned)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!HasGeometry)
        {
            return;
        }

        OpenGlSurfaceMaterial surfaceMaterial = ResolveSurfaceMaterial(Mesh, settings);
        Matrix4x4 modelMatrix = skinned ? Matrix4x4.Identity : Mesh.GetBindWorldMatrix();

        meshShaderProgram.Use();
        meshShaderProgram.SetMatrix4("uModel", modelMatrix);
        meshShaderProgram.SetMatrix4("uSceneAxis", sceneAxisMatrix);
        meshShaderProgram.SetMatrix4("uView", view.ViewMatrix);
        meshShaderProgram.SetMatrix4("uProjection", view.ProjectionMatrix);
        meshShaderProgram.SetVector3("uCameraPosition", view.Position);
        meshShaderProgram.SetVector4("uBaseColor", surfaceMaterial.BaseColor);
        meshShaderProgram.SetFloat("uMaterialSpecularStrength", surfaceMaterial.SpecularStrength);
        meshShaderProgram.SetFloat("uMaterialSpecularPower", surfaceMaterial.SpecularPower);

        GL gl = _context.Gl;
        gl.BindVertexArray(_vertexArrayObject);

        if (_indexCount > 0)
        {
            gl.DrawElements(PrimitiveType.Triangles, (uint)_indexCount, DrawElementsType.UnsignedInt, null);
        }
        else
        {
            gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_vertexCount);
        }

        gl.BindVertexArray(0);
    }

    public void Release()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseGpuResources();
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _ownedBuffers.Clear();
        _bufferByName.Clear();
        _vertexArrayObject = 0;
        _vertexCount = 0;
        _indexCount = 0;
        _skinInfluenceCount = 0;
        _boneCount = 0;
        _skinTransformsScratch = Array.Empty<Matrix4x4>();
        _disposed = true;
    }

    private unsafe void BuildGpuResources()
    {
        if (Mesh.Positions is not { ElementCount: > 0 } positions)
            return;
        if (Mesh.FaceIndices is not { ElementCount: > 0 } faceIndices)
            return;

        Mesh.GenerateNormals(preserveExisting: true);


        ReadOnlySpan<float> positionData = positions.AsReadOnlySpan<float>();
        ReadOnlySpan<float> normalData = Mesh.Normals!.AsSpan<float>();
        ReadOnlySpan<uint> indexData = faceIndices.AsReadOnlySpan<uint>();

        bool hasSkinning = TryBuildSkinningData(Mesh, positions.ElementCount, out uint[] skinIndexData, out float[] skinWeightData, out int influenceCount, out int boneCount);
        _skinInfluenceCount = influenceCount;
        _boneCount = boneCount;

        GL gl = _context.Gl;
        _vertexArrayObject = _context.CreateVertexArray();
        gl.BindVertexArray(_vertexArrayObject);

        CreateBuffer("SkinnedPositions", BufferTargetARB.ArrayBuffer, positionData, BufferUsageARB.DynamicDraw);
        uint outputStride = 3u * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, outputStride, (void*)0);

        CreateBuffer("SkinnedNormals", BufferTargetARB.ArrayBuffer, normalData, BufferUsageARB.DynamicDraw);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, outputStride, (void*)0);

        if (hasSkinning)
        {
            CreateBuffer("Positions", BufferTargetARB.ShaderStorageBuffer, positionData, BufferUsageARB.StaticDraw);
            CreateBuffer("Normals", BufferTargetARB.ShaderStorageBuffer, normalData, BufferUsageARB.StaticDraw);
            CreateBuffer("BoneIndices", BufferTargetARB.ShaderStorageBuffer, skinIndexData, BufferUsageARB.StaticDraw);
            CreateBuffer("BoneWeights", BufferTargetARB.ShaderStorageBuffer, skinWeightData, BufferUsageARB.StaticDraw);

            uint boneTransformsBuffer = _context.CreateBuffer();
            _ownedBuffers.Add(boneTransformsBuffer);
            _bufferByName["SkinTransforms"] = boneTransformsBuffer;
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, boneTransformsBuffer);
            nuint boneMatrixBufferSize = (nuint)(_boneCount * 16 * sizeof(float));
            gl.BufferData(BufferTargetARB.ShaderStorageBuffer, boneMatrixBufferSize, null, BufferUsageARB.DynamicDraw);
        }

        if (!indexData.IsEmpty)
        {
            uint indexBuffer = CreateBuffer("FaceIndices", BufferTargetARB.ElementArrayBuffer, indexData, BufferUsageARB.StaticDraw);
            _indexCount = indexData.Length;
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);
        }
        else
        {
            _indexCount = 0;
        }

        _vertexCount = positions.ElementCount;

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
    }

    private unsafe uint CreateBuffer(string name, BufferTargetARB target, ReadOnlySpan<float> data, BufferUsageARB usage)
    {
        uint bufferHandle = _context.CreateBuffer();
        _ownedBuffers.Add(bufferHandle);
        _bufferByName[name] = bufferHandle;

        GL gl = _context.Gl;
        gl.BindBuffer(target, bufferHandle);
        fixed (float* pointer = data)
        {
            gl.BufferData(target, (nuint)(data.Length * sizeof(float)), pointer, usage);
        }

        return bufferHandle;
    }

    private unsafe uint CreateBuffer(string name, BufferTargetARB target, ReadOnlySpan<uint> data, BufferUsageARB usage)
    {
        uint bufferHandle = _context.CreateBuffer();
        _ownedBuffers.Add(bufferHandle);
        _bufferByName[name] = bufferHandle;

        GL gl = _context.Gl;
        gl.BindBuffer(target, bufferHandle);
        fixed (uint* pointer = data)
        {
            gl.BufferData(target, (nuint)(data.Length * sizeof(uint)), pointer, usage);
        }

        return bufferHandle;
    }

    private void ReleaseGpuResources()
    {
        for (int i = 0; i < _ownedBuffers.Count; i++)
        {
            _context.DeleteBuffer(_ownedBuffers[i]);
        }

        _ownedBuffers.Clear();
        _bufferByName.Clear();

        if (_vertexArrayObject != 0)
        {
            _context.DeleteVertexArray(_vertexArrayObject);
            _vertexArrayObject = 0;
        }

        _vertexCount = 0;
        _indexCount = 0;
        _skinInfluenceCount = 0;
        _boneCount = 0;
        _skinTransformsScratch = Array.Empty<Matrix4x4>();
    }

    private void EnsureSkinTransformsScratchCapacity(int requiredBoneCount)
    {
        if (requiredBoneCount <= 0)
        {
            return;
        }

        if (_skinTransformsScratch.Length < requiredBoneCount)
        {
            _skinTransformsScratch = new Matrix4x4[requiredBoneCount];
        }
    }

    private static OpenGlSurfaceMaterial ResolveSurfaceMaterial(Mesh mesh, OpenGlRenderSettings settings)
    {
        Vector4 baseColor = Vector4.One;
        float specularStrength = settings.SpecularStrength;
        float specularPower = settings.SpecularPower;

        if (mesh.Materials is { Count: > 0 })
        {
            Material firstMaterial = mesh.Materials[0];
            if (firstMaterial.DiffuseColor.HasValue)
            {
                baseColor = firstMaterial.DiffuseColor.Value;
            }

            if (firstMaterial.SpecularStrength.HasValue)
            {
                specularStrength = firstMaterial.SpecularStrength.Value;
            }

            if (firstMaterial.Shininess.HasValue)
            {
                specularPower = firstMaterial.Shininess.Value;
            }
        }

        return new OpenGlSurfaceMaterial(baseColor, specularStrength, specularPower);
    }

    private static float[] CreateDefaultNormalBuffer(int vertexCount)
    {
        float[] output = new float[vertexCount * 3];

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            int componentOffset = vertexIndex * 3;
            output[componentOffset + 1] = 1.0f;
        }

        return output;
    }

    private static bool TryBuildSkinningData(
        Mesh mesh,
        int vertexCount,
        out uint[] skinIndexData,
        out float[] skinWeightData,
        out int influenceCount,
        out int boneCount)
    {
        skinIndexData = Array.Empty<uint>();
        skinWeightData = Array.Empty<float>();
        influenceCount = 0;
        boneCount = 0;

        if (!mesh.HasSkinning || mesh.BoneIndices is null || mesh.BoneWeights is null)
        {
            return false;
        }

        if (mesh.SkinnedBones is not { Count: > 0 } skinnedBones)
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

        ReadOnlySpan<uint> boneIndexComponents = boneIndices.AsReadOnlySpan<uint>();
        ReadOnlySpan<float> boneWeightComponents = boneWeights.AsReadOnlySpan<float>();
        int boneIndexElementWidth = boneIndices.ValueCount * boneIndices.ComponentCount;
        int boneWeightElementWidth = boneWeights.ValueCount * boneWeights.ComponentCount;

        skinIndexData = new uint[vertexCount * influenceCount];
        skinWeightData = new float[vertexCount * influenceCount];

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            int boneIndexBase = vertexIndex * boneIndexElementWidth;
            int boneWeightBase = vertexIndex * boneWeightElementWidth;
            int outputBase = vertexIndex * influenceCount;

            boneIndexComponents.Slice(boneIndexBase, influenceCount).CopyTo(skinIndexData.AsSpan(outputBase, influenceCount));
            boneWeightComponents.Slice(boneWeightBase, influenceCount).CopyTo(skinWeightData.AsSpan(outputBase, influenceCount));
        }

        for (int packedIndex = 0; packedIndex < skinIndexData.Length; packedIndex++)
        {
            if (skinIndexData[packedIndex] >= boneCount)
            {
                int vertexIndex = packedIndex / influenceCount;
                throw new InvalidDataException($"Mesh '{mesh.Name}' contains an invalid skin index {skinIndexData[packedIndex]} at vertex {vertexIndex}.");
            }
        }

        return true;
    }

}
