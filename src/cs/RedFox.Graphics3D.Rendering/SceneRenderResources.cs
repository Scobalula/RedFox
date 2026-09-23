using System.Runtime.CompilerServices;
using RedFox.Graphics3D.Rendering.Handles;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering;

/// <summary>Owns renderer resources associated with scene objects.</summary>
internal static class SceneRenderResources
{
    private static readonly ConditionalWeakTable<object, ResourceSlot> Resources = [];

    public static IRenderHandle? Get(object owner) => Resources.TryGetValue(owner, out ResourceSlot? slot) ? slot.Handle : null;

    public static T GetOrCreate<T>(object owner, Func<T> factory) where T : class, IRenderHandle
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(factory);
        ResourceSlot slot = Resources.GetOrCreateValue(owner);
        if (slot.Handle is T existing)
        {
            return existing;
        }

        Release(owner);
        T handle = factory();
        Resources.GetOrCreateValue(owner).Handle = handle;
        return handle;
    }

    public static IRenderHandle? GetOrCreate(SceneNode node, IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Get(node) ?? node switch
        {
            Mesh mesh => GetOrCreate(mesh, () => new MeshRenderHandle(graphicsDevice, mesh)),
            Material material => GetOrCreate(material, () => new MaterialRenderHandle(graphicsDevice, material)),
            Texture texture => GetOrCreate(texture, () => new TextureRenderHandle(graphicsDevice, texture)),
            SkeletonBone bone => GetOrCreate(bone, () => new SkeletonBoneRenderHandle(graphicsDevice, materialTypes, bone)),
            Light light => GetOrCreate(light, () => new LightRenderHandle(light)),
            _ => null,
        };
    }

    public static void Release(object owner)
    {
        if (Resources.TryGetValue(owner, out ResourceSlot? slot) && slot.Handle is IRenderHandle handle)
        {
            handle.Release();
            handle.Dispose();
            slot.Handle = null;
        }
    }

    private sealed class ResourceSlot
    {
        public IRenderHandle? Handle { get; set; }
    }
}
