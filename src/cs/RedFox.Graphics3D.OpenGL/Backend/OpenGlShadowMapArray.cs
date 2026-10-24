using RedFox.Graphics3D.OpenGL.Resources;
using Silk.NET.OpenGL;
using System;

namespace RedFox.Graphics3D.OpenGL;

internal sealed class OpenGlShadowMapArray : IDisposable
{
    internal const int MaxLayers = 4;

    private readonly OpenGlContext	_context;
    private uint					_texture;
    private uint					_framebuffer;
    private int						_layerCount;
    private int						_size;

    public uint	Texture	=> _texture;
    public int	Size	=> _size;

    public OpenGlShadowMapArray(OpenGlContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public bool Begin(int layer, int layerCount, int size, int textureSlot)
    {
        if (layer >= MaxLayers)
        {
            return false;
        }

        GL gl = _context.Gl;
        if (_layerCount < layerCount || _size != size)
        {
            Release();
            Allocate(Math.Min(layerCount, MaxLayers), size, textureSlot);
        }

        gl.ActiveTexture(TextureUnit.Texture0 + textureSlot);
        gl.BindTexture(TextureTarget.Texture2DArray, 0);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        gl.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, _texture, 0, layer);

        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _context.DefaultFramebufferHandle);
            return false;
        }

        _context.SetViewport(_size, _size);
        gl.DepthMask(true);
        gl.ClearDepth(1.0f);
        gl.Clear((uint)ClearBufferMask.DepthBufferBit);
        return true;
    }

    public void End(int textureSlot)
    {
        if (_texture == 0)
        {
            return;
        }

        GL gl = _context.Gl;
        gl.ActiveTexture(TextureUnit.Texture0 + textureSlot);
        gl.BindTexture(TextureTarget.Texture2DArray, _texture);
    }

    public void Dispose()
    {
        Release();
    }

    private void Allocate(int layerCount, int size, int textureSlot)
    {
        GL gl = _context.Gl;

        _texture = gl.GenTexture();
        gl.ActiveTexture(TextureUnit.Texture0 + textureSlot);
        gl.BindTexture(TextureTarget.Texture2DArray, _texture);
        gl.TexStorage3D(TextureTarget.Texture2DArray, 1, SizedInternalFormat.DepthComponent32f, (uint)size, (uint)size, (uint)layerCount);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareMode, (int)GLEnum.CompareRefToTexture);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareFunc, (int)GLEnum.Lequal);

        _framebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        gl.DrawBuffers(1, [GLEnum.None]);
        gl.ReadBuffer(GLEnum.None);
        _layerCount	= layerCount;
        _size		= size;
    }

    private void Release()
    {
        GL gl = _context.Gl;

        if (_framebuffer != 0)
        {
            gl.DeleteFramebuffer(_framebuffer);
            _framebuffer = 0;
        }

        if (_texture != 0)
        {
            gl.DeleteTexture(_texture);
            _texture = 0;
        }

        _layerCount	= 0;
        _size		= 0;
    }
}
