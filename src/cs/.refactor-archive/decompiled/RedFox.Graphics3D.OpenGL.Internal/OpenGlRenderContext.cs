using System.Numerics;
using RedFox.Graphics3D.OpenGL.Rendering;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Internal;

internal readonly record struct OpenGlRenderContext(GL Gl, OpenGlShaderProgram MeshShaderProgram, OpenGlComputeShaderProgram? SkinningComputeProgram, OpenGlShaderProgram LineShaderProgram, Vector2 ViewportSize, Matrix4x4 SceneAxisMatrix, OpenGlRenderSettings Settings);
