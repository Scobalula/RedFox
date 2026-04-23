using System;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Internal;

internal interface IOpenGlSceneNodeRenderHandle : ISceneNodeRenderHandle, IDisposable
{
	OpenGlRenderLayer Layer { get; }

	bool IsOwnedBy(GL gl);

	void Render(OpenGlRenderContext context, in CameraView view);
}
