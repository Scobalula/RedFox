using RedFox.Graphics3D.OpenGL.Resources;
using RedFox.Graphics3D.Rendering;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

internal sealed class OpenGlVertexArray : IDisposable
{
    private readonly OpenGlContext	_context;
    private OpenGlBuffer?[]			_vertexBuffers = [];
    private OpenGlBuffer?			_indexBuffer;
    private OpenGlPipelineState?	_pipelineState;
    private uint					_handle;

    public OpenGlVertexArray(OpenGlContext context)
    {
        _context	= context;
        _handle		= context.CreateVertexArray();
    }

    public void Bind(OpenGlPipelineState pipelineState, ReadOnlySpan<OpenGlBuffer?> vertexBuffers, OpenGlBuffer? indexBuffer)
    {
        _context.Gl.BindVertexArray(_handle);

        if (ReferenceEquals(_pipelineState, pipelineState) && ReferenceEquals(_indexBuffer, indexBuffer) && vertexBuffers.SequenceEqual(_vertexBuffers))
        {
            return;
        }

        Configure(pipelineState, vertexBuffers, indexBuffer);
    }

    public void Dispose()
    {
        _context.DeleteVertexArray(_handle);
        _handle			= 0;
        _vertexBuffers	= [];
        _indexBuffer	= null;
        _pipelineState	= null;
    }

    private unsafe void Configure(OpenGlPipelineState pipelineState, ReadOnlySpan<OpenGlBuffer?> vertexBuffers, OpenGlBuffer? indexBuffer)
    {
        GL gl = _context.Gl;
        ReadOnlySpan<VertexAttribute> attributes = pipelineState.VertexAttributes;

        for (int slot = vertexBuffers.Length; slot < _vertexBuffers.Length; slot++)
        {
            gl.DisableVertexAttribArray((uint)slot);
        }

        for (int slot = 0; slot < vertexBuffers.Length; slot++)
        {
            if (vertexBuffers[slot] is not { } buffer)
            {
                gl.DisableVertexAttribArray((uint)slot);
                continue;
            }

            VertexAttribute attribute = attributes[slot];
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
            gl.EnableVertexAttribArray((uint)slot);

            if (attribute.Type is VertexAttributeType.Int32 or VertexAttributeType.UInt32 or VertexAttributeType.Int16 or VertexAttributeType.UInt16 or VertexAttributeType.Int8 or VertexAttributeType.UInt8)
            {
                gl.VertexAttribIPointer((uint)slot, attribute.ComponentCount, GetVertexAttribIntegerType(attribute.Type), (uint)attribute.StrideBytes, (void*)attribute.OffsetBytes);
            }
            else
            {
                gl.VertexAttribPointer((uint)slot, attribute.ComponentCount, GetVertexAttribFloatType(attribute.Type), false, (uint)attribute.StrideBytes, (void*)attribute.OffsetBytes);
            }
        }

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer?.Handle ?? 0);
        _vertexBuffers	= vertexBuffers.ToArray();
        _indexBuffer	= indexBuffer;
        _pipelineState	= pipelineState;
    }

    private static VertexAttribIType GetVertexAttribIntegerType(VertexAttributeType vertexAttributeType)
    {
        return vertexAttributeType switch
        {
            VertexAttributeType.Int32 => VertexAttribIType.Int,
            VertexAttributeType.UInt32 => VertexAttribIType.UnsignedInt,
            VertexAttributeType.Int16 => VertexAttribIType.Short,
            VertexAttributeType.UInt16 => VertexAttribIType.UnsignedShort,
            VertexAttributeType.Int8 => VertexAttribIType.Byte,
            VertexAttributeType.UInt8 => VertexAttribIType.UnsignedByte,
            _ => VertexAttribIType.Int,
        };
    }

    private static VertexAttribPointerType GetVertexAttribFloatType(VertexAttributeType vertexAttributeType)
    {
        return vertexAttributeType switch
        {
            VertexAttributeType.Float16 => VertexAttribPointerType.HalfFloat,
            VertexAttributeType.Int32 => VertexAttribPointerType.Int,
            VertexAttributeType.UInt32 => VertexAttribPointerType.UnsignedInt,
            VertexAttributeType.Int16 => VertexAttribPointerType.Short,
            VertexAttributeType.UInt16 => VertexAttribPointerType.UnsignedShort,
            VertexAttributeType.Int8 => VertexAttribPointerType.Byte,
            VertexAttributeType.UInt8 => VertexAttribPointerType.UnsignedByte,
            _ => VertexAttribPointerType.Float,
        };
    }
}
