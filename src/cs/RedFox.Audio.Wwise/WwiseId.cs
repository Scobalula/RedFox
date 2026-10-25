using System.Text;

namespace RedFox.Audio.Wwise;

public static class WwiseId
{
    public static uint Compute(string name)
    {
        uint hash = 2166136261;

        foreach (byte character in Encoding.UTF8.GetBytes(name))
        {
            byte normalized = character is >= (byte)'A' and <= (byte)'Z' ? (byte)(character + 32) : character;
            hash = unchecked(hash * 16777619) ^ normalized;
        }

        return hash;
    }
}
