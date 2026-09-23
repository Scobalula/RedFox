using System;
using System.Collections.Generic;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Provides a backend-neutral material type registry implementation.
/// </summary>
public class MaterialTypeRegistry : IMaterialTypeRegistry, IMaterialPipelineProvider
{
    private readonly Dictionary<string, MaterialTypeDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MaterialTypePipelineCacheEntry> _pipelineCache = new(StringComparer.Ordinal);
    private readonly IMaterialShaderFactory? _shaderFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialTypeRegistry"/> class.
    /// </summary>
    public MaterialTypeRegistry()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialTypeRegistry"/> class for a backend shader factory.
    /// </summary>
    /// <param name="shaderFactory">The backend shader factory.</param>
    public MaterialTypeRegistry(IMaterialShaderFactory shaderFactory)
    {
        _shaderFactory = shaderFactory ?? throw new ArgumentNullException(nameof(shaderFactory));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialTypeRegistry"/> class with material definitions.
    /// </summary>
    /// <param name="definitions">The material definitions to register.</param>
    public MaterialTypeRegistry(IEnumerable<MaterialTypeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (MaterialTypeDefinition definition in definitions)
        {
            Register(definition);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialTypeRegistry"/> class for a backend shader factory with material definitions.
    /// </summary>
    /// <param name="shaderFactory">The backend shader factory.</param>
    /// <param name="definitions">The material definitions to register.</param>
    public MaterialTypeRegistry(IMaterialShaderFactory shaderFactory, IEnumerable<MaterialTypeDefinition> definitions)
        : this(shaderFactory)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (MaterialTypeDefinition definition in definitions)
        {
            Register(definition);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> RegisteredNames => _definitions.Keys;

    /// <inheritdoc/>
    public void Register(MaterialTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Name] = definition;
    }

    /// <inheritdoc/>
    public MaterialTypeDefinition Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _definitions[name];
    }

    /// <inheritdoc/>
    public bool Contains(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _definitions.ContainsKey(name);
    }

    IGpuPipelineState IMaterialPipelineProvider.CreatePipeline(IGraphicsDevice graphicsDevice, string typeName)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        return BuildPipeline(graphicsDevice, typeName);
    }

    IGpuPipelineState IMaterialPipelineProvider.AcquirePipeline(IGraphicsDevice graphicsDevice, string typeName)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        if (_pipelineCache.TryGetValue(typeName, out MaterialTypePipelineCacheEntry? cacheEntry))
        {
            if (!cacheEntry.Pipeline.IsDisposed)
            {
                cacheEntry.ReferenceCount++;
                return cacheEntry.Pipeline;
            }

            _pipelineCache.Remove(typeName);
        }

        MaterialTypePipelineCacheEntry newEntry = new(BuildPipeline(graphicsDevice, typeName));
        _pipelineCache[typeName] = newEntry;
        return newEntry.Pipeline;
    }

    void IMaterialPipelineProvider.ReleasePipeline(string typeName, IGpuPipelineState pipeline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(pipeline);

        if (!_pipelineCache.TryGetValue(typeName, out MaterialTypePipelineCacheEntry? cacheEntry)
            || !ReferenceEquals(cacheEntry.Pipeline, pipeline))
        {
            return;
        }

        cacheEntry.ReferenceCount--;
        if (cacheEntry.ReferenceCount > 0)
        {
            return;
        }

        _pipelineCache.Remove(typeName);
        if (!cacheEntry.Pipeline.IsDisposed)
        {
            cacheEntry.Pipeline.Dispose();
        }
    }

    private IGpuPipelineState BuildPipeline(IGraphicsDevice graphicsDevice, string typeName)
    {
        IMaterialShaderFactory shaderFactory = _shaderFactory
            ?? throw new InvalidOperationException("This material type registry cannot build pipelines because no shader factory was configured.");
        MaterialTypeDefinition definition = Get(typeName);
        return definition.Descriptor.BuildPipeline(graphicsDevice, shaderFactory);
    }
}