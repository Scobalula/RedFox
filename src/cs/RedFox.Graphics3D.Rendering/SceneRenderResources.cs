using System.Runtime.CompilerServices;
using RedFox.Graphics3D.Rendering.Handles;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering;

/// <summary>Owns renderer resources associated with scene objects.</summary>
internal static class SceneRenderResources
{
    private static readonly ConditionalWeakTable<IGraphicsDevice, Dictionary<object, IRenderHandle>> Resources = [];

    public static IRenderHandle? Get(IGraphicsDevice graphicsDevice, object owner)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(owner);
        return Resources.TryGetValue(graphicsDevice, out Dictionary<object, IRenderHandle>? handles) && handles.TryGetValue(owner, out IRenderHandle? handle) ? handle : null;
    }

    public static T GetOrCreate<T>(IGraphicsDevice graphicsDevice, object owner, Func<T> factory) where T : class, IRenderHandle
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(factory);
        Dictionary<object, IRenderHandle> handles = Resources.GetValue(graphicsDevice, static _ => new(ReferenceEqualityComparer.Instance));
        if (handles.TryGetValue(owner, out IRenderHandle? existingHandle) && existingHandle is T existing)
        {
            return existing;
        }

        Release(handles, owner);
        T newHandle = factory();
        handles.Add(owner, newHandle);
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
        if (Resources.TryGetValue(graphicsDevice, out Dictionary<object, IRenderHandle>? handles))
        {
            Release(handles, owner);
        }
    }

    public static void ReleaseAll(IGraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        if (!Resources.TryGetValue(graphicsDevice, out Dictionary<object, IRenderHandle>? handles))
        {
            return;
        }

        foreach (IRenderHandle handle in handles.Values)
        {
            handle.Release();
            handle.Dispose();
        }

        handles.Clear();
    }

    private static void Release(Dictionary<object, IRenderHandle> handles, object owner)
    {
        if (handles.Remove(owner, out IRenderHandle? handle))
        {
            handle.Release();
            handle.Dispose();
        }
    }

}
