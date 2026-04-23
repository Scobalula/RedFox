using System.Numerics;
using RedFox.Graphics3D.OpenGL.Rendering;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Internal;

internal readonly record struct OpenGlLineRenderContext(GL Gl, OpenGlShaderProgram ShaderProgram, Vector2 ViewportSize, Matrix4x4 SceneAxisMatrix, OpenGlRenderSettings Settings);
