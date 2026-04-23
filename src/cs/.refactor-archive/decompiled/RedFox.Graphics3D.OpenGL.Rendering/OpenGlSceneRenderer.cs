using System;
using System.Collections.Generic;
using System.Numerics;
using RedFox.Graphics3D.OpenGL.Internal;
using RedFox.Graphics3D.Rendering;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace RedFox.Graphics3D.OpenGL.Rendering;

/// <summary>
/// Renders RedFox scene meshes with a basic untextured OpenGL pipeline.
/// </summary>
public sealed class OpenGlSceneRenderer : SceneRenderer
{
	private enum OpenGlFramePass
	{
		Opaque,
		Transparent,
		Overlay
	}

	private readonly record struct OpenGlFrameQueues(List<IOpenGlSceneNodeRenderHandle> Opaque, List<OpenGlTransparentQueueEntry> Transparent, List<IOpenGlSceneNodeRenderHandle> Overlay);

	private readonly record struct OpenGlTransparentQueueEntry(IOpenGlSceneNodeRenderHandle Handle, float DistanceSquaredToCamera);

	private const int RequiredOpenGlMajorVersion = 4;

	private const int RequiredOpenGlMinorVersion = 3;

	private const int MaxSceneLights = 4;

	private static readonly OpenGlFramePass[] s_framePassOrder = new OpenGlFramePass[3]
	{
		OpenGlFramePass.Opaque,
		OpenGlFramePass.Transparent,
		OpenGlFramePass.Overlay
	};

	private readonly HashSet<SceneNode> _trackedNodeHandles;

	private readonly OpenGlRenderSettings _settings;

	private readonly IWindow _window;

	private GL? _gl;

	private OpenGlShaderProgram? _shaderProgram;

	private OpenGlComputeShaderProgram? _skinningComputeProgram;

	private OpenGlShaderProgram? _lineShaderProgram;

	private bool _disposed;

	private bool _initialized;

	private int _viewportHeight;

	private int _viewportWidth;

	/// <summary>
	/// Initializes a new renderer instance.
	/// </summary>
	/// <param name="window">The host window that owns the OpenGL context.</param>
	/// <param name="settings">The renderer settings.</param>
	public OpenGlSceneRenderer(IWindow window, OpenGlRenderSettings settings)
	{
		_window = window ?? throw new ArgumentNullException("window");
		_settings = settings ?? throw new ArgumentNullException("settings");
		_trackedNodeHandles = new HashSet<SceneNode>();
	}

	/// <inheritdoc />
	public override void Initialize()
	{
		ThrowIfDisposed();
		if (!_initialized)
		{
			_gl = GL.GetApi(_window);
			_gl.GetInteger(GLEnum.MajorVersion, out var data);
			_gl.GetInteger(GLEnum.MinorVersion, out var data2);
			if (data <= 4 && (data != 4 || data2 < 3))
			{
				throw new InvalidOperationException($"OpenGL {4}.{3}+ is required for compute skinning, but the active context is {data}.{data2}.");
			}
			Console.WriteLine($"[OpenGL] Context: {data}.{data2} (requires {4}.{3}+). SkinningMode: {_settings.SkinningMode}");
			_shaderProgram = new OpenGlShaderProgram(_gl, "#version 330 core\r\n\r\nlayout (location = 0) in vec3 aPosition;\r\nlayout (location = 1) in vec3 aNormal;\r\n\r\nuniform mat4 uModel;\r\nuniform mat4 uSceneAxis;\r\nuniform mat4 uView;\r\nuniform mat4 uProjection;\r\n\r\nout vec3 vWorldPosition;\r\nout vec3 vNormal;\r\n\r\nvoid main()\r\n{\r\n    mat4 worldMatrix = uSceneAxis * uModel;\r\n    vec4 worldPosition = worldMatrix * vec4(aPosition, 1.0);\r\n    vWorldPosition = worldPosition.xyz;\r\n    mat3 normalMatrix = mat3(transpose(inverse(worldMatrix)));\r\n    vNormal = normalize(normalMatrix * aNormal);\r\n    gl_Position = uProjection * uView * worldPosition;\r\n}", "#version 330 core\r\n\r\nin vec3 vWorldPosition;\r\nin vec3 vNormal;\r\n\r\n#define MAX_LIGHTS 4\r\n\r\nuniform vec3 uAmbientColor;\r\nuniform int uLightCount;\r\nuniform vec4 uLightDirectionsAndIntensity[MAX_LIGHTS];\r\nuniform vec3 uLightColors[MAX_LIGHTS];\r\nuniform vec3 uCameraPosition;\r\nuniform int uUseViewBasedLighting;\r\nuniform vec4 uBaseColor;\r\nuniform float uMaterialSpecularStrength;\r\nuniform float uMaterialSpecularPower;\r\n\r\nout vec4 FragColor;\r\n\r\nvoid main()\r\n{\r\n    vec3 normal = normalize(vNormal);\r\n    vec3 viewDirection = normalize(uCameraPosition - vWorldPosition);\r\n    vec3 ambient = uAmbientColor;\r\n\r\n    if (uUseViewBasedLighting != 0)\r\n    {\r\n        float facing = max(dot(normal, viewDirection), 0.0);\r\n        vec3 lit = (ambient + vec3(facing)) * uBaseColor.rgb;\r\n        FragColor = vec4(lit, uBaseColor.a);\r\n        return;\r\n    }\r\n\r\n    vec3 diffuse = vec3(0.0);\r\n    vec3 specular = vec3(0.0);\r\n\r\n    int count = clamp(uLightCount, 0, MAX_LIGHTS);\r\n    for (int i = 0; i < count; i++)\r\n    {\r\n        vec3 lightDirection = normalize(-uLightDirectionsAndIntensity[i].xyz);\r\n        float lightIntensity = uLightDirectionsAndIntensity[i].w;\r\n        float nDotL = max(dot(normal, lightDirection), 0.0);\r\n        diffuse += uLightColors[i] * (lightIntensity * nDotL);\r\n\r\n        if (nDotL > 0.0)\r\n        {\r\n            vec3 reflected = reflect(-lightDirection, normal);\r\n            float spec = pow(max(dot(viewDirection, reflected), 0.0), uMaterialSpecularPower);\r\n            specular += uLightColors[i] * (lightIntensity * spec * uMaterialSpecularStrength);\r\n        }\r\n    }\r\n\r\n    vec3 lit = ((ambient + diffuse) * uBaseColor.rgb) + specular;\r\n    FragColor = vec4(lit, uBaseColor.a);\r\n}");
			_skinningComputeProgram = new OpenGlComputeShaderProgram(_gl, "#version 430 core\r\n\r\nlayout(local_size_x = 64) in;\r\n\r\nlayout(std430, binding = 0) readonly buffer Positions\r\n{\r\n    vec4 Position[];\r\n};\r\n\r\nlayout(std430, binding = 1) readonly buffer Normals\r\n{\r\n    vec4 Normal[];\r\n};\r\n\r\nlayout(std430, binding = 2) readonly buffer BoneIndices\r\n{\r\n    uint BoneIndex[];\r\n};\r\n\r\nlayout(std430, binding = 3) readonly buffer BoneWeights\r\n{\r\n    float BoneWeight[];\r\n};\r\n\r\nlayout(std430, binding = 4) readonly buffer SkinTransforms\r\n{\r\n    mat4 SkinTransform[];\r\n};\r\n\r\nlayout(std430, binding = 5) writeonly buffer SkinnedPositions\r\n{\r\n    vec4 SkinnedPosition[];\r\n};\r\n\r\nlayout(std430, binding = 6) writeonly buffer SkinnedNormals\r\n{\r\n    vec4 SkinnedNormal[];\r\n};\r\n\r\nuniform int VertexCount;\r\nuniform int SkinInfluenceCount;\r\nuniform int SkinningMode;\r\n\r\nconst int SkinningModeLinear = 0;\r\nconst int SkinningModeDualQuaternion = 1;\r\n\r\nvec4 QuaternionMultiply(vec4 a, vec4 b)\r\n{\r\n    return vec4(\r\n        a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,\r\n        a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,\r\n        a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,\r\n        a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);\r\n}\r\n\r\nvec4 QuaternionConjugate(vec4 q)\r\n{\r\n    return vec4(-q.xyz, q.w);\r\n}\r\n\r\nvec3 RotateVectorByQuaternion(vec3 value, vec4 q)\r\n{\r\n    vec4 valueQuat = vec4(value, 0.0);\r\n    vec4 rotatedQuat = QuaternionMultiply(QuaternionMultiply(q, valueQuat), QuaternionConjugate(q));\r\n    return rotatedQuat.xyz;\r\n}\r\n\r\nvec4 QuaternionFromMatrix(mat3 matrix)\r\n{\r\n    // GLSL matrices are column-major (m[col][row]); transpose so indexing matches row-major formulas.\r\n    mat3 m = transpose(matrix);\r\n\r\n    float trace = m[0][0] + m[1][1] + m[2][2];\r\n    vec4 result;\r\n\r\n    if (trace > 0.0)\r\n    {\r\n        float s = sqrt(trace + 1.0) * 2.0;\r\n        result.w = 0.25 * s;\r\n        result.x = (m[2][1] - m[1][2]) / s;\r\n        result.y = (m[0][2] - m[2][0]) / s;\r\n        result.z = (m[1][0] - m[0][1]) / s;\r\n        return result;\r\n    }\r\n\r\n    if (m[0][0] > m[1][1] && m[0][0] > m[2][2])\r\n    {\r\n        float s = sqrt(1.0 + m[0][0] - m[1][1] - m[2][2]) * 2.0;\r\n        result.w = (m[2][1] - m[1][2]) / s;\r\n        result.x = 0.25 * s;\r\n        result.y = (m[0][1] + m[1][0]) / s;\r\n        result.z = (m[0][2] + m[2][0]) / s;\r\n        return result;\r\n    }\r\n\r\n    if (m[1][1] > m[2][2])\r\n    {\r\n        float s = sqrt(1.0 + m[1][1] - m[0][0] - m[2][2]) * 2.0;\r\n        result.w = (m[0][2] - m[2][0]) / s;\r\n        result.x = (m[0][1] + m[1][0]) / s;\r\n        result.y = 0.25 * s;\r\n        result.z = (m[1][2] + m[2][1]) / s;\r\n        return result;\r\n    }\r\n\r\n    float sLast = sqrt(1.0 + m[2][2] - m[0][0] - m[1][1]) * 2.0;\r\n    result.w = (m[1][0] - m[0][1]) / sLast;\r\n    result.x = (m[0][2] + m[2][0]) / sLast;\r\n    result.y = (m[1][2] + m[2][1]) / sLast;\r\n    result.z = 0.25 * sLast;\r\n    return result;\r\n}\r\n\r\nvoid BuildDualQuaternion(mat4 transform, out vec4 rotationQuaternion, out vec4 dualQuaternion)\r\n{\r\n    rotationQuaternion = normalize(QuaternionFromMatrix(mat3(transform)));\r\n    vec3 translation = transform[3].xyz;\r\n    vec4 translationQuaternion = vec4(translation, 0.0);\r\n    dualQuaternion = 0.5 * QuaternionMultiply(translationQuaternion, rotationQuaternion);\r\n}\r\n\r\nvec3 TransformPositionByDualQuaternion(vec3 position, vec4 rotationQuaternion, vec4 dualQuaternion)\r\n{\r\n    vec3 rotatedPosition = RotateVectorByQuaternion(position, rotationQuaternion);\r\n    vec4 translationQuaternion = QuaternionMultiply(dualQuaternion, QuaternionConjugate(rotationQuaternion));\r\n    vec3 translation = 2.0 * translationQuaternion.xyz;\r\n    return rotatedPosition + translation;\r\n}\r\n\r\nvoid main()\r\n{\r\n    uint vertexIndex = gl_GlobalInvocationID.x;\r\n    if (vertexIndex >= uint(VertexCount))\r\n    {\r\n        return;\r\n    }\r\n\r\n    vec4 sourcePosition = Position[vertexIndex];\r\n    vec3 sourceNormal = Normal[vertexIndex].xyz;\r\n\r\n    vec4 outputPosition = vec4(0.0);\r\n    vec3 outputNormal = vec3(0.0);\r\n    float totalWeight = 0.0;\r\n\r\n    vec4 blendedRotationQuaternion = vec4(0.0);\r\n    vec4 blendedDualQuaternion = vec4(0.0);\r\n    bool hasQuaternionReference = false;\r\n    vec4 quaternionReference = vec4(0.0);\r\n\r\n    uint baseOffset = vertexIndex * uint(max(SkinInfluenceCount, 0));\r\n    for (int influenceIndex = 0; influenceIndex < SkinInfluenceCount; influenceIndex++)\r\n    {\r\n        uint packedOffset = baseOffset + uint(influenceIndex);\r\n        float weight = BoneWeight[packedOffset];\r\n        if (weight <= 0.0)\r\n        {\r\n            continue;\r\n        }\r\n\r\n        uint boneIndex = BoneIndex[packedOffset];\r\n        mat4 transform = SkinTransform[boneIndex];\r\n\r\n        if (SkinningMode == SkinningModeDualQuaternion)\r\n        {\r\n            vec4 rotationQuaternion;\r\n            vec4 dualQuaternion;\r\n            BuildDualQuaternion(transform, rotationQuaternion, dualQuaternion);\r\n\r\n            if (!hasQuaternionReference)\r\n            {\r\n                quaternionReference = rotationQuaternion;\r\n                hasQuaternionReference = true;\r\n            }\r\n            else if (dot(rotationQuaternion, quaternionReference) < 0.0)\r\n            {\r\n                rotationQuaternion = -rotationQuaternion;\r\n                dualQuaternion = -dualQuaternion;\r\n            }\r\n\r\n            blendedRotationQuaternion += rotationQuaternion * weight;\r\n            blendedDualQuaternion += dualQuaternion * weight;\r\n        }\r\n        else\r\n        {\r\n            outputPosition += (transform * sourcePosition) * weight;\r\n            outputNormal += (mat3(transform) * sourceNormal) * weight;\r\n        }\r\n\r\n        totalWeight += weight;\r\n    }\r\n\r\n    if (totalWeight > 0.0)\r\n    {\r\n        if (SkinningMode == SkinningModeDualQuaternion)\r\n        {\r\n            float quaternionLength = length(blendedRotationQuaternion);\r\n            if (quaternionLength > 1e-8)\r\n            {\r\n                vec4 normalizedRotationQuaternion = blendedRotationQuaternion / quaternionLength;\r\n                vec4 normalizedDualQuaternion = blendedDualQuaternion / quaternionLength;\r\n                outputPosition = vec4(TransformPositionByDualQuaternion(sourcePosition.xyz, normalizedRotationQuaternion, normalizedDualQuaternion), 1.0);\r\n                outputNormal = RotateVectorByQuaternion(sourceNormal, normalizedRotationQuaternion);\r\n            }\r\n            else\r\n            {\r\n                outputPosition = sourcePosition;\r\n                outputNormal = sourceNormal;\r\n            }\r\n        }\r\n        else\r\n        {\r\n            outputPosition /= totalWeight;\r\n            outputNormal /= totalWeight;\r\n        }\r\n    }\r\n    else\r\n    {\r\n        outputPosition = sourcePosition;\r\n        outputNormal = sourceNormal;\r\n    }\r\n\r\n    float lengthSquared = dot(outputNormal, outputNormal);\r\n    vec3 normalizedNormal = lengthSquared > 1e-12 ? normalize(outputNormal) : vec3(0.0, 1.0, 0.0);\r\n\r\n    SkinnedPosition[vertexIndex] = vec4(outputPosition.xyz, 1.0);\r\n    SkinnedNormal[vertexIndex] = vec4(normalizedNormal, 0.0);\r\n}");
			_lineShaderProgram = new OpenGlShaderProgram(_gl, "#version 330 core\r\n\r\nlayout (location = 0) in vec3 aLineStart;\r\nlayout (location = 1) in vec3 aLineEnd;\r\nlayout (location = 2) in vec4 aColor;\r\nlayout (location = 3) in float aAlong;\r\nlayout (location = 4) in float aSide;\r\nlayout (location = 5) in float aWidthScale;\r\n\r\nuniform mat4 uModel;\r\nuniform mat4 uSceneAxis;\r\nuniform mat4 uView;\r\nuniform mat4 uProjection;\r\nuniform vec2 uViewportSize;\r\nuniform float uLineHalfWidthPx;\r\n\r\nout vec3 vWorldPosition;\r\nout vec4 vColor;\r\n\r\nvoid main()\r\n{\r\n    vec4 localPosition = vec4(mix(aLineStart, aLineEnd, aAlong), 1.0);\r\n    vec4 worldPosition = uSceneAxis * uModel * localPosition;\r\n\r\n    vec4 clipStart = uProjection * uView * (uSceneAxis * uModel * vec4(aLineStart, 1.0));\r\n    vec4 clipEnd = uProjection * uView * (uSceneAxis * uModel * vec4(aLineEnd, 1.0));\r\n    vec4 clipPosition = mix(clipStart, clipEnd, aAlong);\r\n\r\n    float safeStartW = max(abs(clipStart.w), 1e-5);\r\n    float safeEndW = max(abs(clipEnd.w), 1e-5);\r\n\r\n    vec2 ndcStart = clipStart.xy / safeStartW;\r\n    vec2 ndcEnd = clipEnd.xy / safeEndW;\r\n\r\n    vec2 viewport = max(uViewportSize, vec2(1.0));\r\n    vec2 screenStart = (ndcStart * 0.5 + 0.5) * viewport;\r\n    vec2 screenEnd = (ndcEnd * 0.5 + 0.5) * viewport;\r\n\r\n    vec2 screenDirection = screenEnd - screenStart;\r\n    float len = max(length(screenDirection), 1e-5);\r\n    vec2 tangent = screenDirection / len;\r\n    vec2 normal = vec2(-tangent.y, tangent.x);\r\n    float halfWidth = uLineHalfWidthPx * aWidthScale;\r\n    float capSign = (aAlong * 2.0) - 1.0;\r\n\r\n    vec2 offsetScreen = (normal * aSide * halfWidth) + (tangent * capSign * halfWidth);\r\n    vec2 offsetNdc = (offsetScreen / viewport) * 2.0;\r\n    clipPosition.xy += offsetNdc * clipPosition.w;\r\n\r\n    vWorldPosition = worldPosition.xyz;\r\n    vColor = aColor;\r\n    gl_Position = clipPosition;\r\n}", "#version 330 core\r\n\r\nin vec3 vWorldPosition;\r\nin vec4 vColor;\r\n\r\nuniform vec3 uCameraPosition;\r\nuniform float uFadeStartDistance;\r\nuniform float uFadeEndDistance;\r\n\r\nout vec4 FragColor;\r\n\r\nvoid main()\r\n{\r\n    vec4 color = vColor;\r\n\r\n    if (uFadeEndDistance > uFadeStartDistance)\r\n    {\r\n        float dist = distance(uCameraPosition, vWorldPosition);\r\n        float t = clamp((dist - uFadeStartDistance) / (uFadeEndDistance - uFadeStartDistance), 0.0, 1.0);\r\n        color.a *= 1.0 - t;\r\n    }\r\n\r\n    if (color.a <= 0.01)\r\n    {\r\n        discard;\r\n    }\r\n\r\n    FragColor = color;\r\n}");
			_gl.Enable(EnableCap.DepthTest);
			_gl.Enable(EnableCap.CullFace);
			_gl.CullFace(TriangleFace.Back);
			_gl.FrontFace(GetFrontFaceMode(_settings.FaceWinding));
			_initialized = true;
		}
	}

	/// <inheritdoc />
	public override void Resize(int width, int height)
	{
		ThrowIfDisposed();
		if (_initialized && _gl != null)
		{
			_viewportWidth = Math.Max(1, width);
			_viewportHeight = Math.Max(1, height);
			_gl.Viewport(0, 0, (uint)_viewportWidth, (uint)_viewportHeight);
		}
	}

	/// <inheritdoc />
	public override void Render(Scene scene, in CameraView view)
	{
		ThrowIfDisposed();
		ArgumentNullException.ThrowIfNull(scene, "scene");
		if (!_initialized || _gl == null || _shaderProgram == null)
		{
			throw new InvalidOperationException("Renderer must be initialized before rendering.");
		}
		_gl.ClearColor(_settings.ClearColor.X, _settings.ClearColor.Y, _settings.ClearColor.Z, _settings.ClearColor.W);
		_gl.Clear(16640u);
		_gl.FrontFace(GetFrontFaceMode(_settings.FaceWinding));
		Matrix4x4 sceneAxisMatrix = GetSceneAxisMatrix(_settings.UpAxis);
		List<IOpenGlSceneNodeRenderHandle> list = new List<IOpenGlSceneNodeRenderHandle>();
		List<OpenGlTransparentQueueEntry> list2 = new List<OpenGlTransparentQueueEntry>();
		List<IOpenGlSceneNodeRenderHandle> list3 = new List<IOpenGlSceneNodeRenderHandle>();
		Span<Vector4> lightDirectionsAndIntensity = stackalloc Vector4[4];
		Span<Vector3> lightColors = stackalloc Vector3[4];
		ResolveDefaultLights(sceneAxisMatrix, lightDirectionsAndIntensity, lightColors, out var lightCount);
		foreach (SceneNode item in scene.EnumerateDescendants())
		{
			if (item.Flags.HasFlag(SceneNodeFlags.NoDraw))
			{
				continue;
			}
			ISceneNodeRenderHandle orCreateNodeRenderHandle = GetOrCreateNodeRenderHandle(item);
			if (orCreateNodeRenderHandle == null)
			{
				continue;
			}
			orCreateNodeRenderHandle.Update();
			if (orCreateNodeRenderHandle is OpenGlLightRenderHandle lightHandle)
			{
				AppendLight(lightHandle, sceneAxisMatrix, lightDirectionsAndIntensity, lightColors, ref lightCount);
			}
			else if (orCreateNodeRenderHandle is IOpenGlSceneNodeRenderHandle openGlSceneNodeRenderHandle)
			{
				switch (openGlSceneNodeRenderHandle.Layer)
				{
				case OpenGlRenderLayer.Opaque:
					list.Add(openGlSceneNodeRenderHandle);
					break;
				case OpenGlRenderLayer.Transparent:
				{
					float distanceSquaredToCamera = Vector3.DistanceSquared(item.GetActiveWorldPosition(), view.Position);
					list2.Add(new OpenGlTransparentQueueEntry(openGlSceneNodeRenderHandle, distanceSquaredToCamera));
					break;
				}
				case OpenGlRenderLayer.Overlay:
					list3.Add(openGlSceneNodeRenderHandle);
					break;
				default:
					throw new ArgumentOutOfRangeException("Layer", openGlSceneNodeRenderHandle.Layer, "Unknown render layer.");
				}
			}
		}
		_shaderProgram.Use();
		_shaderProgram.SetMatrix4("uSceneAxis", sceneAxisMatrix);
		_shaderProgram.SetMatrix4("uView", view.ViewMatrix);
		_shaderProgram.SetMatrix4("uProjection", view.ProjectionMatrix);
		_shaderProgram.SetVector3("uAmbientColor", _settings.AmbientColor);
		_shaderProgram.SetVector3("uCameraPosition", view.Position);
		_shaderProgram.SetInt("uUseViewBasedLighting", _settings.UseViewBasedLighting ? 1 : 0);
		_shaderProgram.SetInt("uLightCount", lightCount);
		for (int i = 0; i < 4; i++)
		{
			_shaderProgram.SetVector4($"uLightDirectionsAndIntensity[{i}]", lightDirectionsAndIntensity[i]);
			_shaderProgram.SetVector3($"uLightColors[{i}]", lightColors[i]);
		}
		if (_lineShaderProgram != null)
		{
			OpenGlRenderContext context = new OpenGlRenderContext(_gl, _shaderProgram, _skinningComputeProgram, _lineShaderProgram, new Vector2(Math.Max(1, _viewportWidth), Math.Max(1, _viewportHeight)), sceneAxisMatrix, _settings);
			SortTransparentBackToFront(list2);
			ExecuteFramePasses(context, in view, new OpenGlFrameQueues(list, list2, list3));
		}
	}

	private void ExecuteFramePasses(OpenGlRenderContext context, in CameraView view, in OpenGlFrameQueues queues)
	{
		if (_gl == null)
		{
			return;
		}
		RenderHandleList(queues.Opaque, context, in view);
		bool flag = queues.Transparent.Count > 0;
		bool flag2 = _settings.ShowSkeletonBones && queues.Overlay.Count > 0;
		if (!flag && !flag2)
		{
			return;
		}
		bool flag3 = _gl.IsEnabled(EnableCap.CullFace);
		bool flag4 = _gl.IsEnabled(EnableCap.DepthTest);
		_gl.Enable(EnableCap.Blend);
		_gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
		_gl.Disable(EnableCap.CullFace);
		_gl.DepthMask(flag: false);
		try
		{
			for (int i = 0; i < s_framePassOrder.Length; i++)
			{
				switch (s_framePassOrder[i])
				{
				case OpenGlFramePass.Transparent:
					if (flag)
					{
						RenderTransparentList(queues.Transparent, context, in view);
					}
					break;
				case OpenGlFramePass.Overlay:
					if (flag2)
					{
						if (_settings.BonesRenderOnTop && flag4)
						{
							_gl.Disable(EnableCap.DepthTest);
						}
						RenderHandleList(queues.Overlay, context, in view);
						if (_settings.BonesRenderOnTop && flag4)
						{
							_gl.Enable(EnableCap.DepthTest);
						}
					}
					break;
				}
			}
		}
		finally
		{
			_gl.DepthMask(flag: true);
			if (flag3)
			{
				_gl.Enable(EnableCap.CullFace);
			}
			_gl.Disable(EnableCap.Blend);
		}
	}

	private static void RenderHandleList(List<IOpenGlSceneNodeRenderHandle> handles, OpenGlRenderContext context, in CameraView view)
	{
		for (int i = 0; i < handles.Count; i++)
		{
			handles[i].Render(context, in view);
		}
	}

	private static void RenderTransparentList(List<OpenGlTransparentQueueEntry> handles, OpenGlRenderContext context, in CameraView view)
	{
		for (int i = 0; i < handles.Count; i++)
		{
			handles[i].Handle.Render(context, in view);
		}
	}

	private static void SortTransparentBackToFront(List<OpenGlTransparentQueueEntry> handles)
	{
		handles.Sort((OpenGlTransparentQueueEntry left, OpenGlTransparentQueueEntry right) => right.DistanceSquaredToCamera.CompareTo(left.DistanceSquaredToCamera));
	}

	/// <summary>
	/// Clears all cached GPU resources so scene buffers are rebuilt on next render.
	/// </summary>
	public void RebuildResources()
	{
		ThrowIfDisposed();
		if (_gl != null)
		{
			foreach (SceneNode trackedNodeHandle in _trackedNodeHandles)
			{
				if (trackedNodeHandle.GraphicsHandle is IOpenGlSceneNodeRenderHandle openGlSceneNodeRenderHandle)
				{
					openGlSceneNodeRenderHandle.Release();
					trackedNodeHandle.GraphicsHandle = null;
				}
			}
		}
		_trackedNodeHandles.Clear();
	}

	private void AbandonResources()
	{
		foreach (SceneNode trackedNodeHandle in _trackedNodeHandles)
		{
			trackedNodeHandle.GraphicsHandle?.Dispose();
			trackedNodeHandle.GraphicsHandle = null;
		}
		_trackedNodeHandles.Clear();
	}

	/// <inheritdoc />
	public override void Dispose()
	{
		if (!_disposed)
		{
			AbandonResources();
			_lineShaderProgram?.Dispose();
			_skinningComputeProgram?.Dispose();
			_shaderProgram?.Dispose();
			_lineShaderProgram = null;
			_skinningComputeProgram = null;
			_shaderProgram = null;
			_gl = null;
			_initialized = false;
			_disposed = true;
		}
	}

	private void ResolveDefaultLights(Matrix4x4 sceneAxisMatrix, Span<Vector4> lightDirectionsAndIntensity, Span<Vector3> lightColors, out int lightCount)
	{
		Vector3 value = _settings.FallbackLightDirection;
		if (value.LengthSquared() < 1E-10f)
		{
			value = -Vector3.UnitY;
		}
		Vector3 direction = Vector3.Normalize(value);
		direction = TransformDirection(direction, sceneAxisMatrix);
		lightCount = 1;
		lightDirectionsAndIntensity[0] = new Vector4(direction, _settings.FallbackLightIntensity);
		lightColors[0] = _settings.FallbackLightColor;
	}

	private void AppendLight(OpenGlLightRenderHandle lightHandle, Matrix4x4 sceneAxisMatrix, Span<Vector4> lightDirectionsAndIntensity, Span<Vector3> lightColors, ref int lightCount)
	{
		if (lightHandle.Enabled && lightCount < 4)
		{
			Vector3 value = TransformDirection(lightHandle.Direction, sceneAxisMatrix);
			lightDirectionsAndIntensity[lightCount] = new Vector4(value, lightHandle.Intensity);
			lightColors[lightCount] = lightHandle.Color;
			lightCount++;
		}
	}

	private ISceneNodeRenderHandle? GetOrCreateNodeRenderHandle(SceneNode node)
	{
		GL gl = _gl ?? throw new InvalidOperationException("OpenGL context has not been created.");
		if (node is Light)
		{
			if (node.GraphicsHandle is OpenGlLightRenderHandle openGlLightRenderHandle && openGlLightRenderHandle.IsOwnedBy(_settings))
			{
				return openGlLightRenderHandle;
			}
			OpenGlLightRenderHandle result = (OpenGlLightRenderHandle)(node.GraphicsHandle = new OpenGlLightRenderHandle((Light)node, _settings));
			_trackedNodeHandles.Add(node);
			return result;
		}
		if (node.GraphicsHandle is IOpenGlSceneNodeRenderHandle openGlSceneNodeRenderHandle && openGlSceneNodeRenderHandle.IsOwnedBy(gl))
		{
			return openGlSceneNodeRenderHandle;
		}
		if (1 == 0)
		{
		}
		IOpenGlSceneNodeRenderHandle openGlSceneNodeRenderHandle2 = ((node is Mesh mesh) ? new OpenGlMeshRenderHandle(gl, mesh) : ((node is Grid grid) ? ((IOpenGlSceneNodeRenderHandle)new OpenGlGridRenderHandle(gl, grid)) : ((IOpenGlSceneNodeRenderHandle)((!(node is SkeletonBone bone)) ? null : new OpenGlSkeletonBoneRenderHandle(gl, bone, _settings)))));
		if (1 == 0)
		{
		}
		IOpenGlSceneNodeRenderHandle openGlSceneNodeRenderHandle3 = openGlSceneNodeRenderHandle2;
		if (openGlSceneNodeRenderHandle3 == null)
		{
			return null;
		}
		node.GraphicsHandle = openGlSceneNodeRenderHandle3;
		_trackedNodeHandles.Add(node);
		return openGlSceneNodeRenderHandle3;
	}

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
	}

	private static GLEnum GetFrontFaceMode(OpenGlFaceWinding winding)
	{
		return (winding == OpenGlFaceWinding.Cw) ? GLEnum.CW : GLEnum.Ccw;
	}

	private static Matrix4x4 GetSceneAxisMatrix(OpenGlUpAxis upAxis)
	{
		if (1 == 0)
		{
		}
		Matrix4x4 result = upAxis switch
		{
			OpenGlUpAxis.X => Matrix4x4.CreateRotationZ((float)Math.PI / 2f), 
			OpenGlUpAxis.Z => Matrix4x4.CreateRotationX(-(float)Math.PI / 2f), 
			_ => Matrix4x4.Identity, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static Vector3 TransformDirection(Vector3 direction, Matrix4x4 matrix)
	{
		Vector3 value = Vector3.TransformNormal(direction, matrix);
		if (value.LengthSquared() < 1E-10f)
		{
			return -Vector3.UnitY;
		}
		return Vector3.Normalize(value);
	}
}
