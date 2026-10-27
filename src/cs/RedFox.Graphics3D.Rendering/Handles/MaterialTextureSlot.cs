namespace RedFox.Graphics3D.Rendering.Handles;

internal readonly record struct MaterialTextureSlot(TextureRenderHandle? Handle, string SamplerUniform, int Slot, string? FlagUniform);
