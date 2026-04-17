using System.Diagnostics.CodeAnalysis;

namespace RedFox.GameExtraction;

internal sealed class ServiceRegistration
{
    private readonly object _gate = new();
    private readonly Func<AssetManager, object>? _factory;
    private object? _instance;

    private ServiceRegistration(object? instance, Func<AssetManager, object>? factory)
    {
        _instance = instance;
        _factory = factory;
    }

    public static ServiceRegistration FromInstance(object instance) => new(instance, factory: null);

    public static ServiceRegistration FromFactory(Func<AssetManager, object> factory) => new(instance: null, factory);

    public bool TryResolve(AssetManager manager, [NotNullWhen(true)] out object? service)
    {
        if (_instance is not null)
        {
            service = _instance;
            return true;
        }

        if (_factory is null)
        {
            service = null;
            return false;
        }

        lock (_gate)
        {
            if (_instance is null)
            {
                _instance = _factory(manager) ?? throw new InvalidOperationException("Service factories must not return null.");
            }
        }

        service = _instance;
        return true;
    }
}
