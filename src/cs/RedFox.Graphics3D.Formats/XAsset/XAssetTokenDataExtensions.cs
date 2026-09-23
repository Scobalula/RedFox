using System.Numerics;
using CallOfFile;

namespace RedFox.Graphics3D.Formats.XAsset;

internal static class XAssetTokenDataExtensions
{
    public static int GetInt32(this TokenData data) => data switch
    {
        TokenDataInt value => value.Value,
        TokenDataUInt value => checked((int)value.Value),
        _ => throw Invalid(data, "an integer"),
    };

    public static float GetSingle(this TokenData data) => data switch
    {
        TokenDataFloat value => value.Value,
        TokenDataInt value => value.Value,
        TokenDataUInt value => value.Value,
        _ => throw Invalid(data, "a number"),
    };

    public static Vector2 GetVector2(this TokenData data) => data is TokenDataVector2 value ? value.Value : throw Invalid(data, "a Vector2");

    public static Vector3 GetVector3(this TokenData data) => data is TokenDataVector3 value ? value.Value : throw Invalid(data, "a Vector3");

    public static Vector4 GetVector4(this TokenData data) => data is TokenDataVector4 value ? value.Value : throw Invalid(data, "a Vector4");

    private static InvalidDataException Invalid(TokenData data, string expected) => new($"Token '{data.Token.Name}' is not {expected}.");
}
