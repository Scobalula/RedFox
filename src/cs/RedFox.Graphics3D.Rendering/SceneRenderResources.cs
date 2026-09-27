using System.Runtime.CompilerServices;
using RedFox.Graphics3D.Rendering.Handles;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering;

/// <summary>Owns renderer resources associated with scene objects.</summary>
internal static class SceneRenderResources
{
    private static readonly ConditionalWeakTable<IGraphicsDevice, DeviceResources> Resources = [];

    public static IRenderHandle? Get(IGraphicsDevice graphicsDevice, object owner)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(owner);
        return Resources.TryGetValue(graphicsDevice, out DeviceResources? resources) && resources.Handles.TryGetValue(owner, out IRenderHandle? handle) ? handle : null;
    }

    public static T GetOrCreate<T>(IGraphicsDevice graphicsDevice, object owner, Func<T> factory) where T : class, IRenderHandle
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(factory);
        DeviceResources resources = Resources.GetOrCreateValue(graphicsDevice);
        if (resources.Handles.TryGetValue(owner, out IRenderHandle? existingHandle) && existingHandle is T existing)
        {
            return existing;
        }

        Release(resources, owner);
        T newHandle = factory();
        resources.Handles.Add(owner, newHandle);
        return newHandle;
    }

    public static IRenderHandle? GetOrCreate(SceneNode node, IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Get(graphicsDevice, node) ?? node switch
        {
            Mesh mesh => GetOrCreate(graphicsDevice, mesh, () => new MeshRenderHandle(graphicsDevice, mesh)),
            Material material => GetOrCreate(graphicsDevice, material, () => new MaterialRenderHandle(graphicsDevice, material)),
            Texture texture => GetOrCreate(graphicsDevice, texture, () => new TextureRenderHandle(graphicsDevice, texture)),
            SkeletonBone bone => GetOrCreate(graphicsDevice, bone, () => new SkeletonBoneRenderHandle(graphicsDevice, materialTypes, bone)),
            Light light => GetOrCreate(graphicsDevice, light, () => new LightRenderHandle(light)),
            _ => null,
        };
    }

    public static void Release(IGraphicsDevice graphicsDevice, object owner)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(owner);
        if (Resources.TryGetValue(graphicsDevice, out DeviceResources? resources))
        {
            Release(resources, owner);
        }
    }

    public static void ReleaseAll(IGraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        if (!Resources.TryGetValue(graphicsDevice, out DeviceResources? resources))
        {
            return;
        }

        foreach (IRenderHandle handle in resources.Handles.Values)
        {
            handle.Release();
            handle.Dispose();
        }

        resources.Handles.Clear();
    }

    private static void Release(DeviceResources resources, object owner)
    {
        if (resources.Handles.Remove(owner, out IRenderHandle? handle))
        {
            handle.Release();
            handle.Dispose();
        }
    }

    private sealed class DeviceResources
    {
        public Dictionary<object, IRenderHandle> Handles { get; } = new(ReferenceEqualityComparer.Instance);
    }
}
