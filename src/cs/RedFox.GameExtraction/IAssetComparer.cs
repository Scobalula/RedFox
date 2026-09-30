namespace RedFox.GameExtraction;

/// <summary>
/// Compares assets for information-based ordering. Implementations can inspect asset type,
/// loader-provided information, and <see cref="Asset.UserData"/>.
/// </summary>
public interface IAssetComparer : IComparer<Asset>
{
}
